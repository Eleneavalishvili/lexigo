using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Core.Learning;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class DialogueConfiguration :
    IEntityTypeConfiguration<Dialogue>, IEntityTypeConfiguration<DialogueNode>,
    IEntityTypeConfiguration<DialogueChoice>, IEntityTypeConfiguration<DialogueSession>,
    IEntityTypeConfiguration<DialogueAttempt>
{
    public void Configure(EntityTypeBuilder<Dialogue> builder)
    {
        builder.Property(item => item.Title).HasMaxLength(160);
        builder.Property(item => item.Scenario).HasMaxLength(40);
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.CourseId, item.Scenario, item.VersionNumber }).IsUnique();
        builder.ToTable("Dialogues", table => table.HasCheckConstraint("CK_Dialogues_VersionStatus",
            """ "VersionNumber" > 0 AND "Status" IN ('Draft', 'Published', 'Archived') """));
    }

    public void Configure(EntityTypeBuilder<DialogueNode> builder)
    {
        builder.Property(item => item.Text).HasMaxLength(4000);
        builder.HasAlternateKey(item => new { item.DialogueId, item.Id });
        builder.HasOne<Dialogue>().WithMany().HasForeignKey(item => item.DialogueId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(item => item.AudioMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.DialogueId).IsUnique().HasFilter("\"IsStart\"");
        builder.ToTable("DialogueNodes", table => table.HasCheckConstraint("CK_DialogueNodes_StartTerminal",
            """ NOT ("IsStart" AND "IsTerminal") """));
    }

    public void Configure(EntityTypeBuilder<DialogueChoice> builder)
    {
        builder.Property(item => item.Text).HasMaxLength(1000);
        builder.Property(item => item.Feedback).HasMaxLength(4000);
        builder.HasAlternateKey(item => new { item.DialogueId, item.Id });
        builder.HasOne<DialogueNode>().WithMany().HasForeignKey(item => new { item.DialogueId, item.NodeId })
            .HasPrincipalKey(item => new { item.DialogueId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DialogueNode>().WithMany().HasForeignKey(item => new { item.DialogueId, item.NextNodeId })
            .HasPrincipalKey(item => new { item.DialogueId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("DialogueChoices", table => table.HasCheckConstraint("CK_DialogueChoices_NoSelfEdge",
            """ "NextNodeId" IS DISTINCT FROM "NodeId" """));
    }

    public void Configure(EntityTypeBuilder<DialogueSession> builder)
    {
        builder.Property(item => item.VersionSnapshot).HasColumnType("jsonb");
        builder.HasAlternateKey(item => new { item.DialogueId, item.Id });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DialogueNode>().WithMany().HasForeignKey(item => new { item.DialogueId, item.CurrentNodeId })
            .HasPrincipalKey(item => new { item.DialogueId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.UserId, item.DialogueId }).IsUnique().HasFilter("\"CompletedAt\" IS NULL");
        builder.HasIndex(item => new { item.UserId, item.CompletedAt })
            .IncludeProperties(item => item.DialogueId)
            .HasFilter("\"CompletedAt\" IS NOT NULL");
        builder.ToTable("DialogueSessions", table => table.HasCheckConstraint("CK_DialogueSessions_Time",
            """ "CompletedAt" IS NULL OR "CompletedAt" >= "StartedAt" """));
    }

    public void Configure(EntityTypeBuilder<DialogueAttempt> builder)
    {
        builder.HasOne<DialogueSession>().WithMany().HasForeignKey(item => new { item.DialogueId, item.SessionId })
            .HasPrincipalKey(item => new { item.DialogueId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DialogueChoice>().WithMany().HasForeignKey(item => new { item.DialogueId, item.ChoiceId })
            .HasPrincipalKey(item => new { item.DialogueId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.SessionId, item.IdempotencyKey }).IsUnique();
    }
}
