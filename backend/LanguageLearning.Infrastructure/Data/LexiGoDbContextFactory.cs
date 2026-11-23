using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LanguageLearning.Infrastructure.Data;

public sealed class LexiGoDbContextFactory : IDesignTimeDbContextFactory<LexiGoDbContext>
{
    public LexiGoDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("LEXIGO_MIGRATION_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=lexigo;Username=lexigo_dev";
        return new LexiGoDbContext(new DbContextOptionsBuilder<LexiGoDbContext>().UseNpgsql(connection).Options);
    }
}
