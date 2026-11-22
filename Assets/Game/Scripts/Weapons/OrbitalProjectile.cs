using UnityEngine;
using System.Collections.Generic;

public class OrbitalProjectile : MonoBehaviour
{
    [SerializeField] private float _damage = 15f;
    // AJOUTÉ (2026-10-03) - retour utilisateur : un petit repoussement léger montre qu'un ennemi vient de se faire
    // percuter par l'orbe, au lieu de l'encaisser sans aucune réaction.
    // MODIFIÉ (2026-10-04) - 6 -> 4 (retour : "trop fort"). Voir aussi EnemyBase.MaxKnockbackSpeed : le vrai
    // problème était surtout le cumul entre plusieurs orbes différents, maintenant plafonné séparément.
    [SerializeField] private float _knockbackForce = 4f;
    private readonly float _hitCooldown = 0.5f;

    // Cache des timestamps de dernière frappe : ZÉRO UPDATE requis pour nettoyer
    private readonly Dictionary<int, float> _lastHitTimestamps = new Dictionary<int, float>();

    // AJOUTÉ (2026-10-05) - retour utilisateur : "il faut travailler sur les animations des compétences [...]
    // pour les autres il y a rien" - les orbitaux (Orbitaux, Vortex de Mana, Orbes Foudroyants) n'avaient aucune
    // traînée ni éclat d'impact, contrairement à la Boue/la Foudre. Traînée rose (ColorOrbital, même teinte que
    // les chiffres de dégâts) créée une fois en Awake - jamais recréée, juste vidée à chaque retour au pool.
    private TrailRenderer _trail;

    // AJOUTÉ (2026-11-22, lot 1 des modèles Blender) - pierres runiques modélisées (« pierres runiques » est le nom donné
    // aux orbitaux dans les textes, fidèle à l'icône) : un galet gravé d'une rune lumineuse, 3 variantes (spirale, croissant,
    // arbre) distribuées à tour de rôle. Face gravée vers le haut (vue par la caméra), petite rotation et balancement pour
    // qu'elles aient l'air de flotter. La sphère de la racine reste dans le prefab mais masquée : les fusions copient son
    // matériau comme base de leurs cristaux (PrismaticCrownVFX, BladeWaltzVFX...).
    [Header("Visuel modélisé (facultatif)")]
    [SerializeField] private Transform _visual;
    [SerializeField] private Mesh[] _variants;
    [Tooltip("Matériau de chaque variante (même ordre que _variants) : chaque pierre a sa texture cuite.")]
    [SerializeField] private Material[] _variantMaterials;
    [SerializeField] private float _spinSpeed = 70f;
    [SerializeField] private Material _trailMaterial;
    [Tooltip("Traînée derrière la pierre (2026-11-22, retour utilisateur : « je ne veux pas que ça fasse une traînée » : décoché, la brume rose reste autour de la pierre).")]
    [SerializeField] private bool _showTrail = true;
    [Tooltip("Inclinaison de la pierre (degrés) : vue de dessus, une pierre à plat se lit comme une image 2D ; inclinée, on voit son épaisseur et ses reliefs.")]
    [SerializeField] private float _tilt = 28f;
    private static int _nextVariant;
    private float _phase;

    // (2026-11-22, retour utilisateur : « il faut 5 runes différentes car en tout on peut en avoir 5 ») - la pierre vient
    // d'un pool (réutilisée dans n'importe quel ordre) : c'est WeaponOrbital qui donne la place dans le cercle (0 à 4).
    public void SetVariant(int index)
    {
        if (_visual == null || _variants == null || _variants.Length == 0) return;
        int k = ((index % _variants.Length) + _variants.Length) % _variants.Length;
        if (_visual.TryGetComponent(out MeshFilter mf)) mf.sharedMesh = _variants[k];
        if (_variantMaterials != null && k < _variantMaterials.Length && _variantMaterials[k] != null && _visual.TryGetComponent(out MeshRenderer vr))
            vr.sharedMaterial = GlowingCopy(_variantMaterials[k]);
    }

