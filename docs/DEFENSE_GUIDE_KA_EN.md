# LexiGo: დაცვის გზამკვლევი / Presentation and code guide

მომზადებულია 24 ნოემბერს, 2026. ეს არის სასწავლო გზამკვლევი: პასუხები ეფუძნება მიმდინარე კოდს და შემოწმების ანგარიშებს. ის არ აცხადებს მთელი სპეციფიკაციის დასრულებას.

Prepared on 24 November 2026. This study guide explains the current implementation and recorded checks. It does not certify completion of the entire specification.

## 1. ჯერ ეს დაიმახსოვრე / Learn these first

KA: LexiGo არის ენების სასწავლო პლატფორმა. ინგლისურენოვანი მომხმარებელი სწავლობს ესპანურს, იტალიურს, გერმანულსა და რუსულს. ინტერფეისი მუშაობს Angular/TypeScript-ით; სერვერი - C#/ASP.NET Core-ით; მონაცემები ინახება PostgreSQL-ში Entity Framework Core-ის საშუალებით.

EN: LexiGo is a language-learning platform for English speakers studying Spanish, Italian, German and Russian. Angular/TypeScript provides the interface; C#/ASP.NET Core provides the backend; Entity Framework Core accesses PostgreSQL.

- Website / ვებსაიტი: https://206.189.25.5/
- Code / კოდი: https://github.com/Eleneavalishvili/lexigo - private repository.
- Backend: .NET 10, EF Core 10, Npgsql, ASP.NET Core Identity, JWT.
- Frontend: Angular 21, TypeScript, HTML, SCSS, Signals, RxJS, HttpClient.
- Hosting: Ubuntu, Docker Compose, Nginx, HTTPS.
- Payments / გადახდები: simulated test cards; რეალური ბანკის თანხა არ იხარჯება.
- Mobile / მობილური: Home Screen web app; არა App Store-ის native აპლიკაცია. Internet is required.
- Content / მასალა: 161 სასწავლო ნაბიჯი, მათ შორის 129 სასწავლო გაკვეთილი და 32 შემაჯამებელი შემოწმება; 2,148 კითხვა; 88 საწყისი ლექსიკონის ჩანაწერი. These are not complete A1-C2 courses.

KA: მთავარი პრინციპი: ბრაუზერი აჩვენებს ინტერფეისს, მაგრამ ქულებს, უფლებებს, გულებსა და გადახდებს საბოლოოდ სერვერი აკონტროლებს.

EN: The browser displays the interface, but the server is authoritative for scores, permissions, hearts and payment decisions.

## 2. მოკლე პრეზენტაცია / Short presentation script

KA: „ჩემი პროექტია LexiGo, ენების სასწავლო ვებპლატფორმა. მომხმარებელი ირჩევს ენას, სასწავლო მიზანსა და საწყის წერტილს. გაკვეთილში ჯერ ეცნობა მასალას, შემდეგ ასრულებს სხვადასხვა ტიპის სავარჯიშოს და იღებს უკუკავშირს. პროგრესი დაკავშირებულია ანგარიშთან და ინახება მონაცემთა ბაზაში.

Frontend აგებულია Angular-ზე. ფუნქციები განაწილებულია კომპონენტებად, ხოლო მდგომარეობას Signals და შესაბამისი სერვისები მართავს. Backend აგებულია ASP.NET Core-ზე. Controller იღებს HTTP მოთხოვნას, შესაბამისი სერვისი ამოწმებს ბიზნესწესებს, ხოლო Entity Framework Core მუშაობს PostgreSQL-თან.

შეფასება სერვერზე სრულდება. მომხმარებელს არ შეუძლია მხოლოდ ინტერფეისში მნიშვნელობის შეცვლით მიიღოს XP, Premium ან დამატებითი გულები. პროექტს აქვს ლექსიკონი, მიღწევები, კლასები, პროფილი და სიმულირებული გადახდები. საჯარო ვერსია მუშაობს HTTPS-ით. ახლა ვაჩვენებ რეალურ გაკვეთილს, შენახულ პროგრესს და შემდეგ შესაბამის კოდს.“

EN: “My project is LexiGo, a language-learning web platform. A learner chooses a language, a study goal and a starting point. Lessons introduce material before practice and provide feedback. Progress belongs to the account and is stored in the database.

The frontend uses Angular, with feature components and Signals-based state. The backend uses ASP.NET Core. Controllers receive HTTP requests, services enforce business rules, and Entity Framework Core accesses PostgreSQL.

Answers are graded on the server. Changing a browser value cannot itself grant XP, Premium access or hearts. The platform also includes vocabulary, achievements, classes, profiles and simulated payments. The public version uses HTTPS. I will demonstrate a lesson and saved progress, then show the code behind them.”

## 3. კოდის რუკა / Code map

KA: VS Code-ში დააჭირე Ctrl+P-ს, ჩასვი ქვემოთ მოცემული გზა და გახსენი ფაილი. Ctrl+F-ით მოძებნე დასახელებული კლასი ან მეთოდი. ნომრები გამოიყენება ქვემოთ მოცემულ პასუხებში.

EN: In VS Code, press Ctrl+P and paste a path below. Use Ctrl+F to find the named class or method. The reference numbers are used throughout the questions.

### C01 - Backend startup
backend/LanguageLearning.Api/Program.cs

KA: სერვერის გაშვება, middleware, rate limiting, authentication და controller-ების მიერთება. EN: Application startup and the request-processing pipeline.

### C02 - Dependency injection and Identity
backend/LanguageLearning.Infrastructure/DependencyInjection.cs

KA: AddInfrastructure რეგისტრირებს DbContext-ს, სერვისებს, Identity-ს და პაროლის წესებს. EN: Registers infrastructure services, EF access and Identity configuration.

### C03 - Angular configuration
frontend/src/app/app.config.ts

KA: Router, HttpClient, interceptor და საწყისი სესიის აღდგენა. EN: Configures routing, HTTP interception and initial authentication restoration.

### C04 - Routes
frontend/src/app/app.routes.ts

KA: რომელ URL-ზე რომელი კომპონენტი იტვირთება და სად მოქმედებს guard. EN: Maps URLs to components and route guards.

### C05 - Lesson interface
frontend/src/app/features/learning/lesson-player.ts
frontend/src/app/features/learning/lesson-player.html

KA: გაკვეთილის ეკრანი და სხვადასხვა სავარჯიშოს საერთო აწყობა. EN: Coordinates the lesson interface and reusable exercise views.

### C06 - Lesson state
frontend/src/app/core/learning/learning-store.ts

KA: session, feedback, busy, error; load, პასუხის გაგზავნა და მდგომარეობის განახლება. EN: Owns current lesson state and coordinates requests.

### C07 - Learning API client
frontend/src/app/core/learning/learning-api.ts

KA: TypeScript კონტრაქტები და HTTP მოთხოვნები. EN: Typed request/response contracts and HTTP calls.

### C08 - Learning endpoints
backend/LanguageLearning.Api/Learning/LearningController.cs

KA: enrollment, progress, lesson session და პასუხის endpoint-ები. EN: HTTP entry points for enrollment, sessions, answers and completion.

### C09 - Starting a session
backend/LanguageLearning.Infrastructure/Learning/LearningSessionService.cs

KA: StartAsync ქმნის სესიას და სავარჯიშოების საჯარო/პრივატულ snapshot-ებს. EN: Creates a session with stable public and private exercise snapshots.

### C10 - Answer grading
backend/LanguageLearning.Infrastructure/Learning/LearningAnswerService.cs

KA: AnswerAsync/SaveAsync - ვალიდაცია, ownership, idempotency, შეფასება და შენახვა. EN: Validates and grades answers with ownership and replay protection.

### C11 - Text comparison
backend/LanguageLearning.Infrastructure/Learning/TextAnswerEvaluator.cs

KA: Matches და Normalize - გამოტოვებები, პუნქტუაცია და ნებადართული აქცენტების შემწყნარებლობა. EN: Controlled answer normalization and comparison.

### C12 - Completion and XP
backend/LanguageLearning.Infrastructure/Learning/LearningCompletionService.cs

KA: CompleteAsync - დასრულება, ქულა, პირველი წარმატებული გავლა, XP და მიღწევები. EN: Completes lessons and applies durable rewards.

