-- Run after schema.sql. Restores missing baseline rows without changing existing content.
-- Lesson installation is separate so it can use versioning and the publishing workflow.
BEGIN;
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('20000000-0000-0000-0000-000000000001', 'Learner', 'LEARNER', '00000001') ON CONFLICT ("NormalizedName") DO NOTHING;
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('20000000-0000-0000-0000-000000000002', 'Teacher', 'TEACHER', '00000002') ON CONFLICT ("NormalizedName") DO NOTHING;
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('20000000-0000-0000-0000-000000000003', 'ContentAuthor', 'CONTENTAUTHOR', '00000003') ON CONFLICT ("NormalizedName") DO NOTHING;
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('20000000-0000-0000-0000-000000000004', 'Reviewer', 'REVIEWER', '00000004') ON CONFLICT ("NormalizedName") DO NOTHING;
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('20000000-0000-0000-0000-000000000005', 'Moderator', 'MODERATOR', '00000005') ON CONFLICT ("NormalizedName") DO NOTHING;
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('20000000-0000-0000-0000-000000000006', 'Admin', 'ADMIN', '00000006') ON CONFLICT ("NormalizedName") DO NOTHING;
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('20000000-0000-0000-0000-000000000007', 'SuperAdmin', 'SUPERADMIN', '00000007') ON CONFLICT ("NormalizedName") DO NOTHING;

INSERT INTO "Languages" ("Id", "Code", "EnglishName", "NativeName", "WritingDirection")
    VALUES (1, 'en', 'English', 'English', 'ltr') ON CONFLICT DO NOTHING;
INSERT INTO "Languages" ("Id", "Code", "EnglishName", "NativeName", "WritingDirection")
    VALUES (2, 'es', 'Spanish', 'Español', 'ltr') ON CONFLICT DO NOTHING;
INSERT INTO "Languages" ("Id", "Code", "EnglishName", "NativeName", "WritingDirection")
    VALUES (3, 'it', 'Italian', 'Italiano', 'ltr') ON CONFLICT DO NOTHING;
INSERT INTO "Languages" ("Id", "Code", "EnglishName", "NativeName", "WritingDirection")
    VALUES (4, 'de', 'German', 'Deutsch', 'ltr') ON CONFLICT DO NOTHING;
INSERT INTO "Languages" ("Id", "Code", "EnglishName", "NativeName", "WritingDirection")
    VALUES (5, 'ru', 'Russian', 'Русский', 'ltr') ON CONFLICT DO NOTHING;

INSERT INTO "Courses" ("Id", "Description", "IsPublished", "OrderNumber", "SourceLanguageId", "TargetLanguageId", "Title")
    VALUES ('10000000-0000-0000-0000-000000000001', 'Learn Spanish through English.', FALSE, 1, 1, 2, 'Spanish') ON CONFLICT DO NOTHING;
INSERT INTO "Courses" ("Id", "Description", "IsPublished", "OrderNumber", "SourceLanguageId", "TargetLanguageId", "Title")
    VALUES ('10000000-0000-0000-0000-000000000002', 'Learn Italian through English.', FALSE, 2, 1, 3, 'Italian') ON CONFLICT DO NOTHING;
INSERT INTO "Courses" ("Id", "Description", "IsPublished", "OrderNumber", "SourceLanguageId", "TargetLanguageId", "Title")
    VALUES ('10000000-0000-0000-0000-000000000003', 'Learn German through English.', FALSE, 3, 1, 4, 'German') ON CONFLICT DO NOTHING;
INSERT INTO "Courses" ("Id", "Description", "IsPublished", "OrderNumber", "SourceLanguageId", "TargetLanguageId", "Title")
    VALUES ('10000000-0000-0000-0000-000000000004', 'Learn Russian through English.', FALSE, 4, 1, 5, 'Russian') ON CONFLICT DO NOTHING;

