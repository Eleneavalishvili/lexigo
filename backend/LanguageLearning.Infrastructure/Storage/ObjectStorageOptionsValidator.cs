using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Storage;

public sealed partial class ObjectStorageOptionsValidator(IHostEnvironment environment) : IValidateOptions<ObjectStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, ObjectStorageOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        var errors = new List<string>();
        if (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("https" or "http") || endpoint.AbsolutePath != "/"
            || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
        {
            errors.Add("Configure an object storage origin without credentials, paths, queries or fragments.");
        }
        else if (endpoint.Scheme != "https" && !(environment.IsDevelopment() && endpoint.IsLoopback))
        {
            errors.Add("Object storage requires HTTPS; only Development loopback may use HTTP.");
        }

        if (!BucketName().IsMatch(options.Bucket) || options.Bucket.Contains("..", StringComparison.Ordinal)
            || System.Net.IPAddress.TryParse(options.Bucket, out _))
            errors.Add("Configure a valid private storage bucket name.");
        if (!RegionName().IsMatch(options.Region)) errors.Add("Configure a valid storage region.");
        if (!ValidCredential(options.AccessKey) || !ValidCredential(options.SecretKey))
            errors.Add("Configure storage credentials through local secrets or the deployment secret store.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool ValidCredential(string value) => value.Length is >= 16 and <= 256 && !value.Any(char.IsWhiteSpace);

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex BucketName();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex RegionName();
}