### C13 - Active-account access
backend/LanguageLearning.Infrastructure/Learning/LearningAccess.cs

KA: LockAsync ამოწმებს აქტიურ ანგარიშს, დადასტურებულ ელფოსტასა და გაუუქმებელ სესიას. EN: Checks account/session validity while locking the account row.

### C14 - Score calculation
backend/LanguageLearning.Infrastructure/Learning/SessionAssessment.cs

KA: Calculate განასხვავებს graded პასუხებსა და თვითშეფასებას. EN: Calculates score, accuracy and completion eligibility.

### C15 - Database context
backend/LanguageLearning.Infrastructure/Data/LexiGoDbContext.cs

KA: DbSet-ები, Identity და მოდელების კონფიგურაციების გამოყენება. EN: EF context, entity sets and Identity integration.

### C16 - SQL configurations and migrations
backend/LanguageLearning.Infrastructure/Data/Configurations/
backend/LanguageLearning.Infrastructure/Data/Migrations/

KA: FK, indexes, constraints და სქემის ცვლილებები. EN: Relationships, indexes, constraints and versioned schema changes.

### C17 - Domain models
backend/LanguageLearning.Core/Courses/Course.cs
backend/LanguageLearning.Core/Content/Lesson.cs
backend/LanguageLearning.Core/Learning/LearningSession.cs

KA: კურსის, გაკვეთილისა და დაწყებული სესიის განსხვავებული მოდელები. EN: Separate domain representations of courses, lessons and learner sessions.

### C18 - Frontend authentication
frontend/src/app/core/accounts/auth-store.ts
frontend/src/app/core/accounts/auth-interceptor.ts
frontend/src/app/core/accounts/auth-guard.ts

KA: login/refresh/logout, Bearer header და გვერდზე შესვლის წინ შემოწმება. EN: Session restoration, authorization headers and route navigation checks.

### C19 - Backend login and sessions
backend/LanguageLearning.Infrastructure/Accounts/LoginService.cs
backend/LanguageLearning.Infrastructure/Accounts/SessionService.cs
backend/LanguageLearning.Api/Accounts/SessionController.cs

KA: შესვლა, refresh rotation და სესიის გაუქმება. EN: Login, refresh rotation and revocation.

### C20 - JWT and cookies
backend/LanguageLearning.Api/Authentication/AuthenticationSetup.cs
backend/LanguageLearning.Api/Authentication/AccessTokenIssuer.cs
backend/LanguageLearning.Api/Authentication/RefreshCookie.cs
backend/LanguageLearning.Api/Authentication/JwtOptions.cs

KA: RS256 ხელმოწერა, token validation და დაცული refresh cookie. EN: Signed access tokens, validation and protected refresh cookies.

### C21 - Registration and password recovery
backend/LanguageLearning.Infrastructure/Accounts/RegistrationService.cs
backend/LanguageLearning.Infrastructure/Accounts/PasswordRecoveryService.cs

KA: რეგისტრაცია, დადასტურება და reset link-ის ბიზნესლოგიკა. EN: Registration, confirmation and password-reset workflows.

### C22 - Roles and scoped access
backend/LanguageLearning.Core/Accounts/RoleNames.cs
backend/LanguageLearning.Infrastructure/Content/ContentAccess.cs
backend/LanguageLearning.Infrastructure/Classrooms/ClassroomAccess.cs

KA: როლი, ენის scope, ownership და staff MFA. EN: Role, language scope, ownership and privileged-session checks.

### C23 - Email delivery
backend/LanguageLearning.Infrastructure/Email/EmailDeliveryWorker.cs

KA: outbox-ის durable შეტყობინებები, retry და SMTP. EN: Background outbox processing and SMTP delivery.

### C24 - Hearts
backend/LanguageLearning.Core/Learning/HeartRecovery.cs
backend/LanguageLearning.Infrastructure/Learning/HeartBalanceService.cs

KA: მაქსიმუმი 5, ერთსაათიანი აღდგენა და Premium entitlement. EN: Shared heart balance, recovery deadline and Premium entitlement.

### C25 - Payments
backend/LanguageLearning.Infrastructure/Billing/SubscriptionPaymentService.cs
backend/LanguageLearning.Core/Billing/BillingOrder.cs
backend/LanguageLearning.Core/Billing/TestCard.cs

KA: სიმულირებული გადახდა, ფასი snapshot-ში, balance და transaction. EN: Simulated checkout, price snapshots and atomic balance changes.

### C26 - Vocabulary
frontend/src/app/features/vocabulary/vocabulary.ts
backend/LanguageLearning.Infrastructure/Vocabulary/WordReviewService.cs

KA: სიტყვების ნახვა, შენახვა და განმეორების შეფასება. EN: Word discovery, private saves and review logic.

### C27 - Classes and assignments
backend/LanguageLearning.Infrastructure/Classrooms/ClassJoiningService.cs
backend/LanguageLearning.Infrastructure/Classrooms/AssignmentManagement.cs

KA: კოდით შეერთება, წევრობა და მასწავლებლის დავალებები. EN: Invitation-code joining and teacher-owned assignment operations.

### C28 - Pronunciation and recording
frontend/src/app/core/learning/pronunciation.ts
frontend/src/app/core/learning/voice-recorder.ts
backend/LanguageLearning.Infrastructure/Speech/LocalSpeechRecognizer.cs
tools/pronunciation/generate.py

KA: საცნობარო WAV, ბრაუზერის ჩაწერა და ცალკე local speech recognition. EN: Distinct reference playback, recording and recognition mechanisms.

### C29 - Private media
backend/LanguageLearning.Infrastructure/Storage/MediaObjectStore.cs
backend/LanguageLearning.Infrastructure/Storage/WaveAudioInspector.cs

KA: private S3-compatible ობიექტები და აუდიოს შემოწმება. EN: Private signed object access and audio-file inspection.

### C30 - Original teaching content
backend/LanguageLearning.Infrastructure/Content/Starter/es.json
backend/LanguageLearning.Infrastructure/Content/Starter/it.json
backend/LanguageLearning.Infrastructure/Content/Starter/de.json
backend/LanguageLearning.Infrastructure/Content/Starter/ru.json
backend/LanguageLearning.Infrastructure/Content/StarterLessonInstaller.cs

KA: გაკვეთილების საწყისი პაკეტები და კონტროლირებული ინსტალაცია. EN: Embedded original course packs and controlled installation.

### C31 - Placement, achievements and dialogues
backend/LanguageLearning.Infrastructure/Learning/PlacementService.cs
backend/LanguageLearning.Infrastructure/Learning/AchievementAwards.cs
backend/LanguageLearning.Infrastructure/Learning/DialogueService.cs

KA: საწყისი ერთეულის რეკომენდაცია, მიღწევები და branching დიალოგი. EN: Starting-point recommendations, achievements and branching conversations.

### C32 - Profile and deletion
frontend/src/app/features/profile/profile.ts
backend/LanguageLearning.Infrastructure/Accounts/AccountDeletionService.cs

KA: პირადი პარამეტრები და დაცული წაშლა. EN: Profile preferences and guarded deletion.

### C33 - Home Screen app
frontend/public/manifest.webmanifest
frontend/src/index.html
frontend/src/app/features/help/help.html
tools/assets/generate-app-icons.mjs

KA: standalone metadata, Apple icon და დაყენების ინსტრუქცია. EN: App launch metadata, original-logo icons and installation instructions.

### C34 - Shared visual identity
frontend/src/styles.scss
frontend/src/app/shared/brand-logo/brand-logo.ts
frontend/src/app/features/home/world-globe.ts
frontend/angular.json

KA: ფერები, ფონტები, საერთო margins, ლოგო, globe და production optimization. EN: Design tokens, global layout, branding and production build settings.

### C35 - Public deployment
config/deployment/presentation.compose.yml
config/nginx/lexigo-ip.conf.template
tools/deployment/renew_certificate.sh
tools/deployment/expire_droplet.py

KA: კონტეინერები, reverse proxy, HTTPS renewal და დროებითი სერვერის წაშლის guard. EN: Production containers, HTTPS and guarded server expiry.

