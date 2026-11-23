using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record StaffUnit(Guid Id, string Title, string Description, int OrderNumber,
    string Difficulty, DateTimeOffset? ArchivedAt, int NextLessonOrder);

public sealed class ContentQueries(LexiGoDbContext database, ContentAccess access)
{
    public async Task<object?> CoursesAsync(Guid actorId, Guid familyId, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        if (actor is null) return null;
        return await database.Courses.AsNoTracking().Where(item => actor.IsAdmin || actor.LanguageIds.Contains(item.TargetLanguageId)).OrderBy(item => item.OrderNumber)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Description,
                item.TargetLanguageId,
                item.IsPublished,
                item.ArchivedAt,
                item.OrderNumber,
                CanAuthor = actor.IsAdmin || actor.Roles.Contains(RoleNames.ContentAuthor) && actor.LanguageIds.Contains(item.TargetLanguageId),
                CanReview = actor.Roles.Contains(RoleNames.Reviewer) && actor.LanguageIds.Contains(item.TargetLanguageId)
            }).Take(20).ToArrayAsync(cancellationToken);
    }

    public async Task<StaffUnit[]?> UnitsAsync(Guid actorId, Guid familyId, Guid courseId, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        var course = await database.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == courseId, cancellationToken);
        if (actor is null || course is null || !actor.IsAdmin && !actor.LanguageIds.Contains(course.TargetLanguageId)) return null;
        var units = database.Units.AsNoTracking().Where(item => item.CourseId == courseId)
            .OrderBy(item => item.OrderNumber).Take(200);
        return await UnitViews(units).ToArrayAsync(cancellationToken);
    }

    public async Task<StaffUnit?> UnitAsync(Guid actorId, Guid familyId, Guid unitId, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        if (actor is null) return null;
        var units = from unit in database.Units.AsNoTracking()
                    join course in database.Courses on unit.CourseId equals course.Id
                    where unit.Id == unitId && (actor.IsAdmin || actor.LanguageIds.Contains(course.TargetLanguageId))
                    select unit;
        return await UnitViews(units).SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<StaffUnit> UnitViews(IQueryable<Unit> units) =>
        units.Select(unit => new StaffUnit(unit.Id, unit.Title, unit.Description, unit.OrderNumber,
            unit.Difficulty, unit.ArchivedAt,
            (database.Lessons.Where(lesson => lesson.UnitId == unit.Id)
                .Select(lesson => (int?)lesson.OrderNumber).Max() ?? 0) + 1));

    public async Task<object?> LessonsAsync(Guid actorId, Guid familyId, Guid courseId, int page, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        var course = await database.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == courseId, cancellationToken);
        if (actor is null || course is null) return null;
        bool author = actor.CanAuthor(course.TargetLanguageId);
        bool reviewer = actor.Roles.Contains(RoleNames.Reviewer) && actor.LanguageIds.Contains(course.TargetLanguageId);
        var query = from lesson in database.Lessons.AsNoTracking()
                    join unit in database.Units on lesson.UnitId equals unit.Id
                    from version in database.LessonVersions.Where(item => item.LessonId == lesson.Id).OrderByDescending(item => item.VersionNumber).Take(1)
                    where unit.CourseId == courseId && (actor.IsAdmin || author && lesson.OwnerId == actorId || reviewer && (version.Status == "Submitted" || version.Status == "Approved"))
                    select new { lesson.Id, lesson.UnitId, lesson.OwnerId, lesson.Status, version.Title, version.Objective, VersionId = version.Id, version.VersionNumber, VersionStatus = version.Status, UnitOrder = unit.OrderNumber, lesson.OrderNumber };
        int total = await query.CountAsync(cancellationToken);
        return new { total, page, items = await query.OrderBy(item => item.UnitOrder).ThenBy(item => item.OrderNumber).Skip((page - 1) * 20).Take(20).ToArrayAsync(cancellationToken) };
    }

    public async Task<StaffLesson?> LessonAsync(Guid actorId, Guid familyId, Guid lessonId, Guid? versionId, CancellationToken cancellationToken)
    {
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        var parent = await (from lesson in database.Lessons.AsNoTracking()
                            join unit in database.Units on lesson.UnitId equals unit.Id
                            join course in database.Courses on unit.CourseId equals course.Id
                            where lesson.Id == lessonId
                            select new { Lesson = lesson, course.TargetLanguageId, CourseId = course.Id }).SingleOrDefaultAsync(cancellationToken);
        if (actor is null || parent is null) return null;
        var query = database.LessonVersions.AsNoTracking().Where(item => item.LessonId == lessonId);
        if (versionId is { } id) query = query.Where(item => item.Id == id);
        var version = await query.OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        if (version is null || !actor.CanRead(parent.TargetLanguageId, parent.Lesson.OwnerId, version.Status)) return null;
        bool latest = versionId is null || !await database.LessonVersions.AnyAsync(
            item => item.LessonId == lessonId && item.VersionNumber > version.VersionNumber, cancellationToken);
        var exercises = await database.Exercises.AsNoTracking().Where(item => item.LessonVersionId == version.Id).OrderBy(item => item.OrderNumber).Take(100).ToArrayAsync(cancellationToken);
        var ids = exercises.Select(item => item.Id).ToArray();
        var skills = await database.Skills.AsNoTracking().Where(item => item.CourseId == parent.CourseId)
            .OrderBy(item => item.Id).Select(item => new StaffSkill(item.Id, item.Name, item.IsArchived)).ToArrayAsync(cancellationToken);
        var tags = (await database.ExerciseSkills.AsNoTracking().Where(item => ids.Contains(item.ExerciseId))
            .OrderBy(item => item.SkillId).ToArrayAsync(cancellationToken)).ToLookup(item => item.ExerciseId);
        var options = await database.ExerciseOptions.AsNoTracking().Where(item => ids.Contains(item.ExerciseId)).OrderBy(item => item.OrderNumber).ToArrayAsync(cancellationToken);
        var answers = await database.AcceptedAnswers.AsNoTracking().Where(item => ids.Contains(item.ExerciseId)).ToArrayAsync(cancellationToken);
        var reviews = await database.ContentReviews.AsNoTracking().Where(item => item.LessonVersionId == version.Id).OrderByDescending(item => item.ReviewedAt).Take(20)
            .Select(item => new ReviewNote(item.ReviewerId, item.Decision, item.Comment, item.ReviewedAt)).ToArrayAsync(cancellationToken);
        return new(parent.Lesson.Id, parent.Lesson.UnitId, parent.Lesson.OwnerId, parent.TargetLanguageId, parent.Lesson.Status, parent.Lesson.CurrentVersionId,
            version.Id, version.VersionNumber, version.Status, version.Title, version.Objective, version.Explanation, version.CreatedBy,
            exercises.Select(item => new StaffExercise(item.Id, item.Type, item.Prompt, item.Explanation, item.OrderNumber, item.AudioMediaId, item.EvaluationRules,
                options.Where(option => option.ExerciseId == item.Id).Select(option => new StaffOption(option.Id, option.Text, option.OrderNumber, option.MatchKey, option.AudioMediaId)).ToArray(),
                answers.Where(answer => answer.ExerciseId == item.Id).Select(answer => new StaffAnswer(answer.Id, answer.AnswerText, answer.OrderedTokenIds)).ToArray(),
                item.ContextText, tags[item.Id].Select(tag => tag.SkillId).ToArray())).ToArray(),
            reviews, version.PublishedAt, version.PrivateTextErasedAt, latest, skills, parent.Lesson.OrderNumber);
    }

    public async Task<object?> VersionsAsync(Guid actorId, Guid familyId, Guid lessonId, int page, CancellationToken cancellationToken)
    {
        var visible = await LessonAsync(actorId, familyId, lessonId, null, cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, false, cancellationToken);
        if (visible is null || actor is null) return null;
        var query = database.LessonVersions.AsNoTracking().Where(item => item.LessonId == lessonId);
        if (!actor.CanEdit(visible.LanguageId, visible.OwnerId)) query = query.Where(item => item.Status == "Submitted" || item.Status == "Approved");
        return await query.OrderByDescending(item => item.VersionNumber).Skip((page - 1) * 20).Take(20)
            .Select(item => new { item.Id, item.VersionNumber, item.Status, item.CreatedBy, item.PublishedAt, item.PrivateTextErasedAt }).ToArrayAsync(cancellationToken);
    }
}
