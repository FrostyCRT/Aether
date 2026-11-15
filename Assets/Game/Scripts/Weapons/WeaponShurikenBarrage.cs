using UnityEngine;
public class WeaponShurikenBarrage : MonoBehaviour
{
    [Header("Stats")]
    // MODIFIÉ (2026-11-15, retour utilisateur : « Lyra ne tue pas assez vite, elle n'a que des couteaux, le buff de dégâts
    // n'était pas ce qui faisait la différence » ; choix A + C + D de l'utilisateur) - l'arme tirait RAREMENT (1 salve
    // toutes les 2,5 s) et EN LIGNE, alors qu'Aether (explosions) et Kael (aura permanente) touchent une zone : rien ne
    // nettoyait autour de Lyra entre deux salves. Maintenant :
    //  A. salve 2x plus fréquente (0,4 -> 0,8 / s), couteau un peu moins fort (180 -> 150), perforation 2 -> 3 ;
    //  C. 3 couteaux de base en éventail ; 4 au palier « +1 couteau » : 2 côte à côte au centre (presque x2 sur la cible
    //     visée, idée de l'utilisateur) + 1 de chaque côté ;
    //  D. couteaux boomerang : arrivés au bout de leur aller (ou à court de perforation), ils reviennent vers Lyra et
    //     peuvent toucher une 2e fois (ProjectileBasic.SetBoomerang).
    // Les valeurs d'avant restent la référence de la fusion Lames Ricochet (voir FusionInheritedDamage).
    // Réglage mesuré (arme seule au maximum, 4:30 en Violent, 45 s dans la horde) : 2,4-2,6 kills/s contre 2,8 pour la
    // Boule de feu d'Aether et 1,3 pour l'Aura de Kael (avant : ~1,8 avec 110 / 0,65) ; Lame fantôme et explosion du
    // clone en plus en vraie partie.
    [SerializeField] private float _damage = 150f;
    [SerializeField] private float _fireRate = 0.8f;
    private const float LegacyBaseDamage = 180f, NewBaseDamage = 150f;
    [SerializeField] private float _detectionRange = 15f;

    // MODIFIE - reduit de 3 a 2 par couteau : la salve tire maintenant PLUSIEURS
    // couteaux simultanement (voir _knifeCount ci-dessous), donc chacun perce un
    // peu moins pour ne pas cumuler une perforation totale demesuree des le depart.
    [SerializeField] private int _maxPierceCount = 3;

    // AJOUTE - c'est le coeur de la nouvelle identite "salve" : plusieurs couteaux
    // partent en meme temps en eventail, plutot qu'un seul tir precis comme le tir
    // de base. Base = 2, +1 au palier 1 de la carte.
    [Header("Salve (eventail)")]
    [SerializeField] private int _knifeCount = 3;
    [Tooltip("Ecart angulaire entre deux couteaux adjacents de la salve, en degres.")]
    [SerializeField] private float _fanAngleSpread = 18f;
    [Tooltip("Nombre pair de couteaux : écart latéral de la paire centrale (les deux partent droit sur la cible).")]
    [SerializeField] private float _centerPairOffset = 0.28f;

    [Header("Boomerang")]
    [Tooltip("Distance de l'aller avant le retour vers Lyra.")]
    [SerializeField] private float _boomerangOutDistance = 9f;
    // AJOUTÉ (2026-11-15, retour utilisateur : les cartes « +35 % cadence » et « +2 perforations » ne lui plaisaient pas)
    // - paliers 2 et 3 de la carte : Portée (aller plus long) et Lames larges (couteaux plus gros = bande de touche plus large).
    [SerializeField] private float _knifeSizeScale = 1f;

    private float _cooldownTimer = 0f;
    private static readonly Collider[] _detectionBuffer = new Collider[50];

    // AJOUTE - alterne le cote du couteau "surnumeraire" (celui qui ne forme pas
    // une paire symetrique complete, ex: le 2e couteau sur une salve de base a 2
    // couteaux) d'un tir a l'autre, pour rester equilibre visuellement sur la
    // duree plutot que de toujours tirer du meme cote.

