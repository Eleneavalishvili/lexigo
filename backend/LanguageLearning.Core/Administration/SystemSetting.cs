namespace LanguageLearning.Core.Administration;

public sealed class SystemSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
