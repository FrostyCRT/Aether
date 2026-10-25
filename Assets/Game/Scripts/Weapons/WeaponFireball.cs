using UnityEngine;
using System.Collections;
public class WeaponFireball : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _damage = 200f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _fireRate = 0.5f; // 1 tir toutes les 2 secondes
    [SerializeField] private float _detectionRange = 15f;

    // MODIFIE - l'explosion a l'impact est desormais TOUJOURS garantie (100%), plus
    // une chance aleatoire. C'est l'identite meme de l'arme : "impact qui explose",
    // pas "impact qui peut parfois exploser". La progression se fait maintenant sur
    // le RAYON (palier 1) et les degats (palier 2), la Brulure venant en palier 3.
    [Header("Explosion (toujours declenchee)")]
    [Tooltip("Degats de l'explosion en fraction des degats du tir principal (0.4 = 40%).")]
    [SerializeField] private float _fragmentDamageRatio = 0.4f;
    [SerializeField] private float _fragmentRadius = 2.25f; // MODIFIE - 1.5 -> 2.25 (x1.5), rayon de base juge trop petit en jeu

    // AJOUTE (2026-10-20, retour utilisateur : "revoir la boule de feu [...] une boule tout de suite plus grosse
    // qui peut toucher plusieurs ennemis en meme temps, mini explosions de feu derriere, brulure - comme le Q de
    // Smolder dans LoL") - avant ca, le projectile s'arretait net au TOUT PREMIER ennemi touche (ReturnToPool
    // immediat) : l'explosion a l'impact touchait bien les voisins, mais le trait lui-meme ne traversait jamais la
    // cible pour continuer sa route. Desormais perforant : chaque ennemi traverse declenche sa PROPRE explosion
    // (ProjectileBasic.SetFragmentation est deja appele a chaque OnTriggerEnter, pas seulement au premier - la
    // perforation suffit a elle seule a creer "des mini explosions de feu derriere" tout le long du trajet, sans
    // toucher a ProjectileBasic). Le rayon max (20) est tres au-dessus de ce qu'une salve peut reellement croiser
    // en un seul trajet (portee du projectile = 15, voir ProjectileFireball.prefab) - sert juste a ne jamais couper
    // la perforation avant la fin reelle de la trajectoire.
    [SerializeField] private int _maxPierceTargets = 20;

    // AJOUTE - Brulure, palier 3. Desactivee tant que le palier n'est pas pris.
    [Header("Brulure (palier 3)")]
    [SerializeField] private bool _burnEnabled = false;
    [Tooltip("Degats de brulure par seconde, en fraction des degats du tir principal au moment du tir (0.15 = 15%/s).")]
    [SerializeField] private float _burnDamagePerSecondRatio = 0.15f;
    [SerializeField] private float _burnDuration = 3f;

    // AJOUTÉ (2026-10-24, retour utilisateur : "son ciblage c'est comme le tir de base, et du coup c'est répétitif
    // [...] que ça focus là où il y a le plus d'ennemis à chaque fois, dans une certaine range autour du joueur") -
    // la Boule de feu ne vise plus l'ennemi le plus proche mais le centre du plus gros groupe dans _detectionRange
    // (voir FindDensestClusterPoint). Ce rayon définit ce qu'est "un groupe" : les ennemis à moins de cette distance
    // d'un ennemi candidat comptent avec lui. Proche du rayon d'explosion de base (2,25) pour que le groupe visé
    // soit à peu près celui que la déflagration peut toucher.
    [Header("Ciblage (plus gros groupe d'ennemis)")]
    [SerializeField] private float _clusterRadius = 3f;

    private float _cooldownTimer = 0f;
    // MODIFIÉ (2026-10-24) - 50 -> 128 : le ciblage par groupe doit voir toute la foule à portée (jusqu'à ~180
    // ennemis sur la carte en fin de partie), pas seulement les 50 premiers colliders renvoyés.
    private static readonly Collider[] _detectionBuffer = new Collider[128];
    private static readonly Vector3[] _candidatePositions = new Vector3[128];

    // AJOUTE - Fireball n'appliquait jamais le bonus de Reputation Degats,
    // contrairement a WeaponBase qui le fait deja dans son propre Start().
    private void Start()
    {
        if (MetaProgressionManager.Instance != null)
        {
            float bonusDamage = MetaProgressionManager.Instance.GetReputationBonusDamage();
            _damage += _damage * bonusDamage;
        }
    }

    // SUPPRIME - Double Tir retire de cette arme (voir note de session : un tir
    // lourd a explosion garantie n'a pas besoin de se dupliquer, Double Tir reste
    // reserve au tir de base sur WeaponBase). Si UpgradeData.cs essaie encore
    // d'appeler UnlockDoubleShot()/IsDoubleShotUnlocked() ici, la compilation
    // echouera a cet endroit precis - c'est volontaire, ca localise le bug de
    // ciblage signale plutot que de le corriger a l'aveugle sans voir ce fichier.

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;
        _cooldownTimer += Time.deltaTime;
        float cooldownDuration = _fireRate > 0f ? (1f / _fireRate) : 9999f;
        if (_cooldownTimer >= cooldownDuration)
        {
            if (FindDensestClusterPoint(out Vector3 aimPoint))
            {
                Vector3 direction = aimPoint - transform.position;
                direction.y = 0f;

                // AJOUTÉ (2026-10-21, retour utilisateur : "si on est collé dans un angle de 2 murs invisible, et
                // qu'on tire une boule de feu, la boule de feu reste coincé dans l'angle, et ne se casse pas") -
                // root cause : aucune garde ici contre un vecteur direction quasi nul. Dans un coin de carte, les
                // ennemis s'entassent contre le joueur (ils ne peuvent pas le contourner) - si le plus proche se
                // retrouve à une distance quasi nulle, `direction.Normalize()` d'un vecteur (quasi) nul renvoie
                // silencieusement (0,0,0) (aucune erreur Unity) : le projectile tiré avec cette direction ne bouge
                // ensuite plus jamais (`transform.position += direction * speed * dt` ne fait rien à chaque frame),
                // ET ne peut jamais atteindre `_maxRange` pour se désactiver tout seul - visuellement "coincé pour
                // toujours". Si la cible est trop proche pour donner une direction fiable, on saute ce tir (sans
                // consommer le temps de recharge, donc réessayé dès la frame suivante) plutôt que d'en tirer un qui
                // ne pourra jamais se résoudre.
                if (direction.sqrMagnitude < 0.01f) return;

                direction.Normalize();
                FireProjectile(direction);
                _cooldownTimer = 0f;
            }
        }
    }

    // Pour chaque ennemi à portée, compte les ennemis à moins de _clusterRadius de lui ; le plus gros compte gagne
    // (à égalité, le plus proche du joueur, pour ne pas viser un groupe lointain quand un aussi gros est tout près).
    // Le point visé est le CENTRE de ce groupe (moyenne des positions), pas l'ennemi candidat lui-même : la boule
    // perforante traverse alors le cœur de la masse. Un seul ennemi isolé à portée = visé directement, comme avant.
    // Coût : au plus 128² comparaisons de distances, une fois par tir (toutes les ~2 s), négligeable.
    private bool FindDensestClusterPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRange, _detectionBuffer, EnemyLayers.Mask);
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            Collider col = _detectionBuffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            Vector3 pos = col.transform.position;
            pos.y = 0f;
            _candidatePositions[n++] = pos;
        }
        if (n == 0) return false;

        Vector3 origin = transform.position;
        origin.y = 0f;
        float clusterRadiusSqr = _clusterRadius * _clusterRadius;
        int bestCount = -1;
        float bestDistSqr = float.MaxValue;
        Vector3 bestCenter = Vector3.zero;
        Vector3 bestCandidate = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Vector3 candidate = _candidatePositions[i];
            Vector3 sum = Vector3.zero;
            int neighbours = 0;
            for (int j = 0; j < n; j++)
            {
                if ((_candidatePositions[j] - candidate).sqrMagnitude > clusterRadiusSqr) continue;
                sum += _candidatePositions[j];
                neighbours++;
            }
            float distSqr = (candidate - origin).sqrMagnitude;
            if (neighbours > bestCount || (neighbours == bestCount && distSqr < bestDistSqr))
            {
                bestCount = neighbours;
                bestDistSqr = distSqr;
                bestCenter = sum / neighbours;
                bestCandidate = candidate;
            }
        }
        // Groupe qui entoure le joueur : son centre peut tomber sur le joueur lui-même (direction indéfinie, voir la
        // garde dans Update) - on vise alors l'ennemi qui définit le groupe.
        aimPoint = (bestCenter - origin).sqrMagnitude < 1f ? bestCandidate : bestCenter;
        aimPoint.y = transform.position.y;
        return true;
    }
    private void FireProjectile(Vector3 direction)
    {
        if (ObjectPool.Instance == null) return;

        GameObject projectileGO = ObjectPool.Instance.Get("ProjectileFireball", transform.position, Quaternion.identity);
        if (projectileGO == null) return;
        projectileGO.TryGetComponent(out ProjectileBasic projectile);
        if (projectile != null)
        {
            // Buffs dynamiques (Concentration) : appliqués à l'impact direct, à
            // l'explosion ET à la Brûlure — tout ça, c'est "tes dégâts". Vaut 1f
            // si aucun buff actif.
            float dmg = _damage * PlayerBuffs.OutgoingDamageMultiplier;

            projectile.Init(direction, dmg, DamageNumberSpawner.ColorFireball);

            // MODIFIE - explosion toujours a 100%, plus de chance meta/carte a combiner.
            projectile.SetFragmentation(1f, dmg * _fragmentDamageRatio, _fragmentRadius);

            // AJOUTÉ (2026-10-21, retour utilisateur : "revoir la forme de l'explosion [...] très reconnaissable
            // [comme une déflagration de boule de feu]") - voir ProjectileBasic._useFireExplosionVisual : remplace
            // l'anneau générique par FireExplosionVFX (flammes + braises + fumée), réservé à cette arme.
            projectile.SetFireExplosionVisual(true);

            // AJOUTE - perforation : traverse les ennemis au lieu de s'arreter au premier (voir commentaire sur
            // _maxPierceTargets plus haut).
            projectile.SetPiercing(true, _maxPierceTargets);

            if (_burnEnabled)
            {
                projectile.SetBurn(dmg * _burnDamagePerSecondRatio, _burnDuration);
            }
        }
    }

    // MODIFIE - palier 1 : augmente le rayon d'explosion plutot que la chance de
    // fragmentation (qui n'existe plus en tant que variable, l'explosion etant fixe).
    public void AddFragmentRadius(float value) => _fragmentRadius += value;

    // AJOUTE - palier 3 : debloque la Brulure.
    public void EnableBurn() => _burnEnabled = true;

    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    // Lu par UpgradeData.ApplyFusionResult() pour hériter des dégâts déjà accumulés au moment d'une fusion.
    public float CurrentDamage => _damage;
}