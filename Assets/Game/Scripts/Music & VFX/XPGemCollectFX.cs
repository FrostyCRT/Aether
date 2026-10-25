using UnityEngine;

// AJOUTÉ (2026-10-04) - retour utilisateur sur le ramassage d'XP : "pas le côté satisfaisant". Avant, une gemme
// ramassée disparaissait instantanément et silencieusement - aucun retour visuel au moment exact où le joueur
// gagne de l'expérience, pourtant l'événement le plus fréquent de toute la partie. Même famille procédurale que
// EnemyDeathFX (FXSprites, aucun asset à importer), mais volontairement PLUS LÉGER ET PLUS COURT (~0,22 s, 3
// éclats au lieu de 5) : les gemmes peuvent se ramasser en rafale (plusieurs par seconde, voire des dizaines d'un
// coup avec l'Ultime du Cristal qui aspire tout), donc chaque instance doit coûter le moins possible.
public class XPGemCollectFX : MonoBehaviour
{
    private const int SparkCount = 3;
    private const float Duration = 0.22f;

    private class Spark
    {
        public SpriteRenderer sr;
        public Vector3 vel;
        public float size;
    }

    private SpriteRenderer _flash;
    private Spark[] _sparks;
    private Color _hot;
    private float _t;
    private Transform _cam;

    // AJOUTÉ (2026-10-24, session FPS) - réserve d'instances réutilisées : chaque appel créait puis détruisait
    // un XPGemCollectFX complet (racine + 4 SpriteRenderer enfants), des centaines de fois par minute en fin de partie (allocations
    // mémoire + créations d'objets, source d'à-coups). Une instance terminée est désactivée et rangée ici au lieu
    // d'être détruite ; une instance détruite avec sa scène (changement de scène) est simplement ignorée au tirage
    // (test `== null` d'Unity).
    // color : couleur de la gemme ramassée. scale : taille de la gemme (Small/Medium/Large) pour proportionner l'éclat.
    public static void Play(Vector3 position, Color color, float scale)
    {
        XPGemCollectFX fx = null;
        while (fx == null && _pool.Count > 0) fx = _pool.Pop();
        if (fx == null)
        {
            GameObject go = new GameObject("XPGemCollectFX");
            fx = go.AddComponent<XPGemCollectFX>();
            fx.Build();
        }
        fx.transform.position = position;
        fx.Init(color, scale);
        fx.gameObject.SetActive(true);
    }

    private static readonly System.Collections.Generic.Stack<XPGemCollectFX> _pool = new System.Collections.Generic.Stack<XPGemCollectFX>();

    // Renderers créés une seule fois par instance (voir la réserve _pool).
    private void Build()
    {
        _flash = FXSprites.MakeRenderer(transform, "Flash", FXSprites.Glow(), 22, 0.15f);
        _sparks = new Spark[SparkCount];
        for (int i = 0; i < SparkCount; i++)
            _sparks[i] = new Spark { sr = FXSprites.MakeRenderer(transform, "Spark", FXSprites.Glow(), 23, 0.12f) };
    }

    private void Init(Color color, float scale)
    {
        _hot = Color.Lerp(color, Color.white, 0.5f);
        _cam = Camera.main != null ? Camera.main.transform : null;

        _t = 0f;
        if (_cam != null) _flash.transform.rotation = _cam.rotation;
        FXSprites.SetFlat(_flash, 0.05f, 0f);
        // Plage recalibrée (2026-10-04, suite 3) sur les nouvelles tailles de gemmes (0,46 a 0,8, au lieu de 0,3 a 0,6).
        _flashBaseSize = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0.46f, 0.8f, scale));

        for (int i = 0; i < SparkCount; i++)
        {
            Spark s = _sparks[i];
            s.sr.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            s.sr.color = Color.clear;
            float angle = (i / (float)SparkCount) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            float speed = Random.Range(1.6f, 2.6f) * Mathf.Lerp(0.8f, 1.3f, Mathf.InverseLerp(0.46f, 0.8f, scale));
            s.vel = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * speed;
            s.size = Random.Range(0.1f, 0.16f);
        }
    }

    private float _flashBaseSize = 0.7f;

    private void Update()
    {
        _t += Time.deltaTime;
        float q = Mathf.Clamp01(_t / Duration);
        float inv = 1f - q;

        float flashSize = Mathf.Lerp(0.15f, _flashBaseSize, FXSprites.EaseOutQuad(Mathf.Min(1f, q * 4f)));
        float s = flashSize / Mathf.Max(0.01f, _flash.sprite.bounds.size.x);
        _flash.transform.localScale = new Vector3(s, s, 1f);
        if (_cam != null) _flash.transform.rotation = _cam.rotation;
        _flash.color = FXSprites.A(_hot, inv * inv);

        for (int i = 0; i < _sparks.Length; i++)
        {
            Spark sp = _sparks[i];
            Vector3 local = sp.sr.transform.localPosition;
            local += sp.vel * Time.deltaTime * (0.5f + 0.5f * inv);
            sp.sr.transform.localPosition = local;
            if (_cam != null) sp.sr.transform.rotation = _cam.rotation;
            float sparkScale = sp.size * (0.4f + 0.6f * inv) / Mathf.Max(0.01f, sp.sr.sprite.bounds.size.x);
            sp.sr.transform.localScale = new Vector3(sparkScale, sparkScale, 1f);
            sp.sr.color = FXSprites.A(_hot, inv);
        }

        if (q >= 1f)
        {
            gameObject.SetActive(false);
            _pool.Push(this);
        }
    }
}
