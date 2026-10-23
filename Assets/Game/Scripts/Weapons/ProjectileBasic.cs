using UnityEngine;
public class ProjectileBasic : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _speed = 10f;
    [SerializeField] private float _maxRange = 15f;
    // AJOUTE - expose la vitesse du projectile en lecture seule, pour que WeaponBase
    // puisse calculer un temps d'impact estime et anticiper la position d'une
    // cible en mouvement (voir EnemyBase.CurrentVelocity).
    public float Speed => _speed;

    private float _damage;
    private Vector3 _startPosition;
    private Vector3 _direction;
    private float _maxRangeSqr;
    private string _poolKey;
    // AJOUTÉ (2026-10-04) - ce même script sert au tir de base ET à Boule de Feu ET aux Couteaux (seul le prefab
    // visuel change) : jusqu'ici tous les trois teintaient leurs dégâts en `ColorProjectile`, indifféremment. Une
    // couleur par arme, injectée par l'appelant (WeaponBase/WeaponFireball/WeaponShurikenBarrage/fusions), au lieu
    // d'un hardcode unique dans OnTriggerEnter.
    private Color _damageColor = DamageNumberSpawner.ColorProjectile;
    private float _fragmentChance = 0f;
    private float _fragmentDamage = 0f;
    private float _fragmentRadius = 2f;
    // AJOUTÉ (2026-10-21, retour utilisateur : "revoir la forme de l'explosion [...] ça doit être très
    // reconnaissable [que c'est une déflagration de boule de feu]") - ce script est PARTAGÉ par le tir de base
    // (dont le nœud Réputation "Fragmentation" peut AUSSI déclencher une explosion, voir WeaponBase.FireProjectile)
    // ET la Boule de feu : sans ce drapeau, donner à l'explosion générique l'allure "feu" de FireExplosionVFX
    // aurait aussi fait flamber les explosions du tir de base de Kael/Lyra, qui n'ont rien de thématiquement
    // "feu". Seul WeaponFireball le met à true (voir FireProjectile) ; remis à false à chaque Init() comme tous
    // les autres drapeaux de ce fichier, pour qu'un projectile recyclé par le pool ne garde jamais l'effet visuel
    // d'un tir précédent tiré par une autre arme.
    private bool _useFireExplosionVisual = false;
    public void SetFireExplosionVisual(bool enabled) => _useFireExplosionVisual = enabled;
    private static readonly Collider[] _overlapBuffer = new Collider[32];
    // Piercing (Shuriken)
    private bool _hasPiercing = false;
    private int _maxPierceCount = 1;
    private int _currentPierceHits = 0;

    // AJOUTE - Brulure (palier 3 Fireball). 0 = pas de brulure sur ce tir.
    private float _burnDamagePerSecond = 0f;
    private float _burnDuration = 0f;

    // AJOUTE (2026-09-27) - Ricochet (fusion "Lames Ricochet" : Couteaux + Orbe rebondissant). Une fois le budget de
    // perforation épuisé, le projectile se redirige vers l'ennemi non touché le plus proche au lieu de disparaître,
    // jusqu'à épuiser son nombre de rebonds. Générique : n'importe quel ProjectileBasic peut en profiter.
    private bool _hasRicochet = false;
    private int _ricochetsLeft = 0;
    private float _ricochetRange = 10f;
    private readonly System.Collections.Generic.List<int> _hitInstanceIds = new System.Collections.Generic.List<int>(4);
    private static readonly Collider[] _ricochetBuffer = new Collider[32];

    // AJOUTÉ (2026-10-05) - retour utilisateur : "il faut travailler sur les animations des compétences [...]
    // pour les autres il y a rien" - ProjectileBasic sert au tir de base ET Boule de Feu ET Couteaux ET Lames
    // Ricochet (fusion), donc une seule traînée ici profite à 4 armes/fusions d'un coup. Couleur = _damageColor
    // (déjà par-arme, voir plus haut), donc chaque traînée reste identifiable comme les chiffres de dégâts.
    private TrailRenderer _trail;

    private void Awake()
    {
        _maxRangeSqr = _maxRange * _maxRange;
        _poolKey = name.Replace("(Clone)", "").Trim();

        // Largeur relevée (2026-10-05) - voir note équivalente dans BouncingOrbProjectile.cs : un trait trop fin
        // devient quasi invisible vu l'angle de caméra plongeant du jeu, surtout sur une trajectoire rectiligne
        // qui peut se retrouver presque "de face" par rapport à la caméra.
        _trail = gameObject.AddComponent<TrailRenderer>();
        _trail.time = 0.12f;
        _trail.minVertexDistance = 0.05f;
        _trail.widthMultiplier = 0.2f;
        _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        _trail.material = new Material(Shader.Find("Sprites/Default"));
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.sortingOrder = 3;
    }
    // color : optionnel - défaut ColorProjectile (tir de base/Double Tir). Fireball/Knives/fusions passent la leur.
    public void Init(Vector3 direction, float damage, Color? color = null)
    {
        _direction = direction.normalized;
        _startPosition = transform.position;
        _damage = damage;
        _damageColor = color ?? DamageNumberSpawner.ColorProjectile;
        _fragmentChance = 0f;
        _useFireExplosionVisual = false;
        _hasPiercing = false;
        _currentPierceHits = 0;
        // AJOUTE - reset systematique, meme raison que _fragmentChance juste au-dessus :
        // sans ca, un tir sans brulure pourrait heriter de la brulure du tir precedent
        // si l'objet vient d'etre recycle depuis le pool.
        _burnDamagePerSecond = 0f;
        _burnDuration = 0f;
        _hasRicochet = false;
        _ricochetsLeft = 0;
        _hitInstanceIds.Clear();

        _trail.Clear(); // sans ça, un projectile recyclé depuis le pool dessinerait une traînée depuis sa position précédente
        _trail.startColor = FXSprites.A(_damageColor, 0.7f);
        _trail.endColor = FXSprites.A(_damageColor, 0f);
    }

    // AJOUTE - Ricochet (voir plus haut). bounceCount = nombre de rebonds APRÈS la perforation initiale.
    public void SetRicochet(int bounceCount, float range)
    {
        _hasRicochet = bounceCount > 0;
        _ricochetsLeft = bounceCount;
        _ricochetRange = range;
    }

    public void SetFragmentation(float chance, float damage, float radius)
    {
        _fragmentChance = Mathf.Clamp01(chance);
        _fragmentDamage = damage;
        _fragmentRadius = radius;
    }

    // AJOUTE - appele par WeaponFireball quand le palier 3 (Brulure) est debloque.
    public void SetBurn(float damagePerSecond, float duration)
    {
        _burnDamagePerSecond = damagePerSecond;
        _burnDuration = duration;
    }

    public void SetPiercing(bool active, int maxPierceCount)
    {
        _hasPiercing = active;
        _maxPierceCount = Mathf.Max(1, maxPierceCount);
    }
    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;
        float distanceTravelledSqr = (transform.position - _startPosition).sqrMagnitude;
        if (distanceTravelledSqr >= _maxRangeSqr)
        {
            ReturnToPool();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        ApplyDamage(other, _damage, _damageColor, false);
        ApplyBurnIfNeeded(other);
        ImpactSparkVFX.Spawn(transform.position, _damageColor);
        _hitInstanceIds.Add(other.GetInstanceID());

        if (_fragmentChance > 0f && Random.value < _fragmentChance)
        {
            // AJOUTE - anneau d'impact visuel (procedural, aucun asset requis) pour
            // que le joueur voie concretement la zone d'explosion au moment ou elle
            // se declenche - sans ca, l'explosion n'a jamais eu la moindre existence
            // visuelle, seuls les chiffres de degats des cibles secondaires trahissaient
            // qu'il s'etait passe quelque chose.
            // MODIFIÉ (2026-10-21, retour utilisateur : "revoir la forme de l'explosion [...] très reconnaissable")
            // - la Boule de feu (seule arme à mettre _useFireExplosionVisual à true, voir WeaponFireball) utilise
            // désormais une vraie déflagration enflammée (FireExplosionVFX) à la place du simple anneau générique,
            // qui reste inchangé pour le nœud Réputation "Fragmentation" du tir de base (aucun rapport avec le feu).
            if (_useFireExplosionVisual)
                FireExplosionVFX.Spawn(transform.position, _fragmentRadius);
            else
                ExpandingRingVFX.Spawn(transform.position, _fragmentRadius, new Color(1f, 0.5f, 0.15f, 0.85f), 0.35f);
            // AJOUTÉ (2026-10-05) - retour utilisateur : "il faut travailler sur les animations des compétences".
            // L'anneau seul marque bien la LIMITE de la zone, mais manquait de "punch" au centre, au moment pile
            // de l'explosion. Éclat agrandi (même composant que les impacts normaux, juste plus gros).
            ImpactSparkVFX.Spawn(transform.position, DamageNumberSpawner.ColorFireball, 2f);
            // AJOUTÉ (2026-10-05) - secousse de caméra modérée : une explosion doit se SENTIR, pas seulement se
            // voir. Réservé à Boule de feu (l'un des rares "gros impacts" ponctuels du jeu) - jamais sur les
            // armes à cadence élevée, pour ne pas secouer l'écran en permanence en plein combat.
            CameraShakeFX.Shake(0.35f);

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _fragmentRadius, _overlapBuffer, EnemyLayers.Mask);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                // AJOUTE (2026-10-20) - "hit != other" suffisait tant qu'un projectile ne pouvait exploser qu'UNE
                // fois dans sa vie (premier et seul impact). Un projectile perforant (Boule de feu, voir
                // WeaponFireball) explose maintenant a CHAQUE ennemi traverse : sans cette verification en plus,
                // un ennemi deja touche DIRECTEMENT par un impact precedent (donc deja dans _hitInstanceIds)
                // pouvait reprendre un 2e coup de degats via l'explosion d'un impact suivant tout proche.
                if (hit != null && hit.CompareTag("Enemy") && hit != other && !_hitInstanceIds.Contains(hit.GetInstanceID()))
                {
                    ApplyDamage(hit, _fragmentDamage, DamageNumberSpawner.ColorCritical, true);
                    ApplyBurnIfNeeded(hit);
                }
            }
        }
        if (_hasPiercing)
        {
            _currentPierceHits++;
            if (_currentPierceHits < _maxPierceCount)
                return;
        }

        if (_hasRicochet && _ricochetsLeft > 0 && TryRicochet())
            return;

        ReturnToPool();
    }

    // Cherche l'ennemi le plus proche PAS ENCORE TOUCHÉ dans _ricochetRange et redirige le projectile vers lui.
    // Renvoie false (et laisse ReturnToPool faire son travail) si aucune cible n'est trouvée.
    private bool TryRicochet()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _ricochetRange, _ricochetBuffer, EnemyLayers.Mask);
        Transform nearest = null;
        float minDistSqr = _ricochetRange * _ricochetRange;
        for (int i = 0; i < count; i++)
        {
            Collider hit = _ricochetBuffer[i];
            if (hit == null || !hit.CompareTag("Enemy")) continue;
            if (_hitInstanceIds.Contains(hit.GetInstanceID())) continue;
            float distSqr = (hit.transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = hit.transform;
            }
        }
        if (nearest == null) return false;

        Vector3 dir = nearest.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return false;

        _direction = dir.normalized;
        _ricochetsLeft--;
        _currentPierceHits = 0;              // budget de perforation renouvelé après chaque rebond
        _startPosition = transform.position;  // la portée max repart de ce point, pas de repartir à vide
        // AJOUTÉ (2026-10-05) - retour utilisateur : "il faut toujours qu'on voit ce qu'il se passe" - sans ça, un
        // ricochet (changement de cible en cours de vol) était invisible, seule la trajectoire qui tourne le
        // trahissait. Éclat un peu plus grand que l'impact normal pour marquer le changement de cap.
        ImpactSparkVFX.Spawn(transform.position, _damageColor, 1.4f);
        return true;
    }

    // AJOUTE - applique la Brulure sur une cible touchee (directe ou via l'explosion),
    // uniquement sur les EnemyBase pour l'instant (BossBase n'a pas encore cette API -
    // a etendre plus tard si besoin d'appliquer la Brulure sur les boss).
    private void ApplyBurnIfNeeded(Collider target)
    {
        if (_burnDamagePerSecond <= 0f) return;
        EnemyBase enemy = target.GetComponent<EnemyBase>();
        if (enemy != null) enemy.ApplyBurn(_burnDamagePerSecond, _burnDuration);
    }

    private void ApplyDamage(Collider target, float damage, Color numberColor, bool isAOE)
    {
        EnemyBase enemy = target.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, numberColor);
            return;
        }
        // CORRIGÉ (2026-10-04) - retour utilisateur : "sur le boss ça fait la même couleur avec un point
        // d'exclamation... je veux qu'on fasse comme avec les ennemis de base". La branche ennemi juste au-dessus
        // passe bien `numberColor` (donc Fireball/Couteaux/tir de base ressortent chacun avec leur couleur sur un
        // ennemi normal) - cette branche boss l'ignorait et retombait toujours sur la couleur par défaut de
        // `BossBase.TakeDamage` (orange, critique). Résultat : tous les coups de CES armes sur un boss fusionnaient
        // ensemble sous la même couleur/clé, au lieu de rester séparés par arme comme sur un ennemi normal.
        BossBase boss = target.GetComponent<BossBase>();
        if (boss != null)
        {
            boss.TakeDamage(damage, numberColor);
        }
    }
    private void ReturnToPool()
    {
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool(_poolKey, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}