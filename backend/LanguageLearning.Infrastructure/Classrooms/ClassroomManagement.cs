using System.Security.Cryptography;
using System.Text.Json;
using LanguageLearning.Core.Classrooms;
using LanguageLearning.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed record CreateClassroomInput(Guid CourseId, string Name, string Description, Guid RequestKey);
public sealed record EditClassroomInput(string Name, string Description, bool IsArchived, int Revision);

public sealed class ClassroomManagement(LexiGoDbContext database, ClassroomAccess access, TimeProvider clock)
{
    public async Task<ClassroomOutcome> CreateAsync(Guid actorId, Guid familyId, CreateClassroomInput input,
        string traceId, CancellationToken cancellationToken)
    {
        if (!Valid(input.Name, input.Description) || input.RequestKey == Guid.Empty) return new(Error: "Check the class fields and request key.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is not { CanTeach: true }) return new(Forbidden: true);
        string name = input.Name.Trim();
        string description = input.Description.Trim();
        string fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { input.CourseId, name, description })));
        var existing = await database.Classrooms.SingleOrDefaultAsync(item => item.TeacherId == actorId && item.CreationKey == input.RequestKey, cancellationToken);
        if (existing is not null) return existing.CreationFingerprint == fingerprint
            ? new(existing.Id) : new(Error: "These details differ from a class you already submitted. Refresh the list and open that class to edit it.");
        if (!await database.Courses.AnyAsync(item => item.Id == input.CourseId && item.IsPublished, cancellationToken)) return new(Missing: true);
        if (await database.Classrooms.CountAsync(item => item.TeacherId == actorId && !item.IsArchived, cancellationToken) >= 50)
            return new(Error: "Archive an old class before creating more than 50 active classes.");
        var classroom = new Classroom
        {
            TeacherId = actorId, CourseId = input.CourseId, Name = name, Description = description,
            CreationKey = input.RequestKey, CreationFingerprint = fingerprint
        };
        database.Classrooms.Add(classroom);
        access.Audit(actorId, classroom.Id, "ClassroomCreated", "Class created for published course " + input.CourseId, traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(classroom.Id, Created: true);
    }

    public async Task<ClassroomOutcome> EditAsync(Guid actorId, Guid familyId, Guid id, EditClassroomInput input,
        string traceId, CancellationToken cancellationToken)
    {
        if (!Valid(input.Name, input.Description)) return new(Error: "Check class name and description.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var actor = await access.ActorAsync(actorId, familyId, true, cancellationToken);
        if (actor is not { CanTeach: true }) return new(Forbidden: true);
        var classroom = await access.LockAsync(id, cancellationToken);
        if (classroom?.TeacherId != actorId) return new(Missing: true);
        if (classroom.Revision != input.Revision) return new(Error: "This class changed. Refresh before editing.");
        if (classroom.IsArchived) return new(Error: "Archived classes are read-only.");
        classroom.Name = input.Name.Trim();
        classroom.Description = input.Description.Trim();
        classroom.IsArchived = input.IsArchived;
        classroom.Revision++;
        if (input.IsArchived)
            await database.ClassInvitations.Where(item => item.ClassroomId == id && item.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, clock.GetUtcNow()), cancellationToken);
        access.Audit(actorId, id, input.IsArchived ? "ClassroomArchived" : "ClassroomEdited",
            "Class metadata changed; revision " + classroom.Revision, traceId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(id);
    }

    private static bool Valid(string name, string description) =>
        ClassroomAccess.Text(name, 120) && description is not null && description.Length <= 2000 && !description.Contains('\0');
}
