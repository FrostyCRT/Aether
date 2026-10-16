using UnityEngine;

// FUSION (2026-09-27) : Couteaux + Orbe rebondissant, toutes les deux au palier max → cette arme les remplace [Lyra].
// Reprend la salve en éventail des Couteaux (même prefab poolé "ProjectileShuriken") mais chaque lame, une fois son
// budget de perforation épuisé, RICOCHE vers l'ennemi non touché le plus proche au lieu de disparaître (voir
// ProjectileBasic.SetRicochet/TryRicochet, capacité générique ajoutée pour cette fusion).
public class WeaponFusionRicochetBlades : MonoBehaviour
{
    // MODIFIE (2026-09-27) - mesuré en jeu (retour utilisateur : "la fusion doit être plus forte que les 2 upgrades
    // card au max") : la fusion d'origine ne reprenait QUE la salve des Couteaux et perdait entièrement la
    // contribution de l'Orbe rebondissant, qui elle est un mécanisme "toujours actif" (plusieurs orbes qui rebondissent
    // en continu dans l'arène, pas une salve périodique) - remplacer les 2 armes par une seule salve de couteaux,
    // même enrichie du ricochet, ne compensait pas cette perte (mesuré à seulement 1,14x les 2 cartes maxées
    // ensemble). Cadence et nombre de lames augmentés + bonus de dégâts explicite pour combler cet écart - retesté
    // à ~4x après ce changement (voir NOTES.md).
    // REVERTE (2026-10-07, retour utilisateur : "je ne sais pas pourquoi tu as ramené la taille des lames à 2,
    // remet comme c'est au max" + "ça doit être aussi fort que couteaux au max, et orbes rebondissant au max") -
    // la réduction du 2026-10-05 (nombre de lames 5→2, dégâts ×1,3→×0,65) n'était pas demandée et rendait la fusion
    // plus faible que les 2 cartes sources maxées, contrairement à l'objectif explicite du design d'origine
    // (voir plus haut, ~4x mesuré). Lames et dégâts remis à leurs valeurs maximales.
    [Header("Stats (héritées des Couteaux + de l'Orbe rebondissant)")]
    [SerializeField] private float _fireRate = 0.65f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private int _knifeCount = 5;
    [SerializeField] private float _fanAngleSpread = 15f;
    // CORRIGÉ (2026-10-06, retour utilisateur : "la fusion lame ricochet ne fait pas le rebond entre ennemi
    // actuellement en jeu") - mesuré en jeu (10 ennemis dispersés, pas groupés) : avec perforation=2, une lame doit
    // transpercer 2 ennemis ALIGNÉS avant même d'essayer de ricocher (voir ProjectileBasic.OnTriggerEnter, le
    // ricochet n'est vérifié qu'une fois le budget de perforation épuisé) - une condition rarement remplie avec des
    // ennemis dispersés (3 ricochets observés sur ~16 lames tirées en 6 s). Perforation ramenée à 1 : dès le 1er
    // ennemi touché, la lame tente immédiatement un ricochet vers un 2e - beaucoup plus facile à satisfaire, et
    // fidèle au nom de la fusion (le ricochet doit être le comportement visible, pas un cas rare).
    [SerializeField] private int _pierceCount = 1;
    [SerializeField] private int _ricochetCount = 2;
    // MODIFIE (2026-10-07, retour utilisateur : "ça ricoche pas assez entre les ennemis") - 9 restait trop court
    // pour trouver une 2e cible avec des ennemis dispersés (voir TryRicochet, ProjectileBasic.cs - plus proche
    // ennemi PAS ENCORE touché dans ce rayon). Aligné sur _detectionRange (15, la portée à laquelle l'arme choisit
    // déjà sa cible initiale) : pas de raison que le rebond soit nettement plus myope que le tir initial.
    [SerializeField] private float _ricochetRange = 15f;

    private float _damage;
    private float _cooldownTimer = 0f;
    private bool _nextOddFlankOnRight = true;
    private static readonly Collider[] _detectionBuffer = new Collider[50];

    public void Init(float inheritedDamage)
    {
        _damage = inheritedDamage * 1.3f;
    }

    public void AddDamage(float value) => _damage += _damage * value;
    public void AddFireRate(float value) => _fireRate += _fireRate * value;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;
        _cooldownTimer += Time.deltaTime;
        float cooldownDuration = _fireRate > 0f ? (1f / _fireRate) : 9999f;
        if (_cooldownTimer >= cooldownDuration)
        {
            Transform target = FindNearestEnemy();
            if (target != null)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;
                direction.Normalize();
                FireSalvo(direction);
                _cooldownTimer = 0f;
            }
        }
    }

    private void FireSalvo(Vector3 centerDirection)
    {
        FireBlade(centerDirection);

        int extra = _knifeCount - 1;
        if (extra <= 0) return;

        int pairCount = extra / 2;
        bool hasOddFlank = extra % 2 == 1;

        for (int p = 1; p <= pairCount; p++)
        {
            float angle = _fanAngleSpread * p;
            FireBlade(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);
            FireBlade(Quaternion.AngleAxis(-angle, Vector3.up) * centerDirection);
        }
        if (hasOddFlank)
        {
            float angle = _fanAngleSpread * (pairCount + 1) * (_nextOddFlankOnRight ? 1f : -1f);
            FireBlade(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);
            _nextOddFlankOnRight = !_nextOddFlankOnRight;
        }
    }

    private void FireBlade(Vector3 direction)
    {
        if (ObjectPool.Instance == null) return;
        GameObject go = ObjectPool.Instance.Get("ProjectileShuriken", transform.position, Quaternion.identity);
        if (go == null) return;
        ProjectileBasic projectile = go.GetComponent<ProjectileBasic>();
        if (projectile == null) return;
        projectile.Init(direction, _damage * PlayerBuffs.OutgoingDamageMultiplier, DamageNumberSpawner.ColorKnives); // + Concentration / Surpuissance (2026-10-16, audit)
        projectile.SetPiercing(true, _pierceCount);
        projectile.SetRicochet(_ricochetCount, _ricochetRange);
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
            if (distSqr < minDistSqr) { minDistSqr = distSqr; nearest = col.transform; }
        }
        return nearest;
    }
}
