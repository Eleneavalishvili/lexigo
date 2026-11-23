using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed class CatalogQueries(LexiGoDbContext database)
{
    public async Task<IReadOnlyList<CatalogCourse>> CoursesAsync(CancellationToken cancellationToken) =>
        await database.Courses.AsNoTracking().Where(course => course.IsPublished && database.Units.Any(unit => unit.CourseId == course.Id && unit.ArchivedAt == null
            && database.Lessons.Any(lesson => lesson.UnitId == unit.Id && lesson.Status == "Published" && lesson.CurrentVersionId != null)))
            .OrderBy(course => course.OrderNumber).Select(course => new CatalogCourse(course.Id, course.Title, course.Description, course.TargetLanguage.Code,
                database.Lessons.Count(lesson => lesson.Status == "Published" && lesson.CurrentVersionId != null && database.Units.Any(unit => unit.Id == lesson.UnitId && unit.CourseId == course.Id && unit.ArchivedAt == null))))
            .Take(20).ToArrayAsync(cancellationToken);

    public async Task<CatalogCourse?> CourseAsync(Guid id, CancellationToken cancellationToken) => (await CoursesAsync(cancellationToken)).SingleOrDefault(item => item.Id == id);

    public async Task<IReadOnlyList<CatalogUnit>?> UnitsAsync(Guid courseId, CancellationToken cancellationToken)
    {
        if (await CourseAsync(courseId, cancellationToken) is null) return null;
        return await database.Units.AsNoTracking().Where(unit => unit.CourseId == courseId && unit.ArchivedAt == null && database.Lessons.Any(lesson => lesson.UnitId == unit.Id && lesson.Status == "Published" && lesson.CurrentVersionId != null))
            .OrderBy(unit => unit.OrderNumber).Select(unit => new CatalogUnit(unit.Id, unit.Title, unit.Description, unit.Difficulty, unit.OrderNumber)).Take(200).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogLesson>?> LessonsAsync(Guid unitId, CancellationToken cancellationToken)
    {
        var unit = await database.Units.AsNoTracking().SingleOrDefaultAsync(item => item.Id == unitId, cancellationToken);
        if (unit is null || unit.ArchivedAt.HasValue || await CourseAsync(unit.CourseId, cancellationToken) is null) return null;
        return await PublishedLessons(unitId: unitId).Take(200).ToArrayAsync(cancellationToken);
    }

    public Task<CatalogLesson?> LessonAsync(Guid lessonId, CancellationToken cancellationToken) => PublishedLessons(lessonId: lessonId).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<CatalogLesson> PublishedLessons(Guid? unitId = null, Guid? lessonId = null) => from lesson in database.Lessons.AsNoTracking()
                                                                                                      join version in database.LessonVersions on lesson.CurrentVersionId equals version.Id
                                                                                                      join unit in database.Units on lesson.UnitId equals unit.Id
                                                                                                      join course in database.Courses on unit.CourseId equals course.Id
                                                                                                      where lesson.Status == "Published" && version.Status == "Published" && version.LessonId == lesson.Id && course.IsPublished && unit.ArchivedAt == null
                                                                                                          && (!unitId.HasValue || lesson.UnitId == unitId) && (!lessonId.HasValue || lesson.Id == lessonId)
                                                                                                      orderby lesson.OrderNumber
                                                                                                      select new CatalogLesson(lesson.Id, lesson.UnitId, version.Id, version.Title, version.Objective, version.Explanation, lesson.OrderNumber,
                                                                                                          database.Exercises.Count(exercise => exercise.LessonVersionId == version.Id));
}

public sealed record CatalogCourse(Guid Id, string Title, string Description, string LanguageCode, int LessonCount);
public sealed record CatalogUnit(Guid Id, string Title, string Description, string Difficulty, int OrderNumber);
public sealed record CatalogLesson(Guid Id, Guid UnitId, Guid VersionId, string Title, string Objective, string Explanation, int OrderNumber, int ExerciseCount);
