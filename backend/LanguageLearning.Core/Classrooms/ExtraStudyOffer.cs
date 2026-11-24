namespace LanguageLearning.Core.Classrooms;

public sealed class ExtraStudyOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeacherId { get; set; }
    public Guid CourseId { get; set; }
    public bool Enabled { get; set; }
    public decimal PricePerLesson { get; set; }
    public string LevelFrom { get; set; } = "A1";
    public string LevelTo { get; set; } = "B2";
    public string Biography { get; set; } = "";
    public string PortraitKey { get; set; } = "default";
}
