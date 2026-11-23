using LanguageLearning.Tests.Accounts;

namespace LanguageLearning.Tests.Storage;

public sealed class MediaWorkflowFixture : IAsyncLifetime
{
    public RegistrationFixture Accounts { get; } = new();
    public StorageFixture Storage { get; } = new();

    public async Task InitializeAsync()
    {
        await Accounts.InitializeAsync();
        await Storage.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await Accounts.DisposeAsync();
        await Storage.DisposeAsync();
    }
}