    // AJOUTÉ (2026-11-22, validé par l'utilisateur après comparaison avant/après) - la lueur rose des gravures (emission map
    // T_RuneStone_X_Glow) ne s'affichait pas : le mot-clé _EMISSION des matériaux URP Lit créés par script n'est pas conservé
    // au chargement (même cause que les spores, voir EnemyProjectile.EmissiveCopy). Une copie par rune, partagée par toutes
    // les pierres, avec l'émission réellement active.
    private static readonly System.Collections.Generic.Dictionary<Material, Material> _glowCopies = new System.Collections.Generic.Dictionary<Material, Material>();
    private static Material GlowingCopy(Material src)
    {
        if (_glowCopies.TryGetValue(src, out Material copy) && copy != null) return copy;
        copy = new Material(src) { name = src.name + " (lueur)" };
        copy.EnableKeyword("_EMISSION");
        copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        _glowCopies[src] = copy;
        return copy;
    }

    // CORRIGÉ (2026-11-22, retour utilisateur : « quand le personnage tourne, ça fait tourner les orbes en même temps sur
    // elles ») - la pierre est enfant du héros (WeaponOrbital) : une rotation LOCALE héritait de chaque demi-tour du héros.
    // Rotation posée dans le repère du MONDE : elle ne dépend plus que du temps. Inclinée (_tilt) et en lente rotation sur
    // l'axe vertical, avec un léger balancement, pour montrer le relief (retour : « on a l'impression que c'est de la 2D »).
    private void Update()
    {
        if (_visual == null) return;
        float t = Time.time + _phase;
        _visual.rotation = Quaternion.AngleAxis(t * _spinSpeed, Vector3.up)
                         * Quaternion.Euler(_tilt + Mathf.Sin(t * 1.3f) * 8f, 0f, Mathf.Cos(t * 1.1f) * 6f);
    }

