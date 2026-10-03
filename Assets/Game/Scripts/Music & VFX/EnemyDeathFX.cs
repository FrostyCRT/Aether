using UnityEngine;

// AJOUTÉ (2026-10-03) - mort d'un ennemi NORMAL (EnemyBase.Die), jusqu'ici instantanée et totalement muette
// (ReturnToPool sans le moindre retour visuel) alors que c'est l'événement le plus fréquent de toute la partie
// (des centaines de fois par run, parfois plusieurs par seconde en pleine horde). Même famille procédurale que
// BossFX/GroundTelegraphFX/ChargeLaneFX (FXSprites, aucune image à importer), mais VOLONTAIREMENT beaucoup plus
// léger : un flash bref + une petite gerbe de 5 éclats, ~0,3 s, pas de ralenti ni de pilier de lumière. Pensé pour
// être appelé en rafale sans jamais peser sur le framerate.
public class EnemyDeathFX : MonoBehaviour
{
    private const int SparkCount = 5;
    private const float Duration = 0.32f;

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

    // color : couleur dominante de l'ennemi (reprend sa teinte de sprite si connue, sinon un blanc chaud neutre).
    public static void Play(Vector3 position, Color color)
    {
        GameObject go = new GameObject("EnemyDeathFX");
        go.transform.position = position;
        EnemyDeathFX fx = go.AddComponent<EnemyDeathFX>();
        fx.Init(color);
    }

    private void Init(Color color)
    {
        _hot = Color.Lerp(color, Color.white, 0.6f);
        _cam = Camera.main != null ? Camera.main.transform : null;

        _flash = FXSprites.MakeRenderer(transform, "Flash", FXSprites.Glow(), 20, 0.15f);
        if (_cam != null) _flash.transform.rotation = _cam.rotation;
        FXSprites.SetFlat(_flash, 0.05f, 0f);

        _sparks = new Spark[SparkCount];
        for (int i = 0; i < SparkCount; i++)
        {
            Spark s = new Spark();
            s.sr = FXSprites.MakeRenderer(transform, "Spark", FXSprites.Glow(), 21, 0.12f);
            float angle = (i / (float)SparkCount) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            float speed = Random.Range(2.2f, 3.6f);
            s.vel = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * speed;
            s.size = Random.Range(0.16f, 0.26f);
            _sparks[i] = s;
        }
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float q = Mathf.Clamp01(_t / Duration);
        float inv = 1f - q;

        // Flash : pop immédiat, fondu rapide.
        float flashSize = Mathf.Lerp(0.3f, 1.1f, FXSprites.EaseOutQuad(Mathf.Min(1f, q * 3f)));
        float s = flashSize / Mathf.Max(0.01f, _flash.sprite.bounds.size.x);
        _flash.transform.localScale = new Vector3(s, s, 1f);
        if (_cam != null) _flash.transform.rotation = _cam.rotation;
        _flash.color = FXSprites.A(_hot, inv * inv);

        // Éclats : partent du centre, ralentissent, rétrécissent en s'évanouissant.
        for (int i = 0; i < _sparks.Length; i++)
        {
            Spark sp = _sparks[i];
            Vector3 local = sp.sr.transform.localPosition;
            local += sp.vel * Time.deltaTime * (0.4f + 0.6f * inv);
            sp.sr.transform.localPosition = local;
            if (_cam != null) sp.sr.transform.rotation = _cam.rotation;
            float sparkScale = sp.size * (0.5f + 0.5f * inv) / Mathf.Max(0.01f, sp.sr.sprite.bounds.size.x);
            sp.sr.transform.localScale = new Vector3(sparkScale, sparkScale, 1f);
            sp.sr.color = FXSprites.A(_hot, inv * inv);
        }

        if (q >= 1f) Destroy(gameObject);
    }
}
