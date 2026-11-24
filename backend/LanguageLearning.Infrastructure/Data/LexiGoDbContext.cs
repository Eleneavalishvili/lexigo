using LanguageLearning.Core.Classrooms;
using LanguageLearning.Core.Billing;
using LanguageLearning.Core.Vocabulary;
using LanguageLearning.Core.Learning;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Core.Content;
using LanguageLearning.Core.Administration;
using LanguageLearning.Core.Courses;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Data;

public sealed class LexiGoDbContext(DbContextOptions<LexiGoDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<ExtraStudyMessage> ExtraStudyMessages => Set<ExtraStudyMessage>();
    public DbSet<ClassMembership> ClassMemberships => Set<ClassMembership>();
    public DbSet<ClassInvitation> ClassInvitations => Set<ClassInvitation>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentLesson> AssignmentLessons => Set<AssignmentLesson>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<TeacherFeedback> TeacherFeedback => Set<TeacherFeedback>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<BillingJobExecution> BillingJobExecutions => Set<BillingJobExecution>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PlanPrice> PlanPrices => Set<PlanPrice>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<BillingOrder> BillingOrders => Set<BillingOrder>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();
    public DbSet<PaymentReceipt> PaymentReceipts => Set<PaymentReceipt>();
    public DbSet<HeartRefillPurchase> HeartRefillPurchases => Set<HeartRefillPurchase>();
    public DbSet<TestCard> TestCards => Set<TestCard>();
    public DbSet<BillingPreference> BillingPreferences => Set<BillingPreference>();
    public DbSet<CardTransaction> CardTransactions => Set<CardTransaction>();
    public DbSet<BillingOperation> BillingOperations => Set<BillingOperation>();
    public DbSet<VocabularyItem> VocabularyItems => Set<VocabularyItem>();
    public DbSet<VocabularyRevision> VocabularyRevisions => Set<VocabularyRevision>();
    public DbSet<SavedWord> SavedWords => Set<SavedWord>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LearnerHearts> LearnerHearts => Set<LearnerHearts>();
    public DbSet<SpeechCheckAttempt> SpeechCheckAttempts => Set<SpeechCheckAttempt>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<LearningSession> LearningSessions => Set<LearningSession>();
    public DbSet<SessionExercise> SessionExercises => Set<SessionExercise>();
    public DbSet<SessionOptionAudio> SessionOptionAudio => Set<SessionOptionAudio>();
    public DbSet<AnswerAttempt> AnswerAttempts => Set<AnswerAttempt>();
    public DbSet<DailyActivity> DailyActivities => Set<DailyActivity>();
    public DbSet<XpTransaction> XpTransactions => Set<XpTransaction>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<UserChallenge> UserChallenges => Set<UserChallenge>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVersion> LessonVersions => Set<LessonVersion>();
    public DbSet<ContentReview> ContentReviews => Set<ContentReview>();
    public DbSet<LessonPrerequisite> LessonPrerequisites => Set<LessonPrerequisite>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseOption> ExerciseOptions => Set<ExerciseOption>();
    public DbSet<AcceptedAnswer> AcceptedAnswers => Set<AcceptedAnswer>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<UserLanguageScope> UserLanguageScopes => Set<UserLanguageScope>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();
    public DbSet<ExtraStudyOffer> ExtraStudyOffers => Set<ExtraStudyOffer>();
    public DbSet<ExtraStudyRequest> ExtraStudyRequests => Set<ExtraStudyRequest>();
    public DbSet<ExtraStudyLesson> ExtraStudyLessons => Set<ExtraStudyLesson>();
    public DbSet<WordReview> WordReviews => Set<WordReview>();
    public DbSet<CompletionCard> CompletionCards => Set<CompletionCard>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ExerciseSkill> ExerciseSkills => Set<ExerciseSkill>();
    public DbSet<SkillProgress> SkillProgress => Set<SkillProgress>();
    public DbSet<Dialogue> Dialogues => Set<Dialogue>();
    public DbSet<DialogueNode> DialogueNodes => Set<DialogueNode>();
    public DbSet<DialogueChoice> DialogueChoices => Set<DialogueChoice>();
    public DbSet<DialogueSession> DialogueSessions => Set<DialogueSession>();
    public DbSet<DialogueAttempt> DialogueAttempts => Set<DialogueAttempt>();
    public DbSet<WordCollection> WordCollections => Set<WordCollection>();
    public DbSet<CollectionWord> CollectionWords => Set<CollectionWord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("btree_gist");
        builder.ApplyConfigurationsFromAssembly(typeof(LexiGoDbContext).Assembly);
    }
}

