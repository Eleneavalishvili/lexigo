using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Content;

public sealed record StarterQuestion(string Type, string Prompt, string[] Answers, string Explanation, string[] Options, string? ContextText,
    int[][]? TokenOrders = null);
public sealed record StarterLesson(string Key, string Title, string Objective, string Explanation, StarterQuestion[] Exercises,
    string? UnitKey = null, string? UnitTitle = null, string? Difficulty = null, int ContentVersion = 1,
    string? UnitDescription = null, bool IsCheckpoint = false, string[]? PrerequisiteKeys = null);
public sealed record StarterCourse(string Code, Guid CourseId, string UnitTitle, string UnitDescription, StarterLesson[] Lessons);

public sealed class StarterLessonInstaller(LexiGoDbContext database, LessonValidation validation)
{
    public static Guid ContentId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("lexigo.starter.v1/" + key)).AsSpan(0, 16));

    public static StarterCourse Read(string code)
    {
        if (code is not ("es" or "it" or "de" or "ru")) throw new ArgumentException("Unknown starter language.", nameof(code));
        using var stream = typeof(StarterLessonInstaller).Assembly.GetManifestResourceStream(
            $"LanguageLearning.Infrastructure.Content.Starter.{code}.json") ?? throw new InvalidOperationException("Starter content is missing.");
        return JsonSerializer.Deserialize<StarterCourse>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Starter content is invalid.");
    }

    public async Task<int> InstallAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730027020)", cancellationToken);
        Guid ownerId = ContentId("system-owner");
        if (!await database.Users.AnyAsync(item => item.Id == ownerId, cancellationToken))
        {
            // Attribution only: no password, email, roles, confirmation or usable sign-in identity.
            database.Users.Add(new ApplicationUser
            {
                Id = ownerId,
                UserName = "lexigo-built-in-content",
                NormalizedUserName = "LEXIGO-BUILT-IN-CONTENT",
                DisplayName = "LexiGo built-in lesson library (system)",
                Status = "Disabled",
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });
            await database.SaveChangesAsync(cancellationToken);
        }
        int installed = 0;
        foreach (string code in new[] { "es", "it", "de", "ru" })
        {
            var pack = Read(code);
            var course = await database.Courses
                .FromSqlInterpolated($"SELECT * FROM \"Courses\" WHERE \"Id\" = {pack.CourseId} FOR UPDATE")
                .SingleAsync(cancellationToken);
            await EnsurePictureUnitAsync(pack, cancellationToken);
            Guid unitId = ContentId(code + "/unit");
            if (!await database.Units.AnyAsync(item => item.Id == unitId, cancellationToken))
            {
                int order = (await database.Units.Where(item => item.CourseId == course.Id).Select(item => (int?)item.OrderNumber).MaxAsync(cancellationToken) ?? 0) + 1;
                database.Units.Add(new Unit
                {
                    Id = unitId,
                    CourseId = course.Id,
                    Title = pack.UnitTitle,
                    Description = pack.UnitDescription,
                    OrderNumber = order,
                    Difficulty = "Beginner"
                });
                await database.SaveChangesAsync(cancellationToken);
            }
            for (int index = 0; index < pack.Lessons.Length; index++)
            {
                var source = pack.Lessons[index];
                Guid targetUnitId = source.UnitKey is null ? unitId : ContentId(code + "/unit/" + source.UnitKey);
                var targetUnit = await database.Units.SingleOrDefaultAsync(item => item.Id == targetUnitId, cancellationToken);
                if (targetUnit?.ArchivedAt != null) continue;
                if (targetUnit is null)
                {
                    var precedingUnitIds = pack.Lessons.Take(index)
                        .Select(item => item.UnitKey is null ? unitId : ContentId(code + "/unit/" + item.UnitKey))
                        .Distinct().ToArray();
                    int order = (await database.Units.Where(item => item.CourseId == course.Id
                        && precedingUnitIds.Contains(item.Id)).Select(item => (int?)item.OrderNumber)
                        .MaxAsync(cancellationToken) ?? 0) + 1;
                    // Insert new beginner material in curriculum order without changing existing IDs or prerequisites.
                    var followingUnits = await database.Units.Where(item => item.CourseId == course.Id
                        && item.OrderNumber >= order).OrderByDescending(item => item.OrderNumber)
                        .ToArrayAsync(cancellationToken);
                    foreach (var followingUnit in followingUnits)
                    {
                        followingUnit.OrderNumber++;
                        await database.SaveChangesAsync(cancellationToken);
                    }
                    database.Units.Add(new Unit
                    {
                        Id = targetUnitId,
                        CourseId = course.Id,
                        Title = source.UnitTitle ?? throw new InvalidOperationException("A content unit needs a title."),
                        Description = source.UnitDescription ?? (source.UnitKey == "everyday"
                            ? "A1 practice: directions, shopping, home and asking for help."
                            : "A2 practice: plans, past events, reasons and polite requests."),
                        Difficulty = source.Difficulty ?? "A1",
                        OrderNumber = order
                    });
                    await database.SaveChangesAsync(cancellationToken);
                }
                Guid lessonId = ContentId(code + "/" + source.Key);
                var existing = await database.Lessons.SingleOrDefaultAsync(item => item.Id == lessonId, cancellationToken);
                if (existing is not null && await database.LessonVersions.AnyAsync(item => item.LessonId == lessonId
                    && item.VersionNumber >= source.ContentVersion, cancellationToken)) continue;
                Guid[] prerequisiteIds = [];
                if (existing is null)
                {
                    var earlier = pack.Lessons.Take(index).ToArray();
                    string[] keys = source.PrerequisiteKeys ?? earlier
                        .Where(item => !item.IsCheckpoint).TakeLast(1).Select(item => item.Key).ToArray();
                    if (keys.Distinct().Count() != keys.Length || keys.Any(key => !earlier.Any(item => item.Key == key)))
                        throw new InvalidOperationException("Built-in prerequisites must reference distinct earlier lessons.");
                    prerequisiteIds = keys.Select(key => ContentId(code + "/" + key)).ToArray();
                    if (prerequisiteIds.Length > 0 && await (from prerequisite in database.Lessons
                        join prerequisiteUnit in database.Units on prerequisite.UnitId equals prerequisiteUnit.Id
                        where prerequisiteIds.Contains(prerequisite.Id) && prerequisite.Status == "Published"
                            && prerequisiteUnit.ArchivedAt == null
                        select prerequisite.Id).CountAsync(cancellationToken) != prerequisiteIds.Length)
                        continue;
                }
                if (source.ContentVersion < 1) throw new InvalidOperationException("Invalid built-in content version.");
                var lesson = existing ?? new Lesson
                {
                    Id = lessonId,
                    UnitId = targetUnitId,
                    OwnerId = ownerId,
                    Title = source.Title,
                    Objective = source.Objective,
                    OrderNumber = pack.Lessons.Take(index + 1).Count(item => item.UnitKey == source.UnitKey)
                };
                var version = new LessonVersion
                {
                    Id = ContentId(code + "/" + source.Key + "/version-" + source.ContentVersion),
                    LessonId = lessonId,
                    CreatedBy = ownerId,
                    Title = source.Title,
                    Objective = source.Objective,
                    Explanation = source.Explanation,
                    VersionNumber = source.ContentVersion
                };
                if (existing is null) database.Lessons.Add(lesson);
                database.LessonVersions.Add(version);
                for (int questionIndex = 0; questionIndex < source.Exercises.Length; questionIndex++)
                {
                    var question = source.Exercises[questionIndex];
                    var exercise = new Exercise
                    {
                        LessonVersionId = version.Id,
                        Type = question.Type,
                        Prompt = question.Prompt,
                        Explanation = question.Explanation,
                        ContextText = question.ContextText,
                        OrderNumber = questionIndex + 1,
                        EvaluationRules = TextEvaluationRulesCodec.Write(new TextEvaluationRules(true, true))
                    };
                    database.Exercises.Add(exercise);
                    var choices = question.Options.Select((text, optionIndex) => new ExerciseOption
                    {
                        ExerciseId = exercise.Id,
                        Text = text,
                        OrderNumber = optionIndex + 1
                    }).ToArray();
                    database.ExerciseOptions.AddRange(choices);
                    if (question.Type == "SentenceBuilder")
                    {
                        if (!SentenceAnswerKeys.ValidPositions(question.TokenOrders ?? [], choices.Length))
                            throw new InvalidOperationException("Built-in word-bank answers need valid tile positions.");
                        database.AcceptedAnswers.AddRange(question.TokenOrders!.Select(order => new AcceptedAnswer
                        {
                            ExerciseId = exercise.Id,
                            AnswerText = string.Join(" ", order.Select(position => choices[position - 1].Text)),
                            OrderedTokenIds = JsonSerializer.Serialize(order.Select(position => choices[position - 1].Id).ToArray())
                        }));
                    }
                    else database.AcceptedAnswers.AddRange(question.Answers.Select(answer => new AcceptedAnswer { ExerciseId = exercise.Id, AnswerText = answer }));
                }
                await database.SaveChangesAsync(cancellationToken);
                string? error = await validation.ValidateAsync(version.Id, cancellationToken);
                if (error is not null) throw new InvalidOperationException($"Invalid built-in {code}/{source.Key}: {error}");
                database.LessonPrerequisites.AddRange(prerequisiteIds.Select(id => new LessonPrerequisite
                {
                    LessonId = lesson.Id,
                    PrerequisiteLessonId = id
                }));
                version.Status = lesson.Status = "Published";
                version.PublishedAt = DateTimeOffset.UtcNow;
                lesson.CurrentVersionId = version.Id;
                course.IsPublished = !course.ArchivedAt.HasValue;
                database.AuditLogs.Add(new AuditLog
                {
                    Action = "Content.BuiltInInstalled",
                    EntityType = "LessonVersion",
                    EntityId = version.Id.ToString(),
                    TraceId = "starter-v" + source.ContentVersion + "/" + code + "/" + source.Key,
                    Reason = "User-approved built-in starter content. Automated publication validation; no independent human review claimed.",
                    OccurredAt = DateTimeOffset.UtcNow
                });
                await database.SaveChangesAsync(cancellationToken);
                installed++;
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return installed;
    }

    private async Task EnsurePictureUnitAsync(StarterCourse pack, CancellationToken cancellationToken)
    {
        var source = pack.Lessons.FirstOrDefault(item => item.UnitKey == "first-words");
        if (source is null) return;
        Guid unitId = ContentId(pack.Code + "/unit/first-words");
        if (await database.Units.AnyAsync(item => item.Id == unitId, cancellationToken)) return;
        // Descending updates preserve the unique course/order constraint without renumbering lesson IDs.
        var units = await database.Units.Where(item => item.CourseId == pack.CourseId)
            .OrderByDescending(item => item.OrderNumber).ToArrayAsync(cancellationToken);
        foreach (var unit in units)
        {
            unit.OrderNumber++;
            await database.SaveChangesAsync(cancellationToken);
        }
        database.Units.Add(new Unit
        {
            Id = unitId, CourseId = pack.CourseId, Title = source.UnitTitle!,
            Description = source.UnitDescription!, Difficulty = "A1", OrderNumber = 1
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
