using UnityEngine;
using System.Collections;

// AJOUTE - petit effet visuel procedural (aucun asset requis) : un anneau qui
// s'agrandit du centre vers un rayon cible en s'estompant, pour materialiser une
// zone d'effet (explosion, future aura, etc.) en attendant de vraies particules
// dediees. Utilise un LineRenderer forme en cercle plutot qu'un sprite/particule,
// donc fonctionne immediatement sans configuration prealable dans l'Inspector.
//
// HYPOTHESE - suppose que le sol du jeu est le plan XZ (Y = hauteur), coherent
// avec le reste du code vu jusqu'ici (direction.y = 0f partout). Si le jeu est
// en vue 2D pure (plan XY), dis-le-moi et j'adapte DrawCircle().
public class ExpandingRingVFX : MonoBehaviour
{
    private const int SegmentCount = 32;

    public static void Spawn(Vector3 position, float targetRadius, Color color, float duration)
    {
        GameObject go = new GameObject("ExpandingRingVFX");
        go.transform.position = position;

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = SegmentCount;
        line.widthMultiplier = 0.12f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;

        ExpandingRingVFX vfx = go.AddComponent<ExpandingRingVFX>();
        vfx.StartCoroutine(vfx.Animate(line, position, targetRadius, color, duration));
    }

    private IEnumerator Animate(LineRenderer line, Vector3 center, float targetRadius, Color baseColor, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float radius = Mathf.Lerp(0.05f, targetRadius, t);
            DrawCircle(line, center, radius);

            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, t);
            line.startColor = c;
            line.endColor = c;

            yield return null;
        }

        Destroy(gameObject);
    }

    private void DrawCircle(LineRenderer line, Vector3 center, float radius)
    {
        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = (i / (float)SegmentCount) * Mathf.PI * 2f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)) * radius;
            line.SetPosition(i, point);
        }
    }
}