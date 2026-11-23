using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

internal static class DialogueGraphLoader
{
    internal const int MaximumNodes = 40;
    internal const int MaximumChoices = 6;

    internal static async Task<DialogueGraph?> LoadAsync(LexiGoDbContext database, Dialogue dialogue,
        CancellationToken cancellationToken)
    {
        var nodes = await database.DialogueNodes.AsNoTracking().Where(item => item.DialogueId == dialogue.Id)
            .Take(MaximumNodes + 1).ToArrayAsync(cancellationToken);
        if (nodes.Length is < 2 or > MaximumNodes || nodes.Count(item => item.IsStart) != 1) return null;

        var choices = await database.DialogueChoices.AsNoTracking().Where(item => item.DialogueId == dialogue.Id)
            .OrderBy(item => item.Id).Take(MaximumNodes * MaximumChoices + 1).ToArrayAsync(cancellationToken);
        var byNode = choices.ToLookup(item => item.NodeId);
        var graphNodes = nodes.ToDictionary(item => item.Id, item => new DialogueGraphNode(
            item.Id, item.Text, item.IsTerminal, byNode[item.Id].Select(choice => new DialogueGraphChoice(
                choice.Id, choice.Text, choice.Feedback, choice.IsCorrect, choice.NextNodeId)).ToArray()));
        if (graphNodes.Values.Any(node => node.IsTerminal ? node.Choices.Length != 0
            : node.Choices.Length is < 1 or > MaximumChoices)) return null;

        var startId = nodes.Single(item => item.IsStart).Id;
        var visited = new HashSet<Guid>();
        var visiting = new HashSet<Guid>();
        return Visit(startId) && visited.Count == nodes.Length
            ? new DialogueGraph(dialogue.Title, startId, graphNodes.Values.ToArray())
            : null;

        bool Visit(Guid id)
        {
            if (visited.Contains(id)) return true;
            if (!graphNodes.TryGetValue(id, out var node) || !visiting.Add(id)) return false;
            foreach (var choice in node.Choices)
            {
                if (choice.NextNodeId is { } next && !Visit(next)) return false;
            }
            visiting.Remove(id);
            visited.Add(id);
            return true;
        }
    }
}
