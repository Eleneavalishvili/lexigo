using System.Text.Json;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Infrastructure.Data.Configurations;

public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.HasKey(preference => preference.UserId);
        builder.Property(preference => preference.TimeZone).HasMaxLength(100);
        builder.Property(preference => preference.Theme).HasMaxLength(12);
        builder.Ignore(preference => preference.StudyDays);
        builder.Ignore(preference => preference.ReminderTime);
        builder.Ignore(preference => preference.EmailRemindersEnabled);
        builder.OwnsOne(preference => preference.Reminder, reminder =>
        {
            reminder.WithOwner().HasForeignKey(item => item.UserId);
            reminder.HasKey(item => item.UserId);
            reminder.Ignore(item => item.StudyDays);
            reminder.Property(item => item.Channel).HasMaxLength(12).IsRequired();
            reminder.Property(item => item.WeekDays).HasColumnType("jsonb")
                .HasConversion(
                    days => JsonSerializer.Serialize(days, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<int[]>(json, (JsonSerializerOptions?)null)!,
                    new ValueComparer<int[]>(
                        (left, right) => left!.SequenceEqual(right!),
                        days => days.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)),
                        days => days.ToArray()));
            reminder.ToTable("ReminderPreferences", table =>
            {
                table.HasCheckConstraint("CK_ReminderPreferences_Channel", "\"Channel\" = 'Email'");
                table.HasCheckConstraint("CK_ReminderPreferences_WeekDays",
                    """jsonb_typeof("WeekDays") = 'array' AND "WeekDays" <> '[]'::jsonb AND "WeekDays" <@ '[0,1,2,3,4,5,6]'::jsonb""");
            });
        });
        builder.Navigation(preference => preference.Reminder).IsRequired();
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserPreference>(preference => preference.UserId);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(preference => preference.SharedLeaderboardAvatarId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Course>().WithMany().HasForeignKey(preference => preference.ActiveCourseId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable("UserPreferences", table =>
        {
            table.HasCheckConstraint("CK_UserPreferences_DailyGoal", "\"DailyGoalXp\" BETWEEN 1 AND 1000");
            table.HasCheckConstraint("CK_UserPreferences_Theme", "\"Theme\" IN ('system', 'light', 'dark')");
            table.HasCheckConstraint("CK_UserPreferences_SharedLeaderboardAvatar",
                """ "SharedLeaderboardAvatarId" IS NULL OR "LeaderboardOptIn" """);
        });
    }
}

