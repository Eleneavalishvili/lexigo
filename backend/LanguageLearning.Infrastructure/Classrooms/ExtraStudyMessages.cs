using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Classrooms;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record StudyMessageView(Guid Id, string SenderName, string Body, DateTimeOffset CreatedAt, bool IsMine);
public sealed record StudyMessagePage(StudyMessageView[] Items, Guid? NextBefore);
public sealed record SendStudyMessage(Guid Id, string Body);

public sealed partial class ExtraStudySpace
{
    private async Task<bool> LockStudyParticipantsAsync(Guid actorId, Guid requestId, CancellationToken token)
    {
        var participants = await (from request in database.ExtraStudyRequests.AsNoTracking()
                                  join offer in database.ExtraStudyOffers on request.OfferId equals offer.Id
                                  where request.Id == requestId && request.Status == "Accepted"
                                      && (request.LearnerId == actorId || offer.TeacherId == actorId)
                                  select new { request.LearnerId, offer.TeacherId }).SingleOrDefaultAsync(token);
        if (participants is null) return false;
        await ClassroomAccess.LockParticipantsAsync(database, participants.LearnerId, participants.TeacherId, token);
        return true;
    }

    public async Task<StudyMessagePage?> MessagesAsync(Guid userId, Guid familyId, Guid requestId, Guid? before, CancellationToken token)
    {
        if (await AuthorizeAsync(userId, familyId, requestId, false, token) is null) return null;
        var query = database.ExtraStudyMessages.AsNoTracking().Where(item => item.RequestId == requestId);
        if (before is { } cursor)
        {
            var boundary = await query.Where(item => item.Id == cursor).Select(item => new { item.CreatedAt, item.Id }).SingleOrDefaultAsync(token);
            if (boundary is null) return new([], null);
            query = query.Where(item => item.CreatedAt < boundary.CreatedAt
                || (item.CreatedAt == boundary.CreatedAt && item.Id.CompareTo(boundary.Id) < 0));
        }
        var rows = await (from message in query
                          join sender in database.Users on message.SenderId equals sender.Id
                          orderby message.CreatedAt descending, message.Id descending
                          select new StudyMessageView(message.Id, sender.DisplayName, message.Body, message.CreatedAt, message.SenderId == userId))
            .Take(25).ToArrayAsync(token);
        var page = rows.Take(24).ToArray();
        return new(page.Reverse().ToArray(), rows.Length > 24 ? page[^1].Id : null);
    }

    public async Task<ClassroomOutcome> SendMessageAsync(Guid userId, Guid familyId, Guid requestId, SendStudyMessage input, CancellationToken token)
    {
        if (input.Id == Guid.Empty || !ClassroomAccess.Text(input.Body, 2000))
            return new(Error: "Write a message of 1–2000 characters.");
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        // Both accounts are locked in stable order before the request: notification
        // foreign keys must not deadlock against the recipient closing the request.
        if (!await LockStudyParticipantsAsync(userId, requestId, token)) return new(Missing: true);
        var space = await AuthorizeAsync(userId, familyId, requestId, false, token);
        if (space is null || await ExtraStudyRequests.LockRequestAsync(database, requestId, token) is not { Status: "Accepted" })
            return new(Missing: true);
        string body = input.Body.Trim();
        var previous = await database.ExtraStudyMessages.SingleOrDefaultAsync(item => item.Id == input.Id, token);
        if (previous is not null)
            return previous.SenderId == userId && previous.RequestId == requestId && previous.Body == body
                ? new(previous.Id) : new(Error: "This message reference has already been used.");
        if (await database.ExtraStudyMessages.CountAsync(item => item.RequestId == requestId, token) >= 1000)
            return new(Error: "This conversation has reached its 1000-message limit.");
        database.ExtraStudyMessages.Add(new ExtraStudyMessage
        {
            Id = input.Id,
            RequestId = requestId,
            SenderId = userId,
            Body = body,
            CreatedAt = clock.GetUtcNow()
        });
        database.Notifications.Add(new Notification
        {
            UserId = userId == space.TeacherId ? space.LearnerId : space.TeacherId,
            Type = "ExtraStudyMessage",
            Title = "New study message",
            Body = "You have a new private message. Open your Extra study space to read it.",
            CreatedAt = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(input.Id);
    }
}