### C36 - Tests and release checks
backend/LanguageLearning.Tests/
frontend/e2e/
.github/workflows/lexigo-ci.yml
scripts/Verify-DatabaseRestore.ps1
scripts/Verify-NginxEdge.ps1
scripts/Verify-Secrets.ps1
scripts/Verify-ContainerImages.ps1

KA: ბიზნესწესები, PostgreSQL, კომპონენტები, ბრაუზერი, აღდგენა და security gates. EN: Tests for business rules, database behavior, browsers, recovery and release security.

## 4. ერთი პასუხის სრული გზა / Trace one answer end to end

1. KA: მომხმარებელი ირჩევს პასუხს lesson-player-ში. EN: The learner selects an answer in the lesson component. Code: C05.
2. KA: LearningStore ინახავს UI-ის მდგომარეობას და აგზავნის მოქმედებას. EN: LearningStore coordinates the action and loading state. Code: C06.
3. KA: LearningApi აგზავნის HTTP მოთხოვნას; interceptor ურთავს access token-ს. EN: LearningApi sends the request and the interceptor attaches a bearer token. Code: C07, C18.
4. KA: LearningController იღებს request DTO-სა და ავტორიზებულ მომხმარებელს. EN: The controller receives the DTO and authenticated identity. Code: C08.
5. KA: LearningAnswerService ამოწმებს ownership-ს, მიმდინარე სავარჯიშოს, request key-სა და პასუხს. EN: The service checks ownership, exercise order, request identity and correctness. Code: C10, C13.
6. KA: EF Core transaction-ში ინახავს შედეგსა და საჭირო გულის ცვლილებას. EN: EF Core saves the attempt and heart changes inside a transaction. Code: C10, C15, C24.
7. KA: ბრუნდება feedback; Store ანახლებს ეკრანს. EN: Feedback returns and the store updates the view. Code: C06.
8. KA: გაკვეთილის დასრულებისას ცალკე CompleteAsync ითვლის შედეგსა და XP-ს. EN: Completion separately calculates final results and XP. Code: C12.

KA: მოკლე ფრაზა: Component -> Store -> API client -> Controller -> Service -> EF Core -> PostgreSQL -> Feedback.

EN: Memorize the flow, then point to each real file rather than reciting architecture terms without examples.

## 5. ძირითადი კითხვები / Core questions

### Q01. C# და ASP.NET Core ერთი რამ არის? / Are C# and ASP.NET Core the same?
KA: არა. C# პროგრამირების ენაა; ASP.NET Core არის framework, რომლითაც C#-ში ვქმნით HTTP სერვერს. .NET არის runtime და პლატფორმა. ჩვენს controller-ებსა და სერვისებს C#-ში ვწერთ.
EN: C# is the language, ASP.NET Core is the web framework, and .NET is the platform/runtime. Our controllers and services are C# code.
Code: C01, C08.

### Q02. რატომ Angular? / Why Angular?
KA: გვჭირდება ბევრი ურთიერთდაკავშირებული ეკრანი, ფორმები, routing და მდგომარეობის მართვა. Angular გვაძლევს საერთო სტრუქტურას, typed TypeScript-სა და კომპონენტების ხელახლა გამოყენებას.
EN: Angular provides a consistent structure for a multi-screen application with typed code, forms, routing and reusable components.
Code: C03-C07.

### Q03. რა არის კომპონენტი? / What is a component?
KA: ინტერფეისის დამოუკიდებელი ნაწილია საკუთარი ქცევით, template-ითა და სტილით. Lesson player აერთიანებს ცალკე sentence-builder-ს, matching-board-სა და feedback panel-ს.
EN: A component owns a focused piece of UI. The lesson player composes reusable sentence-building, matching and feedback components.
Code: C05; frontend/src/app/features/learning/sentence-builder.ts; frontend/src/app/features/learning/matching-board.ts.

### Q04. რას ნიშნავს standalone? / What does standalone mean?
KA: კომპონენტი საკუთარ dependencies-ს imports-ში აცხადებს და მისი გამოყენება ცალკე feature NgModule-ის გარეშე შეიძლება. ეს არ ნიშნავს backend-ის გარეშე მუშაობას.
EN: A standalone component declares its own imports without requiring a feature NgModule. It still uses the backend for persistent data and rules.
Code: C05.

### Q05. როგორ მუშაობს routing? / How does routing work?
KA: app.routes.ts URL-ს უკავშირებს კომპონენტს. loadComponent კომპონენტის კოდს საჭიროებისას ტვირთავს. Guard მომხმარებელს login-ზე გადაიყვანს, მაგრამ ნამდვილ უსაფრთხოებას backend ამოწმებს.
EN: Routes map URLs to lazy-loaded components. Guards improve navigation; backend authorization remains the security boundary.
Code: C04, C18.

### Q06. რა არის State management? / What is state management?
KA: session, არჩეული პასუხი, loading, feedback და error შეთანხმებულად უნდა იცვლებოდეს. Signals ინახავს ცვალებად მდგომარეობას; derived მნიშვნელობები computed-ით მიიღება. მუდმივი პროგრესი PostgreSQL-შია.
EN: Stores coordinate the current session, selection, loading, feedback and errors. Signals hold reactive state; durable progress is stored on the server.
Code: C06, C18.

### Q07. Signal და ჩვეულებრივი ცვლადი რით განსხვავდება? / How is a Signal different from a normal variable?
KA: Signal-ის წაკითხვისას Angular მის მომხმარებლებს აკვირდება და ცვლილებაზე შესაბამის UI-ს ანახლებს. ჩვეულებრივი ცვლადი ასეთი reactive dependency-ის API არ არის.
EN: Angular tracks signal consumers and updates dependent UI when the signal changes. A normal variable does not provide that reactive dependency mechanism.
Code: C06 - signal, feedback, busy.

### Q08. რატომ HttpClient და RxJS? / Why HttpClient and RxJS?
KA: HttpClient HTTP-სა და typed კონტრაქტებს ემსახურება. RxJS გამოიყენება interceptor-ში request/refresh/error ნაკადებისთვის; firstValueFrom იქ, სადაც ერთჯერადი პასუხის async/await-ით დამუშავებაა მოსახერხებელი.
EN: HttpClient handles HTTP requests. RxJS coordinates request, refresh and error streams; firstValueFrom bridges one response into async/await.
Code: C07, C18.

### Q09. რა არის REST API და endpoint? / What are REST APIs and endpoints?
KA: კლიენტი რესურსებზე მუშაობს HTTP მეთოდებით. Endpoint არის მეთოდისა და გზის კომბინაცია, მაგალითად POST /api/v1/lessons/{id}/sessions. მხოლოდ URL-ის ხსენება მეთოდის გარეშე არასრულია.
EN: REST-style endpoints expose resource operations over HTTP. An endpoint combines a method and path, not just a URL.
Code: C08.

### Q10. Controller და Service რით განსხვავდება? / Controller versus service?
KA: Controller HTTP request/response-ს ამუშავებს. Service ბიზნესწესებს ასრულებს. მაგალითად, სწორი პასუხისა და XP-ის გადაწყვეტა controller-ის HTML ან frontend ღილაკის საქმე არ არის.
EN: Controllers handle HTTP concerns; services implement business rules such as grading and reward eligibility.
Code: C08, C10, C12.

### Q11. რატომ გვაქვს სამი backend პროექტი? / Why three backend projects?
KA: Core შეიცავს domain მოდელებს; Infrastructure - EF-სა და რეალურ სერვისებს; Api - HTTP-სა და startup-ს. ეს კოდს პასუხისმგებლობების მიხედვით ყოფს, მაგრამ სრულად „pure Clean Architecture“-ს ავტომატურად არ ნიშნავს.
EN: Core contains domain models, Infrastructure contains persistence/application services, and Api exposes HTTP. It is a layered structure, not a claim of perfect Clean Architecture.
Code: C01, C02, C15, C17.

### Q12. რა არის Dependency Injection? / What is dependency injection?
KA: კლასს საჭირო სერვისებს constructor-ით ვაწვდით. AddScoped რეგისტრაცია request-ის scope-ში საერთო instance-ს იძლევა; Singleton უფრო ხანგრძლივია. DbContext singleton არ უნდა გავხადოთ.
EN: Dependencies are supplied through constructors. Scoped services live within a request scope; singletons live longer. DbContext is not registered as a singleton.
Code: C02.

