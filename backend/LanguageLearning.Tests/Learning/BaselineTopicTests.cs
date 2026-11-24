using LanguageLearning.Core.Content;
using LanguageLearning.Infrastructure.Content;
using LanguageLearning.Infrastructure.Data;
using LanguageLearning.Tests.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageLearning.Tests.Learning;

public sealed class BaselineTopicTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task NewBeginnerUnitInsertsBeforeEverydayMaterialAndPreservesExistingUnits()
    {
        using var scope = fixture.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LexiGoDbContext>();
        var packs = new[] { "es", "it", "de", "ru" }.Select(StarterLessonInstaller.Read).ToArray();
        var originalUnits = new Dictionary<Guid, int>();
        var staffUnits = new Dictionary<Guid, Guid>();
        foreach (var pack in packs)
        {
            var keys = pack.Lessons.Where(item => item.UnitKey != "dates-work-study")
                .Select(item => item.UnitKey).Distinct().ToArray();
            for (int index = 0; index < keys.Length; index++)
            {
                string? key = keys[index];
                Guid id = StarterLessonInstaller.ContentId(pack.Code + (key is null ? "/unit" : "/unit/" + key));
                database.Units.Add(new Unit
                {
                    Id = id,
                    CourseId = pack.CourseId,
                    Title = "Existing " + (key ?? "basics"),
                    Description = "Existing description",
                    OrderNumber = index + 1,
                    Difficulty = "A1"
                });
                originalUnits.Add(id, index + 1);
            }
            var staffUnit = new Unit
            {
                CourseId = pack.CourseId,
                Title = "Staff-created extension",
                Description = "Keep this material",
                OrderNumber = keys.Length + 1,
                Difficulty = "B1"
            };
            database.Units.Add(staffUnit);
            staffUnits.Add(pack.CourseId, staffUnit.Id);
        }
        await database.SaveChangesAsync();
        var installer = scope.ServiceProvider.GetRequiredService<StarterLessonInstaller>();
        Assert.Equal(packs.Sum(pack => pack.Lessons.Length), await installer.InstallAsync(default));
        foreach (var pack in packs)
        {
            Guid newUnitId = StarterLessonInstaller.ContentId(pack.Code + "/unit/dates-work-study");
            var added = await database.Units.SingleAsync(item => item.Id == newUnitId);
            var next = await database.Units.SingleAsync(item => item.Id ==
                StarterLessonInstaller.ContentId(pack.Code + "/unit/everyday"));
            var grammar = await database.Units.SingleAsync(item => item.Id ==
                StarterLessonInstaller.ContentId(pack.Code + "/unit/grammar-foundations"));
            Assert.Equal(added.OrderNumber + 1, grammar.OrderNumber);
            Assert.True(grammar.OrderNumber < next.OrderNumber);
            foreach (var existing in await database.Units.Where(item => item.CourseId == pack.CourseId).ToArrayAsync())
            {
                if (!originalUnits.TryGetValue(existing.Id, out int oldOrder)) continue;
                Assert.Equal(oldOrder < added.OrderNumber ? oldOrder : oldOrder + 1, existing.OrderNumber);
                Assert.Equal("Existing description", existing.Description);
            }
            Assert.Equal("Staff-created extension", (await database.Units.FindAsync(staffUnits[pack.CourseId]))!.Title);
            var checkId = StarterLessonInstaller.ContentId(pack.Code + "/check-dates-work-study");
            Assert.Equal(2, await database.LessonPrerequisites.CountAsync(item => item.LessonId == checkId));
            Assert.Equal(3, await database.Lessons.CountAsync(item => item.UnitId == newUnitId));
        }
        var orderBefore = await database.Units.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.OrderNumber);
        Assert.Equal(0, await installer.InstallAsync(default));
        var orderAfter = await database.Units.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.OrderNumber);
        Assert.Equal(orderBefore.OrderBy(item => item.Key), orderAfter.OrderBy(item => item.Key));
    }
}
