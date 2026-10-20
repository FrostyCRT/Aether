using UnityEngine;

// AJOUTÉ (2026-10-19, session esthétique - sol/décor + VFX/éclairage, retour utilisateur : "le sol est moche...
// il n'y a jamais de petits détails") - particules d'ambiance légères (poussière de mana flottante) qui dérivent
// en permanence autour du joueur, teintées selon le palier de difficulté (DifficultyTiers.AmbientMoteColor) pour
// renforcer l'identité "Fantasy Médiéval avec magie et mana" - AUCUN effet de jeu, purement visuel. Couleurs
// volontairement HDR (> 1 par canal) pour accrocher le Bloom (voir GameAmbienceProfile/GlobalVolume_Ambience,
// vérifié en Play : un sprite à couleur HDR via Sprites/Default produit bien un halo) sans aucun nouveau
// shader/texture - même matériau que le reste de FXSprites (ImpactSparkVFX, ExpandingRingVFX, BossFX).
//
// Contrairement aux VFX de combat (pool à la demande, voir ExpandingRingVFX), ici un nombre FIXE de motes vit en
// permanence pendant toute la partie - chaque mote boucle sur son propre cycle apparition/dérive/disparition et se
// RÉIMPLANTE SEULEMENT PENDANT QU'ELLE EST INVISIBLE (alpha 0), autour de la position ACTUELLE du joueur : aucune
// téléportation visible, fonctionne aussi bien sur une carte infinie (Modéré, X du Violent) qu'une boîte fermée
// (Apocalyptique) sans aucune notion de "tronçon" (inutile ici, contrairement à InfiniteDecorStreamer : pas de
// géométrie persistante à charger/décharger, juste une couche visuelle qui se réécrit sans arrêt).
public class AmbientManaMotes : MonoBehaviour
{
    [Header("Densité (mise à l'échelle par Graphismes > Qualité)")]
    [Tooltip("Nombre de motes visées au préréglage de qualité le plus haut (index 2, Haute Fidélité).")]
    [SerializeField] private int _baseMoteCount = 45;
    [Tooltip("Facteur multiplicatif de _baseMoteCount par index de qualité (0 Performant, 1 Équilibré, 2 Haute Fidélité).")]
    [SerializeField] private float[] _countFactorByQuality = { 0.3f, 0.65f, 1f };

    [Header("Zone de dérive (autour du joueur)")]
    [SerializeField] private float _areaHalfX = 16f;
    [SerializeField] private float _areaHalfZ = 12f;
    [SerializeField] private float _heightMin = 0.3f;
    [SerializeField] private float _heightMax = 3.2f;

    [Header("Cycle de vie")]
    [SerializeField] private float _minLifetime = 5f;
    [SerializeField] private float _maxLifetime = 9f;
    [SerializeField] private float _fadeDuration = 0.8f;

    [Header("Apparence")]
    [SerializeField] private float _minDiameter = 0.12f;
    [SerializeField] private float _maxDiameter = 0.3f;
    // RALENTI (2026-10-19, retour utilisateur : "les lueurs blanches bougent trop rapidement") - montée ÷3 (0,33 ->
    // 0,11) et balancement latéral ÷3 (0,3-0,6 -> 0,1-0,2 tr/s) pour une dérive bien plus paresseuse, cohérente avec
    // de la poussière de mana plutôt que des insectes.
    [SerializeField] private float _driftRiseSpeed = 0.11f;
    [SerializeField] private float _swayAmplitude = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.8f;

    private struct Mote
    {
        public SpriteRenderer sr;
        public Vector3 basePos;
        public float life;
        public float lifetime;
        public float swayFreq;
        public float swayPhase;
        public float unitScale;
        public float brightness;
    }

