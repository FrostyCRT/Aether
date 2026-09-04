using UnityEngine;
using System.Collections.Generic;
public class WeaponAura : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damagePerSecond = 80f; // MODIFIE - x10, cf. rescale global des degats/PV
    // MODIFIE - 3.5 -> 4.2 (+20%), rendue plus genereuse suite a la demande de
    // l'utilisateur, en meme temps qu'on lui ajoute enfin un visuel (voir plus bas)
    // qui va la rendre visible pour la premiere fois - autant que le rayon reel
    // corresponde a une taille qui se voit clairement a l'ecran.
    [SerializeField] private float _radius = 6f;
    [SerializeField] private float _tickRate = 0.25f;
    [Header("Ralentissement léger")]
    [SerializeField] private float _slowMultiplier = 0.85f; // -15%
    [SerializeField] private float _minSlowMultiplier = 0.4f;
    private float _tickTimer = 0f;
    private static readonly Collider[] _auraOverlapBuffer = new Collider[64];
    private readonly Dictionary<int, EnemyBase> _currentlySlowed = new Dictionary<int, EnemyBase>();
    private readonly HashSet<int> _inRangeThisTick = new HashSet<int>();

    // AJOUTE - meme correctif que MudPuddleZone : le boss ne recevait que les
    // degats, jamais SetSpeedMultiplier(). Un seul boss actif a la fois en
    // pratique, reference simple plutot que d'etendre le dictionnaire generique.
    private BossBase _currentlySlowedBoss = null;

    // AJOUTE - visuel de zone procedural (anneau via LineRenderer, en espace local
    // donc suit automatiquement le joueur sans recalcul de position chaque frame).
    // Palie l'absence totale d'asset dedie pour l'Aura, signalee plus tot dans le
    // projet : jusqu'ici rien ne montrait jamais ou l'Aura agissait reellement.
    private LineRenderer _auraRingRenderer;
    private const int RingSegments = 48;
    [Header("Visuel (procedural, en attendant un vrai asset)")]
    [SerializeField] private Color _auraRingColor = new Color(0.3f, 0.9f, 0.4f, 0.5f);
    [SerializeField] private float _auraRingWidth = 0.1f;

    // AJOUTE - decalage de centrage, public pour reglage direct en Inspector (ou en
    // Play Mode pour tester vite). Corrige le fait que le centre de l'Aura
    // (transform.position, le pivot du personnage) ne correspond pas forcement au
    // centre visuel de Kael selon comment son modele est rigge. S'applique a la
    // fois au visuel (anneau) et a la zone de degats/ralentissement reelle, pour
    // que les deux restent toujours coherents entre eux.
    public Vector3 _centerOffset = Vector3.zero;

    private void Awake()
    {
        CreateAuraRingVisual();
    }

    // AJOUTE - Aura n'appliquait jamais le bonus de Reputation Degats, meme
    // manque que Fireball et Knives. Utilise Start() plutot que d'etendre
    // Awake() : convention deja suivie par WeaponBase (bonus meta appliques
    // dans Start(), pas Awake()).
    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
        {
            float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
            _damagePerSecond += _damagePerSecond * bonusDamage;
        }
    }

    private void CreateAuraRingVisual()
    {
        GameObject ringGO = new GameObject("AuraRingVisual");
        ringGO.transform.SetParent(transform, false);

        _auraRingRenderer = ringGO.AddComponent<LineRenderer>();
        _auraRingRenderer.useWorldSpace = false; // suit le parent (joueur) automatiquement
        _auraRingRenderer.loop = true;
        _auraRingRenderer.positionCount = RingSegments;
        _auraRingRenderer.widthMultiplier = _auraRingWidth;
        _auraRingRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _auraRingRenderer.startColor = _auraRingColor;
        _auraRingRenderer.endColor = _auraRingColor;

        RedrawAuraRing();
    }

    // AJOUTE - repositionne le centre de l'anneau selon _centerOffset a chaque
    // frame (tres bon marche, juste une assignation de Vector3), pour que les
    // ajustements en Play Mode se voient immediatement sans avoir besoin de
    // rappeler RedrawAuraRing().
    private void LateUpdate()
    {
        if (_auraRingRenderer != null)
            _auraRingRenderer.transform.localPosition = _centerOffset;
    }

    // AJOUTE - ne redessine que quand le rayon change (AddRadius), pas chaque
    // frame - inutile de recalculer 48 points par frame pour une forme qui ne
    // bouge pas tant que le rayon ne change pas.
    private void RedrawAuraRing()
    {
        if (_auraRingRenderer == null) return;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (i / (float)RingSegments) * Mathf.PI * 2f;
            Vector3 point = new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)) * _radius;
            _auraRingRenderer.SetPosition(i, point);
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= _tickRate)
        {
            ApplyAuraTick();
            _tickTimer = 0f;
        }
    }
    private void ApplyAuraTick()
    {
        _inRangeThisTick.Clear();
        // MODIFIE - utilise transform.position + _centerOffset au lieu de
        // transform.position seul, pour que la zone de degats/ralentissement
        // reelle corresponde exactement a ce que montre le visuel (anneau).
        Vector3 center = transform.position + _centerOffset;
        int hitCount = Physics.OverlapSphereNonAlloc(center, _radius, _auraOverlapBuffer);
        float tickDamage = _damagePerSecond * _tickRate;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _auraOverlapBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;
            int enemyId = hit.GetInstanceID();
            if (_inRangeThisTick.Contains(enemyId)) continue;
            _inRangeThisTick.Add(enemyId);
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(tickDamage, DamageNumberSpawner.ColorAOE);
                if (!_currentlySlowed.ContainsKey(enemyId))
                {
                    enemy.SetSpeedMultiplier(_slowMultiplier);
                    _currentlySlowed[enemyId] = enemy;
                }
                continue;
            }
            // MODIFIE - le boss est desormais ralenti EN PLUS d'encaisser les degats.
            BossBase boss = hit.GetComponent<BossBase>();
            if (boss != null)
            {
                boss.TakeDamage(tickDamage);
                boss.SetSpeedMultiplier(_slowMultiplier);
                _currentlySlowedBoss = boss;
            }
        }
        // Restaure la vitesse normale des ennemis sortis du rayon depuis le tick précédent
        List<int> toRemove = null;
        foreach (var kvp in _currentlySlowed)
        {
            if (_inRangeThisTick.Contains(kvp.Key)) continue;
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(1f);
            (toRemove ??= new List<int>()).Add(kvp.Key);
        }
        if (toRemove != null)
            foreach (int id in toRemove) _currentlySlowed.Remove(id);

        // AJOUTE - meme restauration pour le boss.
        if (_currentlySlowedBoss != null)
        {
            int bossId = _currentlySlowedBoss.GetInstanceID();
            if (!_inRangeThisTick.Contains(bossId))
            {
                _currentlySlowedBoss.SetSpeedMultiplier(1f);
                _currentlySlowedBoss = null;
            }
        }
    }
    public void AddDamage(float value) => _damagePerSecond += _damagePerSecond * value;

    // MODIFIE - redessine le visuel de zone a chaque fois que le rayon change.
    public void AddRadius(float value)
    {
        _radius += _radius * value;
        RedrawAuraRing();
    }

    public void AddSlowStrength(float value)
    {
        _slowMultiplier = Mathf.Max(_minSlowMultiplier, _slowMultiplier - value);
        foreach (var kvp in _currentlySlowed)
        {
            if (kvp.Value != null) kvp.Value.SetSpeedMultiplier(_slowMultiplier);
        }
        // AJOUTE - applique aussi au boss deja ralenti ce tick, meme raison que
        // pour les ennemis normaux (renfort visible immediatement).
        if (_currentlySlowedBoss != null)
            _currentlySlowedBoss.SetSpeedMultiplier(_slowMultiplier);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + _centerOffset, _radius);
    }
}