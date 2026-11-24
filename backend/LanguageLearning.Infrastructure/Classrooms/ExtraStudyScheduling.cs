using LanguageLearning.Core.Accounts;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record ExtraStudyScheduleInput(DateTimeOffset? ScheduledAt, int? DurationMinutes);

public sealed partial class ExtraStudySpace
{
    public async Task<ClassroomOutcome> ScheduleAsync(Guid userId, Guid familyId, Guid requestId,
        Guid lessonId, ExtraStudyScheduleInput input, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        // Serialize slot changes before taking account/request locks. Other study-space
        // operations never acquire this guard, so they cannot invert its lock order.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730027031)", token);
        if (!await LockStudyParticipantsAsync(userId, requestId, token)) return new(Missing: true);
        var space = await AuthorizeAsync(userId, familyId, requestId, true, token);
        if (space is null || await ExtraStudyRequests.LockRequestAsync(database, requestId, token) is not { Status: "Accepted" })
            return new(Missing: true);
        var lesson = await database.ExtraStudyLessons.SingleOrDefaultAsync(
            item => item.RequestId == requestId && item.LessonId == lessonId, token);
        if (lesson is null || !await PublishedLessons(space.CourseId).AnyAsync(item => item.Id == lessonId, token))
            return new(Missing: true);

        DateTimeOffset? start = input.ScheduledAt?.ToUniversalTime();
        // PostgreSQL stores microseconds; canonicalize before comparing a retry so
        // sub-microsecond .NET ticks cannot create duplicate schedule notices.
        if (start is not null) start = start.Value.AddTicks(-(start.Value.Ticks % 10));
        if ((start is null) != (input.DurationMinutes is null))
            return new(Error: "Provide both a lesson time and duration, or clear both to cancel.");
        if (start is not null)
        {
            if (input.DurationMinutes is < 15 or > 120 || start < clock.GetUtcNow().AddMinutes(5)
                || start > clock.GetUtcNow().AddDays(180))
                return new(Error: "Choose a time between five minutes and 180 days ahead, lasting 15–120 minutes.");
            var end = start.Value.AddMinutes(input.DurationMinutes!.Value);
            bool overlap = await (
                from other in database.ExtraStudyLessons
                join request in database.ExtraStudyRequests on other.RequestId equals request.Id
                join offer in database.ExtraStudyOffers on request.OfferId equals offer.Id
                where request.Status == "Accepted" && other.ScheduledAt != null && other.DurationMinutes != null
                    && !(other.RequestId == requestId && other.LessonId == lessonId)
                    && (offer.TeacherId == userId || request.LearnerId == space.LearnerId)
                    && other.ScheduledAt < end
                    && other.ScheduledAt.Value.AddMinutes(other.DurationMinutes.Value) > start.Value
                select other.RequestId).AnyAsync(token);
            if (overlap) return new(Error: "The teacher or learner already has a lesson at that time. Choose another time.");
        }
        if (lesson.ScheduledAt == start && lesson.DurationMinutes == input.DurationMinutes) return new(lessonId);
        lesson.ScheduledAt = start;
        lesson.DurationMinutes = input.DurationMinutes;
        string message = start is null ? "The appointment was cancelled. The shared lesson remains available."
            : $"Your shared lesson is scheduled for {start:dd MMM yyyy HH:mm} UTC, lasting {input.DurationMinutes} minutes. Open Extra study for your local time.";
        foreach (var recipient in new[] { userId, space.LearnerId }.Distinct())
            database.Notifications.Add(new Notification
            {
                UserId = recipient,
                Type = "ExtraStudySchedule",
                Title = start is null ? "Lesson appointment cancelled" : "Lesson appointment saved",
                Body = message,
                CreatedAt = clock.GetUtcNow()
            });
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(lessonId);
    }
}
