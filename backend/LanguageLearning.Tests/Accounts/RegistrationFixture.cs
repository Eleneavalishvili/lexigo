using System.Text.RegularExpressions;
using LanguageLearning.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.AspNetCore.TestHost;
using LanguageLearning.Infrastructure.Speech;

namespace LanguageLearning.Tests.Accounts;

public sealed class RegistrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17").Build();
    private AccountApplication? application;
    private readonly List<WebApplicationFactory<Program>> additionalApplications = [];
    private WebApplicationFactory<Program>? mediaApplication;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => application!.Services;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        application = new AccountApplication(database.GetConnectionString());
        Client = application.CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        await context.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<RoleSeeder>().SeedAsync();
    }

    public async Task<(Guid UserId, string Token)> ConfirmationAsync(string email)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var user = await context.Users.SingleAsync(user => user.Email == email);
        var message = await context.EmailOutboxMessages.SingleAsync(message => message.UserId == user.Id);
        return (user.Id, Regex.Match(message.HtmlBody, "token=([A-Za-z0-9_-]+)").Groups[1].Value);
    }

    public HttpClient CreateIsolatedClient() => application!.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public HttpClient CreateDiagnosticsClient<TController>(Microsoft.Extensions.Logging.ILoggerProvider logs)
    {
        var diagnostics = application!.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(logs);
            services.AddControllers().AddApplicationPart(typeof(TController).Assembly);
        }));
        additionalApplications.Add(diagnostics);
        return diagnostics.CreateClient();
    }

    public HttpClient CreateBillingClient(TimeProvider clock)
    {
        var billing = application!.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton(clock)));
        additionalApplications.Add(billing);
        return billing.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    public HttpClient CreateRenewalClient(TimeProvider clock, bool poll = false)
    {
        var renewal = application!.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(clock);
            services.Configure<LanguageLearning.Infrastructure.Billing.BillingRenewalOptions>(value => value.Enabled = true);
            if (!poll)
                foreach (var descriptor in services.Where(item => item.ImplementationType == typeof(LanguageLearning.Infrastructure.Billing.SubscriptionRenewalWorker)).ToArray())
                    services.Remove(descriptor);
        }));
        additionalApplications.Add(renewal);
        return renewal.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    public HttpClient CreateSpeechClient(ISpeechRecognizer recognizer)
    {
        var speech = application!.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(recognizer)));
        additionalApplications.Add(speech);
        return speech.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    public HttpClient CreateMediaClient(MediaObjectStore store)
    {
        if (mediaApplication is null)
        {
            mediaApplication = application!.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ObjectStorage:Enabled"] = "true",
                    ["ObjectStorage:CleanupEnabled"] = "false",
                    ["MediaUploadRateLimit"] = "1000",
                    ["ObjectStorage:ServiceUrl"] = "http://localhost:3900",
                    ["ObjectStorage:Region"] = "garage",
                    ["ObjectStorage:Bucket"] = "lexigo-tests",
                    ["ObjectStorage:AccessKey"] = "synthetic-config-key",
                    ["ObjectStorage:SecretKey"] = "synthetic-config-secret"
                }));
                builder.ConfigureTestServices(services => services.AddSingleton(store));
            });
            additionalApplications.Add(mediaApplication);
        }
        return mediaApplication.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    public HttpClient CreateRateLimitedClient(string setting = "AccountRateLimit")
    {
        var limited = application!.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { [setting] = "1" })));
        additionalApplications.Add(limited);
        return limited.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        foreach (var additional in additionalApplications) await additional.DisposeAsync();
        if (application is not null) await application.DisposeAsync();
        await database.DisposeAsync();
    }

    private sealed class AccountApplication(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var contentRoot = Environment.GetEnvironmentVariable("LEXIGO_TEST_API_ROOT")
                ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LanguageLearning.Api");
            builder.UseContentRoot(Path.GetFullPath(contentRoot));
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connection,
                    ["AccountEmail:WebOrigin"] = "http://localhost:4200",
                    ["Smtp:DeliveryEnabled"] = "false",
                    ["BillingRenewals:Enabled"] = "false",
                    ["Challenges:AutomaticWeekly"] = "false",
                    ["ObjectStorage:Enabled"] = "false",
                    ["ObjectStorage:CleanupEnabled"] = "false",
                    ["AccountRateLimit"] = "1000",
                    ["SessionRefreshRateLimit"] = "1000",
                    ["ContentWriteRateLimit"] = "1000",
                    ["ContentReadRateLimit"] = "1000",
                    ["LearningRateLimit"] = "10000",
                    ["SpeechCheckRateLimit"] = "10000"
                }));
        }
    }
}

