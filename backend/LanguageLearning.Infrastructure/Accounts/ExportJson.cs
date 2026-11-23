using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.Infrastructure.Accounts;

internal static class ExportJson
{
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);

    public static Task StartAsync(Stream destination, DateTimeOffset exportedAt, CancellationToken cancellationToken) =>
        WriteValueAsync(destination, "{\"schemaVersion\":1,\"exportedAt\":"u8.ToArray(), exportedAt, cancellationToken);

    public static Task WriteSectionAsync<T>(Stream destination, byte[] prefix, IQueryable<T> query,
        CancellationToken cancellationToken) =>
        WriteValueAsync(destination, prefix, query.AsAsyncEnumerable(), cancellationToken);

    public static async Task WriteValueAsync<T>(Stream destination, byte[] prefix, T value,
        CancellationToken cancellationToken)
    {
        await destination.WriteAsync(prefix, cancellationToken);
        await JsonSerializer.SerializeAsync(destination, value, Format, cancellationToken);
    }
}
