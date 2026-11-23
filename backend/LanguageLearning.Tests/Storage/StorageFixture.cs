using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Tests.Storage;

public sealed class StorageFixture : IAsyncLifetime
{
    private readonly string accessKey = "GK" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    private readonly string secretKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private IContainer? container;
    public AmazonS3Client Client { get; private set; } = null!;
    public MediaObjectStore Store { get; private set; } = null!;
    public string Bucket => "lexigo-tests";
    public string Endpoint { get; private set; } = "";

    public async Task InitializeAsync()
    {
        const string configuration = """
            metadata_dir = "/tmp/meta"
            data_dir = "/tmp/data"
            db_engine = "sqlite"
            replication_factor = 1
            rpc_bind_addr = "127.0.0.1:3901"
            rpc_public_addr = "127.0.0.1:3901"
            [s3_api]
            s3_region = "garage"
            api_bind_addr = "[::]:3900"
            """;
        container = new ContainerBuilder("dxflrs/garage:v2.5.0@sha256:677e5b758e4425a325b811c425c026bf35a4bf42f7ddbaa773de3f4fd779a5a5")
            .WithCommand("/garage", "server", "--single-node", "--default-bucket")
            .WithEnvironment("GARAGE_RPC_SECRET", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant())
            .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", accessKey)
            .WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", secretKey)
            .WithEnvironment("GARAGE_DEFAULT_BUCKET", Bucket)
            .WithResourceMapping(Encoding.UTF8.GetBytes(configuration), "/etc/garage.toml")
            .WithPortBinding(3900, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("/garage", "status"))
            .Build();
        await container.StartAsync();
        Endpoint = $"http://{container.Hostname}:{container.GetMappedPublicPort(3900)}";
        var options = new ObjectStorageOptions
        {
            Enabled = true,
            ServiceUrl = Endpoint,
            Region = "garage",
            Bucket = Bucket,
            AccessKey = accessKey,
            SecretKey = secretKey
        };
        Client = StorageRegistration.CreateClient(options);
        Store = new MediaObjectStore(Client, Options.Create(options), TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (container is not null) await container.DisposeAsync();
    }
}