    // AJOUTE - Knives n'appliquait jamais le bonus de Reputation Degats, meme
    // manque que Fireball et Aura.
    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
        {
            float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
            _damage += _damage * bonusDamage;
        }
    }

    // SUPPRIME - Double Tir retire de cette arme. Une salve deja multi-projectiles
    // n'a pas besoin de se dupliquer en plus - Double Tir reste reserve au tir de
    // base sur WeaponBase. Si UpgradeData.cs essaie encore d'appeler
    // UnlockDoubleShot()/IsDoubleShotUnlocked() ici, la compilation echouera a cet
    // endroit precis - volontaire, ca localise le bug de ciblage signale.

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;
        _cooldownTimer += Time.deltaTime;
        float cooldownDuration = _fireRate > 0f ? (1f / (_fireRate * MetaProgressionManager.CadenceMultiplier)) : 9999f; // Cadence (arbre)
        if (_cooldownTimer >= cooldownDuration)
        {
            Transform target = FindNearestEnemy();
            if (target != null)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;

                // AJOUTÉ (2026-10-21) - même root cause que WeaponFireball.Update() (voir son commentaire détaillé) :
                // un ennemi collé au joueur dans un coin de carte donne une direction quasi nulle, et
                // `.Normalize()` d'un vecteur (quasi) nul renvoie silencieusement (0,0,0) - un projectile tiré
                // ainsi resterait coincé pour toujours (ne bouge plus, n'atteint jamais sa portée max).
                if (direction.sqrMagnitude < 0.01f) return;

                direction.Normalize();
                FireSalvo(direction);
                _cooldownTimer = 0f;
            }
        }
    }

    // MODIFIÉ (2026-11-15) - disposition (choix de l'utilisateur) :
    //  - nombre IMPAIR : 1 couteau pile au centre + des paires symétriques à ±18°, ±36°... (3 de base = centre, ±18°) ;
    //  - nombre PAIR : 2 couteaux côte à côte au centre (décalés de ±0,28 sur le côté, tous deux droit sur la cible
    //    verrouillée : presque x2 sur elle) + des paires à ±18°, ±36°... (4 = paire centrale, ±18°).
    private void FireSalvo(Vector3 centerDirection)
    {
        Vector3 side = Vector3.Cross(Vector3.up, centerDirection).normalized;
        int pairs;
        if (_knifeCount % 2 == 1)
        {
            FireProjectile(centerDirection, Vector3.zero);
            pairs = (_knifeCount - 1) / 2;
        }
        else
        {
            FireProjectile(centerDirection, side * _centerPairOffset);
            FireProjectile(centerDirection, -side * _centerPairOffset);
            pairs = (_knifeCount - 2) / 2;
        }
        for (int p = 1; p <= pairs; p++)
        {
            float angle = _fanAngleSpread * p;
            FireProjectile(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection, Vector3.zero);
            FireProjectile(Quaternion.AngleAxis(-angle, Vector3.up) * centerDirection, Vector3.zero);
        }
    }

    private Transform FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer, EnemyLayers.Mask);
        Transform nearest = null;
        float minDistSqr = _detectionRange * _detectionRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = col.transform;
            }
        }
        return nearest;
    }
    private void FireProjectile(Vector3 direction, Vector3 offset)
    {
        if (ObjectPool.Instance == null) return;
        GameObject projectileGO = ObjectPool.Instance.Get("ProjectileShuriken", transform.position + offset, Quaternion.identity);
        if (projectileGO == null) return;
        projectileGO.TryGetComponent(out ProjectileBasic projectile);
        if (projectile != null)
        {
            projectile.Init(direction, _damage * PlayerBuffs.OutgoingDamageMultiplier, DamageNumberSpawner.ColorKnives); // + Concentration / Surpuissance (2026-10-16, audit)
            projectile.SetPiercing(true, _maxPierceCount);
            projectile.SetBoomerang(transform, _boomerangOutDistance);
            if (_knifeSizeScale != 1f) projectile.SetSizeScale(_knifeSizeScale);
        }
    }
    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    // Lu par UpgradeData.ApplyFusionResult() pour hériter des dégâts déjà accumulés au moment d'une fusion.
    public float CurrentDamage => _damage;
    // Fusion Lames Ricochet : réglée sur les couteaux d'avant le 2026-11-15 (180 de base) ; elle hérite de la même valeur
    // qu'avant pour ne pas devenir plus faible parce que le couteau seul a baissé (il tire plus souvent et revient).
    public float FusionInheritedDamage => _damage * (LegacyBaseDamage / NewBaseDamage);

    // AJOUTE - palier 1 : +1 couteau a la salve.
    public void AddKnife() => _knifeCount += 1;
    public int KnifeCount => _knifeCount;   // info-bulle du menu Pause (UpgradeData.GetCurrentStateDescription)

    // Appele par le palier 3 de la carte Couteaux
    public void AddPierce(int amount) => _maxPierceCount += Mathf.Max(1, amount);

    // Palier 2 de la carte : les couteaux vont plus loin avant de revenir (+value mètres) ; la détection suit.
    public void AddRange(float meters)
    {
        _boomerangOutDistance += meters;
        _detectionRange = Mathf.Max(_detectionRange, _boomerangOutDistance + 4f);
    }
    public float OutDistance => _boomerangOutDistance;

    // Palier 3 de la carte : couteaux plus gros (x value).
    public void SetKnifeSize(float scale) => _knifeSizeScale = Mathf.Max(1f, scale);
    public float KnifeSize => _knifeSizeScale;
}