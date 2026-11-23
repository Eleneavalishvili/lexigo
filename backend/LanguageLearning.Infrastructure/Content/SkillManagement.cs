using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class SkillManagement(LexiGoDbContext database, ContentAccess access,
    ContentTransactions content, TimeProvider clock)
{
    public async Task<ManagedSkillPage?> ListAsync(Guid actorId, Guid familyId, Guid courseId,
        int page, int pageSize, string? search, bool includeArchived, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, token);
        var course = await database.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == courseId, token);
        if (actor is null || course is null || !actor.IsAdmin && !actor.LanguageIds.Contains(course.TargetLanguageId)) return null;
        var query = database.Skills.AsNoTracking().Where(item => item.CourseId == courseId);
        if (!includeArchived) query = query.Where(item => !item.IsArchived);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            query = query.Where(item => item.Name.ToLower().Contains(term) || item.Category.ToLower().Contains(term));
        }
        int total = await query.CountAsync(token);
        var rows = await query.OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new ManagedSkill(item.Id, item.CourseId, item.Name, item.Category, item.IsArchived, item.Revision))
            .ToArrayAsync(token);
        return new(rows, page, pageSize, total);
    }

    public async Task<ManagedSkill?> GetAsync(Guid actorId, Guid familyId, int id, CancellationToken token)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, token);
        if (actor is null) return null;
        return await (from skill in database.Skills.AsNoTracking()
                      join course in database.Courses on skill.CourseId equals course.Id
                      where skill.Id == id && (actor.IsAdmin || actor.LanguageIds.Contains(course.TargetLanguageId))
                      select new ManagedSkill(skill.Id, skill.CourseId, skill.Name, skill.Category, skill.IsArchived, skill.Revision))
            .SingleOrDefaultAsync(token);
    }

    public async Task<SkillWriteResult> SaveAsync(Guid actorId, Guid familyId, int? id, SkillInput input,
        int revision, string reason, string traceId, CancellationToken token)
    {
        if (!Valid(input.Name, 100) || !Valid(input.Category, 40) || !Valid(reason, 500) || reason.Trim().Length < 3)
            return new(Error: "Use a name, category and reason within the stated limits.");
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var actor = await access.ActorAsync(actorId, familyId, true, token);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        Guid? courseId = id is null ? input.CourseId : await database.Skills.Where(item => item.Id == id)
            .Select(item => (Guid?)item.CourseId).SingleOrDefaultAsync(token);
        if (courseId is null || await content.LockCourseAsync(courseId.Value, token) is null) return new(Missing: true);
        if (id is null && await database.Skills.CountAsync(item => item.CourseId == courseId, token) >= 500)
            return new(Error: "This course has reached its 500-skill catalog limit, including archived history.");
        var skill = id is null ? new Skill { CourseId = courseId.Value, Name = input.Name.Trim(), Category = input.Category.Trim() }
            : await database.Skills.SingleOrDefaultAsync(item => item.Id == id, token);
        if (skill is null) return new(Missing: true);
        if (id is not null && skill.Revision != revision) return new(Error: "This skill changed. Reload it before saving.");
        string name = input.Name.Trim();
        if (await database.Skills.AnyAsync(item => item.CourseId == courseId && item.Id != skill.Id && item.Name == name, token))
            return new(Error: "A skill with this name already exists in the course, including archived skills.");
        skill.Name = name;
        skill.Category = input.Category.Trim();
        if (id is null) database.Skills.Add(skill);
        else skill.Revision++;
        await database.SaveChangesAsync(token);
        Audit(actorId, skill.Id, id is null ? "SkillCreated" : "SkillEdited", reason, traceId);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(View(skill));
    }

    public async Task<SkillWriteResult> SetArchiveAsync(Guid actorId, Guid familyId, int id,
        bool archived, int revision, string reason, string traceId, CancellationToken token)
    {
        if (!Valid(reason, 500) || reason.Trim().Length < 3) return new(Error: "Give a reason of 3–500 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        var actor = await access.ActorAsync(actorId, familyId, true, token);
        if (actor is null || !actor.IsAdmin) return new(Forbidden: true);
        Guid? courseId = await database.Skills.Where(item => item.Id == id).Select(item => (Guid?)item.CourseId).SingleOrDefaultAsync(token);
        if (courseId is null || await content.LockCourseAsync(courseId.Value, token) is null) return new(Missing: true);
        var skill = await database.Skills.SingleAsync(item => item.Id == id, token);
        if (skill.Revision != revision) return new(Error: "This skill changed. Reload it before saving.");
        if (skill.IsArchived == archived) return new(View(skill));
        skill.IsArchived = archived;
        skill.Revision++;
        Audit(actorId, id, archived ? "SkillArchived" : "SkillRestored", reason, traceId);
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(View(skill));
    }

    private static bool Valid(string value, int limit) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= limit && !value.Any(char.IsControl);

    private static ManagedSkill View(Skill skill) => new(skill.Id, skill.CourseId, skill.Name, skill.Category, skill.IsArchived, skill.Revision);

    private void Audit(Guid actorId, int id, string action, string reason, string traceId) => database.AuditLogs.Add(new AuditLog
    {
        ActorId = actorId, EntityType = "Skill", EntityId = id.ToString(), Action = action,
        Reason = reason.Trim(), TraceId = traceId, OccurredAt = clock.GetUtcNow()
    });
}
