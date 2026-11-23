using Microsoft.VisualBasic.FileIO;

namespace LanguageLearning.Infrastructure.Content;

public sealed record ImportIssue(long Row, string Message);
internal sealed record ImportedExercise(string Prompt, string Answer, string Feedback);

internal static class LessonImportCsv
{
    public const int MaximumRows = 50;
    public const string Header = "prompt,answer,feedback";

    public static (ImportedExercise[] Exercises, ImportIssue[] Errors) Parse(string csv)
    {
        using var parser = new TextFieldParser(new StringReader(csv.TrimStart('\uFEFF')));
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        var exercises = new List<ImportedExercise>();
        var errors = new List<ImportIssue>();
        try
        {
            if (parser.ReadFields() is not { } header || !header.SequenceEqual(Header.Split(',')))
                return ([], [new(1, "Use the template header: prompt,answer,feedback.")]);

            int rows = 0;
            while (!parser.EndOfData)
            {
                long line = parser.LineNumber;
                if (++rows > MaximumRows)
                {
                    errors.Add(new(line, $"Use at most {MaximumRows} exercise rows."));
                    break;
                }

                var fields = parser.ReadFields();
                if (fields is not { Length: 3 } || !Valid(fields[0], 2000)
                    || !Valid(fields[1], 300) || !Valid(fields[2], 2000))
                {
                    errors.Add(new(line, "Enter a prompt, one accepted answer and feedback within the template limits."));
                    continue;
                }

                exercises.Add(new(fields[0], fields[1], fields[2]));
            }
        }
        catch (MalformedLineException error)
        {
            errors.Add(new(error.LineNumber, "Check the CSV quotes and column separators."));
        }

        if (exercises.Count == 0 && errors.Count == 0) errors.Add(new(2, "Add at least one exercise row."));
        return ([.. exercises], [.. errors]);
    }

    internal static bool Valid(string? value, int limit) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Contains('\0');
}
