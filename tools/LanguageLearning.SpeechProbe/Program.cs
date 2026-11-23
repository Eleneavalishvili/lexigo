using System.Diagnostics;
using System.Text.Json;
using LanguageLearning.Infrastructure.Learning;
using LanguageLearning.Infrastructure.Speech;
using LanguageLearning.Infrastructure.Storage;
using Microsoft.Extensions.Options;

if (args.Length != 2 || !Path.IsPathFullyQualified(args[0]) || !Path.IsPathFullyQualified(args[1]))
{
    Console.Error.WriteLine("Usage: SpeechProbe <absolute-model-directory> <absolute-synthetic-fixtures-directory>");
    return 2;
}
using var engine = new LocalSpeechRecognizer(Options.Create(new LocalSpeechOptions { Enabled = true, ModelsDirectory = args[0] }));
var cases = new Dictionary<string, string>
{
    ["es"] = "hola buenos días",
    ["it"] = "ciao buongiorno",
    ["de"] = "guten morgen",
    ["ru"] = "привет доброе утро"
};
string caseManifest = Path.Combine(args[1], "cases.json");
if (File.Exists(caseManifest))
{
    cases = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(caseManifest))
        ?? throw new InvalidDataException("Test case manifest is missing.");
}
bool allMatched = true;
foreach (var (language, expected) in cases)
{
    await using var file = File.OpenRead(Path.Combine(args[1], language + "-16k.wav"));
    var audio = await WaveAudioInspector.ReadAsync(file);
    var elapsed = Stopwatch.StartNew();
    var result = await engine.RecognizeAsync(language, audio, default);
    bool matches = TextAnswerEvaluator.Matches(result.Text, expected, true, true);
    bool rejectsUnrelated = !TextAnswerEvaluator.Matches(result.Text, "unrelated phrase 7391", true, true);
    allMatched &= result.Status == SpeechRecognitionStatus.Recognized && matches && rejectsUnrelated;
    // Only explicitly supplied test fixtures are printed; never use this utility for learner logs.
    Console.WriteLine(JsonSerializer.Serialize(new { language, status = result.Status.ToString(), testFixtureTranscript = result.Text, matches, rejectsUnrelated, elapsedMilliseconds = elapsed.ElapsedMilliseconds }));
    foreach (string kind in new[] { "wrong", "noise" })
    {
        string negativePath = Path.Combine(args[1], language + "-" + kind + "-16k.wav");
        if (!File.Exists(negativePath)) continue;
        await using var negativeFile = File.OpenRead(negativePath);
        var negative = await engine.RecognizeAsync(language, await WaveAudioInspector.ReadAsync(negativeFile), default);
        bool rejected = negative.Status != SpeechRecognitionStatus.Recognized
            || !TextAnswerEvaluator.Matches(negative.Text, expected, true, true);
        allMatched &= rejected;
        Console.WriteLine(JsonSerializer.Serialize(new { language, kind, testFixtureTranscript = negative.Text, rejected }));
    }
}
return allMatched ? 0 : 1;
