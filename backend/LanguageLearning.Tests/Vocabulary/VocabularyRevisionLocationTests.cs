using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LanguageLearning.Api.Vocabulary;
using LanguageLearning.Core.Accounts;
using LanguageLearning.Infrastructure.Vocabulary;

namespace LanguageLearning.Tests.Vocabulary;

public sealed partial class VocabularyRevisionTests
{
    [Fact]
    public async Task VocabularyCreationLinksToTheExactDraftWithoutExposingOtherAuthorsOrLanguages()
    {
        using var writer = Browser();
        string author = await StaffAsync(writer, 2, RoleNames.ContentAuthor);
        using var outsider = Browser();
        string other = await StaffAsync(outsider, 2, RoleNames.ContentAuthor);
        using var reader = Browser();
        string reviewer = await StaffAsync(reader, 2, RoleNames.Reviewer);
        using var foreign = Browser();
        string italian = await StaffAsync(foreign, 3, RoleNames.Reviewer);
        var text = new VocabularyText("prueba-" + Guid.NewGuid().ToString("N"), "test phrase",
            "A synthetic dictionary example.", "noun", "greetings");
        using var created = await writer.SendAsync(HttpMethod.Post, Path, new CreateVocabularyRevision(Course, null, text), author);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid id = body.RootElement.GetProperty("id").GetGuid();
        string route = $"{Path}/{id}";
        Assert.Equal(route, created.Headers.Location!.AbsolutePath);
        using var detail = await writer.SendAsync(HttpMethod.Get, route, bearer: author);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains("no-store", detail.Headers.CacheControl!.ToString());
        var draft = (await detail.Content.ReadFromJsonAsync<VocabularyRevisionView>())!;
        Assert.Equal(id, draft.Id);
        Assert.Equal(text.Word, draft.Text.Word);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(HttpMethod.Get, route, bearer: other)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.SendAsync(HttpMethod.Get, route, bearer: reviewer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await writer.SendAsync(HttpMethod.Put, route,
            new ChangeVocabularyRevision(draft.RevisionToken, "submit", null, null), author)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.SendAsync(HttpMethod.Get, route, bearer: reviewer)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.SendAsync(HttpMethod.Get, route, bearer: italian)).StatusCode);
    }
}
