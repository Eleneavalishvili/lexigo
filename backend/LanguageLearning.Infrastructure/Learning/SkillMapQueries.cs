using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Learning;

public sealed record SkillMapEntry(int Id, string Name, string Category,
    int CorrectCount, int IncorrectCount, DateTimeOffset? LastPracticedAt);

public sealed class SkillMapQueries(LexiGoDbContext database)
{
    public async Task<SkillMapEntry[]?> ReadAsync(Guid userId, Guid courseId, CancellationToken cancellationToken)
    {
        var rows = await (from enrollment in database.Enrollments
                          join course in database.Courses on enrollment.CourseId equals course.Id
                          where enrollment.UserId == userId && course.Id == courseId && course.IsPublished
                          from skill in database.Skills.Where(item => item.CourseId == course.Id).DefaultIfEmpty()
                          from progress in database.SkillProgress
                              .Where(item => item.UserId == userId && item.SkillId == skill.Id).DefaultIfEmpty()
                          orderby skill.Id
                          select new
                          {
                              Id = (int?)skill.Id,
                              skill.Name,
                              skill.Category,
                              CorrectCount = (int?)progress.CorrectCount ?? 0,
                              IncorrectCount = (int?)progress.IncorrectCount ?? 0,
                              progress.LastPracticedAt
                          }).ToArrayAsync(cancellationToken);

        if (rows.Length == 0) return null;
        return rows.Where(row => row.Id.HasValue).Select(row => new SkillMapEntry(
            row.Id!.Value, row.Name, row.Category, row.CorrectCount, row.IncorrectCount,
            row.LastPracticedAt)).ToArray();
    }
}
