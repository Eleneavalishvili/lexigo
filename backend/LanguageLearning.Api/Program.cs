using LanguageLearning.Api.Authentication;
using LanguageLearning.Api.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LanguageLearning.Api.Health;
using LanguageLearning.Infrastructure;
using LanguageLearning.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var trustedProxy = TrustedProxy.Configure(builder.Configuration["ReverseProxy:Address"]);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
if (builder.Environment.IsDevelopment())
{
    var keyDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".local", "data-protection"));
    keyDirectory.Create();
    builder.Services.AddDataProtection().SetApplicationName("LexiGo").PersistKeysToFileSystem(keyDirectory);
}
else if (builder.Configuration["DataProtection:KeyDirectory"] is { Length: > 0 } productionKeyPath)
{
    if (!Path.IsPathFullyQualified(productionKeyPath))
        throw new InvalidOperationException("DataProtection:KeyDirectory must be an absolute path.");
    var keyDirectory = Directory.CreateDirectory(productionKeyPath);
    builder.Services.AddDataProtection().SetApplicationName("LexiGo").PersistKeysToFileSystem(keyDirectory);
}
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAccountAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddScoped<RefreshCookie>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("message-send", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("MessageRateLimit", 30),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("speech-check", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = builder.Configuration.GetValue("SpeechCheckRateLimit", 12), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("media-upload", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        { PermitLimit = builder.Configuration.GetValue("MediaUploadRateLimit", 10), Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.AddPolicy("learning", context =>
    {
        string partition = context.User.Identity is { IsAuthenticated: true, Name: { } userId }
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("LearningRateLimit", 180),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
    options.AddPolicy("content-read", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = builder.Configuration.GetValue("ContentReadRateLimit", 120), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("content-write", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = builder.Configuration.GetValue("ContentWriteRateLimit", 60), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (rejection, _) =>
    {
        var context = rejection.HttpContext;
        if (rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan delay))
        {
            context.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests. Try again shortly.",
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
    };
    options.AddPolicy("session-refresh", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Environment.IsDevelopment() ? builder.Configuration.GetValue("SessionRefreshRateLimit", 120) : 120,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        }));
    options.AddPolicy("account-entry", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("AccountRateLimit", 10),
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        }));
    options.AddPolicy("account-settings", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("AccountSettingsRateLimit", 30),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

if (args.Contains("--extend-presentation-tutors"))
{
    var request = await System.Text.Json.JsonSerializer.DeserializeAsync<PresentationAccountRequest>(Console.OpenStandardInput());
    if (request is null || string.IsNullOrWhiteSpace(request.PublicKeyPem))
        throw new InvalidOperationException("Provide the operator's public encryption key on standard input.");
    await using var scope = app.Services.CreateAsyncScope();
    var envelope = await scope.ServiceProvider.GetRequiredService<LanguageLearning.Infrastructure.Accounts.PresentationAccountProvisioner>()
        .ExtendTutorsAsync(request.PublicKeyPem, CancellationToken.None);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(envelope));
    return;
}

if (args.Contains("--create-presentation-accounts"))
{
    var request = await System.Text.Json.JsonSerializer.DeserializeAsync<PresentationAccountRequest>(Console.OpenStandardInput());
    if (request is null || string.IsNullOrWhiteSpace(request.PublicKeyPem))
        throw new InvalidOperationException("Provide the operator's public encryption key on standard input.");
    await using var scope = app.Services.CreateAsyncScope();
    var envelope = await scope.ServiceProvider.GetRequiredService<LanguageLearning.Infrastructure.Accounts.PresentationAccountProvisioner>()
        .CreateAsync(request.PublicKeyPem, CancellationToken.None);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(envelope));
    return;
}

if (args.Contains("--install-bundled-content"))
{
    // Operator-only maintenance command: no HTTP listener or background workers
    // are started, and schema migrations remain a separate reviewed release step.
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
    if ((await database.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Apply the reviewed database migration before installing bundled content.");
    int lessons = await scope.ServiceProvider.GetRequiredService<LanguageLearning.Infrastructure.Content.StarterLessonInstaller>()
        .InstallAsync(CancellationToken.None);
    int words = await scope.ServiceProvider.GetRequiredService<LanguageLearning.Infrastructure.Vocabulary.StarterVocabularyInstaller>()
        .InstallAsync(CancellationToken.None);
    Console.WriteLine($"Installed {lessons} bundled lessons and {words} vocabulary entries. Existing versions and progress were preserved.");
    return;
}

if (args.Contains("--initialize-database"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Automatic local initialization is restricted to Development.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<LexiGoDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<RoleSeeder>().SeedAsync();
    return;
}

if (args.Contains("--install-starter-lessons"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Local content installation requires Development.");
    await using var scope = app.Services.CreateAsyncScope();
    int count = await scope.ServiceProvider.GetRequiredService<LanguageLearning.Infrastructure.Content.StarterLessonInstaller>()
        .InstallAsync(CancellationToken.None);
    Console.WriteLine($"Installed {count} built-in starter lessons. Existing lessons and learner progress were preserved.");
    return;
}

if (args.Contains("--install-starter-vocabulary"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Local vocabulary installation requires Development.");
    await using var scope = app.Services.CreateAsyncScope();
    int count = await scope.ServiceProvider.GetRequiredService<LanguageLearning.Infrastructure.Vocabulary.StarterVocabularyInstaller>()
        .InstallAsync(CancellationToken.None);
    Console.WriteLine($"Installed {count} built-in vocabulary entries. Existing words and saved progress were preserved.");
    return;
}

if (args.Contains("--install-starter-dialogues"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Local dialogue installation requires Development.");
    await using var scope = app.Services.CreateAsyncScope();
    int count = await scope.ServiceProvider.GetRequiredService<LanguageLearning.Infrastructure.Content.StarterDialogueInstaller>()
        .InstallAsync(CancellationToken.None);
    Console.WriteLine($"Installed {count} built-in conversations.");
    return;
}

if (args.Contains("--bootstrap-super-admin"))
{
    // Secrets arrive on stdin; command arguments and persistent configuration contain no proof.
    var proof = await System.Text.Json.JsonSerializer.DeserializeAsync<BootstrapProof>(Console.OpenStandardInput());
    await using var scope = app.Services.CreateAsyncScope();
    bool success = proof is not null && await scope.ServiceProvider
        .GetRequiredService<LanguageLearning.Infrastructure.Accounts.SuperAdminBootstrapService>()
        .PromoteAsync(proof.Email ?? "", proof.Password ?? "", proof.Code ?? "", CancellationToken.None);
    Console.WriteLine(success ? "Initial administrator provisioned. Sign in again using two-factor authentication."
        : "Provisioning refused. Check account eligibility, proof and whether an administrator already exists.");
    Environment.ExitCode = success ? 0 : 1;
    return;
}

if (trustedProxy is not null) app.UseForwardedHeaders(trustedProxy);
app.UseRouting();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.TryAdd("Cache-Control", "no-store");
        return Task.CompletedTask;
    });
    await next(context);
});
app.UseMiddleware<RequestSummaryMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(async statusPage =>
{
    var context = statusPage.HttpContext;
    int status = context.Response.StatusCode;
    if (!context.Request.Path.StartsWithSegments("/api/v1") || status is not (400 or 401 or 403 or 404 or 409))
        return;

    await Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
        .ExecuteAsync(context);
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.Run();

public partial class Program;
internal sealed record BootstrapProof(string? Email, string? Password, string? Code);
internal sealed record PresentationAccountRequest(string? PublicKeyPem);
