using System.Text.Json;
using System.Text.Json.Serialization;
using LanguageLearning.Core.Content;

namespace LanguageLearning.Infrastructure.Content;

public static class TextEvaluationRulesCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static string Write(TextEvaluationRules rules) => JsonSerializer.Serialize(rules, Json);
    public static TextEvaluationRules? Read(string value)
    {
        try { return JsonSerializer.Deserialize<TextEvaluationRules>(value, Json); }
        catch (JsonException) { return null; }
    }
}
