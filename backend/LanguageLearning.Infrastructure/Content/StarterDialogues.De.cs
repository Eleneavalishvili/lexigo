namespace LanguageLearning.Infrastructure.Content;

internal static partial class StarterDialogues
{
    internal static readonly Scenario[] De =
    [
        new("lost-and-found", "At lost and found", "Guten Tag. Was haben Sie verloren?",
            new("Ich habe einen Rucksack verloren.", "Welche Farbe hat er?", "Er ist blau.", "Wir haben einen blauen Rucksack. Ist es dieser?"),
            new("Ich habe meinen Regenschirm verloren.", "Wo haben Sie ihn zuletzt gesehen?", "Im Bus.", "Ich rufe den Fahrer an."),
            "«Ich habe … verloren» means “I’ve lost …” and «im Bus» means “on the bus.”", true),
        new("cafe", "At the café", "Guten Morgen. Was möchten Sie?",
            new("Einen Kaffee, bitte.", "Mit Milch oder schwarz?", "Mit Milch, danke.", "Hier ist Ihr Kaffee mit Milch."),
            new("Einen Tee, bitte.", "Schwarzen oder grünen Tee?", "Grünen Tee, danke.", "Hier ist Ihr grüner Tee."),
            "«Bitte» softens the order. «Einen» is the accusative form used here with Kaffee and Tee."),
        new("hotel", "At the hotel", "Guten Abend. Haben Sie reserviert?",
            new("Ja, auf den Namen Weber.", "Darf ich Ihren Pass sehen?", "Ja, hier ist er.", "Vielen Dank. Hier ist Ihr Schlüssel."),
            new("Nein. Haben Sie noch ein Zimmer frei?", "Ja. Für eine oder zwei Personen?", "Für eine Person, bitte.", "Wir haben ein Einzelzimmer frei."),
            "Use «Sie» with staff in this situation. «Auf den Namen» introduces the reservation name."),
        new("tickets", "Buying a ticket", "Guten Tag. Wohin möchten Sie fahren?",
            new("Nach Berlin, bitte.", "Einfach oder hin und zurück?", "Einfach, bitte.", "Hier ist Ihre Fahrkarte nach Berlin."),
            new("Nach Hamburg, bitte.", "Für heute oder für morgen?", "Für morgen, bitte.", "Hier ist Ihre Fahrkarte nach Hamburg für morgen."),
            "«Nach» introduces these city destinations. «Einfach» means one-way at a ticket counter."),
        new("directions", "Asking for directions", "Guten Tag. Kann ich Ihnen helfen?",
            new("Entschuldigung, wo ist der Bahnhof?", "Gehen Sie bis zur Ampel geradeaus.", "Ist es weit?", "Nein, zu Fuß sind es fünf Minuten."),
            new("Entschuldigung, wo ist das Museum?", "Biegen Sie am Platz links ab.", "Ist es in der Nähe?", "Ja, es ist gleich neben dem Platz."),
            "«Entschuldigung» gets someone’s attention politely. «Geradeaus» means straight ahead."),
    ];
}
