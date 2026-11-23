using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Tests.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LanguageLearning.Tests.Accounts;

public sealed class LearningDataExportTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task ExportScopesPersonalDataAndConfirmedDeletionErasesOwnedPrivateRecords()
    {
        const string path = "/api/v1/me/export";
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        var owner = await SecurityTestAccount.CreateAsync(fixture);
        var other = await SecurityTestAccount.CreateAsync(fixture);
        string token = await owner.LoginAsync(browser);
        using var otherBrowser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        await other.LoginAsync(otherBrowser);
        var content = await LearningTestContent.CreateAsync(fixture, owner.Id);
        var now = DateTimeOffset.UtcNow;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var challenge = new Challenge
            {
                Id = Guid.NewGuid(), Title = "Study challenge", RuleType = "StudyDays",
                StartsAt = now.AddDays(-1), EndsAt = now.AddDays(6), RewardXp = 30, Target = 3
            };
            db.Challenges.Add(challenge);
            var classroom = new Classroom { TeacherId = other.Id, CourseId = content.CourseId, Name = "Shared class" };
            var assignment = new Assignment
            {
                ClassroomId = classroom.Id, Title = "Say hello", CreatedAt = now.AddDays(-1), DueAt = now.AddDays(1)
            };
            var offer = new ExtraStudyOffer { TeacherId = other.Id, CourseId = content.CourseId, Enabled = true };
            db.AddRange(classroom, assignment, offer);
            foreach (var user in new[] { owner, other })
            {
                bool own = user == owner;
                db.MediaAssets.Add(new MediaAsset
                {
                    UploadedBy = user.Id, StorageKey = $"media/{Guid.NewGuid():N}",
                    MimeType = "audio/wav", ByteSize = 2000, LanguageId = 2,
                    Source = own ? "Owned upload" : "Foreign upload", License = "Test fixture license"
                });
                db.VocabularyRevisions.Add(new LanguageLearning.Core.Vocabulary.VocabularyRevision
                {
                    AuthorId = user.Id, CourseId = content.CourseId, CreatedAt = now,
                    Word = own ? "owned-draft" : "foreign-draft", EnglishTranslation = "A test word"
                });
                db.ImportJobs.Add(new ImportJob
                {
                    AuthorId = user.Id, Status = "Valid", FileKey = own ? "owned-import-key" : "foreign-import-key",
                    ValidationErrors = "[]", Payload = """{"answers":["hidden-import-answer"]}""", CreatedAt = now
                });
                db.AuditLogs.Add(new AuditLog
                {
                    ActorId = user.Id, Action = own ? "OwnedAction" : "ForeignAction", EntityType = "Test",
                    EntityId = Guid.NewGuid().ToString(), TraceId = Guid.NewGuid().ToString(),
                    Reason = "internal-audit-reason", OccurredAt = now
                });
                db.Enrollments.Add(new Enrollment { UserId = user.Id, CourseId = content.CourseId, EnrolledAt = now });
                db.LessonProgress.Add(new LessonProgress
                {
                    UserId = user.Id, LessonId = content.LessonId, Status = "Completed",
                    BestScore = own ? 80 : 100, CompletedAt = now
                });
                db.XpTransactions.Add(new XpTransaction
                {
                    UserId = user.Id, CourseId = content.CourseId, SourceType = "Lesson",
                    SourceId = content.LessonId, Amount = own ? 20 : 100, EarnedAt = now
                });
                var session = new LearningSession
                {
                    UserId = user.Id, CourseId = content.CourseId, LessonId = content.LessonId,
                    LessonVersionId = content.VersionId, Title = "Hello", Explanation = "",
                    StartedAt = now, CompletedAt = now
                };
                var exercise = new SessionExercise
                {
                    UserId = user.Id, SessionId = session.Id, ExerciseId = content.ExerciseIds[0],
                    OrderNumber = 1, PublicSnapshot = "{}", PrivateEvaluationSnapshot = """{"key":"private-answer-key"}"""
                };
                db.AddRange(session, exercise, new AnswerAttempt
                {
                    UserId = user.Id, SessionExerciseId = exercise.Id, IdempotencyKey = Guid.NewGuid(),
                    AnswerPayload = own ? """{"answerText":"owned-attempt"}""" : """{"answerText":"foreign-attempt"}""",
                    ResponseSnapshot = """{"feedback":"private-response-snapshot"}""",
                    IsGraded = !own, IsCorrect = !own, SubmittedAt = now
                });
                db.SpeechCheckAttempts.Add(new SpeechCheckAttempt
                {
                    UserId = user.Id, SessionExerciseId = exercise.Id, IdempotencyKey = Guid.NewGuid(),
                    AudioSha256 = new string(own ? 'a' : 'b', 64), Status = own ? "NoSpeech" : "Mismatch",
                    HeartsCharged = 0, ResponseSnapshot = """{"message":"private-speech-response"}""", CreatedAt = now
                });
                db.LearnerHearts.Add(new LearnerHearts { UserId = user.Id, Count = own ? 3 : 4 });
                var email = new EmailOutboxMessage
                {
                    UserId = user.Id, Recipient = user.Email, Subject = "Test reminder",
                    HtmlBody = "Private transport body", Purpose = "StudyReminder", CreatedAt = now,
                    SentAt = now, Attempts = 1
                };
                db.EmailOutboxMessages.Add(email);
                db.ReminderDeliveries.Add(new ReminderDelivery
                {
                    Id = email.Id, UserId = user.Id, LocalDate = DateOnly.FromDateTime(now.UtcDateTime),
                    Status = own ? "Sent" : "Failed", SentAt = own ? now : null
                });
                db.DailyActivities.Add(new DailyActivity
                {
                    UserId = user.Id, LocalDate = DateOnly.FromDateTime(now.UtcDateTime),
                    EarnedXp = own ? 20 : 100, CompletedLessons = 1, GoalReached = true
                });
                db.UserAchievements.Add(new UserAchievement
                {
                    UserId = user.Id, EarnedAt = now, AchievementId = Guid.Parse(own
                        ? "40000000-0000-0000-0000-000000000001" : "40000000-0000-0000-0000-000000000004")
                });
                db.UserChallenges.Add(new UserChallenge
                {
                    UserId = user.Id, ChallengeId = challenge.Id, Progress = own ? 1 : 2
                });
                var submission = new AssignmentSubmission
                {
                    AssignmentId = assignment.Id, UserId = user.Id, SessionId = session.Id, SubmittedAt = now
                };
                db.AddRange(
                    new ClassMembership { ClassroomId = classroom.Id, UserId = user.Id, JoinedAt = now },
                    submission,
                    new TeacherFeedback
                    {
                        SubmissionId = submission.Id, TeacherId = other.Id,
                        Message = own ? "Owned feedback" : "Foreign feedback", CreatedAt = now
                    },
                    new ExtraStudyRequest { OfferId = offer.Id, LearnerId = user.Id, CreatedAt = now, Status = own ? "Pending" : "Accepted" },
                    new Notification
                    {
                        UserId = user.Id, AssignmentId = assignment.Id, Type = "Assignment",
                        Title = "Assignment reminder", Body = own ? "Owned notification" : "Foreign notification", CreatedAt = now
                    },
                    new ContentReport
                    {
                        ReporterId = user.Id, LanguageId = 2, ResourceType = "Exercise",
                        ResourceId = content.ExerciseIds[0], VersionId = content.VersionId,
                        Reason = "Text", Comment = own ? "Owned report" : "Foreign report", CreatedAt = now
                    });
            }
            await db.SaveChangesAsync();
        }
        var save = await browser.SendAsync(HttpMethod.Post, "/api/v1/me/saved-words",
            new { vocabularyItemId = Guid.Parse("15110000-0000-0000-0000-000000000001") }, token);
        save.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(path)).StatusCode);
        var response = await browser.SendAsync(HttpMethod.Get, path + "?userId=" + other.Id, bearer: token);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("lexigo-learning-data.json", response.Content.Headers.ContentDisposition?.FileName);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(owner.Email, data.GetProperty("profile").GetProperty("email").GetString());
        Assert.True(data.GetProperty("preferences").TryGetProperty("reminderTime", out _));
        Assert.Equal(content.CourseId, Assert.Single(data.GetProperty("enrollments").EnumerateArray()).GetProperty("courseId").GetGuid());
        Assert.Equal(80, Assert.Single(data.GetProperty("lessonProgress").EnumerateArray()).GetProperty("bestScore").GetInt32());
        Assert.Equal(20, Assert.Single(data.GetProperty("xpHistory").EnumerateArray()).GetProperty("amount").GetInt32());
        var sessionData = Assert.Single(data.GetProperty("learningSessions").EnumerateArray());
        var attempt = Assert.Single(data.GetProperty("exerciseAttempts").EnumerateArray());
        Assert.Equal(sessionData.GetProperty("id").GetGuid(), attempt.GetProperty("sessionId").GetGuid());
        Assert.Contains("owned-attempt", attempt.GetProperty("answerPayload").GetString());
        Assert.False(attempt.GetProperty("isGraded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, attempt.GetProperty("isCorrect").ValueKind);
        Assert.Equal(20, Assert.Single(data.GetProperty("dailyActivity").EnumerateArray()).GetProperty("earnedXp").GetInt32());
        Assert.Equal("first-lesson", Assert.Single(data.GetProperty("achievements").EnumerateArray()).GetProperty("code").GetString());
        Assert.Equal(1, Assert.Single(data.GetProperty("challenges").EnumerateArray()).GetProperty("progress").GetInt32());
        Assert.Equal("Shared class", Assert.Single(data.GetProperty("classMemberships").EnumerateArray()).GetProperty("name").GetString());
        var assignmentData = Assert.Single(data.GetProperty("assignmentSubmissions").EnumerateArray());
        Assert.Equal(sessionData.GetProperty("id").GetGuid(), assignmentData.GetProperty("sessionId").GetGuid());
        var feedback = Assert.Single(data.GetProperty("assignmentFeedback").EnumerateArray());
        Assert.Equal(assignmentData.GetProperty("id").GetGuid(), feedback.GetProperty("submissionId").GetGuid());
        Assert.Equal("Owned feedback", feedback.GetProperty("message").GetString());
        Assert.False(feedback.TryGetProperty("teacherId", out _));
        Assert.Equal("Pending", Assert.Single(data.GetProperty("extraStudyRequests").EnumerateArray()).GetProperty("status").GetString());
        Assert.Equal("Owned notification", Assert.Single(data.GetProperty("notifications").EnumerateArray()).GetProperty("body").GetString());
        Assert.Equal("Owned report", Assert.Single(data.GetProperty("contentReports").EnumerateArray()).GetProperty("comment").GetString());
        Assert.Equal(3, data.GetProperty("hearts").GetProperty("count").GetInt32());
        Assert.Equal("NoSpeech", Assert.Single(data.GetProperty("speechChecks").EnumerateArray()).GetProperty("status").GetString());
        Assert.Equal("Sent", Assert.Single(data.GetProperty("reminderHistory").EnumerateArray()).GetProperty("status").GetString());
        Assert.Single(data.GetProperty("signInHistory").EnumerateArray());
        Assert.Equal(content.LessonId, Assert.Single(data.GetProperty("authoredLessons").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(content.VersionId, Assert.Single(data.GetProperty("authoredLessonVersions").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal("Owned upload", Assert.Single(data.GetProperty("uploadedMedia").EnumerateArray()).GetProperty("source").GetString());
        Assert.Equal("owned-draft", Assert.Single(data.GetProperty("authoredVocabularyRevisions").EnumerateArray()).GetProperty("word").GetString());
        Assert.Empty(data.GetProperty("taughtClasses").EnumerateArray());
        Assert.Empty(data.GetProperty("extraStudyOffers").EnumerateArray());
        Assert.Equal("Valid", Assert.Single(data.GetProperty("contentImports").EnumerateArray()).GetProperty("status").GetString());
        Assert.Contains(data.GetProperty("administrativeActions").EnumerateArray(), item =>
            item.GetProperty("action").GetString() == "OwnedAction");
        Assert.Single(data.GetProperty("savedWords").EnumerateArray());
        Assert.DoesNotContain("passwordHash", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(other.Email, data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("foreign-attempt", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-answer-key", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-response-snapshot", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Foreign feedback", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Foreign notification", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Foreign report", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audioSha256", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenHash", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private transport body", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Foreign upload", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("foreign-draft", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("foreign-import-key", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden-import-answer", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ForeignAction", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal-audit-reason", data.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var deletion = new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
            recoveryCode = owner.RecoveryCodes[1]
        };
        (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", deletion, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", deletion, token, csrf: true)).StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            // Complete prerequisite cleanup only in this isolated fixture.
            await db.VocabularyRevisions.Where(item => item.AuthorId == owner.Id).ExecuteDeleteAsync();
            await db.MediaAssets.Where(item => item.UploadedBy == owner.Id).ExecuteDeleteAsync();
        }
        var deleted = await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", deletion, token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await browser.SendAsync(HttpMethod.Get, path, bearer: token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await browser.SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", csrf: true)).StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
            var erased = await db.Users.SingleAsync(item => item.Id == owner.Id);
            Assert.Equal("Deleted", erased.Status);
            Assert.Equal("Deleted account", erased.DisplayName);
            Assert.Null(erased.Email);
            Assert.Null(erased.PasswordHash);
            Assert.False(await db.UserTokens.AnyAsync(item => item.UserId == owner.Id));
            Assert.False(await db.RefreshSessions.AnyAsync(item => item.UserId == owner.Id));
            Assert.False(await db.UserPreferences.AnyAsync(item => item.UserId == owner.Id));
            Assert.False(await db.SavedWords.AnyAsync(item => item.UserId == owner.Id));
            Assert.False(await db.LearningSessions.AnyAsync(item => item.UserId == owner.Id));
            Assert.False(await db.AnswerAttempts.AnyAsync(item => item.UserId == owner.Id));
            Assert.False(await db.EmailOutboxMessages.AnyAsync(item => item.UserId == owner.Id));
            Assert.False(await db.ContentReports.AnyAsync(item => item.ReporterId == owner.Id));
            Assert.True(await db.Lessons.AnyAsync(item => item.Id == content.LessonId));
            Assert.True(await db.XpTransactions.AnyAsync(item => item.UserId == owner.Id));
            Assert.True(await db.LearningSessions.AnyAsync(item => item.UserId == other.Id));
            Assert.True(await db.SavedWords.AllAsync(item => item.UserId != owner.Id));
            Assert.True(await db.Users.AnyAsync(item => item.Id == other.Id && item.Email == other.Email));
            Guid collectionId = Guid.NewGuid();
            var prevented = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO "WordCollections" ("Id", "UserId", "CourseId", "Name") VALUES ({collectionId}, {owner.Id}, {content.CourseId}, 'Late request')"""));
            Assert.Equal("23514", prevented.SqlState);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO "WordCollections" ("Id", "UserId", "CourseId", "Name") VALUES ({collectionId}, {other.Id}, {content.CourseId}, 'Other account')""");
            var reassignment = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "WordCollections" SET "UserId" = {owner.Id} WHERE "Id" = {collectionId}"""));
            Assert.Equal("23514", reassignment.SqlState);
        }
    }
}
