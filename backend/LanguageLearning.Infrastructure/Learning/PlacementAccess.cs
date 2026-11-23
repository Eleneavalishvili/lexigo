using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static LanguageLearning.Infrastructure.Learning.LearningQueries;

namespace LanguageLearning.Infrastructure.Learning;

internal static class PlacementAccess
{
    public static async Task<HashSet<Guid>> BypassedAsync(LexiGoDbContext database, Guid userId, Guid courseId, CancellationToken cancellationToken)
    {
        var applied = await database.LearningSessions.AsNoTracking().Where(item => item.UserId == userId
            && item.CourseId == courseId && item.PlacementAppliedAt != null && item.RecommendedUnitId != null)
            .Select(item => new { item.PlacementMap, item.RecommendedUnitId }).ToArrayAsync(cancellationToken);
        return applied.SelectMany(item => Decode<PlacementUnitSnapshot[]>(item.PlacementMap!)
            .TakeWhile(unit => unit.Id != item.RecommendedUnitId).SelectMany(unit => unit.LessonIds)).ToHashSet();
    }
}
