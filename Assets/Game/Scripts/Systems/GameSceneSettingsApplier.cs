using UnityEngine;

public class GameSceneSettingsApplier : MonoBehaviour
{
    [SerializeField] private Light _directionalLight;

    private void Start()
    {
        bool shadowsOn = GameSettings.ShadowsEnabled;
        if (_directionalLight != null)
        {
            _directionalLight.shadows = shadowsOn ? LightShadows.Soft : LightShadows.None;

            // AJOUTÉ (2026-10-19, session esthétique - retour utilisateur : "la lumière est blanche neutre, aucune
            // identité") - teinte la lumière selon le palier de difficulté (voir DifficultyTiers.LightColor/
            // LightIntensity).
            // ÉTENDU (2026-10-20, carrousel de difficulté) - s'applique maintenant aussi en Sans fin et Ruée de
            // boss (le palier y a un vrai effet de jeu désormais, plus seulement le Classique) ; Choc des titans
            // reste à Modéré par défaut, seul mode sans palier de difficulté.
            DifficultyTier tier = (GameModes.IsClassic || GameModes.IsEndless || GameModes.IsBossRush) ? DifficultyTiers.Current : DifficultyTier.Moderate;
            _directionalLight.color = DifficultyTiers.LightColor(tier);
            _directionalLight.intensity = DifficultyTiers.LightIntensity(tier);
        }
    }
}