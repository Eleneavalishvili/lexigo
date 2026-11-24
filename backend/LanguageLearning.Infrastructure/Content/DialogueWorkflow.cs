using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class DialogueWorkflow(LexiGoDbContext database, DialogueDrafts drafts, TimeProvider clock)
{
    public async Task<ContentOutcome> TransitionAsync(Guid actorId, Guid familyId, Guid id,
        string operation, DialogueTransition request, string traceId, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var state = await drafts.LockAsync(actorId, familyId, id, token);
        if (state is null) return new(Forbidden: true);
        var (actor, language, dialogue) = state.Value;
        if (dialogue.EditToken != request.EditToken) return new(Error: "This version changed. Reload before continuing.");
        switch (operation)
        {
            case "submit":
                if (!actor.CanEdit(language, dialogue.OwnerId ?? Guid.Empty)) return new(Forbidden: true);
                if (dialogue.Status != "Draft") return new(Error: "Only a draft can be submitted.");
                if (await DialogueGraphLoader.LoadAsync(database, dialogue, token) is null) return new(Error: "Save a valid complete graph first.");
                dialogue.Status = "Submitted";
                break;
            case "approve":
            case "return":
                if (!actor.CanReview(language, dialogue.OwnerId ?? Guid.Empty, dialogue.OwnerId ?? Guid.Empty)) return new(Forbidden: true);
                if (dialogue.Status != "Submitted") return new(Error: "Only submitted material can be reviewed.");
                dialogue.Status = operation == "approve" ? "Approved" : "Draft";
                dialogue.ReviewerId = actorId;
                dialogue.ReviewedAt = clock.GetUtcNow();
                dialogue.ReviewComment = request.Comment.Trim();
                break;
            case "publish":
                if (!actor.IsAdmin) return new(Forbidden: true);
                if (dialogue.Status != "Approved" || dialogue.ReviewerId is null || dialogue.ReviewerId == dialogue.OwnerId)
                    return new(Error: "Independent review is required before publication.");
                if (await database.Courses.AnyAsync(item => item.Id == dialogue.CourseId && item.ArchivedAt != null, token))
                    return new(Error: "Restore the course before publishing material.");
                if (await DialogueGraphLoader.LoadAsync(database, dialogue, token) is null) return new(Error: "The graph is not valid.");
                var mediaIds = await database.DialogueNodes.Where(node => node.DialogueId == id && node.AudioMediaId != null)
                    .Select(node => node.AudioMediaId!.Value).Distinct().ToArrayAsync(token);
                await ReviewedAudio.LockAsync(database, mediaIds, token);
                if (await ReviewedAudio.ForLanguage(database.MediaAssets, language).CountAsync(media => mediaIds.Contains(media.Id), token) != mediaIds.Length)
                    return new(Error: "A recording is no longer reviewed and available for this language.");
                var previous = await database.Dialogues.Where(item => item.CourseId == dialogue.CourseId
                    && item.Scenario == dialogue.Scenario && item.Status == "Published").ToArrayAsync(token);
                foreach (var version in previous) version.Status = "Archived";
                dialogue.Status = "Published";
                break;
            case "archive":
                if (dialogue.Status == "Published" ? !actor.IsAdmin : !actor.CanEdit(language, dialogue.OwnerId ?? Guid.Empty))
                    return new(Forbidden: true);
                bool previouslyPublished = dialogue.OwnerId is null || dialogue.Status == "Published"
                    || await database.AuditLogs.AnyAsync(log => log.EntityType == "Dialogue" && log.EntityId == id.ToString()
                        && log.Action == "Dialogue.publish", token);
                if (!previouslyPublished)
                    await database.DialogueNodes.Where(node => node.DialogueId == id)
                        .ExecuteUpdateAsync(update => update.SetProperty(node => node.AudioMediaId, (Guid?)null), token);
                dialogue.Status = "Archived";
                break;
            default:
                return new(Error: "Unknown dialogue action.");
        }
        dialogue.EditToken = Guid.NewGuid();
        drafts.Audit(actorId, dialogue, "Dialogue." + operation, request.Comment.Trim(), traceId);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(id);
    }

    public async Task<ContentOutcome> RevisionAsync(Guid actorId, Guid familyId, Guid id,
        string traceId, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var state = await drafts.LockAsync(actorId, familyId, id, token);
        if (state is null) return new(Forbidden: true);
        var (actor, language, original) = state.Value;
        if (!actor.CanAuthor(language)) return new(Forbidden: true);
        if (original.Status is not ("Published" or "Archived")) return new(Error: "Revise a published or archived version.");
        var siblings = await database.Dialogues.Where(item => item.CourseId == original.CourseId && item.Scenario == original.Scenario).ToArrayAsync(token);
        if (siblings.Any(item => item.Status is "Draft" or "Submitted" or "Approved"))
            return new(Error: "This scenario already has an open revision.");
        var copy = new Dialogue
        {
            CourseId = original.CourseId,
            Title = original.Title,
            Scenario = original.Scenario,
            VersionNumber = siblings.Max(item => item.VersionNumber) + 1,
            OwnerId = actorId,
            RequiresPremium = original.RequiresPremium
        };
        database.Dialogues.Add(copy);
        var nodes = await database.DialogueNodes.AsNoTracking().Where(item => item.DialogueId == id).ToArrayAsync(token);
        var ids = nodes.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        foreach (var node in nodes)
            database.DialogueNodes.Add(new DialogueNode
            {
                Id = ids[node.Id],
                DialogueId = copy.Id,
                Text = node.Text,
                AudioMediaId = node.AudioMediaId,
                IsStart = node.IsStart,
                IsTerminal = node.IsTerminal
            });
        var choices = await database.DialogueChoices.AsNoTracking().Where(item => item.DialogueId == id).ToArrayAsync(token);
        foreach (var choice in choices)
            database.DialogueChoices.Add(new DialogueChoice
            {
                DialogueId = copy.Id,
                NodeId = ids[choice.NodeId],
                Text = choice.Text,
                Feedback = choice.Feedback,
                IsCorrect = choice.IsCorrect,
                NextNodeId = choice.NextNodeId is { } next ? ids[next] : null
            });
        drafts.Audit(actorId, copy, "DialogueRevisionCreated", "Independent draft copied from version " + original.VersionNumber, traceId);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(copy.Id);
    }
}