### Q13. OOP სად გამოიყენეთ? / Where is OOP used?
KA: entity და service კლასებში; interfaces-ით პასუხისმგებლობის კონტრაქტში; constructor-ებით დამოკიდებულებების მიწოდებაში. მაგალითად, IAccessTokenIssuer-ის რეალიზაცია არის AccessTokenIssuer.
EN: OOP appears in entity/service classes, interfaces and constructor dependencies. IAccessTokenIssuer has a concrete token-issuer implementation.
Code: C17, C20; backend/LanguageLearning.Core/Accounts/IAccessTokenIssuer.cs.

### Q14. async/await რატომ გვჭირდება? / Why async/await?
KA: ბაზისა და ქსელის პასუხის ლოდინმა request-ის thread არ უნდა დაბლოკოს. CancellationToken გაუქმებულ მოთხოვნაზე მუშაობის შეჩერებას ეხმარება. ეს თავისთავად ყველა გამოთვლას არ აჩქარებს.
EN: Asynchronous I/O avoids blocking a request thread while waiting for the database or network. Cancellation tokens support stopping abandoned work.
Code: C10, C12, C25.

## 6. SQL და მონაცემები / SQL and data

### Q15. რატომ PostgreSQL და არა localStorage? / Why PostgreSQL instead of localStorage?
KA: გვჭირდება დაკავშირებული მონაცემები, constraints, transactions და სხვადასხვა მოწყობილობაზე შენახული პროგრესი. localStorage შეიძლება მომხმარებელმა შეცვალოს და სერვერის წესების წყარო ვერ იქნება.
EN: PostgreSQL provides relationships, constraints, transactions and cross-device persistence. Browser storage is user-controlled and cannot be the authority.
Code: C15-C17.

### Q16. რა არის EF Core? / What is EF Core?
KA: ORM-ია: C# მოდელებსა და LINQ query-ებს relational ბაზასთან აკავშირებს. DbContext ცვლილებებს აკვირდება; SaveChangesAsync მათ SQL ოპერაციებად ასრულებს. Npgsql PostgreSQL provider-ია.
EN: EF Core maps C# entities and queries to relational data. DbContext tracks changes and persists them; Npgsql is the PostgreSQL provider.
Code: C02, C15.

### Q17. Migration რას აკეთებს? / What does a migration do?
KA: სქემის ცვლილებას ვერსიად ინახავს: ახალი ცხრილი, ველი, index ან constraint. უკვე გაშვებულ ბაზაზე სქემას კონტროლირებულად ვცვლით; ყოველ startup-ზე მონაცემებს არ ვშლით.
EN: Migrations version schema changes such as columns, tables and constraints. They support controlled evolution rather than resetting production data.
Code: C16.

### Q18. PK, FK და relationships სად ჩანს? / Where are primary and foreign keys?
KA: Id ხშირად primary key-ა. UnitId, UserId ან CourseId foreign key-ებია შესაბამის ურთიერთობებში. HasOne/WithMany/HasForeignKey კონფიგურაციაში აჩენს კავშირს; FK-ის ქცევას SQL-იც იცავს.
EN: Entity IDs are primary keys; fields such as UnitId and CourseId reference related records. EF configurations create relationships enforced by SQL.
Code: C16, C17.

### Q19. Many-to-many მაგალითი? / A many-to-many example?
KA: მომხმარებლები და კლასები ClassMembership-ით უკავშირდება ერთმანეთს; სიტყვები და კოლექციები - CollectionWord-ით. შემაკავშირებელ ჩანაწერს შეიძლება თავისი დამატებითი ველებიც ჰქონდეს.
EN: Users and classes are linked through ClassMembership; vocabulary and collections through CollectionWord. Join entities can hold additional attributes.
Code: C15; backend/LanguageLearning.Core/Classrooms/ClassMembership.cs.

### Q20. რატომ არის unique index საჭირო? / Why unique indexes?
KA: მხოლოდ „ჯერ შეამოწმე, მერე ჩასვი“ ერთდროულ მოთხოვნებს ვერ აკონტროლებს. SQL unique index იცავს წესს მაშინაც, როცა ორი request ერთად მოდის. XP-სთვის UserId/SourceType/SourceId უნიკალურია.
EN: An application pre-check alone races under concurrency. Unique indexes enforce invariants, including one XP award for a given user and source.
Code: C16 - XpTransactionConfiguration.cs; AnswerAttemptConfiguration.cs.

### Q21. რა არის transaction? / What is a transaction?
KA: ურთიერთდაკავშირებული ცვლილებები ერთად სრულდება ან ერთად უქმდება. სიმულირებული ბალანსის ჩამოჭრა და გამოწერის აქტივაცია ერთიანად უნდა ჩაიწეროს, არა ნახევრად.
EN: A transaction commits related changes together or rolls them back together. A debit and subscription activation must not be saved independently.
Code: C25, C12.

### Q22. FOR UPDATE რატომ გამოიყენეთ? / Why FOR UPDATE?
KA: row lock ერთდროულ ცვლილებებს არეგულირებს. ანგარიშის შემოწმება და შესაბამისი ბიზნესოპერაცია ერთ transaction-ში ხდება. SQL ტექსტში მნიშვნელობები parameterized interpolation-ით გადადის.
EN: Row locks serialize conflicting updates. Account validation and mutation occur within a transaction, using parameterized SQL values.
Code: C13.

### Q23. JSONB რატომ არის, თუ SQL ბაზა გვაქვს? / Why JSONB in a relational database?
KA: ძირითადი კავშირები relational-ია. JSONB გამოიყენება შესაბამის მოქნილ მონაცემებზე, წესებსა და snapshot-ებზე; დამტკიცებული მოდელების სხვა JSONB ველები შენარჩუნებულია. მთელი ბაზა JSON დოკუმენტებით არ შეგვიცვლია.
EN: Core relationships remain relational. JSONB stores suitable structured rules/snapshots and other documented fields; it does not replace the relational model.
Code: C09, C10, C16.

### Q24. რატომ decimal ფულისთვის? / Why decimal for money?
KA: binary floating-point-ის დამრგვალება ფულად ოპერაციებში არასასურველია. TestCard.Balance არის decimal და SQL precision არის 18,2. ამ პროექტში თანხა მაინც მხოლოდ სიმულირებულია.
EN: Decimal avoids inappropriate binary floating-point rounding for monetary values. Test-card balance uses decimal with SQL precision 18,2.
Code: C25; Data/Configurations/TestCardConfiguration.cs.

## 7. შესვლა და უსაფრთხოება / Authentication and security

### Q25. Authentication და Authorization? / Authentication versus authorization?
KA: Authentication ამოწმებს ვინ ხარ; Authorization - რისი გაკეთება შეგიძლია. სწორად შესულ Learner-საც არ შეუძლია სხვისი wallet-ის ან admin მონაცემების წაკითხვა.
EN: Authentication establishes identity; authorization determines permitted actions. Signing in does not grant access to other users' records.
Code: C19, C20, C22.

### Q26. პაროლები სად ინახება? / Where are passwords stored?
KA: Identity ინახავს salted password hash-ს, არა წასაკითხ პაროლს. Hash-ს უკან არ ვშიფრავთ; შესვლისას framework ამოწმებს შესაბამისობას. პაროლი არც GitHub-ში და არც log-ში უნდა მოხვდეს.
EN: Identity stores salted password hashes, not readable passwords. Verification compares through the framework; it does not decrypt the original password.
Code: C02, C21.

### Q27. რა წესები აქვს პაროლს? / What are the password rules?
KA: მიმდინარე კონფიგურაციაში მინიმუმ 12 სიმბოლო, ერთი დიდი ასო და სპეციალური სიმბოლოა საჭირო. მხოლოდ space სპეციალურ სიმბოლოდ არ უნდა ჩაითვალოს. Backend validator აუცილებელია frontend ვალიდაციასთან ერთად.
EN: Current configuration requires at least 12 characters, an uppercase letter and a symbol. The backend also uses a custom symbol validator.
Code: C02; backend/LanguageLearning.Infrastructure/Identity/PasswordSymbolValidator.cs.

