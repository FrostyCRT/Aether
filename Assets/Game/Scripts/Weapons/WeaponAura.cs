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
    // MODIFIÉ (2026-10-06, retour utilisateur : "il faudrait que ça soit la même range que pour la boue") - aligné
    // sur WeaponMudPuddle._spawnDistance (6.5, la distance à laquelle les flaques de boue apparaissent autour du
    // joueur), pour que les 2 effets au sol donnent la même sensation de portée autour du personnage.
    [SerializeField] private float _radius = 6.5f;
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
    [Header("Visuel (procedural, en attendant un vrai asset Tripo)")]
    [SerializeField] private Color _auraRingColor = new Color(0.3f, 0.9f, 0.4f, 0.5f);
    [SerializeField] private float _auraRingWidth = 0.1f;
    // AJOUTÉ (2026-10-06, "hiérarchie Y des effets au sol") - même logique que WeaponMudPuddle._groundY /
    // CrystalSystem._novaGroundY : le pivot du joueur (transform.position.y) est à hauteur de torse (~1,5), pas au
    // sol. L'anneau/disque de l'Aura étaient enfants directs du joueur avec un simple petit décalage LOCAL (0,05 /
    // 0,03) jamais compensé - ils flottaient donc réellement à ~1,5-1,53 de hauteur (mesuré en jeu), pas au sol où
    // se trouvent la Boue et la Nova (~0,2) : au lieu d'une zone au sol, un disque suspendu en l'air au niveau du
    // torse du joueur.
    [SerializeField] private float _ringGroundY = 0.05f;
    [SerializeField] private float _fillGroundY = 0.03f;

    // AJOUTÉ (2026-10-03) - retour utilisateur : "la zone de l'aura je la trouve neutre, c'est juste un cercle
    // vert". Disque plein très doux (présence de la zone, pas juste son contour) avec une légère pulsation calée
    // sur le rythme des dégâts (_tickRate - on SENT le tick). Toujours dans la teinte verte de Kael
    // (_auraRingColor, réglable) - jamais une couleur différente de celle déjà établie pour cette branche.
    // RETIRÉ (2026-10-04) - un 2e essai avait ajouté un anneau gradué tournant (FXSprites.TickRing) par-dessus pour
    // un look "glyphe magique" ; retour utilisateur : exactement le même motif que le portail d'invocation du
    // mini-boss (boss 3, SummonPortalFX) ET le même que les zones de danger/warning rouge (GroundTelegraphFX,
    // qui utilise aussi TickRing) - un cercle gradué qui tourne autour du joueur se lit comme un danger/une
    // invocation, pas comme un buff. Plutôt que de chercher encore un nouveau motif procédural générique (le
    // risque de retomber sur un symbole déjà pris ailleurs reste entier), on s'arrête là en attendant un vrai
    // visuel dédié (modèle 3D ou image 2D générée) : le disque + contour existant restent neutres et ne
    // ressemblent à rien d'autre dans le jeu. Bénéfice secondaire : un calque alpha en moins, GRAND (jusqu'à
    // ~36 unités de diamètre une fois le rayon maxé) et permanent tout au long de la partie une fois l'Aura prise
    // - un poste de surcoût GPU (overdraw transparent) réel qui disparaît avec lui.
    private SpriteRenderer _auraFill;
    private float _auraPulse;

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
        _auraRingRenderer.sortingOrder = 12; // au-dessus du disque/anneau gradué, même hiérarchie que GroundTelegraphFX

        RedrawAuraRing();

        // AJOUTÉ (2026-10-03) - disque de remplissage très doux : la zone a enfin une présence au sol, pas
        // seulement un contour. Teinte de _auraRingColor.
        _auraFill = FXSprites.MakeRenderer(transform, "AuraFillVisual", FXSprites.Disc(), 10, _fillGroundY - transform.position.y);
        FXSprites.SetFlat(_auraFill, _radius * 2f, 0f);
    }

    // AJOUTE - repositionne le centre de l'anneau selon _centerOffset a chaque
    // frame (tres bon marche, juste une assignation de Vector3), pour que les
    // ajustements en Play Mode se voient immediatement sans avoir besoin de
    // rappeler RedrawAuraRing().
    private void LateUpdate()
    {
        // AJOUTÉ (2026-10-06, "hiérarchie Y des effets au sol") - transform.position.y = hauteur de torse du
        // joueur (~1,5), pas le sol. Convertit les hauteurs sol voulues (_ringGroundY/_fillGroundY) en décalage
        // LOCAL par rapport à ce pivot, recalculé chaque frame (au cas où la hauteur du joueur bougerait un jour,
        // ex. terrain non plat) - même raisonnement que CrystalSystem.NovaRoutine / WeaponMudPuddle._groundY.
        float toGround = -transform.position.y;

        if (_auraRingRenderer != null)
        {
            Vector3 ringLocalPos = _centerOffset;
            ringLocalPos.y = _ringGroundY + toGround;
            _auraRingRenderer.transform.localPosition = ringLocalPos;
            // CORRIGÉ (2026-10-03) - retour utilisateur : "la rotation du joueur quand il bouge impact aussi
            // l'aura, donc ça fait tout bouger". Ces 3 objets sont enfants du joueur (pour suivre sa position
            // automatiquement) mais héritaient AUSSI de sa rotation (il tourne pour faire face à son déplacement) -
            // le glyphe graduć (non symétrique) sursautait donc à chaque changement de direction, en plus de sa
            // propre rotation lente voulue. Fixé en assignant `.rotation` (MONDE, pas localRotation) à chaque frame
            // APRÈS le Update() du joueur : ça annule exactement la rotation du parent quelle qu'elle soit, la
            // zone reste à plat et fixe dans l'espace monde, seule _auraTickAngle continue de la faire tourner.
            _auraRingRenderer.transform.rotation = Quaternion.identity;
        }
        if (_auraFill == null) return;

        _auraFill.transform.localPosition = new Vector3(_centerOffset.x, _fillGroundY + toGround, _centerOffset.z);
        _auraFill.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Légère pulsation calée sur _tickRate, pour que l'intensité visuelle respire au même rythme que les
        // dégâts réels plutôt que de rester figée.
        _auraPulse = _tickRate > 0f ? Mathf.PingPong(_tickTimer / _tickRate, 1f) : 0f;
        float pulse = 0.85f + 0.15f * (1f - _auraPulse);
        _auraFill.color = FXSprites.A(_auraRingColor, 0.10f * pulse);
    }

    // AJOUTE - ne redessine que quand le rayon change (AddRadius), pas chaque
    // frame - inutile de recalculer 48 points par frame pour une forme qui ne
    // bouge pas tant que le rayon ne change pas.
    private void RedrawAuraRing()
    {
        if (_auraRingRenderer == null) return;
        for (int i = 0; i < RingSegments; i++)
        {
            // Y=0 ici : la hauteur au sol est maintenant appliquée une seule fois, sur le parent (ringGO), dans
            // LateUpdate() - éviter de l'appliquer aussi ici empêche un double décalage.
            float angle = (i / (float)RingSegments) * Mathf.PI * 2f;
            Vector3 point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _radius;
            _auraRingRenderer.SetPosition(i, point);
        }

        // AJOUTÉ (2026-10-03) - le disque de remplissage doit suivre le rayon réel (AddRadius en jeu), sinon il se
        // désynchroniserait visuellement de la zone de dégâts réelle.
        if (_auraFill != null) FXSprites.SetFlat(_auraFill, _radius * 2f, 0f);
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
        int hitCount = Physics.OverlapSphereNonAlloc(center, _radius, _auraOverlapBuffer, EnemyLayers.Mask);
        float tickDamage = _damagePerSecond * _tickRate * PlayerBuffs.OutgoingDamageMultiplier; // + Concentration / Surpuissance (2026-10-16, audit)
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
            BossBase boss = hit.GetComponentInParent<BossBase>();
            if (boss != null)
            {
                // CORRIGE (2026-09-24) - voir MudPuddleZone : la restauration compare l'ID du composant BossBase,
                // pas celui du collider -> le boss était remis à vitesse normale dans le même tick.
                if (!_inRangeThisTick.Add(boss.GetInstanceID())) continue; // déjà traité ce tick (plusieurs colliders)
                boss.TakeDamage(tickDamage, DamageNumberSpawner.ColorAOE);
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
    // AJOUTE (2026-09-27) - meme correctif que WeaponOrbital.OnDestroy() : AuraRingVisual est un GameObject créé à
    // la main (new GameObject), jamais nettoyé par Unity quand ce composant est détruit (ex: fusion ManaVortex).
    // Sans ça, l'anneau restait un enfant orphelin fixe du joueur pour le reste de la partie.
    private void OnDestroy()
    {
        if (_auraRingRenderer != null) Destroy(_auraRingRenderer.gameObject);
        // AJOUTÉ (2026-10-03) - même raison que _auraRingRenderer ci-dessus : objet créé à la main, jamais
        // nettoyé automatiquement par Unity quand ce composant disparaît (ex: fusion ManaVortex).
        if (_auraFill != null) Destroy(_auraFill.gameObject);
    }

    public void AddDamage(float value) => _damagePerSecond += _damagePerSecond * value;

    // Lu par UpgradeData.ApplyFusionResult() pour hériter des dégâts déjà accumulés au moment d'une fusion.
    public float CurrentDamagePerSecond => _damagePerSecond;

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