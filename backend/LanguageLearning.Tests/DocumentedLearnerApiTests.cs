using System.Net;
using System.Text.Json;
using LanguageLearning.Tests.Accounts;

namespace LanguageLearning.Tests;

public sealed class DocumentedLearnerApiTests(RegistrationFixture fixture) : IClassFixture<RegistrationFixture>
{
    [Fact]
    public async Task LearnerRoutesPublishTypedSuccessResponsesAndRequireAuthentication()
    {
        var routes = new (HttpMethod Method, string Path, string Schema, bool Array)[]
        {
            (HttpMethod.Post, "/api/v1/courses/{id}/placement-sessions", "SessionView", false),
            (HttpMethod.Get, "/api/v1/me/mistakes", "MistakePage", false),
            (HttpMethod.Get, "/api/v1/dialogues", "DialogueListing", true),
            (HttpMethod.Post, "/api/v1/dialogues/{id}/sessions", "DialogueView", false),
            (HttpMethod.Get, "/api/v1/me/dialogue-sessions/{id}", "DialogueView", false),
            (HttpMethod.Post, "/api/v1/me/dialogue-sessions/{id}/choices", "DialogueView", false),
            (HttpMethod.Get, "/api/v1/me/subscription", "SubscriptionView", false),
            (HttpMethod.Get, "/api/v1/me/entitlements", "EntitlementView", false),
            (HttpMethod.Get, "/api/v1/me/test-cards/{id}/transactions", "BillingActivityPage", false),
            (HttpMethod.Get, "/api/v1/me/transactions", "BillingActivityPage", false),
            (HttpMethod.Get, "/api/v1/me", "AccountSummary", false),
            (HttpMethod.Patch, "/api/v1/me", "RenamedProfile", false),
            (HttpMethod.Get, "/api/v1/me/progress", "CourseProgress", false),
            (HttpMethod.Post, "/api/v1/lessons/{id}/sessions", "SessionView", false),
            (HttpMethod.Get, "/api/v1/me/learning-sessions/{id}", "SessionView", false),
            (HttpMethod.Post, "/api/v1/me/learning-sessions/{id}/answers", "AnswerFeedback", false),
            (HttpMethod.Post, "/api/v1/me/learning-sessions/{id}/complete", "SessionView", false),
            (HttpMethod.Post, "/api/v1/me/practice-sessions", "SessionView", false),
            (HttpMethod.Get, "/api/v1/me/activity", "ActivityPage", false),
            (HttpMethod.Get, "/api/v1/me/completion-cards", "CompletionCardPage", false)
        };
        using var browser = fixture.CreateIsolatedClient();
        using var swagger = JsonDocument.Parse(await browser.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = swagger.RootElement.GetProperty("paths");

        foreach (var route in routes)
        {
            var operation = paths.GetProperty(route.Path).GetProperty(route.Method.Method.ToLowerInvariant());
            var schema = operation.GetProperty("responses").GetProperty("200").GetProperty("content")
                .GetProperty("application/json").GetProperty("schema");
            if (route.Array) schema = schema.GetProperty("items");
            Assert.Equal($"#/components/schemas/{route.Schema}", schema.GetProperty("$ref").GetString());

            string url = route.Path.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal);
            using var request = new HttpRequestMessage(route.Method, url);
            using var response = await browser.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
