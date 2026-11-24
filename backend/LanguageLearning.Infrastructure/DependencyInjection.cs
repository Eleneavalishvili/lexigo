using LanguageLearning.Infrastructure.Classrooms;
using LanguageLearning.Infrastructure.Billing;
using LanguageLearning.Infrastructure.Speech;
using LanguageLearning.Infrastructure.Vocabulary;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Storage;
using LanguageLearning.Infrastructure.Accounts;
using LanguageLearning.Infrastructure.Administration;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Infrastructure.Email;
using LanguageLearning.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BillingRenewalOptions>().Bind(configuration.GetSection("BillingRenewals"))
            .Validate(value => value.GraceMinutes is >= 1 and <= 60, "Renewal grace must be 1–60 minutes.").ValidateOnStart();
        services.AddScoped<SubscriptionPaymentRecorder>();
        services.AddScoped<BillingAdministrationAccess>();
        services.AddScoped<BillingPermissionService>();
        services.AddScoped<BillingAdministrationQueries>();
        services.AddScoped<BillingPlanQueries>();
        services.AddScoped<BillingPlanEditor>();
        services.AddScoped<PlanPriceScheduling>();
        services.AddScoped<RefundService>();
        services.AddScoped<StarterLessonInstaller>();
        services.AddScoped<StarterDialogueInstaller>();
        services.AddScoped<SubscriptionHistoryQueries>();
        services.AddScoped<NotificationService>();
        services.AddScoped<SubscriptionRenewalProcessor>();
        services.AddHostedService<SubscriptionRenewalWorker>();
        services.AddDbContext<LexiGoDbContext>(options => options.UseNpgsql(
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Configure the database connection using local secrets or environment variables.")));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddPasswordValidator<PasswordSymbolValidator>()
        .AddEntityFrameworkStores<LexiGoDbContext>()
        .AddDefaultTokenProviders();
        services.AddScoped<IUserStore<ApplicationUser>, ProtectedUserStore>();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));
        services.AddOptions<AccountEmailOptions>().Bind(configuration.GetSection("AccountEmail"))
            .Validate(options => Uri.TryCreate(options.WebOrigin, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https" && uri.AbsolutePath == "/"
                && (uri.IsLoopback || uri.Scheme == "https") && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment), "Configure a valid frontend origin.")
            .ValidateOnStart();
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("Smtp")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
        services.AddSingleton(TimeProvider.System);
        services.AddPrivateObjectStorage(configuration);
        services.AddScoped<SessionService>();
        services.AddScoped<MfaCodeVerifier>();
        services.AddScoped<TwoFactorService>();
        services.AddScoped<AuthenticatorReplacementService>();
        services.AddScoped<SuperAdminBootstrapService>();
        services.AddScoped<AdministrationQueries>();
        services.AddScoped<SystemSettings>();
        services.AddScoped<LearningAnalytics>();
        services.AddScoped<SubscriptionManagementService>();
        services.AddScoped<SubscriptionQueries>();
        services.AddScoped<BillingOrderService>();
        services.AddScoped<SubscriptionPaymentService>();
        services.AddScoped<BillingAccountAccess>();
        services.AddScoped<HeartPurchaseService>();
        services.AddScoped<TestCardService>();
        services.AddScoped<TestCardQueries>();
        services.AddScoped<LearningAccess>();
        services.AddScoped<WeeklySummaryQueries>();
        services.AddScoped<CompletionCardIssuer>();
        services.AddScoped<CompletionCardQueries>();
        services.AddScoped<CompletionCardManagement>();
        services.AddScoped<ClassroomAccess>();
        services.AddScoped<ExtraStudyDirectory>();
        services.AddScoped<ExtraStudyRequests>();
        services.AddScoped<ExtraStudySpace>();
        services.AddScoped<WordReviewService>();
        services.AddScoped<WordCollections>();
        services.AddScoped<ClassroomQueries>();
        services.AddScoped<ClassroomManagement>();
        services.AddScoped<ClassroomTransferService>();
        services.AddScoped<AssignmentManagement>();
        services.AddScoped<AssignmentQueries>();
        services.AddScoped<AssignmentSubmissions>();
        services.AddScoped<AssignmentResults>();
        services.AddScoped<AssignmentFeedback>();
        services.AddScoped<ContentReportService>();
        services.AddSingleton<RankingReportTokens>();
        services.AddScoped<AssignmentDeadlineNotices>();
        services.AddHostedService<AssignmentDeadlineWorker>();
        services.AddScoped<ClassInvitationService>();
        services.AddScoped<ClassJoiningService>();
        services.AddScoped<ClassMemberService>();
        services.AddScoped<HeartBalanceService>();
        services.AddOptions<LocalSpeechOptions>().Bind(configuration.GetSection("LocalSpeech"))
            .Validate(options => !options.Enabled || Path.IsPathFullyQualified(options.ModelsDirectory)
                && Directory.Exists(options.ModelsDirectory), "Configure an existing local speech model directory.")
            .ValidateOnStart();
        services.AddSingleton<ISpeechRecognizer, LocalSpeechRecognizer>();
        services.AddScoped<SpeechCheckService>();
        services.AddScoped<VocabularyQueries>();
        services.AddScoped<StarterVocabularyInstaller>();
        services.AddScoped<VocabularyAudio>();
        services.AddScoped<StaffVocabularyAudio>();
        services.AddScoped<VocabularyRevisions>();
        services.AddScoped<VocabularyExport>();
        services.AddScoped<LearningDataExport>();
        services.AddScoped<LearnerCommunicationExport>();
        services.AddScoped<SimulatedBillingExport>();
        services.AddScoped<AuthoredContentExport>();
        services.AddScoped<AccountDeletionService>();
        services.AddScoped<GuestTrialService>();
        services.AddScoped<AccountDeletionResponsibilities>();
        services.AddScoped<LessonDraftErasure>();
        services.AddScoped<LessonOwnershipService>();
        services.AddScoped<SavedWordService>();
        services.AddScoped<EnrollmentService>();
        services.AddScoped<PlacementService>();
        services.AddScoped<MistakeQueries>();
        services.AddScoped<SkillMapQueries>();
        services.AddScoped<DialogueService>();
        services.AddScoped<DialogueRewards>();
        services.AddScoped<MistakePracticeService>();
        services.AddScoped<LearningQueries>();
        services.AddScoped<DashboardQueries>();
        services.AddScoped<ActivityQueries>();
        services.AddScoped<AchievementProgressQueries>();
        services.AddScoped<AchievementQueries>();
        services.AddScoped<AchievementAwards>();
        services.AddScoped<WeeklyChallengeCatalog>();
        services.AddScoped<ChallengeQueries>();
        services.AddScoped<ChallengeProgression>();
        services.AddHostedService<WeeklyChallengeWorker>();
        services.AddScoped<LeaderboardQueries>();
        services.AddScoped<LeaderboardParticipationService>();
        services.AddScoped<LearningSessionService>();
        services.AddScoped<LearningAnswerService>();
        services.AddScoped<LearningAudioService>();
        services.AddScoped<LearningCompletionService>();
        services.AddScoped<ContentAccess>();
        services.AddScoped<RewardCatalogAccess>();
        services.AddScoped<RewardCatalogQueries>();
        services.AddScoped<AchievementManagement>();
        services.AddScoped<ChallengeManagement>();
        services.AddScoped<MediaUploadService>();
        services.AddScoped<MediaManagementService>();
        services.AddScoped<MediaQueries>();
        services.AddScoped<ContentTransactions>();
        services.AddScoped<ContentStructureService>();
        services.AddScoped<CourseCreation>();
        services.AddScoped<PurchaseReceiptNotice>();
        services.AddScoped<PresentationAccountProvisioner>();
        services.AddScoped<DialogueDrafts>();
        services.AddScoped<DialogueWorkflow>();
        services.AddScoped<SkillManagement>();
        services.AddScoped<ManagedExerciseQueries>();
        services.AddScoped<ContentQueries>();
        services.AddScoped<LessonPreviewGrading>();
        services.AddScoped<CatalogQueries>();
        services.AddScoped<LessonDraftService>();
        services.AddScoped<LessonImports>();
        services.AddScoped<DraftExerciseService>();
        services.AddScoped<LessonValidation>();
        services.AddScoped<LessonWorkflow>();
        services.AddScoped<PrerequisiteService>();
        services.AddScoped<UserAdministrationService>();
        services.AddScoped<AccountQueryService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<ProfileAvatarService>();
        services.AddScoped<EmailChangeService>();
        services.AddScoped<LoginService>();
        services.AddScoped<TeacherAccessHasher>();
        services.AddScoped<PasswordRecoveryService>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<RoleSeeder>();
        services.AddHostedService<EmailDeliveryWorker>();
        services.AddScoped<StudyReminderQueue>();
        services.AddHostedService<StudyReminderWorker>();
        return services;
    }
}

