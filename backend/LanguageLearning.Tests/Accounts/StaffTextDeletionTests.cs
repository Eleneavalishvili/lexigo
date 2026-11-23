using System.Net;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Accounts;

public sealed class StaffTextDeletionTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    private const string PrivateNote = "Synthetic private staff contact details";
    private const string RemovedNote = "Private note erased after account deletion.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyConfirmedDeletionErasesOwnNotesAndPreservesOtherContributions(bool confirmed)
    {
        var departing = await SecurityTestAccount.CreateAsync(fixture);
        var other = await SecurityTestAccount.CreateAsync(fixture);
        var content = await LearningTestContent.CreateAsync(fixture, other.Id);
        var published = await LearningTestContent.CreateAsync(fixture, departing.Id);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var now = DateTimeOffset.UtcNow;
        var review = new ContentReview
        {
            LessonVersionId = content.VersionId, ReviewerId = departing.Id,
            Decision = "Approved", Comment = PrivateNote, ReviewedAt = now
        };
        var otherReview = new ContentReview
        {
            LessonVersionId = published.VersionId, ReviewerId = other.Id,
            Decision = "Approved", Comment = "Another reviewer contribution", ReviewedAt = now
        };
        var vocabulary = new VocabularyRevision
        {
            CourseId = content.CourseId, AuthorId = other.Id, ReviewerId = departing.Id,
            Status = "Approved", Word = "hola", EnglishTranslation = "hello",
            ReviewComment = PrivateNote, CreatedAt = now, ReviewedAt = now
        };
        var report = new ContentReport
        {
            ReporterId = other.Id, LanguageId = 2, ResourceType = "Exercise",
            ResourceId = content.ExerciseIds[0], VersionId = content.VersionId,
            Reason = "Translation", Comment = "Keep the reporter's contribution",
            Status = "Resolved", ResolvedBy = departing.Id, Resolution = PrivateNote, CreatedAt = now
        };
        var ownAudit = Audit(departing.Id, "Lesson", content.LessonId, PrivateNote);
        var targetAudit = Audit(other.Id, "User", departing.Id, PrivateNote);
        var otherAudit = Audit(other.Id, "Lesson", published.LessonId, "Other staff audit note");
        database.AddRange(review, otherReview, vocabulary, report, ownAudit, targetAudit, otherAudit);
        await database.SaveChangesAsync();
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await departing.LoginAsync(browser);
        var response = await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = confirmed ? SecurityTestAccount.Password : "Incorrect password",
            confirmation = "DELETE", recoveryCode = departing.RecoveryCodes[1]
        }, token, csrf: true);
        Assert.Equal(confirmed ? HttpStatusCode.NoContent : HttpStatusCode.Conflict, response.StatusCode);

        database.ChangeTracker.Clear();
        var savedReview = await database.ContentReviews.SingleAsync(item => item.Id == review.Id);
        Assert.Equal(confirmed ? RemovedNote : PrivateNote, savedReview.Comment);
        Assert.Equal("Approved", savedReview.Decision);
        Assert.Equal(departing.Id, savedReview.ReviewerId);
        var savedVocabulary = await database.VocabularyRevisions.SingleAsync(item => item.Id == vocabulary.Id);
        Assert.Equal(confirmed ? null : PrivateNote, savedVocabulary.ReviewComment);
        Assert.Equal("Approved", savedVocabulary.Status);
        var savedReport = await database.ContentReports.SingleAsync(item => item.Id == report.Id);
        Assert.Equal(confirmed ? RemovedNote : PrivateNote, savedReport.Resolution);
        Assert.Equal("Resolved", savedReport.Status);
        Assert.Equal(report.Comment, savedReport.Comment);
        foreach (Guid id in new[] { ownAudit.Id, targetAudit.Id })
            Assert.Equal(confirmed ? RemovedNote : PrivateNote,
                await database.AuditLogs.Where(item => item.Id == id).Select(item => item.Reason).SingleAsync());
        Assert.Equal(otherReview.Comment, await database.ContentReviews.Where(item => item.Id == otherReview.Id)
            .Select(item => item.Comment).SingleAsync());
        Assert.Equal(otherAudit.Reason, await database.AuditLogs.Where(item => item.Id == otherAudit.Id)
            .Select(item => item.Reason).SingleAsync());
        Assert.Equal(departing.Id, await database.LessonVersions.Where(item => item.Id == published.VersionId)
            .Select(item => item.CreatedBy).SingleAsync());
        Assert.Equal("Hello", await database.LessonVersions.Where(item => item.Id == published.VersionId)
            .Select(item => item.Title).SingleAsync());
        if (confirmed)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await browser.SendAsync(HttpMethod.Get, "/api/v1/me/export", bearer: token)).StatusCode);
    }

    [Fact]
    public async Task DepartingTeacherNotesAreErasedWithoutRemovingLearnerScoresOrSubmissionHistory()
    {
        var teacher = await SecurityTestAccount.CreateAsync(fixture);
        var learner = await SecurityTestAccount.CreateAsync(fixture);
        var content = await LearningTestContent.CreateAsync(fixture, teacher.Id);
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var now = DateTimeOffset.UtcNow;
        var classroom = new Classroom
        {
            TeacherId = teacher.Id, CourseId = content.CourseId, Name = PrivateNote,
            Description = PrivateNote, IsArchived = true
        };
        var assignment = new Assignment
        {
            ClassroomId = classroom.Id, Title = PrivateNote, CreatedAt = now, DueAt = now.AddDays(1)
        };
        var session = new LearningSession
        {
            UserId = learner.Id, CourseId = content.CourseId, LessonId = content.LessonId,
            LessonVersionId = content.VersionId, Title = "Completed greeting lesson", Explanation = "",
            StartedAt = now.AddMinutes(-5), CompletedAt = now, Score = 90, AwardedXp = 20
        };
        var submission = new AssignmentSubmission
        {
            AssignmentId = assignment.Id, UserId = learner.Id, SessionId = session.Id, SubmittedAt = now
        };
        var feedback = new TeacherFeedback
        {
            SubmissionId = submission.Id, TeacherId = teacher.Id, Message = PrivateNote, CreatedAt = now
        };
        var notice = new Notification
        {
            UserId = learner.Id, AssignmentId = assignment.Id, Type = "AssignmentCreated",
            Title = "New class assignment", Body = PrivateNote, CreatedAt = now
        };
        database.Enrollments.Add(new Enrollment { UserId = learner.Id, CourseId = content.CourseId, EnrolledAt = now });
        database.AddRange(classroom, assignment, session, submission, feedback, notice);
        await database.SaveChangesAsync();
        using var browser = new SessionTestBrowser(fixture.CreateIsolatedClient());
        string token = await teacher.LoginAsync(browser);
        var response = await browser.SendAsync(HttpMethod.Delete, "/api/v1/me", new
        {
            currentPassword = SecurityTestAccount.Password, confirmation = "DELETE",
            recoveryCode = teacher.RecoveryCodes[1]
        }, token, csrf: true);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        database.ChangeTracker.Clear();
        Assert.Equal(RemovedNote, await database.TeacherFeedback.Where(item => item.Id == feedback.Id)
            .Select(item => item.Message).SingleAsync());
        var archived = await database.Classrooms.SingleAsync(item => item.Id == classroom.Id);
        Assert.Equal("Archived class", archived.Name);
        Assert.Equal("", archived.Description);
        Assert.True(archived.IsArchived);
        Assert.Equal("Archived assignment", await database.Assignments.Where(item => item.Id == assignment.Id)
            .Select(item => item.Title).SingleAsync());
        Assert.Equal("Archived assignment", await database.Notifications.Where(item => item.Id == notice.Id)
            .Select(item => item.Body).SingleAsync());
        Assert.True(await database.AssignmentSubmissions.AnyAsync(item => item.Id == submission.Id));
        var history = await database.LearningSessions.SingleAsync(item => item.Id == session.Id);
        Assert.Equal(90, history.Score);
        Assert.Equal(20, history.AwardedXp);
        Assert.Equal(learner.Id, history.UserId);
    }

    private static AuditLog Audit(Guid actor, string entityType, Guid entityId, string note) => new()
    {
        ActorId = actor, EntityType = entityType, EntityId = entityId.ToString(),
        Action = "SyntheticDeletionEvidence", Reason = note, TraceId = Guid.NewGuid().ToString(),
        OccurredAt = DateTimeOffset.UtcNow
    };
}
