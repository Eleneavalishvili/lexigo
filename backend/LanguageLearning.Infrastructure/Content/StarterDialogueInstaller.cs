using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class StarterDialogueInstaller(LexiGoDbContext database, ContentTransactions content)
{
    private const int Version = 2;

    public async Task<int> InstallAsync(CancellationToken cancellationToken)
    {
        var courses = await database.Courses.AsNoTracking().Where(item => item.SourceLanguage.Code == "en")
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.TargetLanguage.Code }).ToArrayAsync(cancellationToken);
        int installed = 0;
        foreach (var course in courses)
        {
            var scenarios = StarterDialogues.ForLanguage(course.Code);
            if (scenarios.Length == 0) continue;
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            await content.LockCourseAsync(course.Id, cancellationToken);
            var existing = await database.Dialogues.Where(item => item.CourseId == course.Id)
                .Select(item => new
                {
                    item.Scenario, item.VersionNumber, item.Title,
                    Opening = database.DialogueNodes.Where(node => node.DialogueId == item.Id && node.IsStart)
                        .Select(node => node.Text).FirstOrDefault()
                }).ToArrayAsync(cancellationToken);
            foreach (var scenario in scenarios)
            {
                if (existing.Any(item => item.Scenario == scenario.Key && (item.VersionNumber >= Version
                    || item.Title != scenario.Title || item.Opening != scenario.Opening))) continue;
                AddScenario(course.Id, scenario, course.Code);
                installed++;
            }
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return installed;
    }

    private void AddScenario(Guid courseId, StarterDialogues.Scenario scenario, string language)
    {
        var dialogue = new Dialogue
        {
            CourseId = courseId, Scenario = scenario.Key, Title = scenario.Title,
            VersionNumber = Version, Status = "Published", RequiresPremium = scenario.RequiresPremium
        };
        var opening = new DialogueNode { DialogueId = dialogue.Id, Text = scenario.Opening, IsStart = true };
        database.AddRange(dialogue, opening);
        AddBranch(scenario.First);
        AddBranch(scenario.Second);
        var (response, endingText) = language switch
        {
            "es" => ("¡Date prisa!", "¿Puede pedírmelo con más calma?"),
            "it" => ("Sbrigati!", "Può ripetere la richiesta con più calma?"),
            "de" => ("Beeil dich!", "Können Sie Ihre Bitte noch einmal ruhig formulieren?"),
            "ru" => ("Поторопись!", "Можете повторить просьбу спокойнее?"),
            _ => throw new InvalidOperationException("Unsupported dialogue language.")
        };
        var correction = new DialogueNode { DialogueId = dialogue.Id, Text = endingText, IsTerminal = true };
        database.DialogueNodes.Add(correction);
        database.DialogueChoices.Add(new DialogueChoice
        {
            DialogueId = dialogue.Id, NodeId = opening.Id, NextNodeId = correction.Id,
            Text = response, IsCorrect = false,
            Feedback = "This means “Hurry up!” and doesn’t answer their question. Try making a specific, polite request."
        });

        void AddBranch(StarterDialogues.Branch branch)
        {
            var followUp = new DialogueNode { DialogueId = dialogue.Id, Text = branch.FollowUp };
            var ending = new DialogueNode { DialogueId = dialogue.Id, Text = branch.Ending, IsTerminal = true };
            database.AddRange(followUp, ending);
            database.DialogueChoices.AddRange(
                new DialogueChoice
                {
                    DialogueId = dialogue.Id, NodeId = opening.Id, NextNodeId = followUp.Id,
                    Text = branch.Response, Feedback = scenario.Feedback, IsCorrect = true
                },
                new DialogueChoice
                {
                    DialogueId = dialogue.Id, NodeId = followUp.Id, NextNodeId = ending.Id,
                    Text = branch.Reply, Feedback = scenario.Feedback, IsCorrect = true
                });
        }
    }
}