INSERT INTO "VocabularyItems" ("Id", "CourseId", "Word", "EnglishTranslation", "Example", "PartOfSpeech", "Topic")
    VALUES
    ('15110000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'hola', 'hello', '¡Hola! ¿Cómo estás?', 'interjection', 'greetings'),
    ('15110000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'dos', 'two', 'Tengo dos entradas.', 'numeral', 'numbers'),
    ('15110000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', 'la hermana', 'sister', 'La hermana de Ana vive aquí.', 'noun', 'family'),
    ('15110000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000001', 'el pan', 'bread', 'El pan está en la mesa.', 'noun', 'food'),
    ('15110000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000001', 'la estación', 'station', 'La estación está cerca.', 'noun', 'travel'),
    ('15110000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000001', 'la lluvia', 'rain', 'La lluvia moja las calles.', 'noun', 'routines'),
    ('15110000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000002', 'ciao', 'hello', 'Ciao, come stai?', 'interjection', 'greetings'),
    ('15110000-0000-0000-0000-000000000008', '10000000-0000-0000-0000-000000000002', 'due', 'two', 'Ho due biglietti.', 'numeral', 'numbers'),
    ('15110000-0000-0000-0000-000000000009', '10000000-0000-0000-0000-000000000002', 'la sorella', 'sister', 'La sorella di Anna abita qui.', 'noun', 'family'),
    ('15110000-0000-0000-0000-000000000010', '10000000-0000-0000-0000-000000000002', 'il pane', 'bread', 'Il pane è sul tavolo.', 'noun', 'food'),
    ('15110000-0000-0000-0000-000000000011', '10000000-0000-0000-0000-000000000002', 'la stazione', 'station', 'La stazione è vicina.', 'noun', 'travel'),
    ('15110000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000002', 'la pioggia', 'rain', 'La pioggia bagna le strade.', 'noun', 'routines'),
    ('15110000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000003', 'Hallo', 'hello', 'Hallo, wie geht es dir?', 'interjection', 'greetings'),
    ('15110000-0000-0000-0000-000000000014', '10000000-0000-0000-0000-000000000003', 'zwei', 'two', 'Ich habe zwei Fahrkarten.', 'numeral', 'numbers'),
    ('15110000-0000-0000-0000-000000000015', '10000000-0000-0000-0000-000000000003', 'die Schwester', 'sister', 'Die Schwester von Anna wohnt hier.', 'noun', 'family'),
    ('15110000-0000-0000-0000-000000000016', '10000000-0000-0000-0000-000000000003', 'das Brot', 'bread', 'Das Brot liegt auf dem Tisch.', 'noun', 'food'),
    ('15110000-0000-0000-0000-000000000017', '10000000-0000-0000-0000-000000000003', 'der Bahnhof', 'train station', 'Der Bahnhof ist ganz in der Nähe.', 'noun', 'travel'),
    ('15110000-0000-0000-0000-000000000018', '10000000-0000-0000-0000-000000000003', 'der Regen', 'rain', 'Der Regen macht die Straßen nass.', 'noun', 'routines'),
    ('15110000-0000-0000-0000-000000000019', '10000000-0000-0000-0000-000000000004', 'привет', 'hello', 'Привет! Как дела?', 'interjection', 'greetings'),
    ('15110000-0000-0000-0000-000000000020', '10000000-0000-0000-0000-000000000004', 'два', 'two', 'У меня два билета.', 'numeral', 'numbers'),
    ('15110000-0000-0000-0000-000000000021', '10000000-0000-0000-0000-000000000004', 'сестра', 'sister', 'Моя сестра живёт здесь.', 'noun', 'family'),
    ('15110000-0000-0000-0000-000000000022', '10000000-0000-0000-0000-000000000004', 'хлеб', 'bread', 'Хлеб лежит на столе.', 'noun', 'food'),
    ('15110000-0000-0000-0000-000000000023', '10000000-0000-0000-0000-000000000004', 'вокзал', 'train station', 'Вокзал находится рядом.', 'noun', 'travel'),
    ('15110000-0000-0000-0000-000000000024', '10000000-0000-0000-0000-000000000004', 'дождь', 'rain', 'На улице идёт дождь.', 'noun', 'routines')
    ON CONFLICT ("Id") DO NOTHING;
COMMIT;
