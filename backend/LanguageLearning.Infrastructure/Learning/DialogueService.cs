using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class DialogueService(LexiGoDbContext database, LearningAccess access, ContentTransactions content,
    TimeProvider clock, SubscriptionQueries subscriptions, DialogueRewards rewards)
{
    private const string ExtraDialogues = "ExtraDialogues";
    private IQueryable<Dialogue> Available(Guid userId) =>
        from dialogue in database.Dialogues.AsNoTracking()
        join course in database.Courses on dialogue.CourseId equals course.Id
        where course.IsPublished && dialogue.Status == "Published"
            && database.Enrollments.Any(item => item.CourseId == course.Id && item.UserId == userId)
            && !database.Dialogues.Any(item => item.CourseId == dialogue.CourseId && item.Scenario == dialogue.Scenario
                && item.Status == "Published" && item.VersionNumber > dialogue.VersionNumber)
        select dialogue;

    public async Task<DialogueListing[]> ListAsync(Guid userId, Guid courseId, CancellationToken cancellationToken)
    {
        bool premium = await subscriptions.HasAsync(userId, ExtraDialogues, cancellationToken);
        return await Available(userId).Where(item => item.CourseId == courseId)
            .OrderBy(item => item.RequiresPremium).ThenBy(item => item.Scenario).Take(40)
            .Select(item => new DialogueListing(item.Id, item.Title, item.Scenario, item.VersionNumber,
                item.RequiresPremium && !premium ? null : (from session in database.DialogueSessions
                 join version in database.Dialogues on session.DialogueId equals version.Id
                 where session.UserId == userId && session.CompletedAt == null
                    && version.CourseId == item.CourseId && version.Scenario == item.Scenario
                 orderby session.StartedAt descending, session.Id descending
                 select (Guid?)session.Id).FirstOrDefault(),
                (from session in database.DialogueSessions
                 join version in database.Dialogues on session.DialogueId equals version.Id
                 where session.UserId == userId && session.CompletedAt != null
                    && version.CourseId == item.CourseId && version.Scenario == item.Scenario
                 select session.Id).Any(), item.RequiresPremium, item.RequiresPremium && !premium))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DialogueOutcome> StartAsync(Guid userId, Guid familyId, Guid dialogueId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var requested = await database.Dialogues.AsNoTracking().Where(item => item.Id == dialogueId
            && database.Courses.Any(course => course.Id == item.CourseId && course.IsPublished)
            && database.Enrollments.Any(enrollment => enrollment.CourseId == item.CourseId && enrollment.UserId == userId)
            && (item.Status == "Published" || database.DialogueSessions.Any(session =>
                session.DialogueId == item.Id && session.UserId == userId)))
            .Select(item => new { item.CourseId, item.Scenario }).SingleOrDefaultAsync(cancellationToken);
        if (requested is null) return new(Missing: true);
        await content.LockCourseAsync(requested.CourseId, cancellationToken);
        var dialogue = await Available(userId).SingleOrDefaultAsync(item => item.CourseId == requested.CourseId
            && item.Scenario == requested.Scenario, cancellationToken);
        if (dialogue is null) return new(Missing: true);
        if (dialogue.RequiresPremium && !await subscriptions.HasAsync(userId, ExtraDialogues, cancellationToken))
            return new(PremiumRequired: true);

        var existing = await (from saved in database.DialogueSessions.AsNoTracking()
                              join version in database.Dialogues on saved.DialogueId equals version.Id
                              where saved.UserId == userId && saved.CompletedAt == null
                                  && version.CourseId == dialogue.CourseId && version.Scenario == dialogue.Scenario
                              orderby saved.StartedAt descending, saved.Id descending
                              select saved).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (await IsLockedAsync(existing, cancellationToken)) return new(PremiumRequired: true);
            return new(await ViewAsync(existing, cancellationToken));
        }
        if (await database.DialogueSessions.CountAsync(item => item.UserId == userId && item.CompletedAt == null, cancellationToken) >= 20)
            return new(Error: "Finish an open conversation before starting another.");

        var graph = await DialogueGraphLoader.LoadAsync(database, dialogue, cancellationToken);
        if (graph is null) return new(Error: "This conversation is not ready to start.");
        var language = await LanguageAsync(dialogue.Id, cancellationToken);
        var session = new DialogueSession
        {
            UserId = userId, DialogueId = dialogue.Id, CurrentNodeId = graph.StartId,
            VersionSnapshot = Encode(graph), StartedAt = clock.GetUtcNow()
        };
        database.DialogueSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Project(session, graph, [], language,
            rewardXp: await rewards.ValueAsync(cancellationToken)), Created: true);
    }

    public async Task<DialogueOutcome> ReadAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await database.DialogueSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null) return new(Missing: true);
        if (!session.CompletedAt.HasValue && await IsLockedAsync(session, cancellationToken))
            return new(PremiumRequired: true);
        return new(await ViewAsync(session, cancellationToken));
    }

    public async Task<DialogueOutcome> ChooseAsync(Guid userId, Guid familyId, Guid sessionId,
        Guid choiceId, Guid key, CancellationToken cancellationToken)
    {
        if (choiceId == Guid.Empty || key == Guid.Empty) return new(Error: "Choose a response and provide a request key.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await access.LockAsync(userId, familyId, cancellationToken)) return new(Missing: true);
        var session = await database.DialogueSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null) return new(Missing: true);
        if (await IsLockedAsync(session, cancellationToken)) return new(PremiumRequired: true);

        var graph = Decode<DialogueGraph>(session.VersionSnapshot);
        var attempts = await ChoicesAsync(session.Id, cancellationToken);
        var replay = attempts.SingleOrDefault(item => item.IdempotencyKey == key);
        if (replay is not null)
        {
            if (replay.ChoiceId != choiceId) return new(Error: "This request key was already used.");
            return new(await ViewAsync(session, cancellationToken));
        }
        if (session.CompletedAt.HasValue) return new(Error: "This conversation is complete.");

        var current = graph.Nodes.Single(item => item.Id == session.CurrentNodeId);
        var choice = current.Choices.SingleOrDefault(item => item.Id == choiceId);
        if (choice is null) return new(Error: "Choose one of the responses shown for this turn.");
        var language = await LanguageAsync(session.DialogueId, cancellationToken);
        var submittedAt = clock.GetUtcNow();
        var attempt = new DialogueAttempt
        {
            DialogueId = session.DialogueId, SessionId = session.Id, ChoiceId = choice.Id,
            IdempotencyKey = key, SubmittedAt = submittedAt
        };
        database.DialogueAttempts.Add(attempt);
        if (choice.NextNodeId is { } next) session.CurrentNodeId = next;
        if (choice.NextNodeId is null || graph.Nodes.Single(item => item.Id == session.CurrentNodeId).IsTerminal)
            session.CompletedAt = submittedAt;

        var completedAttempts = attempts.Append(attempt).ToArray();
        int rewardXp = await rewards.ValueAsync(cancellationToken);
        int awardedXp = await rewards.AwardLockedAsync(session, graph, completedAttempts, rewardXp, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Project(session, graph, completedAttempts, language, awardedXp, rewardXp));
    }

    private async Task<bool> IsLockedAsync(DialogueSession session, CancellationToken cancellationToken) =>
        await database.Dialogues.AnyAsync(item => item.Id == session.DialogueId && item.RequiresPremium, cancellationToken)
        && !await subscriptions.HasAsync(session.UserId, ExtraDialogues, cancellationToken);

    private async Task<DialogueView> ViewAsync(DialogueSession session, CancellationToken cancellationToken) =>
        Project(session, Decode<DialogueGraph>(session.VersionSnapshot), await ChoicesAsync(session.Id, cancellationToken),
            await LanguageAsync(session.DialogueId, cancellationToken),
            await rewards.ForSessionAsync(session.UserId, session.Id, cancellationToken),
            await rewards.ValueAsync(cancellationToken));

    private Task<string> LanguageAsync(Guid dialogueId, CancellationToken cancellationToken) =>
        (from dialogue in database.Dialogues
         join course in database.Courses on dialogue.CourseId equals course.Id
         where dialogue.Id == dialogueId
         select course.TargetLanguage.Code).SingleAsync(cancellationToken);

    private Task<DialogueAttempt[]> ChoicesAsync(Guid sessionId, CancellationToken cancellationToken) =>
        database.DialogueAttempts.AsNoTracking().Where(item => item.SessionId == sessionId).ToArrayAsync(cancellationToken);

    private static DialogueView Project(DialogueSession session, DialogueGraph graph, DialogueAttempt[] attempts,
        string language, int awardedXp = 0, int rewardXp = 10)
    {
        var nodes = graph.Nodes.ToDictionary(item => item.Id);
        var selected = attempts.Select(item => item.ChoiceId).ToHashSet();
        var path = new List<DialogueStep>();
        var review = new List<DialogueReviewPhrase>();
        var node = nodes[graph.StartId];
        while (node.Id != session.CurrentNodeId || session.CompletedAt.HasValue)
        {
            var choice = node.Choices.SingleOrDefault(item => selected.Contains(item.Id));
            if (choice is null) break;
            path.Add(new(node.Text, choice.Text, choice.Feedback, choice.IsCorrect));
            if (session.CompletedAt.HasValue)
            {
                var example = choice.IsCorrect ? choice : node.Choices.FirstOrDefault(item => item.IsCorrect);
                if (example is not null) review.Add(new(example.Text, example.Feedback));
            }
            if (choice.NextNodeId is not { } next) break;
            node = nodes[next];
        }

        var current = nodes[session.CurrentNodeId];
        var options = session.CompletedAt.HasValue ? [] : current.Choices.Select(item => new DialogueOption(item.Id, item.Text)).ToArray();
        return new(session.Id, session.DialogueId, graph.Title,
            new(current.Id, current.Text, current.IsTerminal, options), session.CompletedAt, path.ToArray(),
            review.DistinctBy(phrase => phrase.Text, StringComparer.OrdinalIgnoreCase).ToArray(), language, awardedXp, rewardXp);
    }
}
