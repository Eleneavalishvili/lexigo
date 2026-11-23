namespace LanguageLearning.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public bool Enabled { get; set; }
    public bool CleanupEnabled { get; set; } = true;
    public string ServiceUrl { get; set; } = "";
    public string Region { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}
