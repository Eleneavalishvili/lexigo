namespace LanguageLearning.Infrastructure.Content;

internal static partial class StarterDialogues
{
    internal static readonly Scenario[] It =
    [
        new("lost-and-found", "At lost and found", "Buonasera. Che cosa ha perso?",
            new("Ho perso uno zaino.", "Di che colore è?", "È blu.", "Abbiamo uno zaino blu. È questo?"),
            new("Ho perso il mio ombrello.", "Dove l’ha visto l’ultima volta?", "Sull’autobus.", "Chiamo l’autista."),
            "«Ho perso» means “I’ve lost.” «Sull’autobus» means “on the bus.”", true),
        new("cafe", "At the café", "Buongiorno, che cosa prende?",
            new("Un caffè, per favore.", "Lo vuole macchiato?", "Sì, grazie.", "Ecco il suo caffè macchiato."),
            new("Un tè, per favore.", "Preferisce il tè nero o verde?", "Verde, grazie.", "Ecco il suo tè verde."),
            "«Per favore» makes a request polite. «Grazie» thanks the person serving you."),
        new("hotel", "At the hotel", "Buonasera. Ha una prenotazione?",
            new("Sì, a nome Rossi.", "Posso vedere il suo passaporto?", "Certo, eccolo.", "Grazie. Ecco la chiave."),
            new("No. Avete una camera libera?", "Sì. Per una o due persone?", "Per una persona, per favore.", "Perfetto, abbiamo una singola."),
            "«A nome» introduces the booking name. «Ha» is the formal singular form used with a guest."),
        new("tickets", "Buying a ticket", "Buongiorno. Dove vuole andare?",
            new("A Roma, per favore.", "Solo andata o andata e ritorno?", "Solo andata, per favore.", "Ecco il suo biglietto per Roma."),
            new("A Firenze, per favore.", "Per oggi o per domani?", "Per domani, per favore.", "Ecco il biglietto per Firenze per domani."),
            "«A» goes before these city names. «Solo andata» means one-way."),
        new("directions", "Asking for directions", "Buongiorno. Posso aiutarla?",
            new("Mi scusi, dov’è la stazione?", "Vada sempre dritto fino al semaforo.", "È lontana?", "No, è a cinque minuti a piedi."),
            new("Mi scusi, dov’è il museo?", "Giri a sinistra nella piazza.", "È vicino alla piazza?", "Sì, è accanto alla piazza."),
            "«Mi scusi» is a polite way to address a stranger. «Vicino a» means near."),
    ];
}
