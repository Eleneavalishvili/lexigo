using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Storage;

public static class StorageRegistration
{
    public static IServiceCollection AddPrivateObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ObjectStorageOptions>().Bind(configuration.GetSection("ObjectStorage")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ObjectStorageOptions>, ObjectStorageOptionsValidator>();
        services.AddSingleton<IAmazonS3>(provider => CreateClient(provider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value));
        services.AddSingleton<MediaObjectStore>();
        services.AddSingleton(provider => new Lazy<MediaObjectStore>(provider.GetRequiredService<MediaObjectStore>));
        services.AddScoped<MediaCleanupService>();
        services.AddHostedService<MediaCleanupWorker>();
        return services;
    }

    public static AmazonS3Client CreateClient(ObjectStorageOptions options)
    {
        if (!options.Enabled) throw new InvalidOperationException("Private object storage is disabled.");
        return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            AuthenticationRegion = options.Region,
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
    }
}