### Q28. JWT რა არის? / What is a JWT?
KA: ხელმოწერილი access token-ია claims-ით, მაგალითად sub, sid, role და expiry. Payload-ის წაკითხვა შესაძლებელია; ხელმოწერა encryption არ არის. პაროლები token-ში არ იწერება.
EN: A JWT is a signed token containing identity/session claims and expiry. Its payload is readable; signing is not encryption. Passwords are not token claims.
Code: C20.

### Q29. token სად ინახება? / Where are tokens stored?
KA: access token AuthStore-ის მეხსიერებაშია. refresh token HttpOnly cookie-ით მოძრაობს; production-ში აქვს Secure, SameSite=Strict და __Host- prefix. ბაზაში refresh token-ის hash ინახება.
EN: Access tokens are held in memory. Refresh tokens use a protected HttpOnly cookie, while their hashes are stored server-side.
Code: C18-C20.

### Q30. refresh რისთვის არის? / Why refresh tokens?
KA: access token მოკლეხნიანია, default 5 წუთი. Refresh session საწყისად 7 დღეა; rotation ახალ token-ს გასცემს, მაგრამ ოჯახის expiry-ს არ აგრძელებს. ძველი token-ის ხელახალი გამოყენება family-ის გაუქმებას იწვევს.
EN: Access tokens are short-lived, defaulting to five minutes. Refresh sessions initially last seven days; rotation preserves the family deadline and detects reuse.
Code: C19, C20 - JwtOptions and SessionService.

### Q31. რა ხდება logout-ზე ან პაროლის reset-ზე? / What happens on logout or reset?
KA: შესაბამისი refresh session უქმდება და cookie იშლება. Password reset დამატებით აუქმებს არსებულ სესიებს. დაცული ოპერაციები session-ის მოქმედებას სერვერზეც ამოწმებს.
EN: Logout revokes the session and clears the cookie. Password reset revokes existing sessions; protected operations also validate active session state.
Code: C19, C21, C13.

### Q32. CSRF და XSS ერთი რამ არის? / Are CSRF and XSS the same?
KA: არა. CSRF სხვა საიტიდან cookie-ზე დაფუძნებული მოქმედების გამოწვევაა; authentication mutations იყენებს antiforgery token-ს. XSS არის არასანდო script-ის გაშვება; Angular escaping და CSP დაცვის ფენებია, არა სრული გარანტია.
EN: CSRF abuses ambient credentials; authentication mutations require antiforgery proof. XSS executes untrusted scripts; escaping and CSP help reduce that risk.
Code: C18 - authPost; C20 - AuthenticationSetup; C35.

### Q33. MFA ყველას სჭირდება? / Does everyone need MFA?
KA: ჩვეულებრივი Learner-ისთვის სავალდებულო არ არის. პრივილეგირებულ ანგარიშებს დამატებითი დაცვა აქვთ; როლის არსებობა საკმარისი არ არის, მოქმედ სესიასაც სჭირდება შესაბამისი MFA proof.
EN: Ordinary learners are not forced to enroll. Privileged access requires appropriate MFA/session proof in addition to roles.
Code: C22; docs/PRIVILEGED_SETUP.md.

### Q34. Frontend guard საკმარისია? / Is a frontend guard sufficient?
KA: არა. მომხმარებელს შეუძლია პირდაპირ API გამოიძახოს. Backend ამოწმებს role-ს, ownership-ს, ენის scope-ს, წევრობასა და entitlement-ს შესაბამის ოპერაციაზე.
EN: No. A user can call the API directly. The server must enforce role, ownership, language, membership and entitlement rules.
Code: C13, C22, C24, C25.

### Q35. Rate limiting რისთვისაა? / Why rate limiting?
KA: ზღუდავს მოკლე დროში ზედმეტ მოთხოვნებს. Program.cs-ში account, session და learning პოლიტიკებია. სერვერი აბრუნებს 429-ს; ეს ბრუტფორსისა და დატვირთვის დაცვის ერთ-ერთი ფენაა.
EN: Rate limits constrain excessive requests and return 429. They are one defensive layer, not complete protection against every attack.
Code: C01.

### Q36. Email confirmation/reset როგორ მუშაობს? / How do account emails work?
KA: სერვერი ქმნის შესაბამის token-ს და outbox ჩანაწერს; worker SMTP-ით აგზავნის წერილს. Reset-ის მოთხოვნა ძველ პაროლს არ ცვლის; ცვლილება valid reset proof-ის შემდეგ ხდება. საჯარო პასუხი ანგარიშის არსებობას არ უნდა ამჟღავნებდეს.
EN: Tokens and durable outbox messages support confirmation/reset. Requesting a reset does not change the password; valid proof is required to complete it.
Code: C21, C23.

### Q37. Email-ს ზუსტად ერთხელ აგზავნით? / Are emails delivered exactly once?
KA: ზოგადად SMTP-სა და SQL transaction-ს ერთი atomic commit არ აქვთ. Outbox იძლევა შენახვასა და retry-ს, მაგრამ ყველა crash-ის დროს exactly-once SMTP გარანტია არ გვაქვს. Reminder-ის reservation პოლიტიკა ცალკეა.
EN: SMTP and SQL do not share one atomic commit. The outbox provides durability/retries, but does not guarantee exactly-once delivery across every crash.
Code: C23.

## 8. სწავლა და ბიზნესწესები / Learning rules

### Q38. რატომ არ აფასებს JavaScript პასუხს? / Why not grade in JavaScript?
KA: ბრაუზერის კოდი მომხმარებლის კონტროლქვეშაა. სერვერზე შეფასება იცავს XP-სა და პროგრესს გაყალბებისგან. UI მხოლოდ დაბრუნებულ feedback-ს აჩვენებს.
EN: Browser code is user-controlled. Server-side grading protects the authoritative score and progress; the UI renders the result.
Code: C10, C12.

### Q39. სწორი პასუხები რატომ არის ცალკე? / Why separate public and private snapshots?
KA: PublicSnapshot შეიცავს გამოსაჩენ კითხვას/არჩევანს; PrivateEvaluationSnapshot - შეფასებისთვის საჭირო პასუხებსა და წესებს. მოულოდნელი content edit დაწყებულ სესიას არ ცვლის და answer key წინასწარ არ იგზავნება.
EN: Public snapshots describe the displayed exercise. Private snapshots hold grading data, keeping active sessions stable without exposing answer keys.
Code: C09.

### Q40. ორჯერ დაჭერისას ორჯერ XP ხომ არ დაემატება? / Can duplicate clicks award twice?
KA: UI busy flag მხოლოდ კომფორტია. Idempotency key, transaction, account lock და unique reward ledger იცავს სერვერს. ერთი key-ის სხვა payload-ით გამოყენება conflict-ია.
EN: UI locking is not enough. Request keys, transactions, row locks and unique reward records protect against repeated or concurrent requests.
Code: C10, C12, C16.

### Q41. XP როდის გაიცემა? / When is XP awarded?
KA: პირველი წარმატებული graded lesson completion ქმნის FirstLessonCompletion ledger ჩანაწერს. თანხა კონფიგურირებული LessonXp-დან მოდის. იგივე გაკვეთილის გამეორება იგივე first-pass reward-ს არ იმეორებს.
EN: The first successful graded completion creates a durable reward entry using configured LessonXp. Replays do not duplicate that reward.
Code: C09, C12.

### Q42. Score და accuracy განსხვავდება? / Score versus accuracy?
KA: score ითვლის სწორ პასუხებს ყველა graded სავარჯიშოს მიმართ; accuracy - graded პასუხგაცემულების მიმართ. მაგალითად, 10 კითხვიდან პასუხგაცემულია 8 და სწორია 6: score 60%, accuracy 75%.
EN: Score uses all graded exercises as the denominator; accuracy uses graded answered exercises. Six correct out of eight answered in a ten-question set gives 60% score and 75% accuracy.
Code: C14.

