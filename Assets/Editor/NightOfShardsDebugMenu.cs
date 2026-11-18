using UnityEditor;
using UnityEngine;

// Raccourci de test : la touche = lance la Nuit des Éclats pendant une partie en Play (sans attendre l'éclat).
public static class NightOfShardsDebugMenu
{
    private const string Path = "Aether/Debug/Lancer la Nuit des Éclats _=";

    [MenuItem(Path)]
    private static void StartNight()
    {
        NightOfShards night = Object.FindFirstObjectByType<NightOfShards>();
        if (night == null) { Debug.LogWarning("[Nuit des Éclats] Introuvable : lancez une partie classique/escarmouche (pas Titans/Ruée/entraînement)."); return; }
        night.DebugStartNightNow();
    }

    [MenuItem(Path, true)]
    private static bool Validate() => Application.isPlaying;
}
