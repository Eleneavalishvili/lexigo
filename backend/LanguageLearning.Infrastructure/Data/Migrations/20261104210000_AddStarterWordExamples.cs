using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LanguageLearning.Infrastructure.Data.Migrations;

[DbContext(typeof(LexiGoDbContext))]
[Migration("20261104210000_AddStarterWordExamples")]
public sealed class AddStarterWordExamples : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "VocabularyItems" AS word
            SET "Example" = starter.example
            FROM (VALUES
                ('15110000-0000-0000-0000-000000000001'::uuid, 'hola', '¡Hola! ¿Cómo estás?'),
                ('15110000-0000-0000-0000-000000000002'::uuid, 'dos', 'Tengo dos entradas.'),
                ('15110000-0000-0000-0000-000000000003'::uuid, 'la hermana', 'La hermana de Ana vive aquí.'),
                ('15110000-0000-0000-0000-000000000004'::uuid, 'el pan', 'El pan está en la mesa.'),
                ('15110000-0000-0000-0000-000000000005'::uuid, 'la estación', 'La estación está cerca.'),
                ('15110000-0000-0000-0000-000000000006'::uuid, 'la lluvia', 'La lluvia moja las calles.'),
                ('15110000-0000-0000-0000-000000000007'::uuid, 'ciao', 'Ciao, come stai?'),
                ('15110000-0000-0000-0000-000000000008'::uuid, 'due', 'Ho due biglietti.'),
                ('15110000-0000-0000-0000-000000000009'::uuid, 'la sorella', 'La sorella di Anna abita qui.'),
                ('15110000-0000-0000-0000-000000000010'::uuid, 'il pane', 'Il pane è sul tavolo.'),
                ('15110000-0000-0000-0000-000000000011'::uuid, 'la stazione', 'La stazione è vicina.'),
                ('15110000-0000-0000-0000-000000000012'::uuid, 'la pioggia', 'La pioggia bagna le strade.'),
                ('15110000-0000-0000-0000-000000000013'::uuid, 'Hallo', 'Hallo, wie geht es dir?'),
                ('15110000-0000-0000-0000-000000000014'::uuid, 'zwei', 'Ich habe zwei Fahrkarten.'),
                ('15110000-0000-0000-0000-000000000015'::uuid, 'die Schwester', 'Die Schwester von Anna wohnt hier.'),
                ('15110000-0000-0000-0000-000000000016'::uuid, 'das Brot', 'Das Brot liegt auf dem Tisch.'),
                ('15110000-0000-0000-0000-000000000017'::uuid, 'der Bahnhof', 'Der Bahnhof ist ganz in der Nähe.'),
                ('15110000-0000-0000-0000-000000000018'::uuid, 'der Regen', 'Der Regen macht die Straßen nass.'),
                ('15110000-0000-0000-0000-000000000019'::uuid, 'привет', 'Привет! Как дела?'),
                ('15110000-0000-0000-0000-000000000020'::uuid, 'два', 'У меня два билета.'),
                ('15110000-0000-0000-0000-000000000021'::uuid, 'сестра', 'Моя сестра живёт здесь.'),
                ('15110000-0000-0000-0000-000000000022'::uuid, 'хлеб', 'Хлеб лежит на столе.'),
                ('15110000-0000-0000-0000-000000000023'::uuid, 'вокзал', 'Вокзал находится рядом.'),
                ('15110000-0000-0000-0000-000000000024'::uuid, 'дождь', 'На улице идёт дождь.')
            ) AS starter(id, spelling, example)
            WHERE word."Id" = starter.id AND word."Word" = starter.spelling
                AND word."Example" = ''
                AND NOT EXISTS (
                    SELECT 1 FROM "VocabularyRevisions" AS revision
                    WHERE revision."WordId" = word."Id" AND revision."Status" = 'Published'
                );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Example sentences may have been adopted or revised by staff since the backfill.
    }
}
