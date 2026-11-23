using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Learning;

public sealed class DialogueFoundationTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task DatabaseRejectsCrossDialogueTransitionsAndDuplicateOpenSessions()
    {
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Guid courseId = await db.Courses.OrderBy(item => item.OrderNumber).Select(item => item.Id).FirstAsync();
        var first = new Dialogue { CourseId = courseId, Title = "Café", Scenario = "cafe" };
        var second = new Dialogue { CourseId = courseId, Title = "Hotel", Scenario = "hotel" };
        var start = new DialogueNode { DialogueId = first.Id, Text = "Welcome", IsStart = true };
        var foreign = new DialogueNode { DialogueId = second.Id, Text = "Goodbye", IsTerminal = true };
        db.AddRange(first, second, start, foreign);
        await db.SaveChangesAsync();

        db.DialogueChoices.Add(new DialogueChoice
        {
            DialogueId = first.Id, NodeId = start.Id, NextNodeId = foreign.Id,
            Text = "Continue", Feedback = "Try again"
        });
        var invalidEdge = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(invalidEdge.InnerException).SqlState);
        db.ChangeTracker.Clear();

        DialogueSession Session() => new()
        {
            DialogueId = first.Id, UserId = learner.Id, CurrentNodeId = start.Id,
            StartedAt = DateTimeOffset.UtcNow, VersionSnapshot = "{}"
        };

        db.DialogueSessions.Add(Session());
        await db.SaveChangesAsync();
        db.DialogueSessions.Add(Session());
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
    }
}
