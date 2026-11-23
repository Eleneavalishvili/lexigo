import { authGuard } from './core/accounts/auth-guard';
import { administrationGuard } from './core/administration/administration-guard';
import { contentGuard } from './core/content/content-guard';
import { Routes } from '@angular/router';
import { billingGuard } from './core/billing/billing-guard';

export const routes: Routes = [
  {
    path: 'manage/skills',
    canActivate: [administrationGuard],
    loadComponent: () =>
      import('./features/content/skill-management').then((module) => module.SkillManagementPage),
    title: 'Course skills — LexiGo',
  },
  {
    path: 'activity',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/activity-history').then((module) => module.ActivityHistory),
    title: 'Learning activity — LexiGo',
  },
  {
    path: 'completion-cards',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/completion-cards').then((module) => module.CompletionCardsPage),
    title: 'Your completed chapters — LexiGo',
  },
  {
    path: 'shared/completion/:code',
    loadComponent: () =>
      import('./features/learning/shared-completion-card').then(
        (module) => module.SharedCompletionCardPage,
      ),
    title: 'A completed LexiGo chapter',
  },
  {
    path: 'weekly-summary',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/weekly-summary').then((module) => module.WeeklySummaryPage),
    title: 'Your week in words — LexiGo',
  },
  {
    path: 'help',
    loadComponent: () => import('./features/help/help').then((module) => module.HelpPage),
    title: 'A little help — LexiGo',
  },
  {
    path: 'try/:courseId',
    loadComponent: () =>
      import('./features/learning/guest-trial').then((module) => module.GuestTrialPage),
    title: 'Try your first words — LexiGo',
  },
  {
    path: 'account/delete',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/accounts/delete-account').then((module) => module.DeleteAccount),
    title: 'Delete account — LexiGo',
  },
  {
    path: 'classes/extra-study/:requestId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/classrooms/extra-study-space').then((module) => module.ExtraStudySpace),
    title: 'Private study space — LexiGo',
  },
  {
    path: 'classes/extra-study',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/classrooms/extra-study').then((module) => module.ExtraStudy),
    title: 'Extra study — LexiGo',
  },
  {
    path: 'classes/handover',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/classrooms/accept-class-transfer').then(
        (module) => module.AcceptClassTransfer,
      ),
    title: 'Class handover — LexiGo',
  },
  {
    path: 'classes/:classId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/classrooms/classroom-detail').then((module) => module.ClassroomDetailPage),
    title: 'Your shared journey — LexiGo',
  },
  {
    path: 'classes',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/classrooms/classrooms').then((module) => module.Classrooms),
    title: 'Your classes — LexiGo',
  },
  {
    path: 'manage/rewards',
    canActivate: [administrationGuard],
    loadComponent: () =>
      import('./features/content/reward-catalog').then((module) => module.RewardCatalog),
    title: 'The motivation desk — LexiGo',
  },
  {
    path: 'challenges',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/challenges').then((module) => module.Challenges),
    title: 'Your weekly challenges — LexiGo',
  },
  {
    path: 'achievements',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/achievements').then((module) => module.Achievements),
    title: 'Your milestones — LexiGo',
  },
  {
    path: 'admin/plans',
    title: 'The plan catalogue — LexiGo',
    canActivate: [billingGuard],
    loadComponent: () =>
      import('./features/billing/plan-catalogue').then((module) => module.PlanCatalogue),
  },
  {
    path: 'admin/billing',
    canActivate: [billingGuard],
    loadComponent: () =>
      import('./features/billing/billing-administration').then(
        (module) => module.BillingAdministration,
      ),
    title: 'The billing desk — LexiGo',
  },
  {
    path: 'billing/history',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/billing/subscription-history').then(
        (module) => module.SubscriptionHistory,
      ),
    title: 'Your paper trail — LexiGo',
  },
  {
    path: 'notifications',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/notifications/notifications').then((module) => module.Notifications),
    title: 'Your postcards — LexiGo',
  },
  {
    path: 'reports',
    canActivate: [authGuard],
    loadComponent: () => import('./features/reports/reports').then((module) => module.Reports),
    title: 'Your reports — LexiGo',
  },
  {
    path: 'manage/reports',
    canActivate: [authGuard],
    data: { staff: true },
    loadComponent: () => import('./features/reports/reports').then((module) => module.Reports),
    title: 'Content reports — LexiGo',
  },
  {
    path: 'pricing',
    loadComponent: () => import('./features/billing/pricing').then((module) => module.Pricing),
    title: 'Your little plan — LexiGo',
  },
  {
    path: 'billing/subscribe',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/billing/subscription-checkout').then(
        (module) => module.SubscriptionCheckout,
      ),
    title: 'A new chapter — LexiGo',
  },
  {
    path: 'billing/subscription',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/billing/subscription-checkout').then(
        (module) => module.SubscriptionCheckout,
      ),
    title: 'Your subscription — LexiGo',
  },
  {
    path: 'billing/receipt/:paymentId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/billing/payment-receipt').then((module) => module.PaymentReceipt),
    title: 'Your test receipt — LexiGo',
  },
  {
    path: 'billing/hearts',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/billing/heart-checkout').then((module) => module.HeartCheckout),
    title: 'A little more heart — LexiGo',
  },
  {
    path: 'billing/cards',
    canActivate: [authGuard],
    loadComponent: () => import('./features/billing/test-cards').then((module) => module.TestCards),
    title: 'Your little test wallet — LexiGo',
  },
  {
    path: 'vocabulary',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/vocabulary/vocabulary').then((module) => module.Vocabulary),
    title: 'Words worth keeping — LexiGo',
  },
  {
    path: 'leaderboard',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/leaderboard').then((module) => module.Leaderboard),
    title: 'Every word counts — LexiGo',
  },
  {
    path: 'manage/vocabulary/revisions',
    canActivate: [contentGuard],
    loadComponent: () =>
      import('./features/content/vocabulary-revision-workbench').then(
        (module) => module.VocabularyRevisionWorkbench,
      ),
    title: 'Vocabulary drafts — LexiGo',
  },
  {
    path: 'manage/vocabulary',
    canActivate: [administrationGuard],
    loadComponent: () =>
      import('./features/content/vocabulary-audio-workbench').then(
        (module) => module.VocabularyAudioWorkbench,
      ),
    title: 'Vocabulary recordings — LexiGo',
  },
  {
    path: 'manage/media',
    canActivate: [contentGuard],
    loadComponent: () =>
      import('./features/content/media-library').then((module) => module.MediaLibrary),
    title: 'The listening library — LexiGo',
  },
  {
    path: 'speaking',
    loadComponent: () =>
      import('./features/learning/speaking-practice').then((module) => module.SpeakingPractice),
    title: 'Find your voice — LexiGo',
  },
  {
    path: 'companions',
    loadComponent: () =>
      import('./features/companions/companions').then((module) => module.Companions),
    title: 'Good company — LexiGo',
  },
  {
    path: 'alphabet/russian',
    loadComponent: () =>
      import('./features/learning/russian-alphabet').then((module) => module.RussianAlphabet),
    title: 'Meet the Russian alphabet — LexiGo',
  },
  {
    path: 'learn/start/:courseId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/course-start').then((module) => module.CourseStart),
    title: 'Your first words — LexiGo',
  },
  {
    path: 'learn/placement/:sessionId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/placement-result').then((module) => module.PlacementResultPage),
    title: 'Your starting point — LexiGo',
  },
  {
    path: 'learn/session/:sessionId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/lesson-player').then((module) => module.LessonPlayer),
    title: 'One little step — LexiGo',
  },
  {
    path: 'learn/:courseId/dialogues/:sessionId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/dialogue-page').then((module) => module.DialoguePage),
    title: 'Conversation practice — LexiGo',
  },
  {
    path: 'learn/:courseId/dialogues',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/dialogue-page').then((module) => module.DialoguePage),
    title: 'Conversations — LexiGo',
  },
  {
    path: 'learn/:courseId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/journey').then((module) => module.LearningJourney),
    title: 'Your next chapter — LexiGo',
  },
  {
    path: 'learn',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/learning/journey').then((module) => module.LearningJourney),
    title: 'Your language journeys — LexiGo',
  },
  {
    path: 'courses',
    loadComponent: () => import('./features/catalog/catalog').then((module) => module.Catalog),
    title: 'Where will your words take you? — LexiGo',
  },
  {
    path: 'manage/lessons',
    canActivate: [contentGuard],
    loadComponent: () =>
      import('./features/content/workbench').then((module) => module.ContentWorkbench),
    title: 'The lesson workshop — LexiGo',
  },
  {
    path: 'manage/imports',
    canActivate: [contentGuard],
    loadComponent: () =>
      import('./features/content/lesson-import').then((module) => module.LessonImport),
    title: 'Import a lesson draft — LexiGo',
  },
  {
    path: 'admin/users',
    canActivate: [administrationGuard],
    loadComponent: () =>
      import('./features/administration/users').then((module) => module.ManagedUsers),
    title: 'People & permissions — LexiGo',
  },
  {
    path: 'admin/analytics',
    canActivate: [administrationGuard],
    loadComponent: () =>
      import('./features/administration/analytics').then((module) => module.Analytics),
    title: 'Learning analytics — LexiGo',
  },
  {
    path: 'admin/audit',
    canActivate: [administrationGuard],
    data: { superAdminOnly: true },
    loadComponent: () =>
      import('./features/administration/audit-log').then((module) => module.AuditLog),
    title: 'Administrative audit log — LexiGo',
  },
  {
    path: 'admin/settings',
    canActivate: [administrationGuard],
    data: { superAdminOnly: true },
    loadComponent: () =>
      import('./features/administration/system-settings').then(
        (module) => module.SystemSettingsPage,
      ),
    title: 'System settings — LexiGo',
  },
  {
    path: 'account/security/replace',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/accounts/replace-authenticator').then(
        (module) => module.ReplaceAuthenticator,
      ),
    title: 'A fresh key — LexiGo',
  },
  {
    path: 'account/email',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/accounts/change-email').then((module) => module.ChangeEmail),
    title: 'A little address change — LexiGo',
  },
  {
    path: 'confirm-email-change',
    loadComponent: () =>
      import('./features/accounts/confirm-email-change').then(
        (module) => module.ConfirmEmailChange,
      ),
    title: 'Confirm your new email — LexiGo',
  },
  {
    path: 'account/profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile').then((module) => module.Profile),
    title: 'Your pace, your possibilities — LexiGo',
  },
  {
    path: 'account/security',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/accounts/two-factor').then((module) => module.TwoFactor),
    title: 'A little extra protection — LexiGo',
  },
  {
    path: 'contact',
    loadComponent: () => import('./features/contact/contact').then((module) => module.Contact),
    title: 'A little hello — LexiGo',
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () => import('./features/accounts/account').then((module) => module.Account),
    title: 'Your little big world — LexiGo',
  },
  {
    path: 'login',
    loadComponent: () => import('./features/accounts/login').then((module) => module.Login),
    title: 'Hello again — LexiGo',
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/accounts/forgot-password').then((module) => module.ForgotPassword),
    title: 'A fresh start — LexiGo',
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/accounts/reset-password').then((module) => module.ResetPassword),
    title: 'Turn the page — LexiGo',
  },
  {
    path: 'resend-confirmation',
    loadComponent: () =>
      import('./features/accounts/resend-confirmation').then((module) => module.ResendConfirmation),
    title: 'Another little letter — LexiGo',
  },
  {
    path: 'register',
    loadComponent: () => import('./features/accounts/register').then((module) => module.Register),
    title: 'Your little beginning — LexiGo',
  },
  {
    path: 'verify-email',
    loadComponent: () =>
      import('./features/accounts/verify-email').then((module) => module.VerifyEmail),
    title: 'Confirm your email — LexiGo',
  },
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/home/home').then((module) => module.Home),
    title: 'LexiGo — A little language. A bigger world.',
  },
  {
    path: '**',
    loadComponent: () =>
      import('./features/navigation/not-found').then((module) => module.NotFoundPage),
    title: 'Find your way — LexiGo',
  },
];
