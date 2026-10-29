using System.Text.RegularExpressions;

// AJOUTÉ (2026-10-28, retour utilisateur : "faire un petit espace des deux côtés des deux points pour les heures, partout
// où elle est écrite, même dans les records") - toutes les durées « minutes:secondes » du jeu (chrono du HUD, pause,
// écrans de fin, records, mode de jeu, compte à rebours du boss) passent par ici : « 03 : 24 » avec une espace fine de
// chaque côté (balise TextMeshPro <space>, la même largeur dans toutes les polices et toutes les langues).
public static class TimeText
{
    private const string Colon = "<space=0.12em>:<space=0.12em>";

    public static readonly string Empty = "--" + Colon + "--";

    public static string Clock(int minutes, int seconds) => minutes.ToString("00") + Colon + seconds.ToString("00");

    // Phrase traduite qui contient déjà une durée (« Survie 03:24 · ... ») : seuls les deux-points ENTRE deux chiffres
    // sont espacés, la ponctuation des phrases (« Temps : ») ne bouge pas.
    private static readonly Regex DigitColon = new Regex(@"(?<=\d):(?=\d)", RegexOptions.Compiled);

    public static string SpaceColons(string text) => string.IsNullOrEmpty(text) ? text : DigitColon.Replace(text, Colon);
}
