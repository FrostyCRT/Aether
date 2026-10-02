using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// AJOUTE (2026-09-27, retour utilisateur : "je veux aussi que tu rajoute un effet quand avec la compétence de
// la foudre, un prefab qui fait qu'on voit quand la foudre frappe, et qu'on voit le ricochet entre ennemis") -
// jusqu'ici WeaponLightningChain/LightningChainUtility ne dessinaient qu'un Debug.DrawLine, visible UNIQUEMENT
// dans la Scene view de l'éditeur, jamais en jeu réel (aucun retour visuel pour le joueur). Utilitaire statique
// sans état, construit ses GameObjects entièrement en code (pas de prefab à assigner/perdre) - même esprit que
// ProceduralGlowUI. Deux effets bâtis sur la même primitive (un éclair en zigzag entre 2 points via LineRenderer) :
// SpawnBolt (le "ricochet" visible entre 2 points, joueur→1er ennemi ou ennemi→ennemi suivant) et
// SpawnStrikeFlash (plusieurs mini-éclairs radiaux courts = flash d'impact, "on voit quand la foudre frappe").
//
// MODIFIE (2026-09-27, retour utilisateur : "quand il y a de la foudre sur plein d'ennemis et en boucle, j'ai
// une grosse chute de fps, je passe de 240 à 40") - cause trouvée : WeaponFusionCursedSwamp décharge jusqu'à 7
// flaques indépendamment toutes les 0,35s (voir son commentaire) ; avec plusieurs ennemis dedans en même temps,
// ça peut déclencher des dizaines de chaînes par seconde, chacune générant ~7 GameObjects (1 éclair + 1 flash de
// 5-7 mini-éclairs) PAR maillon touché - facilement plusieurs centaines d'Instantiate/Destroy par seconde, le vrai
// coût n'étant pas le rendu (quelques LineRenderer fins) mais le CHURN d'objets. Deux correctifs, sans changer le
// rendu visuel voulu ("le visuel est super bien") : (1) un pool statique de LightningBoltRunner - réutilisés
// (SetActive/position/couleur remis à neuf) au lieu de Instantiate+Destroy à chaque éclair ; (2) un plafond dur
// sur le nombre d'éclairs simultanés (au-delà, une nouvelle demande est silencieusement ignorée plutôt que de
// continuer à empiler des objets sans limite dans un cas pathologique).
public static class LightningBoltVFX
{
    private const int MaxConcurrentBolts = 120;

    private static Material _material;
    private static readonly Stack<LightningBoltRunner> _pool = new Stack<LightningBoltRunner>();
    private static int _activeCount = 0;

    private static Material GetMaterial()
    {
        if (_material == null)
            _material = new Material(Shader.Find("Sprites/Default"));
        return _material;
    }

    public static void SpawnBolt(Vector3 from, Vector3 to, Color color, float duration = 0.16f, float width = 0.12f)
    {
        if (_activeCount >= MaxConcurrentBolts) return;

        LightningBoltRunner runner = _pool.Count > 0 ? _pool.Pop() : null;
        if (runner == null)
        {
            GameObject go = new GameObject("LightningBoltVFX");
            runner = go.AddComponent<LightningBoltRunner>();
        }
        else
        {
            runner.gameObject.SetActive(true);
        }

        _activeCount++;
        runner.Play(from, to, color, GetMaterial(), duration, width, ReturnToPool);
    }

    public static void SpawnStrikeFlash(Vector3 position, Color color, float duration = 0.18f)
    {
        // MODIFIE - 5-7 → 3-4 mini-éclairs : toujours un vrai éclat radial à l'œil, pour une fraction du coût
        // en objets quand ça se déclenche en rafale (voir commentaire en tête de fichier).
        int sparkCount = Random.Range(3, 5);
        for (int i = 0; i < sparkCount; i++)
        {
            float angle = (360f / sparkCount) * i + Random.Range(-15f, 15f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float len = Random.Range(0.35f, 0.7f);
            SpawnBolt(position, position + dir * len, color, duration * 0.85f, 0.07f);
        }
    }

    private static void ReturnToPool(LightningBoltRunner runner)
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        runner.gameObject.SetActive(false);
        _pool.Push(runner);
    }
}

public class LightningBoltRunner : MonoBehaviour
{
    private LineRenderer _line;
    private Vector3[] _pointsBuffer = new Vector3[10]; // taille max utilisée ci-dessous (segments+1 <= 10)
    private System.Action<LightningBoltRunner> _onFadeComplete;
    private Coroutine _fadeCoroutine;

    public void Play(Vector3 from, Vector3 to, Color color, Material material, float duration, float width, System.Action<LightningBoltRunner> onFadeComplete)
    {
        _onFadeComplete = onFadeComplete;

        if (_line == null)
        {
            _line = GetComponent<LineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.textureMode = LineTextureMode.Stretch;
            _line.numCapVertices = 3;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }
        _line.material = material;

        Vector3 dir = to - from;
        float length = dir.magnitude;
        Vector3 dirNorm = length > 0.001f ? dir / length : Vector3.right;
        Vector3 perp = Vector3.Cross(dirNorm, Vector3.up);
        if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(dirNorm, Vector3.forward);
        perp.Normalize();

        // Chemin en zigzag : décalage perpendiculaire aléatoire qui s'annule pile aux 2 extrémités (facteur en
        // cloche), pour bien ancrer le trait sur les 2 points réels tout en zigzaguant entre eux.
        int segments = Mathf.Clamp(Mathf.RoundToInt(length * 2f) + 3, 4, 9);
        if (_pointsBuffer.Length < segments + 1) _pointsBuffer = new Vector3[segments + 1];

        _pointsBuffer[0] = from;
        _pointsBuffer[segments] = to;
        for (int i = 1; i < segments; i++)
        {
            float t = (float)i / segments;
            float edgeFactor = Mathf.Sin(t * Mathf.PI);
            float offset = Random.Range(-1f, 1f) * edgeFactor * Mathf.Min(length * 0.25f, 0.5f);
            _pointsBuffer[i] = Vector3.Lerp(from, to, t) + perp * offset + Vector3.up * 0.05f;
        }
        _line.positionCount = segments + 1;
        _line.SetPositions(_pointsBuffer);
        _line.startWidth = width;
        _line.endWidth = width * 0.6f;

        Color c = color;
        c.a = 1f;
        _line.startColor = c;
        _line.endColor = c;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeAndRelease(duration));
    }

    private IEnumerator FadeAndRelease(float duration)
    {
        float t = 0f;
        Color baseColor = _line.startColor;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            // Petit crépitement (sursauts d'intensité) plutôt qu'un fondu linéaire - lit comme électrique plutôt
            // que comme un simple fade-out générique.
            float flicker = 0.75f + 0.25f * Mathf.Sin(progress * 40f);
            float alpha = Mathf.Lerp(1f, 0f, progress) * flicker;
            Color c = baseColor;
            c.a = Mathf.Clamp01(alpha);
            _line.startColor = c;
            _line.endColor = new Color(c.r, c.g, c.b, c.a * 0.5f);
            yield return null;
        }
        _fadeCoroutine = null;
        _onFadeComplete?.Invoke(this);
    }
}
