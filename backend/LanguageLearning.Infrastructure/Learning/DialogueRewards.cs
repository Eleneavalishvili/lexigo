using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Administration;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed class DialogueRewards(LexiGoDbContext database, SystemSettings settings)
{
    private const string Source = "FirstDialogueCompletion";

    // DialogueService holds the account lock and completion transaction.
    internal async Task<int> AwardLockedAsync(DialogueSession session, DialogueGraph graph,
        DialogueAttempt[] attempts, int completionXp, CancellationToken token)
    {
        if (session.CompletedAt is null || attempts.Length == 0 || completionXp <= 0) return 0;
        var answers = graph.Nodes.SelectMany(item => item.Choices).ToDictionary(item => item.Id);
        int correct = attempts.Count(item => answers.TryGetValue(item.ChoiceId, out var answer) && answer.IsCorrect);
        if (correct * 100 < attempts.Length * 80) return 0;

        var scenario = await database.Dialogues.Where(item => item.Id == session.DialogueId)
            .Select(item => new { item.CourseId, item.Scenario }).SingleAsync(token);
        bool alreadyEarned = await (from reward in database.XpTransactions
                                   join previous in database.DialogueSessions on reward.SourceId equals previous.Id
                                   join dialogue in database.Dialogues on previous.DialogueId equals dialogue.Id
                                   where reward.UserId == session.UserId && reward.SourceType == Source
                                       && previous.UserId == session.UserId && dialogue.CourseId == scenario.CourseId
                                       && dialogue.Scenario == scenario.Scenario
                                   select reward.Id).AnyAsync(token);
        if (alreadyEarned) return 0;

        database.XpTransactions.Add(new XpTransaction
        {
            UserId = session.UserId, CourseId = scenario.CourseId, SourceType = Source,
            SourceId = session.Id, Amount = completionXp, EarnedAt = session.CompletedAt.Value
        });
        var preference = await database.UserPreferences.SingleAsync(item => item.UserId == session.UserId, token);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(preference.TimeZone);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(session.CompletedAt.Value, zone).DateTime);
        var day = await database.DailyActivities.SingleOrDefaultAsync(
            item => item.UserId == session.UserId && item.LocalDate == date, token);
        if (day is null)
        {
            day = new DailyActivity { UserId = session.UserId, LocalDate = date };
            database.DailyActivities.Add(day);
        }
        day.EarnedXp += completionXp;
        day.GoalReached |= day.EarnedXp >= preference.DailyGoalXp;
        return completionXp;
    }

    public Task<int> ForSessionAsync(Guid userId, Guid sessionId, CancellationToken token) =>
        database.XpTransactions.Where(item => item.UserId == userId && item.SourceType == Source
            && item.SourceId == sessionId).Select(item => item.Amount).SingleOrDefaultAsync(token);

    public async Task<int> ValueAsync(CancellationToken token) =>
        (await settings.ReadAsync(token)).Defaults.DialogueXp;
}
