using System.Data;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record ClassPage<T>(T[] Items, int Page, int PageSize, int TotalCount);
public sealed record ClassroomView(Guid Id, Guid CourseId, string CourseTitle, string Name, string Description,
    string TeacherName, bool IsArchived, int Revision, bool IsTeacher, bool CanLeave, bool CoursePublished);
public sealed record ClassMemberView(Guid UserId, string DisplayName, DateTimeOffset JoinedAt, string Status);
public sealed record ClassInvitationStatus(DateTimeOffset ExpiresAt, int MaxUses, int UsedCount);
public sealed record ClassroomDetail(ClassroomView Classroom, ClassPage<ClassMemberView>? Members, ClassInvitationStatus? Invitation);

public sealed class ClassroomQueries(LexiGoDbContext database, ClassroomAccess access, TimeProvider clock)
{
    private IQueryable<Classroom> Visible(ClassActor actor) =>
        database.Classrooms.AsNoTracking().Where(item => actor.CanTeach && item.TeacherId == actor.Id
            || actor.CanLearn && database.ClassMemberships.Any(member => member.ClassroomId == item.Id && member.UserId == actor.Id && member.Status == "Active"));

    private IQueryable<ClassroomView> Project(IQueryable<Classroom> source, ClassActor actor) =>
        from classroom in source
        join course in database.Courses on classroom.CourseId equals course.Id
        join teacher in database.Users on classroom.TeacherId equals teacher.Id
        select new ClassroomView(classroom.Id, course.Id, course.Title, classroom.Name, classroom.Description,
            teacher.DisplayName, classroom.IsArchived, classroom.Revision,
            actor.CanTeach && classroom.TeacherId == actor.Id,
            classroom.TeacherId != actor.Id && actor.CanLearn, course.IsPublished);

    public async Task<ClassPage<ClassroomView>?> ListAsync(Guid userId, Guid familyId, int page, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, false, cancellationToken);
        if (actor is null) return null;
        var query = Visible(actor);
        int total = await query.CountAsync(cancellationToken);
        var rows = await Project(query.OrderBy(item => item.IsArchived).ThenBy(item => item.Name).ThenBy(item => item.Id)
            .Skip((page - 1) * 20).Take(20), actor).ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(rows, page, 20, total);
    }

    public async Task<ClassroomDetail?> DetailAsync(Guid userId, Guid familyId, Guid id, int memberPage, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var actor = await access.ActorAsync(userId, familyId, false, cancellationToken);
        if (actor is null) return null;
        var classroom = await Project(Visible(actor).Where(item => item.Id == id), actor).SingleOrDefaultAsync(cancellationToken);
        if (classroom is null) return null;
        ClassPage<ClassMemberView>? members = null;
        ClassInvitationStatus? invitation = null;
        if (classroom.IsTeacher)
        {
            var query = database.ClassMemberships.AsNoTracking().Where(item => item.ClassroomId == id);
            int count = await query.CountAsync(cancellationToken);
            var rows = await (from member in query.OrderBy(item => item.JoinedAt).ThenBy(item => item.UserId).Skip((memberPage - 1) * 20).Take(20)
                              join user in database.Users on member.UserId equals user.Id
                              orderby member.JoinedAt, member.UserId
                              select new ClassMemberView(user.Id, user.DisplayName, member.JoinedAt, member.Status))
                .ToArrayAsync(cancellationToken);
            members = new(rows, memberPage, 20, count);
            invitation = await database.ClassInvitations.AsNoTracking()
                .Where(item => item.ClassroomId == id && item.RevokedAt == null && item.ExpiresAt > clock.GetUtcNow())
                .OrderByDescending(item => item.ExpiresAt)
                .Select(item => new ClassInvitationStatus(item.ExpiresAt, item.MaxUses, item.UsedCount)).FirstOrDefaultAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(classroom, members, invitation);
    }
}
