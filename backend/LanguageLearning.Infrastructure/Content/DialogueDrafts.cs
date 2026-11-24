using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Learning;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class DialogueDrafts(LexiGoDbContext database, ContentAccess access,
    ContentTransactions content, TimeProvider clock)
{
    public async Task<StaffDialoguePage?> ListAsync(Guid actorId, Guid familyId, Guid courseId,
        int page, int pageSize, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, token);
        var course = await database.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == courseId, token);
        if (actor is null || course is null) return null;
        var query = database.Dialogues.AsNoTracking().Where(item => item.CourseId == courseId
            && (actor.IsAdmin || item.OwnerId == actorId && actor.LanguageIds.Contains(course.TargetLanguageId)
                || actor.Roles.Contains("ContentAuthor") && actor.LanguageIds.Contains(course.TargetLanguageId) && item.Status == "Published"
                || actor.Roles.Contains("Reviewer") && actor.LanguageIds.Contains(course.TargetLanguageId)
                    && item.Status != "Draft"));
        int totalCount = await query.CountAsync(token);
        var items = await query.OrderBy(item => item.Scenario).ThenByDescending(item => item.VersionNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new StaffDialogueRow(item.Id, item.CourseId, item.Title, item.Scenario,
                item.VersionNumber, item.Status, item.RequiresPremium, item.OwnerId, item.EditToken))
            .ToArrayAsync(token);
        return new(items, page, pageSize, totalCount);
    }

    public async Task<StaffDialogue?> DetailAsync(Guid actorId, Guid familyId, Guid id, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, token);
        var dialogue = await database.Dialogues.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, token);
        if (actor is null || dialogue is null) return null;
        var language = await database.Courses.Where(item => item.Id == dialogue.CourseId).Select(item => item.TargetLanguageId).SingleAsync(token);
        if (!CanRead(actor, language, dialogue)) return null;
        var nodes = await database.DialogueNodes.AsNoTracking().Where(item => item.DialogueId == id).OrderByDescending(item => item.IsStart).ThenBy(item => item.Id).ToArrayAsync(token);
        var choices = await database.DialogueChoices.AsNoTracking().Where(item => item.DialogueId == id).OrderBy(item => item.Id).ToArrayAsync(token);
        return new(new(id, dialogue.CourseId, dialogue.Title, dialogue.Scenario, dialogue.VersionNumber,
            dialogue.Status, dialogue.RequiresPremium, dialogue.OwnerId, dialogue.EditToken),
            nodes.Select(node => new DialogueNodeInput(node.Id.ToString(), node.Text, node.IsStart, node.IsTerminal,
                choices.Where(choice => choice.NodeId == node.Id).Select(choice => new DialogueChoiceInput(
                    choice.Text, choice.Feedback, choice.IsCorrect, choice.NextNodeId?.ToString())).ToArray(), node.AudioMediaId)).ToArray(),
            dialogue.ReviewerId, dialogue.ReviewedAt, dialogue.ReviewComment);
    }

    public async Task<ContentOutcome> CreateAsync(Guid actorId, Guid familyId, CreateDialogueInput input,
        string traceId, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var actor = await access.ActorAsync(actorId, familyId, true, token);
        var course = await content.LockCourseAsync(input.CourseId, token);
        if (actor is null || course is null || !actor.CanAuthor(course.TargetLanguageId)) return new(Forbidden: true);
        if (course.ArchivedAt != null) return new(Error: "Restore the course before creating material.");
        if (await database.Dialogues.AnyAsync(item => item.CourseId == course.Id && item.Scenario == input.Scenario, token))
            return new(Error: "This scenario exists. Open its latest version and create a revision.");
        var dialogue = new Dialogue
        {
            CourseId = course.Id,
            Title = input.Title.Trim(),
            Scenario = input.Scenario,
            OwnerId = actorId,
            RequiresPremium = input.RequiresPremium
        };
        database.Dialogues.Add(dialogue);
        Audit(actorId, dialogue, "DialogueDraftCreated", "New unpublished scenario.", traceId);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(dialogue.Id);
    }

    public async Task<ContentOutcome> SaveAsync(Guid actorId, Guid familyId, Guid id, DialogueDraftInput input,
        string traceId, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var state = await LockAsync(actorId, familyId, id, token);
        if (state is null) return new(Forbidden: true);
        var (actor, language, dialogue) = state.Value;
        if (!actor.CanEdit(language, dialogue.OwnerId ?? Guid.Empty)) return new(Forbidden: true);
        if (dialogue.Status != "Draft" || dialogue.EditToken != input.EditToken)
            return new(Error: "This draft changed or is no longer editable. Reload it first.");
        if (input.Nodes.Any(node => string.IsNullOrWhiteSpace(node.Key) || string.IsNullOrWhiteSpace(node.Text)
            || node.Choices.Any(choice => string.IsNullOrWhiteSpace(choice.Text) || string.IsNullOrWhiteSpace(choice.Feedback)))
            || input.Nodes.Select(node => node.Key).Distinct().Count() != input.Nodes.Length)
            return new(Error: "Use unique node keys and fill in every text and feedback field.");
        var ids = input.Nodes.ToDictionary(node => node.Key, _ => Guid.NewGuid());
        if (input.Nodes.Any(node => node.Choices.Any(choice => choice.NextNodeKey is { } key && !ids.ContainsKey(key))))
            return new(Error: "Each destination must be a node in this dialogue.");
        if (!ValidGraph(input.Nodes)) return new(Error: "Use one start, reachable nodes, no cycles, and one to six choices per non-ending node. Ending nodes have no choices.");
        var mediaIds = input.Nodes.Where(node => node.AudioMediaId != null).Select(node => node.AudioMediaId!.Value).Distinct().ToArray();
        await ReviewedAudio.LockAsync(database, mediaIds, token);
        if (await ReviewedAudio.ForLanguage(database.MediaAssets, language).CountAsync(media => mediaIds.Contains(media.Id), token) != mediaIds.Length)
            return new(Error: "Choose independently reviewed ready audio in the course language.");
        // Drafts cannot have learner sessions. Published revisions are never rewritten.
        if (await database.DialogueSessions.AnyAsync(item => item.DialogueId == id, token))
            return new(Error: "This version has learning history. Create a revision instead.");
        await database.DialogueChoices.Where(item => item.DialogueId == id).ExecuteDeleteAsync(token);
        await database.DialogueNodes.Where(item => item.DialogueId == id).ExecuteDeleteAsync(token);
        foreach (var node in input.Nodes)
        {
            database.DialogueNodes.Add(new DialogueNode
            {
                Id = ids[node.Key],
                DialogueId = id,
                Text = node.Text.Trim(),
                IsStart = node.IsStart,
                IsTerminal = node.IsTerminal,
                AudioMediaId = node.AudioMediaId
            });
            foreach (var choice in node.Choices)
                database.DialogueChoices.Add(new DialogueChoice
                {
                    DialogueId = id,
                    NodeId = ids[node.Key],
                    Text = choice.Text.Trim(),
                    Feedback = choice.Feedback.Trim(),
                    IsCorrect = choice.IsCorrect,
                    NextNodeId = choice.NextNodeKey is { } key ? ids[key] : null
                });
        }
        dialogue.Title = input.Title.Trim();
        dialogue.RequiresPremium = input.RequiresPremium;
        dialogue.ReviewerId = null;
        dialogue.ReviewedAt = null;
        dialogue.ReviewComment = null;
        dialogue.EditToken = Guid.NewGuid();
        await database.SaveChangesAsync(token);
        if (await DialogueGraphLoader.LoadAsync(database, dialogue, token) is null)
            return new(Error: "Use one start, reachable nodes, no cycles, and one to six choices per non-ending node. Ending nodes have no choices.");
        Audit(actorId, dialogue, "DialogueDraftEdited", "Draft graph saved.", traceId);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(id);
    }

    internal async Task<(ContentActor Actor, int Language, Dialogue Dialogue)?> LockAsync(
        Guid actorId, Guid familyId, Guid id, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, familyId, true, token);
        var courseId = await database.Dialogues.Where(item => item.Id == id).Select(item => (Guid?)item.CourseId).SingleOrDefaultAsync(token);
        if (actor is null || courseId is null) return null;
        var course = await content.LockCourseAsync(courseId.Value, token);
        var dialogue = await database.Dialogues.SingleAsync(item => item.Id == id, token);
        return course is null || !CanRead(actor, course.TargetLanguageId, dialogue)
            ? null : (actor, course.TargetLanguageId, dialogue);
    }

    private static bool CanRead(ContentActor actor, int language, Dialogue dialogue) => actor.IsAdmin
        || actor.CanEdit(language, dialogue.OwnerId ?? Guid.Empty)
        || actor.CanAuthor(language) && dialogue.Status == "Published"
        || actor.Roles.Contains("Reviewer") && actor.LanguageIds.Contains(language) && dialogue.Status != "Draft";

    private static bool ValidGraph(DialogueNodeInput[] nodes)
    {
        if (nodes.Length is < 2 or > 40 || nodes.Count(node => node.IsStart) != 1
            || nodes.Any(node => node.IsStart && node.IsTerminal
                || (node.IsTerminal ? node.Choices.Length != 0 : node.Choices.Length is < 1 or > 6 || !node.Choices.Any(choice => choice.IsCorrect)))) return false;
        var byKey = nodes.ToDictionary(node => node.Key);
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        return Visit(nodes.Single(node => node.IsStart).Key) && visited.Count == nodes.Length;
        bool Visit(string key)
        {
            if (visited.Contains(key)) return true;
            if (!byKey.TryGetValue(key, out var node) || !visiting.Add(key)) return false;
            if (node.Choices.Any(choice => choice.NextNodeKey is { } next && !Visit(next))) return false;
            visiting.Remove(key);
            visited.Add(key);
            return true;
        }
    }

    internal void Audit(Guid actorId, Dialogue dialogue, string action, string reason, string traceId) =>
        database.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            EntityType = "Dialogue",
            EntityId = dialogue.Id.ToString(),
            Action = action,
            Reason = reason,
            TraceId = traceId,
            OccurredAt = clock.GetUtcNow()
        });
}
