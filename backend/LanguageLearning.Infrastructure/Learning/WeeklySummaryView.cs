namespace LanguageLearning.Infrastructure.Learning;

public sealed record WeeklyCourseSummary(Guid CourseId, string Title, string LanguageCode, bool IsPublished,
    long EarnedXp, int NewLessonsCompleted, int ReviewedWords, int GradedAnswers, int CorrectAnswers, int DueWords,
    int ConversationsFinished)
{
    public int? Accuracy => GradedAnswers == 0 ? null : (int)((long)CorrectAnswers * 100 / GradedAnswers);
}

public sealed record WeeklySummaryDay(DateOnly Date, long EarnedXp, bool Studied);

public sealed record WeeklySummaryView(DateOnly WeekStart, DateOnly WeekEnd, string TimeZone, DateOnly Today,
    int WeeksAgo, WeeklySummaryDay[] Days, WeeklyCourseSummary[] Courses)
{
    public long EarnedXp => Courses.Sum(item => item.EarnedXp);
    public int NewLessonsCompleted => Courses.Sum(item => item.NewLessonsCompleted);
    public int ReviewedWords => Courses.Sum(item => item.ReviewedWords);
    public int ConversationsFinished => Courses.Sum(item => item.ConversationsFinished);
    public int StudyDays => Days.Count(item => item.Studied);
    public int GradedAnswers => Courses.Sum(item => item.GradedAnswers);
    public int CorrectAnswers => Courses.Sum(item => item.CorrectAnswers);
    public int? Accuracy => GradedAnswers == 0 ? null : (int)((long)CorrectAnswers * 100 / GradedAnswers);
}
