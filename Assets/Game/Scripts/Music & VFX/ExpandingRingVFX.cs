using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// AJOUTE - petit effet visuel procedural (aucun asset requis) : un anneau qui
// s'agrandit du centre vers un rayon cible en s'estompant, pour materialiser une
// zone d'effet (explosion, future aura, etc.) en attendant de vraies particules
// dediees. Utilise un LineRenderer forme en cercle plutot qu'un sprite/particule,
// donc fonctionne immediatement sans configuration prealable dans l'Inspector.
//
// HYPOTHESE - suppose que le sol du jeu est le plan XZ (Y = hauteur), coherent
// avec le reste du code vu jusqu'ici (direction.y = 0f partout). Si le jeu est
// en vue 2D pure (plan XY), dis-le-moi et j'adapte DrawCircle().
//
// MODIFIE (2026-10-07, retour utilisateur : "les FPS baissent tout au long de la game... toujours une perte de
// fps [au combat de boss], c'est pas normal si il n'y a plus que le boss") - ce fichier n'avait JAMAIS reçu le
// correctif anti-churn déjà appliqué à ses cousins ImpactSparkVFX/LightningBoltVFX (retour utilisateur antérieur,
// "je passe de 240 à 40 FPS" avec la Foudre en boucle, cause = Instantiate/Destroy + new Material() à chaque
// appel). Spawn() faisait exactement la même chose : un GameObject + un LineRenderer + un new Material(Shader.
// Find(...)) RECRÉÉS à chaque appel, jamais réutilisés, détruits ~0,2-0,6s plus tard. Cet effet est appelé en
// continu pendant un combat de boss (repositionnement/implosion de BossCorruptedSource toutes les quelques
// secondes PENDANT TOUTE LA DURÉE du combat, chaque projectile ennemi qui touche le joueur, chaque gemme d'XP
// ramassée...) - du vrai CHURN d'objets/matériaux qui dure du début à la fin d'une run, pas du tout lié au
// nombre d'ennemis à l'écran à un instant donné. Repris sur le même modèle que LightningBoltVFX : pool statique
// de Runner réutilisés (SetActive au lieu de Destroy) + UN SEUL Material partagé (jamais recréé) + plafond dur
// de rayons simultanés.
public static class ExpandingRingVFX
{
    private const int MaxConcurrent = 60;

    private static Material _material;
    private static readonly Stack<ExpandingRingRunner> _pool = new Stack<ExpandingRingRunner>();
    private static int _activeCount = 0;

    private static Material GetMaterial()
    {
        if (_material == null)
            _material = new Material(Shader.Find("Sprites/Default"));
        return _material;
    }

    public static void Spawn(Vector3 position, float targetRadius, Color color, float duration)
    {
        if (_activeCount >= MaxConcurrent) return;

        ExpandingRingRunner runner = _pool.Count > 0 ? _pool.Pop() : null;
        if (runner == null)
        {
            GameObject go = new GameObject("ExpandingRingVFX");
            runner = go.AddComponent<ExpandingRingRunner>();
        }
        else
        {
            runner.gameObject.SetActive(true);
        }

        _activeCount++;
        runner.Play(position, targetRadius, color, GetMaterial(), duration, ReturnToPool);
    }

    private static void ReturnToPool(ExpandingRingRunner runner)
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        runner.gameObject.SetActive(false);
        _pool.Push(runner);
    }
}

public class ExpandingRingRunner : MonoBehaviour
{
    private const int SegmentCount = 32;

    private LineRenderer _line;
    private System.Action<ExpandingRingRunner> _onComplete;
    private Coroutine _co;

    public void Play(Vector3 center, float targetRadius, Color baseColor, Material material, float duration, System.Action<ExpandingRingRunner> onComplete)
    {
        _onComplete = onComplete;
        transform.position = center;

        if (_line == null)
        {
            _line = GetComponent<LineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = SegmentCount;
            _line.widthMultiplier = 0.12f;
        }
        _line.material = material;
        _line.startColor = baseColor;
        _line.endColor = baseColor;

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Animate(center, targetRadius, baseColor, duration));
    }

    private IEnumerator Animate(Vector3 center, float targetRadius, Color baseColor, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float radius = Mathf.Lerp(0.05f, targetRadius, t);
            DrawCircle(center, radius);

            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, t);
            _line.startColor = c;
            _line.endColor = c;

            yield return null;
        }

        _co = null;
        _onComplete?.Invoke(this);
    }

    private void DrawCircle(Vector3 center, float radius)
    {
        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = (i / (float)SegmentCount) * Mathf.PI * 2f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)) * radius;
            _line.SetPosition(i, point);
        }
    }
}
