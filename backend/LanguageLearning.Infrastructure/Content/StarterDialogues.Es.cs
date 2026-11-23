namespace LanguageLearning.Infrastructure.Content;

internal static partial class StarterDialogues
{
    internal static readonly Scenario[] Es =
    [
        new("cafe", "At the café", "Buenos días. ¿Qué desea?",
            new("Un café, por favor.", "¿Con leche o solo?", "Con leche, gracias.", "Aquí tiene su café con leche."),
            new("Un té, por favor.", "¿Negro o verde?", "Verde, gracias.", "Aquí tiene su té verde."),
            "Use «por favor» to make the order polite. «Gracias» acknowledges the follow-up."),
        new("hotel", "At the hotel", "Buenas tardes. ¿Tiene reserva?",
            new("Sí, a nombre de Ruiz.", "¿Me muestra su pasaporte?", "Sí, aquí lo tiene.", "Gracias. Aquí tiene la llave."),
            new("No. ¿Tiene una habitación libre?", "Sí. ¿Para una o dos personas?", "Para una persona, por favor.", "Perfecto. Hay una habitación individual."),
            "«A nombre de» gives the name on the booking. «¿Tiene…?» is a polite way to ask the receptionist."),
        new("tickets", "Buying a ticket", "Buenos días. ¿Adónde quiere viajar?",
            new("A Sevilla, por favor.", "¿Solo ida o ida y vuelta?", "Solo ida, por favor.", "Aquí tiene su billete a Sevilla."),
            new("A Madrid, por favor.", "¿Para hoy o para mañana?", "Para mañana, por favor.", "Aquí tiene su billete a Madrid para mañana."),
            "«A» introduces the destination. «Solo ida» means one-way, and «para mañana» gives the travel day."),
        new("directions", "Asking for directions", "Hola. ¿En qué puedo ayudarle?",
            new("Disculpe, ¿dónde está la estación?", "Siga recto hasta el semáforo.", "¿Está lejos?", "No, está a cinco minutos a pie."),
            new("Disculpe, ¿dónde está el museo?", "Gire a la izquierda en la plaza.", "¿Está cerca de la plaza?", "Sí, está junto a la plaza."),
            "«Disculpe» gets a stranger’s attention politely. «Cerca de» means near."),
        new("lost-and-found", "At lost and found", "Buenas tardes. ¿Qué ha perdido?",
            new("He perdido una mochila.", "¿De qué color es?", "Es azul.", "Tenemos una mochila azul. ¿Es esta?"),
            new("He perdido mi paraguas.", "¿Dónde lo vio por última vez?", "En el autobús.", "Voy a llamar al conductor."),
            "«He perdido» means “I’ve lost.” Use «es» to describe the item and «en» to say where you last saw it.", true),
    ];
}
