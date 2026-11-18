using System.Collections.Generic;
using TMPro;
using UnityEngine;

// AJOUTÉ (2026-11-18, vérification des textes dans toutes les langues) - outils pour qu'un texte tienne dans la place prévue.
public static class TextFit
{
    private static readonly Dictionary<int, float> _baseSize = new Dictionary<int, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _baseSize.Clear(); }

    /// <summary>
    /// Une seule ligne, taille fixe : si le texte est plus large que `width` (cadre du texte par défaut), la police est réduite
    /// pour qu'il tienne (jamais sous minFraction de la taille d'origine) ; remise à sa taille d'origine si ça tient. La taille
    /// d'origine est mémorisée au premier appel : pas de rétrécissement cumulé d'une langue à l'autre.
    /// </summary>
    public static void ShrinkToWidth(TMP_Text t, float width = -1f, float minFraction = 0.55f)
    {
        if (t == null || t.enableAutoSizing) return;
        int id = t.GetInstanceID();
        if (!_baseSize.TryGetValue(id, out float baseSize)) { baseSize = t.fontSize; _baseSize[id] = baseSize; }
        t.fontSize = baseSize;
        if (width <= 0f) width = t.rectTransform.rect.width;
        if (width < 20f || string.IsNullOrEmpty(t.text)) return;
        float pref = t.GetPreferredValues(t.text).x;
        if (pref > width) t.fontSize = Mathf.Max(baseSize * minFraction, baseSize * width / pref);
    }
}
