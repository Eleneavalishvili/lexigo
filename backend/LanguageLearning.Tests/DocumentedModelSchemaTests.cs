using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests;

public sealed class DocumentedModelSchemaTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Theory]
    [InlineData("Exercises", "Id", "Id:uuid LessonVersionId:uuid>LessonVersions Type:text Prompt:text Explanation:text OrderNumber:int4 AudioMediaId:uuid?>MediaAssets EvaluationRules:jsonb")]
    [InlineData("ExerciseOptions", "Id", "Id:uuid ExerciseId:uuid>Exercises Text:text OrderNumber:int4 MatchKey:text? AudioMediaId:uuid?>MediaAssets")]
    [InlineData("MediaAssets", "Id", "Id:uuid StorageKey:text MimeType:text ByteSize:int8 LanguageId:int4?>Languages Source:text License:text UploadedBy:uuid>Users")]
    [InlineData("VocabularyItems", "Id", "Id:uuid CourseId:uuid>Courses Word:text EnglishTranslation:text Example:text AudioMediaId:uuid?>MediaAssets PartOfSpeech:text")]
    [InlineData("Users", "Id", "Id:uuid Email:text? NormalizedEmail:text? PasswordHash:text? DisplayName:text AvatarMediaId:uuid?>MediaAssets EmailConfirmed:bool Status:text CreatedAt:timestamptz")]
    [InlineData("AspNetRoles", "Id", "Id:uuid Name:text?")]
    [InlineData("AspNetUserRoles", "UserId,RoleId", "UserId:uuid>Users RoleId:uuid>AspNetRoles")]
    [InlineData("UserPreferences", "UserId", "UserId:uuid>Users TimeZone:text DailyGoalXp:int4 Theme:text ReduceMotion:bool SoundEnabled:bool LeaderboardOptIn:bool")]
    [InlineData("Dialogues", "Id", "Id:uuid CourseId:uuid>Courses Title:text Scenario:text VersionNumber:int4 Status:text RequiresPremium:bool")]
    [InlineData("DialogueNodes", "Id", "Id:uuid DialogueId:uuid>Dialogues Text:text AudioMediaId:uuid?>MediaAssets IsStart:bool IsTerminal:bool")]
    [InlineData("DialogueChoices", "Id", "Id:uuid DialogueId:uuid>DialogueNodes.DialogueId NodeId:uuid>DialogueNodes Text:text Feedback:text IsCorrect:bool NextNodeId:uuid?>DialogueNodes")]
    [InlineData("DialogueSessions", "Id", "Id:uuid UserId:uuid>Users DialogueId:uuid>DialogueNodes.DialogueId CurrentNodeId:uuid>DialogueNodes VersionSnapshot:jsonb StartedAt:timestamptz CompletedAt:timestamptz?")]
    [InlineData("DialogueAttempts", "Id", "Id:uuid SessionId:uuid>DialogueSessions ChoiceId:uuid>DialogueChoices IdempotencyKey:uuid SubmittedAt:timestamptz")]
    [InlineData("Assignments", "Id", "Id:uuid ClassroomId:uuid>Classrooms Title:text DueAt:timestamptz MinimumScore:int4")]
    [InlineData("AssignmentLessons", "AssignmentId,LessonId", "AssignmentId:uuid>Assignments LessonId:uuid>Lessons")]
    [InlineData("Notifications", "Id", "Id:uuid UserId:uuid>Users Type:text Title:text Body:text ReadAt:timestamptz? CreatedAt:timestamptz")]
    [InlineData("AuditLogs", "Id", "Id:uuid ActorId:uuid?>Users Action:text EntityType:text EntityId:text Reason:text? TraceId:text OccurredAt:timestamptz")]
    [InlineData("SubscriptionPlans", "Id", "Id:uuid Code:text Name:text IsActive:bool CreatedAt:timestamptz")]
    [InlineData("PlanPrices", "Id", "Id:uuid PlanId:uuid>SubscriptionPlans Period:text Amount:numeric Currency:text ValidFrom:timestamptz ValidTo:timestamptz?")]
    [InlineData("Features", "Id", "Id:int4 Code:text Description:text")]
    [InlineData("PlanFeatures", "PlanId,FeatureId", "PlanId:uuid>SubscriptionPlans FeatureId:int4>Features LimitValue:int4?")]
    [InlineData("TestCards", "Id", "Id:uuid UserId:uuid>Users Nickname:text DisplayLastFour:text Currency:text Balance:numeric Status:text Version:int8 CreatedAt:timestamptz")]
    [InlineData("BillingPreferences", "UserId", "UserId:uuid>Users DefaultTestCardId:uuid?>TestCards AutoRenew:bool")]
    [InlineData("BillingOrders", "Id", "Id:uuid UserId:uuid>Users PlanPriceId:uuid>PlanPrices SubscriptionId:uuid?>Subscriptions AmountSnapshot:numeric Currency:text PeriodSnapshot:text Status:text OrderType:text RenewalPeriodKey:text? CreatedAt:timestamptz")]
    [InlineData("PaymentAttempts", "Id", "Id:uuid OrderId:uuid>BillingOrders TestCardId:uuid>TestCards Status:text FailureCode:text? IdempotencyKey:uuid Amount:numeric Currency:text CreatedAt:timestamptz CompletedAt:timestamptz?")]
    [InlineData("Subscriptions", "Id", "Id:uuid UserId:uuid>Users PlanId:uuid>SubscriptionPlans Status:text CurrentPeriodStart:timestamptz CurrentPeriodEnd:timestamptz CancelAtPeriodEnd:bool ScheduledPlanPriceId:uuid?>PlanPrices CreatedAt:timestamptz")]
    [InlineData("SubscriptionEvents", "Id", "Id:uuid SubscriptionId:uuid>Subscriptions EventType:text RelatedPaymentId:uuid?>PaymentAttempts OccurredAt:timestamptz ActorId:uuid?>Users")]
    [InlineData("CardTransactions", "Id", "Id:uuid TestCardId:uuid>TestCards PaymentId:uuid?>PaymentAttempts RefundId:uuid?>Refunds Type:text Amount:numeric BalanceAfter:numeric Reference:text CreatedAt:timestamptz")]
    [InlineData("Refunds", "Id", "Id:uuid PaymentId:uuid>PaymentAttempts Amount:numeric Reason:text Status:text IdempotencyKey:uuid CreatedBy:uuid>Users CompletedAt:timestamptz?")]
    [InlineData("PaymentReceipts", "Id", "Id:uuid PaymentId:uuid>PaymentAttempts ReceiptNumber:text IssuedAt:timestamptz")]
    [InlineData("BillingJobExecutions", "Id", "Id:uuid SubscriptionId:uuid>Subscriptions PeriodKey:text Status:text LastAttemptAt:timestamptz NextAttemptAt:timestamptz?")]
    [InlineData("DailyActivities", "UserId,LocalDate", "UserId:uuid>Users LocalDate:date EarnedXp:int4 CompletedLessons:int4 GoalReached:bool")]
    [InlineData("Languages", "Id", "Id:int4 Code:text EnglishName:text NativeName:text WritingDirection:text")]
    [InlineData("UserLanguageScopes", "UserId,LanguageId", "UserId:uuid>Users LanguageId:int4>Languages")]
    [InlineData("Courses", "Id", "Id:uuid SourceLanguageId:int4>Languages TargetLanguageId:int4>Languages Title:text Description:text IsPublished:bool OrderNumber:int4")]
    [InlineData("Units", "Id", "Id:uuid CourseId:uuid>Courses Title:text Description:text OrderNumber:int4 Difficulty:text")]
    [InlineData("Lessons", "Id", "Id:uuid UnitId:uuid>Units Title:text Objective:text OrderNumber:int4 Status:text CurrentVersionId:uuid?>LessonVersions")]
    [InlineData("LessonPrerequisites", "LessonId,PrerequisiteLessonId", "LessonId:uuid>Lessons PrerequisiteLessonId:uuid>Lessons")]
    [InlineData("LessonVersions", "Id", "Id:uuid LessonId:uuid>Lessons VersionNumber:int4 Explanation:text CreatedBy:uuid>Users Status:text PublishedAt:timestamptz?")]
    [InlineData("ContentReviews", "Id", "Id:uuid LessonVersionId:uuid>LessonVersions ReviewerId:uuid>Users Decision:text Comment:text ReviewedAt:timestamptz")]
    [InlineData("Skills", "Id", "Id:int4 CourseId:uuid>Courses Name:text Category:text")]
    [InlineData("ExerciseSkills", "ExerciseId,SkillId", "ExerciseId:uuid>Exercises SkillId:int4>Skills")]
    [InlineData("AcceptedAnswers", "Id", "Id:uuid ExerciseId:uuid>Exercises AnswerText:text OrderedTokenIds:jsonb?")]
    [InlineData("Enrollments", "UserId,CourseId", "UserId:uuid>Users CourseId:uuid>Courses EnrolledAt:timestamptz PlacementLevel:text?")]
    [InlineData("LessonProgress", "UserId,LessonId", "UserId:uuid>Users LessonId:uuid>Lessons Status:text BestScore:int4 CompletedAt:timestamptz?")]
    [InlineData("LearningSessions", "Id", "Id:uuid UserId:uuid>Enrollments.UserId CourseId:uuid>Enrollments.CourseId LessonId:uuid?>LessonVersions.LessonId Mode:text Hearts:int4 StartedAt:timestamptz CompletedAt:timestamptz? Score:int4 AwardedXp:int4")]
    [InlineData("SessionExercises", "Id", "Id:uuid SessionId:uuid>LearningSessions ExerciseId:uuid>Exercises OrderNumber:int4 PublicSnapshot:jsonb PrivateEvaluationSnapshot:jsonb")]
    [InlineData("AnswerAttempts", "Id", "Id:uuid SessionExerciseId:uuid>SessionExercises AnswerPayload:jsonb IsCorrect:bool IdempotencyKey:uuid SubmittedAt:timestamptz")]
    [InlineData("SkillProgress", "UserId,SkillId", "UserId:uuid>Users SkillId:int4>Skills CorrectCount:int4 IncorrectCount:int4 LastPracticedAt:timestamptz?")]
    [InlineData("SavedWords", "Id", "Id:uuid UserId:uuid>Users VocabularyItemId:uuid>VocabularyItems DueAt:timestamptz IntervalDays:int4 CorrectStreak:int4")]
    [InlineData("CompletionCards", "Id", "Id:uuid UserId:uuid>Users UnitId:uuid>Units IssuedAt:timestamptz VerificationCode:text")]
    [InlineData("XpTransactions", "Id", "Id:uuid UserId:uuid>Enrollments.UserId CourseId:uuid>Enrollments.CourseId SourceType:text SourceId:uuid Amount:int4 EarnedAt:timestamptz")]
    public async Task MigratedPostgresContainsDocumentedFieldsAndValidatedKeys(
        string table, string primaryKey, string documentedColumns)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var columns = await database.Database.SqlQuery<StoredColumn>($"""
            SELECT column_name AS "Name", udt_name AS "Type", is_nullable = 'YES' AS "Nullable",
                   numeric_precision AS "Precision", numeric_scale AS "Scale"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = {table}
            """).ToDictionaryAsync(column => column.Name);
        var primary = await database.Database.SqlQuery<string>($"""
            SELECT column_definition.attname::text AS "Value"
            FROM pg_constraint constraint_definition
            JOIN pg_class source ON source.oid = constraint_definition.conrelid
            JOIN pg_namespace source_schema ON source_schema.oid = source.relnamespace
            CROSS JOIN LATERAL unnest(constraint_definition.conkey) WITH ORDINALITY AS key_column(number, position)
            JOIN pg_attribute column_definition ON column_definition.attrelid = source.oid
                AND column_definition.attnum = key_column.number
            WHERE source_schema.nspname = 'public' AND source.relname = {table}
                AND constraint_definition.contype = 'p' AND constraint_definition.convalidated
            ORDER BY key_column.position
            """).ToArrayAsync();
        var foreign = await database.Database.SqlQuery<StoredReference>($"""
            SELECT source_column.attname::text AS "Column", target.relname::text AS "TargetTable",
                   target_column.attname::text AS "TargetColumn"
            FROM pg_constraint constraint_definition
            JOIN pg_class source ON source.oid = constraint_definition.conrelid
            JOIN pg_namespace source_schema ON source_schema.oid = source.relnamespace
            JOIN pg_class target ON target.oid = constraint_definition.confrelid
            CROSS JOIN LATERAL unnest(constraint_definition.conkey, constraint_definition.confkey)
                AS key_column(source_number, target_number)
            JOIN pg_attribute source_column ON source_column.attrelid = source.oid
                AND source_column.attnum = key_column.source_number
            JOIN pg_attribute target_column ON target_column.attrelid = target.oid
                AND target_column.attnum = key_column.target_number
            WHERE source_schema.nspname = 'public' AND source.relname = {table}
                AND constraint_definition.contype = 'f' AND constraint_definition.convalidated
            """).ToArrayAsync();

        Assert.Equal(primaryKey.Split(','), primary);
        foreach (string specification in documentedColumns.Split(' '))
        {
            string[] parts = specification.Split(':', '>');
            string name = parts[0];
            string type = parts[1].TrimEnd('?');
            Assert.True(columns.TryGetValue(name, out var column), $"Missing {table}.{name}");
            Assert.Equal(parts[1].EndsWith('?'), column.Nullable);
            Assert.Equal(type, type == "text" && column.Type == "varchar" ? "text" : column.Type);
            if (type == "numeric")
            {
                Assert.Equal(18, column.Precision);
                Assert.Equal(2, column.Scale);
            }
            if (parts.Length == 3)
            {
                string[] target = parts[2].Split('.', 2);
                Assert.Contains(new StoredReference(name, target[0], target.Length == 2 ? target[1] : "Id"), foreign);
            }
        }

        if (table == "TestCards") Assert.Equal(documentedColumns.Split(' ').Length, columns.Count);
    }

    private sealed record StoredColumn(string Name, string Type, bool Nullable, int? Precision, int? Scale);
    private sealed record StoredReference(string Column, string TargetTable, string TargetColumn);
}