### Q43. Speaking ყოველთვის graded არის? / Is all speaking graded?
KA: არა. თვითშეფასება ungraded-ია და accuracy-ს არ აყალბებს. ჩაწერა/მოსმენა, სიტყვების ამოცნობა და ფონეტიკური გამოთქმის ზუსტი შეფასება განსხვავებული რამ არის. Speaking-only practice XP-ს არ იძლევა.
EN: Self-assessment is ungraded. Recording, word recognition and precise pronunciation scoring are different capabilities; speaking-only practice does not earn lesson XP.
Code: C14, C28.

### Q44. გულები როგორ აღდგება? / How do hearts recover?
KA: მაქსიმუმია 5. ამოწურვისას სერვერი აფიქსირებს ერთსაათიან deadline-ს; countdown მისი დროიდან ითვლება. კომპიუტერის საათის შეცვლით backend-ის ბალანსი ვერ შეიცვლება.
EN: The maximum is five. Exhaustion creates a server-side one-hour recovery deadline; changing the client clock does not grant server-side hearts.
Code: C24.

### Q45. აქცენტიანი ასოს გამოტოვება ყოველთვის მისაღებია? / Are missing accents always accepted?
KA: კონტროლირებული წესით plain Latin vowel შეიძლება accent-იანი vowel-ის ნაცვლად მივიღოთ. ყველა ასო არ იშლება: ñ ავტომატურად n არ არის, რუსული transliteration არ არის სწორი Cyrillic პასუხი. ეკრანი სწორ ფორმას მაინც აჩვენებს.
EN: Controlled rules accept certain unaccented Latin vowels. They do not erase arbitrary letter differences or accept Latin transliteration as Russian.
Code: C11, C10.

### Q46. მასალა საიდან მოდის? / Where does teaching content come from?
KA: LexiGo-სთვის მომზადებული საწყისი JSON პაკეტებიდან და staff-ის ვერსიიანი workflow-დან. ეს არ არის Duolingo-ს კურსების ასლი ან მისი მოცულობის სრული ანალოგი.
EN: Original embedded starter packs and a versioned staff workflow supply content. The project does not claim copied Duolingo lessons or equivalent full curriculum breadth.
Code: C30.

### Q47. Placement სრულ CEFR დონეს ადასტურებს? / Is placement CEFR certification?
KA: არა. მიმდინარე კითხვებიდან ტექსტური diagnostic ურჩევს საწყის ერთეულს. გამოტოვებული გაკვეთილებისთვის XP არ გაიცემა და უკვე შენახული პროგრესი არ იშლება.
EN: It recommends a starting unit from a text diagnostic. It is not CEFR certification, awards no skipped XP and preserves existing progress.
Code: C31 - PlacementService.

### Q48. ხმა საიდან მოდის? / Where does the voice come from?
KA: საცნობარო ტექსტი ადგილობრივად გენერირდება Piper-ით და WAV ფაილად ისმის. MediaRecorder მომხმარებლის ხმას იწერს. LocalSpeechRecognizer ცალკე recognition გზაა; ეს ფუნქციები AI ვიდეოზარი არ არის.
EN: Piper-generated WAVs provide reference pronunciation. MediaRecorder captures the learner; local recognition is separate. None of these alone creates an AI video call.
Code: C28.

## 9. სხვა მოდულები / Other modules

### Q49. Premium frontend ღილაკით აქტიურდება? / Can a button grant Premium?
KA: არა. Backend ამოწმებს owned order/card-ს, ბალანსს, სტატუსსა და request key-ს. წარმატებული სიმულირებული transaction-ის შემდეგ ჩნდება subscription; entitlement სერვერზე მოწმდება.
EN: The backend validates the owned order/card and payment state. A successful simulated transaction activates a subscription; entitlements remain server-enforced.
Code: C25, C24.

### Q50. რატომ ინახავთ ფასის snapshot-ს? / Why snapshot the price?
KA: გადახდისას ნაჩვენები შეთანხმებული ფასი მომავალ catalog edit-ზე არ უნდა გადაიწეროს. Order ინახავს amount/currency/period snapshot-ს; ახალი ფასი ძველ ტრანზაქციას არ ცვლის.
EN: An order preserves the agreed amount, currency and period so later catalog edits do not rewrite historical transactions.
Code: C25.

### Q51. რეალური გადახდა/Flitt გვაქვს? / Are real payments integrated?
KA: არა. არსებული სისტემა test-card simulator-ია. რეალური merchant onboarding და საბანკო გადახდები არ არის დასრულებული ან ჩართული. ნამდვილი card number/CVV არ უნდა შევიყვანოთ.
EN: No. Current payments are simulated. Real merchant onboarding and bank-card charges are not implemented or enabled.
Code: C25.

### Q52. ლექსიკონი რით განსხვავდება უბრალო სიიდან? / How is vocabulary more than a list?
KA: მომხმარებელს აქვს პირადი saves და collections, due review და server-graded პასუხები. სხვისი saved-word მონაცემი მისი account scope-ის გარეთ არ უნდა იკითხებოდეს.
EN: It supports private saved words, collections and scheduled server-graded reviews, with ownership checks.
Code: C26.

### Q53. მასწავლებელს ყველა მომხმარებლის შედეგი შეუძლია ნახოს? / Can teachers see everyone's results?
KA: არა. წვდომა დამოკიდებულია teacher role/MFA-ზე, კლასის ownership-სა და წევრობაზე. Learner მხოლოდ ნებადართულ საკუთარ კლასსა და დავალებებზე მუშაობს.
EN: No. Teacher access depends on role/MFA, classroom ownership and membership. Learners only access their permitted classroom data.
Code: C22, C27.

### Q54. ავტორს თავისი მასალის approval შეუძლია? / Can authors approve their own work?
KA: ჩვეულებრივი reviewer workflow მოითხოვს სხვა უფლებამოსილ reviewer-ს შესაბამისი ენის scope-ით. ContentAccess.CanReview გამორიცხავს owner-სა და version creator-ს. Built-in starter content-ის დაშვება ცალკე შეთანხმებული გზაა.
EN: Normal staff review requires an authorized independent reviewer in the language scope. Built-in starter content uses the separately approved installation route.
Code: C22, C30.

### Q55. ფაილები public URL-ით ინახება? / Are uploaded files public?
KA: private მედია S3-compatible საცავშია; ნებადართული წვდომა signed, ვადიანი URL-ით გაიცემა. ეს არ ეხება საჯარო static reference WAV-ებს, რომლებიც სასწავლო მასალაა.
EN: Uploaded private media uses signed, expiring access. Public static reference WAVs are a separate category of teaching assets.
Code: C28, C29.

### Q56. ანგარიშის წაშლა ყველაფრის DELETE-ია? / Does account deletion delete everything indiscriminately?
KA: პირადი მონაცემები იშლება/რედაქტირდება შეთანხმებული პოლიტიკით; ზოგი ფინანსური ან გამოქვეყნებული ისტორია ანონიმურად რჩება. Staff-ის აქტიური პასუხისმგებლობები ჯერ გადაცემას შეიძლება საჭიროებდეს.
EN: Private data is erased/redacted under the agreed policy, while some billing/publication history is anonymized. Active staff responsibilities can block deletion until transferred.
Code: C32; docs/REQUIREMENTS.md.

## 10. ტესტირება, გაშვება და რთული კითხვები / Testing, deployment and hard questions

### Q57. რა ტიპის ტესტები გაქვთ? / What kinds of tests?
KA: Angular component/store tests, backend business/security tests, რეალური disposable PostgreSQL integration და Playwright browser journeys. ეს განსხვავებული მტკიცებულებებია; ერთი unit test მთელი ვებსაიტის მუშაობას არ ამტკიცებს.
EN: Component/store tests, backend business/security tests, PostgreSQL integration and browser journeys cover different layers and risks.
Code: C36.

### Q58. კონკრეტულად რა შედეგებია? / What results can you show?
KA: დაფიქსირებულია backend-ის 686 და frontend-ის 390 წარმატებული ტესტი. სრული local browser run: 74 pass/2 skip; გამოტოვებული private-profile წყვილი ცალკე გარემოში 2 pass. ყველა შედეგი ერთ უწყვეტ გაშვებად არ უნდა წარმოვადგინოთ.
EN: Recorded runs include 686 backend and 390 frontend passes. The local browser suite had 74 passes/two skips; a separate private-storage profile run passed those two cases.
Code: docs/TEST_REPORT_KA.md; work/deployment/ logs are private local evidence.

