using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class StarterDialogueTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task AllFourLanguagesHaveFourFreeConversationsAndOnePremiumConversation()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var installer = scope.ServiceProvider.GetRequiredService<StarterDialogueInstaller>();
        var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        Assert.Equal(20, await installer.InstallAsync(CancellationToken.None));
        Assert.Equal(0, await installer.InstallAsync(CancellationToken.None));

        var dialogues = await db.Dialogues.AsNoTracking().ToArrayAsync();
        Assert.Equal(4, dialogues.Select(item => item.CourseId).Distinct().Count());
        var nodes = await db.DialogueNodes.AsNoTracking().ToArrayAsync();
        var choices = await db.DialogueChoices.AsNoTracking().ToArrayAsync();
        foreach (var course in dialogues.GroupBy(item => item.CourseId))
        {
            Assert.Equal(new[] { "cafe", "directions", "hotel", "tickets" },
                course.Where(item => !item.RequiresPremium).Select(item => item.Scenario).Order());
            Assert.Equal("lost-and-found", Assert.Single(course, item => item.RequiresPremium).Scenario);
            foreach (var dialogue in course)
            {
                var graph = nodes.Where(item => item.DialogueId == dialogue.Id).ToDictionary(item => item.Id);
                Assert.Equal(6, graph.Count);
                var start = Assert.Single(graph.Values, item => item.IsStart);
                var openings = choices.Where(item => item.NodeId == start.Id).ToArray();
                Assert.Equal(3, openings.Length);
                var incorrect = Assert.Single(openings, choice => !choice.IsCorrect);
                Assert.True(graph[incorrect.NextNodeId!.Value].IsTerminal);
                var endings = openings.Where(choice => choice.IsCorrect).Select(choice =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(choice.Feedback));
                    var next = graph[choice.NextNodeId!.Value];
                    Assert.False(next.IsTerminal);
                    var followUp = Assert.Single(choices, item => item.NodeId == next.Id);
                    var ending = graph[followUp.NextNodeId!.Value];
                    Assert.True(ending.IsTerminal);
                    return ending.Id;
                }).Distinct();
                Assert.Equal(2, endings.Count());
            }
        }
    }
}
