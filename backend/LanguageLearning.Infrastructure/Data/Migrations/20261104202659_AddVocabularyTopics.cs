using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageLearning.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularyTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "VocabularyItems",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "VocabularyItems" ("Id", "CourseId", "Word", "EnglishTranslation", "Example", "PartOfSpeech", "Topic")
                VALUES
                ('15110000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'hola', 'hello', '', 'interjection', 'greetings'),
                ('15110000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'dos', 'two', '', 'numeral', 'numbers'),
                ('15110000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', 'la hermana', 'sister', '', 'noun', 'family'),
                ('15110000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000001', 'el pan', 'bread', '', 'noun', 'food'),
                ('15110000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000001', 'la estación', 'station', '', 'noun', 'travel'),
                ('15110000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000001', 'la lluvia', 'rain', '', 'noun', 'routines'),
                ('15110000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000002', 'ciao', 'hello', '', 'interjection', 'greetings'),
                ('15110000-0000-0000-0000-000000000008', '10000000-0000-0000-0000-000000000002', 'due', 'two', '', 'numeral', 'numbers'),
                ('15110000-0000-0000-0000-000000000009', '10000000-0000-0000-0000-000000000002', 'la sorella', 'sister', '', 'noun', 'family'),
                ('15110000-0000-0000-0000-000000000010', '10000000-0000-0000-0000-000000000002', 'il pane', 'bread', '', 'noun', 'food'),
                ('15110000-0000-0000-0000-000000000011', '10000000-0000-0000-0000-000000000002', 'la stazione', 'station', '', 'noun', 'travel'),
                ('15110000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000002', 'la pioggia', 'rain', '', 'noun', 'routines'),
                ('15110000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000003', 'Hallo', 'hello', '', 'interjection', 'greetings'),
                ('15110000-0000-0000-0000-000000000014', '10000000-0000-0000-0000-000000000003', 'zwei', 'two', '', 'numeral', 'numbers'),
                ('15110000-0000-0000-0000-000000000015', '10000000-0000-0000-0000-000000000003', 'die Schwester', 'sister', '', 'noun', 'family'),
                ('15110000-0000-0000-0000-000000000016', '10000000-0000-0000-0000-000000000003', 'das Brot', 'bread', '', 'noun', 'food'),
                ('15110000-0000-0000-0000-000000000017', '10000000-0000-0000-0000-000000000003', 'der Bahnhof', 'train station', '', 'noun', 'travel'),
                ('15110000-0000-0000-0000-000000000018', '10000000-0000-0000-0000-000000000003', 'der Regen', 'rain', '', 'noun', 'routines'),
                ('15110000-0000-0000-0000-000000000019', '10000000-0000-0000-0000-000000000004', 'привет', 'hello', '', 'interjection', 'greetings'),
                ('15110000-0000-0000-0000-000000000020', '10000000-0000-0000-0000-000000000004', 'два', 'two', '', 'numeral', 'numbers'),
                ('15110000-0000-0000-0000-000000000021', '10000000-0000-0000-0000-000000000004', 'сестра', 'sister', '', 'noun', 'family'),
                ('15110000-0000-0000-0000-000000000022', '10000000-0000-0000-0000-000000000004', 'хлеб', 'bread', '', 'noun', 'food'),
                ('15110000-0000-0000-0000-000000000023', '10000000-0000-0000-0000-000000000004', 'вокзал', 'train station', '', 'noun', 'travel'),
                ('15110000-0000-0000-0000-000000000024', '10000000-0000-0000-0000-000000000004', 'дождь', 'rain', '', 'noun', 'routines')
                ON CONFLICT ("Id") DO UPDATE SET "Topic" = EXCLUDED."Topic";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep starter rows so rolling back cannot remove personal saves.
            migrationBuilder.DropColumn(
                name: "Topic",
                table: "VocabularyItems");
        }
    }
}