### Q59. GitHub Actions მთლიანად მწვანეა? / Is hosted CI fully green?
KA: არა. დაფიქსირებულ hosted run-ში ორი password-recovery browser check ჩავარდა, მიუხედავად local წარმატებისა. მიზეზი request/response-ითა და log-ით გამოსაკვლევია. უბრალოდ timeout-ის გაზრდას შესწორებად არ ვაცხადებთ.
EN: Not yet. Two hosted password-recovery browser checks failed despite local passes. They need diagnosis rather than an unsupported “all green” claim.
Code: .github/workflows/lexigo-ci.yml; frontend/e2e/account-recovery.spec.ts.

### Q60. Docker რას აკეთებს? / What does Docker do?
KA: პროცესებს იზოლირებულ კონტეინერებში უშვებს; Compose აკავშირებს API-ს, PostgreSQL-ს, private storage-სა და Nginx-ს. Volume ინახავს მონაცემებს container recreation-ის მიღმა, მაგრამ სერვერის წაშლას backup-ად ვერ ცვლის.
EN: Docker isolates services; Compose connects them. Volumes preserve data across container recreation but do not replace off-server backups.
Code: C35.

### Q61. Nginx რისთვისაა? / Why Nginx?
KA: აწვდის Angular static ფაილებს, HTTPS-ს ამუშავებს და /api მოთხოვნებს ASP.NET-ზე გადაამისამართებს. Angular deep link-ების refresh-ს index.html fallback სჭირდება; private backend/DB პორტები საჯარო არ არის.
EN: Nginx serves static files, terminates HTTPS and proxies API requests. SPA deep links need an index fallback, while private services remain unexposed.
Code: C35.

### Q62. IP, domain და hosting რა განსხვავებაა? / IP versus domain versus hosting?
KA: IP სერვერის ქსელური მისამართია; domain დასამახსოვრებელი სახელია, რომელსაც DNS IP-ს უკავშირებს; hosting ის გარემოა, სადაც პროგრამა მუშაობს. domain-ის ყიდვა სერვერის საფასურს არ ანაცვლებს.
EN: An IP identifies the server, DNS maps a domain name to it, and hosting runs the software. Buying a domain does not pay for hosting.
Code: C35; live URL.

### Q63. Home Screen ვერსია native iOS აპია? / Is the Home Screen version native iOS?
KA: არა. ეს standalone web app-ია Safari-ის Home Screen ფუნქციით. იყენებს იმავე backend-სა და ანგარიშს; internet სჭირდება. App Store membership, native package და Apple approval არ გვქონია.
EN: It is a standalone Home Screen web app using the same backend. It needs internet and has not been published through Apple's App Store.
Code: C33.

### Q64. რატომ იყო გვერდები მარცხნივ მიწებებული? / Why did pages look pushed left?
KA: Angular-ის production critical-CSS optimization stylesheet-ს print media-თი და inline onload handler-ით ტვირთავდა. Strict script-src 'self' ამ handler-ს ბლოკავდა; საერთო .wrap წესები არ მოქმედებდა. inlineCritical:false-მა normal stylesheet link აღადგინა, CSP-ის შესუსტების გარეშე.
EN: Production critical-CSS loading relied on an inline handler blocked by CSP. Disabling that inlining restored normal stylesheet loading and shared margins without weakening the policy.
Code: C34 - angular.json and styles.scss; C36 - shared-layout.spec.ts.

### Q65. ვებსაიტი სრულად დაცულია? / Is the site completely secure?
KA: ამის გარანტია არ არსებობს. ვაჩვენებთ კონკრეტულ ფენებს: server authorization, Identity, MFA, HTTPS, constraints, private storage, rate limiting და scans. დამოუკიდებელი review და დარჩენილი შემოწმებები კვლავ საჭიროა.
EN: No absolute guarantee is justified. Explain the implemented defenses and evidence, while acknowledging remaining review and maintenance work.
Code: C13, C20-C22, C29, C35, C36.

### Q66. დასრულებულია ყველა მოთხოვნა? / Is every requirement finished?
KA: არა. ბოლო baseline აღრიცხვა არის 313/335 verified და 22 partial/pending. checklist entry არ უდრის თანაბარი ზომის feature-ს ან სამუშაოს პროცენტს. ზოგი API contract, content review, physical-device და release საკითხი ღიაა.
EN: No. The recorded baseline is 313/335 verified entries with 22 partial/pending. Entries are not equally sized features or a reliable percentage of remaining effort.
Code: docs/REQUIREMENTS.md; docs/API_RELEASE_REVIEW.md.

### Q67. რამე მნიშვნელოვანი არ გაქვთ? / What should you not claim?
KA: არ ვაცხადებთ სრულ A1-C2 კურსებს, Duolingo-სთან სრულ პარიტეტს, 100% გამოთქმას, ნამდვილ AI ვიდეოზარებს, რეალურ bank payments-ს ან App Store publication-ს.
EN: Do not claim complete A1-C2 courses, Duolingo parity, perfect pronunciation, live AI video calls, real payments or App Store publication.
Code: docs/CURRICULUM_STATUS.md; docs/REQUIREMENTS.md.

### Q68. რას შეცვლიდი შემდეგ? / What would you improve next?
KA: მოვაგვარებდი hosted recovery failures-ს, დავასრულებდი documented management API contract-ებს, დამოუკიდებლად გადავამოწმებდი ენებს/აუდიოს და გავივლიდი რეალურ iPhone-სა და staff account demo-ს. პრიორიტეტია საიმედოობა, შემდეგ ახალი შესაძლებლობები.
EN: Fix hosted recovery failures, finish management contracts, review language/audio quality and rehearse physical-device/staff access before expanding features.
Code: docs/API_RELEASE_REVIEW.md; docs/REQUIREMENTS.md.

### Q69. რატომ არა ერთი დიდი ფაილი? / Why not one large file?
KA: UI, HTTP, ბიზნესწესები და persistence სხვადასხვა პასუხისმგებლობაა. მათ განცალკევებას შეცვლისა და ტესტირების ფასი ამცირებს. პატარა expression-bodied controller თავისთავად ცუდი კოდი არ არის; გიგანტური mixed-responsibility ფაილია პრობლემა.
EN: Separating UI, transport, business rules and persistence improves maintainability. Concise thin controllers are different from enormous mixed-responsibility files.
Code: C05-C15.

### Q70. თუ კოდის ავტორობაზე გკითხეს? / What if they ask about authorship?
KA: კოდი AI-assisted არის; არ უნდა თქვა, რომ ყველაფერი დამოუკიდებლად ხელით დაიწერა. იხელმძღვანელე კურსის წესებით და აჩვენე, რომ კონკრეტული ფუნქციების ახსნა, შემოწმება და შეცვლა შეგიძლია.
EN: The code is AI-assisted; do not claim entirely unaided authorship. Follow course policy and demonstrate your understanding through concrete explanations and changes.
Code: გამოიყენე C05 -> C06 -> C08 -> C10 -> C15 გზა.

## 11. საინტერესო ნაწილები, რომლებიც აჩვენო / Interesting code to demonstrate

1. Snapshot-ები / Session snapshots: C09. KA: მიმდინარე სესია არ იცვლება published content-ის ცვლილებაზე. EN: Ongoing sessions retain their started version.
2. Idempotency / გამეორების უსაფრთხო დამუშავება: C10. KA: იგივე მოთხოვნა იგივე შედეგს აბრუნებს; სხვა payload იგივე key-ით conflict-ია. EN: Safe retries do not duplicate effects.
3. XP ledger: C12/C16. KA: reward uniqueness სერვერსა და SQL-შია. EN: The database reinforces reward uniqueness.
4. Account row lock: C13. KA: კონკრეტული race condition-ის ახსნა აჩვენებს ბიზნესწესის ცოდნას. EN: Explain what simultaneous requests could otherwise do.
5. Refresh rotation: C19. KA: ძველი token-ის reuse ოჯახის გაუქმებას იწვევს. EN: Session-family revocation limits refresh-token reuse.
6. Price snapshot/transaction: C25. KA: წარსული ფასი არ იცვლება და ნახევარი გადახდა არ რჩება. EN: Historical price integrity and atomic payment effects.
7. Language scope: C22. KA: Reviewer შეიძლება მხოლოდ მის ენას ხედავდეს და საკუთარ მასალას ვერ ამტკიცებდეს. EN: Role plus scope plus ownership.
8. UI state cleanup: C06/C18. KA: account-ის ცვლილებაზე წინა მომხმარებლის დაგვიანებული პასუხი არ უნდა გამოისახოს. EN: Ignore stale responses across account changes.
9. Readiness and restore tests: C36. KA: compile არ უდრის მუშაობას; persistent data-საც ვამოწმებთ. EN: Build success alone is not behavior verification.
10. CSP layout fix: C34/C35/C36. KA: რეალური production failure, მიზეზი და regression check. EN: A concrete debugging example, not a theoretical claim.