    private void Awake()
    {
        if (_visual != null)
        {
            Renderer root = GetComponent<MeshRenderer>();
            if (root != null) root.enabled = false;
            SetVariant(_nextVariant++);   // provisoire : WeaponOrbital fixe la vraie rune selon la place dans le cercle
            _phase = Random.value * 10f;
        }
        _trail = gameObject.AddComponent<TrailRenderer>();
        if (_trailMaterial != null)
        {
            // (2026-11-22) traînée de fumée aquarelle (même texture que l'anneau OrbitalAuraVFX) au lieu d'un trait rose uni
            _trail.time = 0.18f;   // petite traînée : à 0,25 s et plus, vu la vitesse d'orbite, elle reliait les pierres en cercle
            _trail.minVertexDistance = 0.05f;
            _trail.widthMultiplier = 0.38f;   // fine : la pierre reste l'élément principal (retour utilisateur)
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f));
            _trail.textureMode = LineTextureMode.Stretch;
            _trail.material = _trailMaterial;
            _trail.startColor = new Color(0.96f, 0.84f, 0.9f, 0.5f);   // rose poudré de l'illustration (pas magenta)
            _trail.endColor = new Color(0.92f, 0.72f, 0.86f, 0f);
        }
        else
        {
            _trail.time = 0.15f;
            _trail.minVertexDistance = 0.04f;
            _trail.widthMultiplier = 0.14f;
            _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            _trail.material = new Material(Shader.Find("Sprites/Default"));
            _trail.startColor = FXSprites.A(DamageNumberSpawner.ColorOrbital, 0.6f);
            _trail.endColor = FXSprites.A(DamageNumberSpawner.ColorOrbital, 0f);
        }
        _trail.enabled = _showTrail;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.sortingOrder = 3;
    }

    public void SetDamage(float damage)
    {
        _damage = damage;
    }

    // AJOUTÉ (2026-10-27, hitbox des boss - voir BossBodyShape et ProjectileBasic._pendingBosses) - un orbe qui entre
    // dans la boîte d'un boss sans toucher son corps attend (OnTriggerStay) de toucher vraiment le corps : un seul coup
    // par passage, comme avant.
    private const float BodyHitMargin = 0.3f;
    private readonly System.Collections.Generic.List<Collider> _pendingBosses = new System.Collections.Generic.List<Collider>(2);

    private void OnTriggerStay(Collider other)
    {
        if (!_pendingBosses.Contains(other)) return;
        if (!other.CompareTag("Enemy")) { _pendingBosses.Remove(other); return; }
        if (other.TryGetComponent(out BossBase boss) && boss.IsOnBody(transform.position, BodyHitMargin))
        {
            _pendingBosses.Remove(other);
            OnTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        _pendingBosses.Remove(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        if (other.TryGetComponent(out BossBase bodyCheck) && !bodyCheck.IsOnBody(transform.position, BodyHitMargin))
        {
            if (!_pendingBosses.Contains(other)) _pendingBosses.Add(other);
            return;
        }

        // Utilisation de l'InstanceID unique (int) au lieu du GameObject complet pour éviter les fuites de mémoire
        int enemyId = other.GetInstanceID();
        float currentTime = Time.time;

        if (_lastHitTimestamps.TryGetValue(enemyId, out float lastHitTime))
        {
            if (currentTime - lastHitTime < _hitCooldown)
            {
                return; // L'ennemi est encore sous le coup du cooldown (I-frame)
            }
        }

        // Buffs dynamiques du joueur (Concentration) lus à l'impact. 1f si inactif.
        float dmg = _damage * PlayerBuffs.OutgoingDamageMultiplier;

        // On cherche d'abord la base commune d'ennemi normal
        other.TryGetComponent(out EnemyBase enemy);
        if (enemy != null)
        {
            enemy.TakeDamage(dmg, DamageNumberSpawner.ColorOrbital);
            // CORRIGÉ (2026-10-03) - retour utilisateur : "il faut que ça repousse à l'extérieur, donc à l'opposé
            // du joueur". Calculer "away" depuis la position de L'ORBE (transform.position) était faux : l'orbe se
            // déplace sur son orbite, sa position par rapport à l'ennemi touché n'a aucun rapport fixe avec la
            // direction réellement voulue (pouvait pousser de travers, voire vers le joueur). WeaponOrbital parente
            // chaque orbe directement au joueur (orbital.transform.SetParent(transform) dans SpawnOrbitals) - le
            // centre voulu est donc transform.parent.position, pas transform.position.
            Vector3 center = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 away = other.transform.position - center;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) enemy.ApplyKnockback(away.normalized * _knockbackForce);
            ImpactSparkVFX.Spawn(transform.position, DamageNumberSpawner.ColorOrbital);
            _lastHitTimestamps[enemyId] = currentTime;
            return;
        }

        // Si ce n'est pas un ennemi basique, on check le Boss
        other.TryGetComponent(out BossBase boss);
        if (boss != null)
        {
            boss.TakeDamage(dmg, DamageNumberSpawner.ColorOrbital);
            ImpactSparkVFX.Spawn(transform.position, DamageNumberSpawner.ColorOrbital);
            _lastHitTimestamps[enemyId] = currentTime;
        }
    }

    // Si le projectile est désactivé ou replacé dans un Object Pool, on nettoie son dictionnaire
    private void OnDisable()
    {
        _lastHitTimestamps.Clear();
        _pendingBosses.Clear();
    }

    // AJOUTÉ (2026-10-05) - sans ça, un orbe recyclé depuis le pool dessinerait une traînée reliant sa position
    // précédente (ailleurs sur la carte) à sa nouvelle position de réapparition - un trait parasite traversant
    // tout l'écran. Vidée aussi à l'activation initiale (Awake peut arriver avant le premier SetActive(true)).
    private void OnEnable()
    {
        if (_trail != null) _trail.Clear();
        _clearTrailFrames = 2;
    }

    // AJOUTÉ (2026-11-22) - la pierre sort du pool AU CENTRE du héros (WeaponOrbital.SpawnOrbitals) puis saute sur l'orbite à
    // la frame suivante : la traînée dessinait ce saut (traits droits du héros vers chaque pierre, bien visibles avec la
    // traînée de fumée). On la vide encore pendant les 2 premières frames, une fois la pierre posée sur l'orbite.
    private int _clearTrailFrames;

    // (2026-11-22) brume propre à chaque pierre (ParticleSystem enfants « Mist »/« Glitter », émis à la distance) : même
    // nettoyage que la traînée, sinon le saut du centre vers l'orbite sèmerait une ligne de brume.
    private ParticleSystem[] _fx;

    private void LateUpdate()
    {
        if (_clearTrailFrames <= 0) return;
        _clearTrailFrames--;
        if (_trail != null) _trail.Clear();
        if (_fx == null) _fx = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in _fx) ps.Clear(true);
    }
}