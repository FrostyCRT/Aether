using TMPro;
using UnityEngine;

// AJOUTÉ (2026-11-11, retour utilisateur : "si dans les paramètres d'interface on retire l'or de la partie, il faut que
// l'affichage des autres éléments, donc les kills et les défis se repositionnent automatiquement en haut à droite") - les
// lignes visibles de la colonne de droite (or, éliminations, défi) occupent les rangées dans l'ordre, sans trou.
// Posé et configuré par HudStyler (ApplyTopRight), qui calcule les rangées. (Le halo sombre essayé derrière ces lignes a été
// retiré à la demande de l'utilisateur : « c'est pas ce à quoi je m'attendais visuellement ».)
public class HudRightColumn : MonoBehaviour
{
    private struct Line
    {
        public RectTransform rect;
        public GameObject visibility;   // ce qui est masqué (texte ou groupe)
        public float extraY;
    }

    private Line[] _lines;
    private float[] _rowY;
    private int _lastMask = -1;

    public void Configure(TextMeshProUGUI gold, TextMeshProUGUI kills, RectTransform challengeGroup, float[] rowY, float killsExtraY)
    {
        _rowY = rowY;
        _lines = new[]
        {
            new Line { rect = gold != null ? gold.rectTransform : null, visibility = gold != null ? gold.gameObject : null },
            new Line { rect = kills != null ? kills.rectTransform : null, visibility = kills != null ? kills.gameObject : null, extraY = killsExtraY },
            new Line { rect = challengeGroup, visibility = challengeGroup != null ? challengeGroup.gameObject : null },
        };
        _lastMask = -1;   // HudStyler vient de replacer les lignes : on réapplique l'empilement
    }

    private void LateUpdate()
    {
        if (_lines == null) return;
        int mask = 0;
        for (int i = 0; i < _lines.Length; i++)
            if (_lines[i].visibility != null && _lines[i].visibility.activeInHierarchy) mask |= 1 << i;
        if (mask == _lastMask) return;
        _lastMask = mask;

        int row = 0;
        for (int i = 0; i < _lines.Length; i++)
        {
            if ((mask & (1 << i)) == 0 || _lines[i].rect == null) continue;
            Vector2 p = _lines[i].rect.anchoredPosition;
            _lines[i].rect.anchoredPosition = new Vector2(p.x, _rowY[Mathf.Min(row, _rowY.Length - 1)] + _lines[i].extraY);
            row++;
        }
    }
}