## 12. მოკლე ტერმინები / Rapid definitions

- DTO: KA - API-სთვის განკუთვნილი მონაცემის ფორმა. EN - A request/response contract, not an EF entity exposed indiscriminately.
- Entity: KA - მონაცემთა/domain მოდელი. EN - A modeled object persisted/mapped by the application.
- LINQ: KA - typed query-ის C# ფორმა. EN - Typed query expressions, often translated by EF into SQL.
- CRUD: Create, Read, Update, Delete.
- FK: Foreign key; კავშირი სხვა ჩანაწერთან / relational reference.
- Index: ძიებისა და/ან უნიკალურობის დასაცავი SQL სტრუქტურა / lookup and uniqueness support.
- Transaction: ყველა ცვლილება ერთად ან rollback / atomic unit of work.
- Idempotency: განმეორებამ ერთი მოქმედება არ უნდა გააორმაგოს / safe repeated requests.
- Middleware: request pipeline-ის ეტაპი / a request-processing layer.
- Claim: token-ში არსებული მტკიცება / a token assertion such as user ID or role.
- Hash: one-way verifier; encryption-ისგან განსხვავდება / distinct from reversible encryption.
- HTTPS: transport encryption and authenticated server connection; არ აფიქსირებს ყველა application bug-ს.
- CORS: browser cross-origin response access policy; authentication-ის შემცვლელი არ არის.
- CSP: browser content-execution policy; correct configuration and testing are still required.
- Unit test: მცირე ლოგიკური ნაწილის შემოწმება / focused behavior test.
- Integration test: ნაწილების ერთად შემოწმება / real database/service interaction.
- E2E: ბრაუზერიდან მომხმარებლის გზა / browser user journey.
- Migration: სქემის ვერსიიანი ცვლილება / versioned schema change.
- Snapshot: იმ მომენტში დაფიქსირებული ასლი / a captured version of data.
- Entitlement: კონკრეტულ შესაძლებლობაზე სერვერით დადასტურებული წვდომა / server-confirmed feature access.

## 13. სტატუსის კოდები / HTTP status codes

- 200: წარმატებული წაკითხვა/შედეგი / successful response.
- 201: შეიქმნა ახალი რესურსი; სასურველია Location / created resource with a location.
- 204: წარმატება response body-ის გარეშე / success with no content.
- 400: არასწორი request / invalid input.
- 401: საჭიროა მოქმედი authentication / missing or invalid authentication.
- 403: ცნობილ მომხმარებელს უფლება არ აქვს / authenticated but forbidden.
- 404: რესურსი არ მოიძებნა ან მის არსებობას არ ვამჟღავნებთ / missing or intentionally concealed resource.
- 409: მიმდინარე მდგომარეობასთან კონფლიქტი / state or idempotency conflict.
- 429: მოთხოვნების ზღვარი / rate limit.
- 500: მოულოდნელი server error; მომხმარებელს არ უნდა გაეგზავნოს private stack trace.

## 14. პრაქტიკული SQL და debugging / Practical SQL and debugging

KA: შემდეგი SELECT-ები read-only მაგალითებია. გაუშვი მხოლოდ შენს ნებადართულ ბაზაში. არ აჩვენო user rows, PasswordHash, token-ები ან პირადი email-ები ეკრანზე.

EN: These are read-only examples for an authorized database. Do not display user records, password hashes, tokens or personal email data.

SQL: SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name;

KA: ეს აჩვენებს რეალურ ცხრილებს. „70 documented models“ ავტომატურად არ ნიშნავს ზუსტად 70 SQL table-ს. EN: Count real tables instead of confusing documented-model counts with database-table counts.

SQL: SELECT "Title", "IsPublished" FROM "Courses" ORDER BY "OrderNumber";

SQL: SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

KA: თუ კითხვა გაქვს „ეს რომელ endpoint-ს იძახებს?“: Browser Developer Tools -> Network -> Fetch/XHR -> აირჩიე request -> ნახე method, URL, status და JSON. Password/token-ის ასლები სხვას არ გაუზიარო.

EN: Use the Network panel to trace the real method, URL, status and JSON, then search the route in the controller and follow its injected service.

KA: თუ „ცვლილება არ ჩანს“: გადაამოწმე local და live მისამართები, deployed build, HTML entry და cache. Editor-ში შენახვა ავტომატურად DigitalOcean-ზე deploy არ არის.

EN: Distinguish local development from the public deployed build. Saving a source file does not automatically deploy it.

## 15. როგორ გაუშვა და აჩვენო / Startup and demonstration

KA: საჯარო ვერსიას შენი ლეპტოპის ჩართული ყოფნა არ სჭირდება. Local ვერსიისთვის Docker Desktop უნდა მუშაობდეს; VS Code-ში project root გახსენი და Ctrl+Shift+B -> Start LexiGo, ან terminal-ში ./scripts/Start-Demo.ps1 -OpenBrowser.

EN: The public version runs independently of the laptop. For local use, start Docker Desktop and run the repository's Start LexiGo task or Start-Demo script.

- Local frontend: http://localhost:4200/
- Local API: http://localhost:5187/
- Local Swagger: http://localhost:5187/swagger - Development only.
- Local email inbox: http://localhost:8025/ - წერილები აქ რჩება / not Gmail delivery.
- Live: https://206.189.25.5/

KA: Visual Studio-ში solution არის backend/LexiGo.slnx. ერთ პორტზე API-ის ორი ასლი ერთდროულად არ გაუშვა. არ გახსნა/არ შეცვალო private .env, signing keys ან secrets პრეზენტაციისას.

EN: Open backend/LexiGo.slnx in Visual Studio. Avoid duplicate API instances on the same port and keep private secrets out of the presentation.

### რეკომენდებული მიმდევრობა / Suggested demo order

1. Homepage -> ოთხი ენა / four languages; სურვილისამებრ atlas.
2. Course -> Start from scratch; აჩვენე teach-first material / introduce material before practice.
3. ერთი სწორი და ერთი არასწორი პასუხი / correct and incorrect feedback.
4. Complete -> XP/results -> reload; აჩვენე შენახვა / demonstrate persistence.
5. Vocabulary -> save/review -> profile/preferences.
6. Simulated wallet/Premium; მკაფიოდ თქვი test balance / explain no real charges.
7. Staff screens მხოლოდ მომზადებული უფლებამოსილი MFA ანგარიშით / only with prepared authorized access.
8. Code: C05 -> C06 -> C08 -> C10 -> C15 -> C12.
9. Tests, migrations, HTTPS and truthful remaining scope.

### ბოლო 15 წუთის სწავლა / Last 15 minutes of preparation

KA: ჯერ ისწავლე Q01, Q06, Q10, Q15-Q17, Q25, Q28-Q29, Q38-Q44 და Q60-Q66. შემდეგ გახსენი მთავარი რვა ფაილი და საკუთარი სიტყვებით გაიმეორე ერთი პასუხის სრული გზა.

EN: Prioritize the stack, state, controller/service separation, database/migrations, authentication, server grading, idempotency, rewards and deployment. Then rehearse the end-to-end answer flow with the actual files open.

KA: თუ ზუსტი დეტალი არ გახსოვს: „ზუსტ კონფიგურაციას ამ ფაილში გადავამოწმებ“. ეს სჯობს არარსებული ფუნქციის ან ტესტის გამოგონებას.

EN: If unsure, say “I will check the exact setting in this file,” then locate it. Do not invent a feature, result or security guarantee.
