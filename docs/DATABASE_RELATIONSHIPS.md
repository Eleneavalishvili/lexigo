# მონაცემთა ბაზის ძირითადი კავშირები

დიაგრამები აჩვენებს კურსის გამოქვეყნების, სასწავლო პროგრესის, კლასებისა და
სიმულირებული გადახდების ძირითად კავშირებს. ცხრილების სრული აღწერა და ყველა
შეზღუდვა მოცემულია [SQL სქემაში](../database/schema.sql).

## კურსი, გაკვეთილი და სესია

```mermaid
erDiagram
    Languages ||--o{ Courses : source
    Languages ||--o{ Courses : target
    Courses ||--o{ Units : contains
    Units ||--o{ Lessons : contains
    Lessons ||--o{ LessonVersions : revisions
    Lessons ||--o{ LessonPrerequisites : dependent
    Lessons ||--o{ LessonPrerequisites : prerequisite
    LessonVersions ||--o{ Exercises : contains
    Users ||--o{ Enrollments : joins
    Courses ||--o{ Enrollments : offers
    Enrollments ||--o{ LearningSessions : owns
    LearningSessions ||--o{ SessionExercises : snapshots
    SessionExercises ||--o{ AnswerAttempts : receives
```

გაკვეთილის აქტიური ვერსია იმავე გაკვეთილს ეკუთვნის. სესიის სავარჯიშო
ინახავს დაწყებისას გამოყენებულ შინაარსს, ამიტომ ახალი ვერსიის გამოქვეყნება
უკვე დაწყებული სესიის შეფასების წესებს არ ცვლის.

## ლექსიკა და კლასი

```mermaid
erDiagram
    Courses ||--o{ VocabularyItems : includes
    VocabularyItems ||--o{ SavedWords : saved_as
    Users ||--o{ SavedWords : owns
    SavedWords ||--o{ WordReviews : reviews
    Courses ||--o{ Classrooms : supports
    Classrooms ||--o{ ClassMemberships : admits
    Users ||--o{ ClassMemberships : joins
    Classrooms ||--o{ Assignments : sets
    Assignments ||--o{ AssignmentSubmissions : receives
    LearningSessions ||--o{ AssignmentSubmissions : records
```

## სიმულირებული გადახდები

```mermaid
erDiagram
    Users ||--o{ TestCards : owns
    TestCards ||--o{ CardTransactions : records
    SubscriptionPlans ||--o{ PlanPrices : prices
    Users ||--o{ Subscriptions : owns
    PlanPrices ||--o{ Subscriptions : current_price
    Users ||--o{ BillingOrders : places
    PlanPrices ||--o{ BillingOrders : priced_by
    BillingOrders ||--o{ PaymentAttempts : attempts
    TestCards ||--o{ PaymentAttempts : funds
    PaymentAttempts ||--o| PaymentReceipts : receipt
    PaymentAttempts ||--o{ Refunds : refunds
```

გადახდის მცდელობის შედგენილი უცხო გასაღებები აერთიანებს მომხმარებლის
იდენტიფიკატორს შეკვეთასთან და სატესტო ბარათთან. ამ ცხრილებში რეალური
საბანკო ბარათის ნომრები და CVV არ ინახება.
