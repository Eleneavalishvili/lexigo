using LanguageLearning.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace LanguageLearning.Tests.Storage;

public sealed class ObjectStorageOptionsTests
{
    [Theory]
    [InlineData("http://localhost:3900", "Development", true)]
    [InlineData("http://127.0.0.1:3900", "Development", true)]
    [InlineData("http://localhost:3900", "Production", false)]
    [InlineData("http://storage.example", "Development", false)]
    [InlineData("https://storage.example", "Production", true)]
    [InlineData("https://user:password@storage.example", "Production", false)]
    [InlineData("https://storage.example/private", "Production", false)]
    [InlineData("https://storage.example?key=hidden", "Production", false)]
    [InlineData("https://storage.example#fragment", "Production", false)]
    public void TransportRequiresCleanOriginAndProductionTls(string endpoint, string environment, bool valid)
    {
        var result = new ObjectStorageOptionsValidator(new TestEnvironment { EnvironmentName = environment }).Validate(null,
            new ObjectStorageOptions
            {
                Enabled = true,
                ServiceUrl = endpoint,
                Bucket = "lexigo-media",
                Region = "garage",
                AccessKey = "synthetic-access-key",
                SecretKey = "synthetic-secret-key"
            });
        Assert.Equal(valid, result.Succeeded);
        if (!valid) Assert.DoesNotContain("synthetic-secret-key", result.FailureMessage ?? "");
    }

    [Fact]
    public void DisabledStorageDoesNotRequireCredentialsOrAllowClientCreation()
    {
        var options = new ObjectStorageOptions();
        Assert.True(new ObjectStorageOptionsValidator(new TestEnvironment()).Validate(null, options).Succeeded);
        Assert.Throws<InvalidOperationException>(() => StorageRegistration.CreateClient(options));
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "LexiGo.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