    private Mote[] _motes;
    private Transform _player;
    // CORRIGÉ (2026-10-20, retour utilisateur : "on pourrait voir un peu de mana bleu sur la map, on en voit
    // jamais, alors que ça fait partie intégrante de la DA") - ces motes reprenaient jusqu'ici la teinte de
    // DifficultyTiers.AmbientMoteColor (la même que la lumière directionnelle du palier : or chaud en Modéré,
    // ambre en Violent, violet en Apocalyptique) - AUCUNE n'est bleue, donc le joueur ne voyait jamais de mana
    // bleu nulle part sur la carte, alors que le bleu EST la couleur établie de la "mana" partout ailleurs dans le
    // jeu (flash de l'Ultime/Cristal - voir GameUI.UltVfxColor, éclats de mana des props de sol). Teinte fixe,
    // la MÊME que l'Ultime, réutilisée ici pour la même identité "mana" - volontairement DÉCOUPLÉE du palier de
    // difficulté : la mana est un élément constant de la DA, pas une ambiance qui doit changer avec le palier.
    private static readonly Color ManaBlue = new Color(0.55f, 0.85f, 1f);
    private const float ManaBlueBoost = 2.6f; // même boost HDR que DifficultyTiers.AmbientMoteColor, pour accrocher le Bloom

    private Color _tintColor = Color.white;

    private void Start()
    {
        GameObject playerGo = GameObject.FindWithTag("Player");
        if (playerGo == null)
        {
            enabled = false;
            return;
        }
        _player = playerGo.transform;

        _tintColor = new Color(ManaBlue.r * ManaBlueBoost, ManaBlue.g * ManaBlueBoost, ManaBlue.b * ManaBlueBoost, 1f);

        int qualityIndex = Mathf.Clamp(GameSettings.GetInt(GameSettings.Quality), 0, _countFactorByQuality.Length - 1);
        int count = Mathf.Max(0, Mathf.RoundToInt(_baseMoteCount * _countFactorByQuality[qualityIndex]));

        _motes = new Mote[count];
        Sprite glow = FXSprites.Glow();
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = FXSprites.MakeRenderer(transform, "Mote" + i, glow, 5, 0f);
            _motes[i] = SpawnMote(sr, Random.Range(0f, 1f));
        }
    }

    // startLifeFraction : permet d'étaler les motes sur des phases de vie différentes à l'apparition initiale
    // (sinon toutes nées à life=0 clignoteraient en fondu ensemble, visible au lancement).
    private Mote SpawnMote(SpriteRenderer sr, float startLifeFraction)
    {
        float diameter = Random.Range(_minDiameter, _maxDiameter);
        float scale = diameter / Mathf.Max(0.01f, sr.sprite.bounds.size.x);

        Mote m = new Mote
        {
            sr = sr,
            basePos = _player.position + new Vector3(Random.Range(-_areaHalfX, _areaHalfX), _heightMin, Random.Range(-_areaHalfZ, _areaHalfZ)),
            lifetime = Random.Range(_minLifetime, _maxLifetime),
            swayFreq = Random.Range(0.1f, 0.2f),
            swayPhase = Random.Range(0f, Mathf.PI * 2f),
            unitScale = scale,
            brightness = Random.Range(0.85f, 1.15f)
        };
        m.life = startLifeFraction * m.lifetime;

        sr.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        sr.transform.localScale = Vector3.one * scale;

        return m;
    }

    private void Update()
    {
        if (_player == null || _motes == null) return;

        for (int i = 0; i < _motes.Length; i++)
            UpdateMote(ref _motes[i]);
    }

    private void UpdateMote(ref Mote m)
    {
        m.life += Time.deltaTime;
        if (m.life >= m.lifetime)
        {
            m = SpawnMote(m.sr, 0f);
            return;
        }

        float riseHeight = m.life * _driftRiseSpeed;
        float sway = m.life * m.swayFreq * Mathf.PI * 2f + m.swayPhase;
        Vector3 pos = m.basePos + new Vector3(Mathf.Sin(sway) * _swayAmplitude, riseHeight, Mathf.Cos(sway) * _swayAmplitude);
        m.sr.transform.position = pos;

        float fadeIn = Mathf.Clamp01(m.life / _fadeDuration);
        float fadeOut = Mathf.Clamp01((m.lifetime - m.life) / _fadeDuration);
        float alpha = Mathf.Min(fadeIn, fadeOut) * _maxAlpha;

        float pulse = 1f + Mathf.Sin(m.life * 1f + m.swayPhase) * 0.12f;
        m.sr.transform.localScale = Vector3.one * m.unitScale * pulse;

        Color c = _tintColor * m.brightness;
        c.a = alpha;
        m.sr.color = c;
    }
}
