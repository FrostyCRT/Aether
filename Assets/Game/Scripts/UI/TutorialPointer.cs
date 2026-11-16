using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-16, partie d'entraînement) - flèches animées qui désignent des points du monde (gemmes à ramasser, projectile à
// absorber, ennemi qui arrive). Une flèche dorée qui rebondit au-dessus de la cible ; si la cible est hors de l'écran, la flèche
// se colle au bord et pointe dans sa direction. Canevas propre, juste sous la fenêtre du didacticiel (ne bloque aucun clic).
public class TutorialPointer : MonoBehaviour
{
    private const int MaxArrows = 6;
    private RectTransform _root;
    private readonly List<RectTransform> _arrows = new List<RectTransform>();
    private static Sprite _sprite;

    public static TutorialPointer Create()
    {
        GameObject go = new GameObject("TutorialPointer");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 5900;
        CanvasScaler sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;
        TutorialPointer p = go.AddComponent<TutorialPointer>();
        p._root = go.GetComponent<RectTransform>();
        return p;
    }

    private static Sprite ArrowSprite()
    {
        if (_sprite != null) return _sprite;
        const int n = 64;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // triangle pointant vers le bas (pointe en y = 4), bords adoucis
                float half = Mathf.Clamp01((y - 4f) / (n - 8f)) * 29f;
                float d = half - Mathf.Abs(x - 31.5f);
                float a = y < 4 ? 0f : Mathf.Clamp01(d * 0.8f + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px);
        t.Apply();
        _sprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return _sprite;
    }

    private RectTransform NewArrow()
    {
        RectTransform rt = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(_root, false);
        rt.sizeDelta = new Vector2(64f, 64f);
        Image shadow = new GameObject("Shadow", typeof(RectTransform)).AddComponent<Image>();
        shadow.transform.SetParent(rt, false);
        shadow.sprite = ArrowSprite(); shadow.color = new Color(0.05f, 0.03f, 0.02f, 0.9f); shadow.raycastTarget = false;
        shadow.rectTransform.anchorMin = Vector2.zero; shadow.rectTransform.anchorMax = Vector2.one;
        shadow.rectTransform.offsetMin = new Vector2(-6f, -6f); shadow.rectTransform.offsetMax = new Vector2(6f, 6f);
        Image fill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        fill.transform.SetParent(rt, false);
        fill.sprite = ArrowSprite(); fill.color = new Color(1f, 0.80f, 0.36f, 1f); fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>Affiche une flèche par position du monde (au plus 6). Liste vide = rien.</summary>
    public void Show(List<Vector3> worldPositions)
    {
        Camera cam = Camera.main;
        int n = cam == null || worldPositions == null ? 0 : Mathf.Min(worldPositions.Count, MaxArrows);
        while (_arrows.Count < n) _arrows.Add(NewArrow());
        float w = _root.rect.width, h = _root.rect.height;
        float t = Time.unscaledTime;
        for (int i = 0; i < _arrows.Count; i++)
        {
            RectTransform a = _arrows[i];
            if (i >= n) { a.gameObject.SetActive(false); continue; }
            a.gameObject.SetActive(true);
            Vector3 sp = cam.WorldToScreenPoint(worldPositions[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, new Vector2(sp.x, sp.y), null, out Vector2 local);
            Vector2 p = local + new Vector2(w * 0.5f, h * 0.5f);   // origine en bas à gauche
            float bob = Mathf.Abs(Mathf.Sin(t * 5f)) * 22f;
            bool behind = sp.z < 0f;
            const float margin = 70f;
            bool outside = behind || p.x < margin || p.x > w - margin || p.y < margin || p.y > h - margin;
            if (!outside)
            {
                a.anchoredPosition = new Vector2(p.x - w * 0.5f, p.y - h * 0.5f + 78f + bob);
                a.localRotation = Quaternion.identity;
            }
            else
            {
                Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
                Vector2 dir = behind ? -(p - c) : (p - c);
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                dir.Normalize();
                // intersection de la demi-droite avec le rectangle réduit
                float kx = dir.x != 0f ? (w * 0.5f - margin) / Mathf.Abs(dir.x) : float.MaxValue;
                float ky = dir.y != 0f ? (h * 0.5f - margin - 60f) / Mathf.Abs(dir.y) : float.MaxValue;
                Vector2 edge = dir * Mathf.Min(kx, ky);
                a.anchoredPosition = edge - dir * bob;
                a.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);   // la pointe vers la cible
            }
            float pulse = 1f + 0.08f * Mathf.Sin(t * 8f);
            a.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    public void Hide()
    {
        for (int i = 0; i < _arrows.Count; i++) _arrows[i].gameObject.SetActive(false);
    }
}
