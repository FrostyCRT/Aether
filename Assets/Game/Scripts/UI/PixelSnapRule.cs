using UnityEngine;

// AJOUTÉ (2026-10-26, retour utilisateur, capture du menu Pause : "les barres verticales n'ont pas la même taille en
// épaisseur, celle de la carte de gauche paraît plus épaisse") - un filet fin (Image sans sprite) mis à l'échelle (cartes
// du menu Pause : x1,08) fait une épaisseur non entière (3 x 1,08 = 3,24 px) posée à une position non entière : selon la
// carte, il couvre 3 ou 4 colonnes de pixels (mesuré : 4 à gauche, 3 à droite). Ce composant arrondit l'épaisseur à un
// nombre entier de pixels et cale le filet sur la grille de pixels de l'écran, pour qu'il soit identique partout.
// Filet vertical ou horizontal, tourné ou non (le séparateur de fusion est un filet horizontal tourné de 90°).
// Canvas en Screen Space - Overlay : une unité du monde = un pixel d'écran.
[RequireComponent(typeof(RectTransform))]
public class PixelSnapRule : MonoBehaviour
{
    private readonly Vector3[] _corners = new Vector3[4];

    private void LateUpdate()
    {
        RectTransform rt = (RectTransform)transform;
        rt.GetWorldCorners(_corners);
        float minX = Mathf.Min(_corners[0].x, _corners[2].x), maxX = Mathf.Max(_corners[0].x, _corners[2].x);
        float minY = Mathf.Min(_corners[0].y, _corners[2].y), maxY = Mathf.Max(_corners[0].y, _corners[2].y);
        bool vertical = maxX - minX < maxY - minY;
        float thickness = vertical ? maxX - minX : maxY - minY;
        float center = vertical ? (minX + maxX) * 0.5f : (minY + maxY) * 0.5f;
        if (thickness <= 0f) return;

        float pixels = Mathf.Max(1f, Mathf.Round(thickness));
        if (Mathf.Abs(pixels - thickness) > 0.01f)
        {
            // L'épaisseur est sizeDelta.x d'un filet vertical non tourné, sizeDelta.y d'un filet horizontal tourné de 90°.
            bool turned = Mathf.Abs(Mathf.Sin(rt.eulerAngles.z * Mathf.Deg2Rad)) > 0.5f;
            bool thicknessIsX = vertical != turned;
            Vector2 size = rt.sizeDelta;
            float scale = thicknessIsX ? size.x / thickness : size.y / thickness;
            if (thicknessIsX) size.x = pixels * scale; else size.y = pixels * scale;
            rt.sizeDelta = size;
        }

        // Bords sur des pixels entiers : centre sur un demi-pixel pour une épaisseur impaire, sur un pixel entier sinon.
        float snapped = pixels % 2f == 1f ? Mathf.Floor(center) + 0.5f : Mathf.Round(center);
        float delta = snapped - center;
        if (Mathf.Abs(delta) > 0.001f)
            rt.position += vertical ? new Vector3(delta, 0f, 0f) : new Vector3(0f, delta, 0f);
    }
}
