using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _maxHealth = 300f; // MODIFIE - x10, cf. rescale global des degats/PV

    // AJOUTE - coche cette case sur les ennemis volants (ex: le Corbeau). Utilise
    // par les effets de zone au sol (Boue) pour accorder une immunite totale -
    // logique puisqu'un volant ne touche jamais le sol. Champ generique plutot
    // qu'un script dedie au Corbeau, pour rester reutilisable si d'autres ennemis
    // volants sont ajoutes plus tard, sans rien casser pour tous les ennemis
    // existants (false par defaut, aucun changement de comportement pour eux).
    [Header("Vol")]
    [SerializeField] private bool _isFlying = false;
    public bool IsFlying => _isFlying;

    // CORRIGÉ (2026-10-21, retour utilisateur : "comme tu as recentré le corbeau, il est maintenant à la même
    // hauteur que les autres ennemis, sauf que le corbeau est un oiseau volant donc il doit être en l'air") - le
    // correctif de spawn au sol (suite 28, voir EnemySpawner.FindFreeSpawnPosition) force Y=0 pour TOUS les
    // ennemis, y compris les volants, qui n'ont jamais eu d'altitude propre. Ajouté UNIQUEMENT pour les ennemis
    // _isFlying (0 = aucun effet pour tous les ennemis au sol existants) - EnemySpawner ajoute cette valeur à la
    // position Y après le spawn au sol habituel.
    [SerializeField] private float _flightAltitude = 0f;
    public float FlightAltitude => _flightAltitude;

    [Header("Drops")]
    [SerializeField] private float _xpValue = 10f;
    [SerializeField] private int _goldValue = 2;

    [Header("Pool")]
    [SerializeField] private string _poolTag = "Enemy";
    public string PoolTag => _poolTag;

    [Header("Distance Joueur")]
    [SerializeField] private float _playerContactRadius = 1.2f;

    [Header("Dégâts de contact")]
    [SerializeField] private float _contactDamage = 150f; // MODIFIE - x10, cf. rescale global des degats/PV
    [SerializeField] private float _contactDamageCooldown = 0.5f;
    // AJOUTÉ (2026-10-28, retour utilisateur : "fais un délai par ennemi pour les dégâts de contact") - chaque ennemi a
    // son propre délai entre deux coups (avant : un seul délai global de 0,5 s côté joueur, donc traverser une horde de
    // 50 ennemis faisait aussi mal qu'en toucher un seul). Remis à zéro à chaque sortie du pool (OnEnable).
    private float _nextContactHitTime;

    [Header("Scaling difficulté")]
    private float _healthMultiplier = 1f;
    // AJOUTÉ (2026-10-27, retour utilisateur : "je veux qu'on doive mettre une attaque de plus pour en tuer un") - PV
    // ajoutés tels quels après le multiplicateur (palier de temps x difficulté), environ un tir de base : le même
    // "un tir de plus" pour un loup comme pour un golem, quelle que soit la difficulté. Posé par ObjectPool.Get.
    private float _bonusHealth = 0f;

    // AJOUTÉ (2026-10-20, retour utilisateur : "il y a trop d'xp en difficulté 3, j'suis à 5min sur les 15min et je
    // suis déjà niveau 18") - _healthMultiplier inclut maintenant le palier DE DIFFICULTÉ (x1/1,25/1,5, voir
    // EnemySpawner.SpawnEnemy) EN PLUS du palier de temps normal (progression dans la partie + Sans Fin au-delà de
    // 15 min) - et Die() (plus bas) fait suivre l'XP à la MOITIÉ de la croissance de _healthMultiplier (voir le
    // commentaire du 2026-10-11 sur xpHealthMultiplier). Un ennemi Apocalyptique touché par les 2 facteurs à la
    // fois gagnait donc un bonus d'XP PERMANENT lié au palier de difficulté, par-dessus la courbe de temps déjà
    // calibrée - ce n'était jamais l'intention ("plus difficile" ne doit pas vouloir dire "on monte plus vite", ça
    // viderait le sens du palier). _xpHealthMultiplier reçoit uniquement la partie TEMPORELLE (palier de vague +
    // Sans Fin), sans le facteur de difficulté - la difficulté reste plus dure à tuer, sans rapporter plus d'XP.
    // -1 = jamais fixé explicitement (chemins de spawn hors EnemySpawner, ex. tests) : Die() retombe alors sur
    // _healthMultiplier en entier, comportement d'avant ce correctif.
    private float _xpHealthMultiplier = -1f;
    public void SetXpHealthMultiplier(float multiplier) => _xpHealthMultiplier = multiplier;

    // AJOUTÉ (2026-10-21, retour utilisateur : "je suis niveau 16 alors qu'on est qu'à 5 min [...] c'est pas
    // normal") - multiplicateur DIRECT sur l'XP finale, appliqué APRÈS la formule amortie de Die() (ci-dessous),
    // jamais mélangé à _xpHealthMultiplier qui passe par "1+(x-1)*0,5" - y injecter une correction l'aurait réduite
    // de moitié par erreur. Compense le surplus de kills/minute d'un palier de difficulté qui fait spawner plus
    // d'ennemis (voir EnemySpawner.SpawnEnemy), sans quoi "plus difficile" finit aussi par vouloir dire "on monte
    // plus vite" - jamais l'intention. 1 = pas de correction (comportement par défaut, chemins hors EnemySpawner).
    private float _xpPacingMultiplier = 1f;
    public void SetXpPacingMultiplier(float multiplier) => _xpPacingMultiplier = multiplier;

    // AJOUTE - systeme de Brulure (palier 3 Fireball). Un seul stack actif a la
    // fois : reapplique = redemarre la duree plutot que d'additionner plusieurs
    // brulures en parallele, pour eviter un scaling degats hors de controle si le
    // joueur touche le meme ennemi plusieurs fois pendant qu'il brule deja.
    [Header("Brulure (visuel + degats sur la duree)")]
    [SerializeField] private float _burnTickInterval = 0.5f;
    [Tooltip("Intensite du pulse de teinte orange sur le sprite pendant la brulure (0 = invisible, 1 = teinte plein orange).")]
    [SerializeField] private float _burnTintIntensity = 0.55f;
    private static readonly Color BurnTintColor = new Color(1f, 0.45f, 0.12f);
    private static readonly Color BurnDamageNumberColor = new Color(1f, 0.55f, 0.15f);
    private Coroutine _burnCoroutine;
    private SpriteRenderer _spriteRenderer;
    private Color _spriteBaseColor = Color.white;
    private bool _spriteBaseColorCaptured = false;

    // AJOUTÉ (2026-10-03) - flash de dégâts : jusqu'ici, un coup reçu n'avait AUCUN retour sur le modèle lui-même (seul
    // le chiffre de DamageNumberSpawner signalait quoi que ce soit). Un bref pulse donne du poids à CHAQUE tir, pas
    // seulement au kill (déjà couvert par EnemyDeathFX). Via MaterialPropertyBlock - jamais de Material instancié,
    // aucun risque de fuite mémoire avec le pooling.
    //
    // CORRIGÉ (2026-10-03) - retour utilisateur : "je ne vois presque aucune différence". Deux bugs trouvés en
    // inspectant le vrai Renderer en Play (capture + lecture directe des propriétés du shader) :
    //  1) GetComponentInChildren<Renderer>() attrapait le MeshRenderer placeholder du prefab d'ORIGINE
    //     (desactive, jamais affiché) plutôt que le vrai SkinnedMeshRenderer importé de Tripo - le flash
    //     s'appliquait donc sur un objet invisible. → on cherche maintenant explicitement un SkinnedMeshRenderer
    //     en priorité, un Renderer "enabled" en repli.
    //  2) Le roster utilise DEUX familles de shader : "Universal Render Pipeline/Lit" (Goblin/Golem - propriétés
    //     _BaseColor/_EmissionColor) ET "Shader Graphs/glTF-pbrMetallicRoughness" pour les modèles importés de
    //     Tripo (Loup, boss... - propriétés baseColorFactor/emissiveFactor, noms standard glTF, PAS les noms
    //     Unity). _EmissionColor ne fait RIEN sur ce 2e shader (aucune erreur, juste aucun effet - c'est ce qui
    //     rendait le flash invisible). Et même sur le 1er shader, l'émission dépend d'un mot-clé _EMISSION pas
    //     forcément actif sur le matériau. → on pulse maintenant la COULEUR DE BASE (toujours active, sur
    //     n'importe quel shader, jamais de mot-clé optionnel à activer), en écrivant les DEUX noms de propriété
    //     à chaque frame (celui qui n'existe pas sur le shader du matériau est silencieusement ignoré, sans
    //     erreur - sans risque à laisser les deux). NOTE : ce même bug touche très probablement la lueur de rage
    //     d'EnemyKaiju (_emissionColorProperty = "_EmissionColor" sur un Renderer Tripo) - pas corrigé ici, à
    //     vérifier séparément si besoin.
    private const float HitFlashDuration = 0.12f;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int GltfBaseColorID = Shader.PropertyToID("baseColorFactor");
    private Renderer _hitFlashRenderer;
    private MaterialPropertyBlock _hitFlashPropBlock;
    private float _hitFlashTimer;
    protected virtual bool SuppressHitFlash => false;

    // Pas de Renderer trouvé (configuration inattendue) : on ne retente pas à chaque coup, coûteux pour rien.
    private bool _hitFlashUnavailable;

    private Renderer FindVisibleRenderer()
    {
        SkinnedMeshRenderer skinned = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinned != null) return skinned;

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r.enabled) return r;

        return null;
    }

    // AJOUTÉ (2026-10-03) - teinte du flash = couleur de dégâts de l'arme qui a touché (déjà distincte par arme,
    // voir DamageNumberSpawner.ColorXXX : foudre/critique orange, Orbital gris clair, Aura/AOE vert...) plutôt
    // qu'un blanc neutre générique pour tout le monde - cohérent avec la demande "un feedback avec la foudre" :
    // le flash se teinte déjà différemment par arme sans code dédié par arme.
    private Color _hitFlashColor = Color.white;

    private void TriggerHitFlash(Color color)
    {
        if (SuppressHitFlash || _hitFlashUnavailable) return;
        if (_hitFlashRenderer == null) _hitFlashRenderer = FindVisibleRenderer();
        if (_hitFlashRenderer == null) { _hitFlashUnavailable = true; return; }
        _hitFlashColor = color;
        _hitFlashTimer = HitFlashDuration;
    }

    // p : 0 = teinte normale (identité, blanc = pas de changement), 1 = flash au plus fort (surbrillance,
    // teintée vers _hitFlashColor - la couleur de dégâts de l'arme qui vient de toucher).
    // MODIFIÉ (2026-10-03) - retour utilisateur : "le flash est un peu trop fort". ×4,5 avait été monté pour
    // compenser l'invisibilité du flash (cause réelle trouvée depuis : mauvais renderer/mauvais nom de propriété
    // shader, voir FindVisibleRenderer) - maintenant que la vraie cause est corrigée, plus besoin d'une telle
    // surenchère. Redescendu à ×2,2, nettement visible sans éblouir.
    private void SetHitFlashIntensity(float p)
    {
        Color hue = Color.Lerp(Color.white, _hitFlashColor, p * 0.7f);
        Color c = hue * Mathf.Lerp(1f, 2.2f, p);
        c.a = 1f;
        if (_hitFlashPropBlock == null) _hitFlashPropBlock = new MaterialPropertyBlock();
        _hitFlashRenderer.GetPropertyBlock(_hitFlashPropBlock);
        _hitFlashPropBlock.SetColor(BaseColorID, c);
        _hitFlashPropBlock.SetColor(GltfBaseColorID, c);
        _hitFlashRenderer.SetPropertyBlock(_hitFlashPropBlock);
    }

    private void UpdateHitFlash()
    {
        if (_hitFlashTimer <= 0f || _hitFlashRenderer == null) return;

        _hitFlashTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(_hitFlashTimer / HitFlashDuration);
        SetHitFlashIntensity(t * t);

        if (_hitFlashTimer <= 0f) SetHitFlashIntensity(0f);
    }

    public void SetHealthMultiplier(float multiplier, float bonusHealth = 0f)
    {
        _healthMultiplier = multiplier;
        _bonusHealth = Mathf.Max(0f, bonusHealth);
    }

    public float CurrentHealth => _currentHealth;

    // AJOUTE (2026-10-11, retour utilisateur : "les araignées sautent sur le Lyra même si elle est invisible en
    // lâchant son clone") - _currentTarget est déjà redirigé automatiquement vers PlayerController.ActivePhantomClone
    // tant qu'il existe (voir Update() plus bas) ; exposé en lecture pour qu'un ennemi avec sa PROPRE logique de
    // ciblage (ex. EnemyCorruptedWeaver.JumpAttack, qui calculait sa distance/position de saut sur une référence
    // joueur mise en cache à part, jamais redirigée) puisse viser la même cible que tout le monde au lieu de
    // continuer à cibler Lyra directement pendant l'invisibilité.
    protected Transform CurrentTarget => _currentTarget;

    // CORRIGÉ (2026-10-05) - private -> protected : EnemyShooter réutilise maintenant ce même lissage (voir son
    // UpdateBehaviour) au lieu de dupliquer sa propre logique de déplacement SANS lissage - c'était la cause du
    // tremblement signalé (retour utilisateur : "les ennemis shooter [...] tremblent car ils changent de direction
    // tout le temps quand ils sont proches les uns des autres").
    protected Vector3 _smoothedMoveDirection = Vector3.forward;
    protected float MoveSpeed => _moveSpeed;

    private float _currentHealth;
    private Transform _playerTransform;
    private Transform _currentTarget;

    protected float _speedMultiplier = 1f;
    private float _speedMultiplierTarget = 1f;

    // AJOUTÉ (2026-10-05) - retour utilisateur : "~30 FPS quand il y a beaucoup d'ennemis", flaggé comme
    // EXTRÊMEMENT important. Benchmark en Play (EditorApplication.Step + Time.realtimeSinceStartup, sans AUCUNE
    // arme équipée) : 0 ennemis = 7,1 ms/frame, 50 = 8,1 ms, 150 = 12,5 ms, 300 = 19,0 ms - une pente quasi
    // linéaire d'environ 0,04 ms/ennemi QUI EXISTE MÊME SANS COMBAT. Équiper 4 armes à 300 ennemis n'a quasiment
    // rien changé (17,3 ms) : le coût dominant est donc le PAR-ENNEMI lui-même, pas les armes/VFX. Inspection
    // directe d'un clone en Play (Wolf) a trouvé 2 causes précises, toutes deux des réglages par défaut coûteux
    // à l'échelle d'une horde de centaines d'unités, aucune des deux visible à l'œil :
    //  1) Animator.cullingMode = AlwaysAnimate sur chaque ennemi -> évalue le squelette complet (51 Transform
    //     ici) MÊME HORS CHAMP DE CAMÉRA. -> CullUpdateTransforms : ne calcule plus l'animation pour un ennemi
    //     invisible (aucun artefact possible, il n'est par définition pas affiché), reprend normalement dès qu'il
    //     revient dans le champ.
    //  2) shadowCastingMode = On sur CHAQUE Renderer de CHAQUE ennemi -> avec 150-300 ennemis simultanés, ça fait
    //     autant d'ombres dynamiques à culler/trier (CPU) et rendre dans l'atlas d'ombres (GPU) pour un confort
    //     visuel quasi nul sur de si petites unités vues de haut - aucun jeu du genre (Vampire Survivors, Brotato,
    //     HoloCure...) ne projette d'ombre dynamique sur ses sbires. -> Off pour les ennemis courants.
    // Fait UNE SEULE FOIS par instance poolée (Awake, pas OnEnable) car ces réglages ne changent jamais d'une vie
    // à l'autre - inutile de refaire le GetComponentsInChildren à chaque réapparition du pool.
    private Animator[] _cachedAnimators;
    private int _animLodGroup;
    private static int _animLodCounter;

    private void Awake()
    {
        _cachedAnimators = GetComponentsInChildren<Animator>(true);
        foreach (Animator a in _cachedAnimators)
            a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // MODIFIÉ (2026-10-25, retour utilisateur : "les ennemis doivent avoir une ombre, la seule exception c'est les
        // projectiles") - le point 2) ci-dessus est annulé : le modèle de l'ennemi (mesh VAT ou arme tenue, ex. la
        // sarbacane du gobelin) projette son ombre, animée grâce à la passe ShadowCaster de VATLit.shader. Effets
        // (particules, traînées, lignes, sprites) toujours sans ombre. Les projectiles ennemis sont des objets à part.
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
            r.shadowCastingMode = r is MeshRenderer || r is SkinnedMeshRenderer
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

        // AJOUTÉ (2026-10-05) - suite du correctif perf ci-dessus : le culling hors-champ + le GPU Resident Drawer
        // (activé sur les 3 profils URP) n'ont donné que +10 FPS en vrai jeu - pas assez, le vrai poste de coût
        // reste l'évaluation du squelette elle-même, confirmée par A/B direct (désactiver tous les Animators sur
        // 300 ennemis = -22% du temps de frame, 4,26 ms sur 19,7 ms). Ni le culling (ne joue que sur "visible ou
        // pas") ni le GPU Resident Drawer (optimise la SOUMISSION du rendu, pas le calcul du squelette, et le
        // rendu piloté par GPU des meshs skinnés reste de toute façon limité sur cette version d'Unity) ne
        // touchaient à ce coût precis. _animLodGroup réparti par compteur statique (round-robin, jamais 2 ennemis
        // nés au même moment dans le même groupe) pour qu'un ennemi loin sur 3 saute l'évaluation de son
        // Animator CETTE frame plutôt que CELLE-LÀ - la réduction de coût est la même, mais répartie dans le
        // temps au lieu que tous les ennemis lointains gèlent/reprennent sur la MÊME frame (qui ferait un à-coup
        // visible groupé plutôt qu'une réduction discrète).
        _animLodGroup = _animLodCounter++ % AnimLodSkipGroups;
    }

    // CORRIGÉ (2026-10-05) - retour utilisateur critique : "dans ce champ de vision on voit les ennemis, donc on
    // les voit lag". Le seuil de 12 était choisi à l'aveugle, sans vérifier le vrai champ visible de la caméra -
    // mesuré après coup (caméra orthographique, taille 10) : les 4 coins de l'écran touchent le sol à ~20-21
    // unités du joueur. Un ennemi à 12-20 unités est donc ENCORE À L'ÉCRAN mais se faisait throttle à 1 frame sur
    // 3 - exactement le "on les voit lag" signalé. Remonté à 24 (marge au-delà du coin le plus éloigné, ~21) :
    // ne throttle plus que les ennemis réellement hors champ (rare avec un rayon de spawn de 15 < 21 de visible -
    // cette LOD ne concerne donc qu'une minorité d'ennemis en bordure, PAS le levier principal du problème de
    // perf, voir plutôt la limite d'ennemis simultanés par phase dans EnemySpawner).
    private const float AnimLodNearDistanceSqr = 24f * 24f;
    private const int AnimLodSkipGroups = 3; // au-delà : anime 1 frame sur 3 (-66% de coût d'Animator pour ces ennemis-là)

    private void UpdateAnimatorLod()
    {
        if (_cachedAnimators == null || _cachedAnimators.Length == 0 || _currentTarget == null) return;

        float sqrDistance = (transform.position - _currentTarget.position).sqrMagnitude;
        bool animateThisFrame = sqrDistance <= AnimLodNearDistanceSqr
            || (Time.frameCount + _animLodGroup) % AnimLodSkipGroups == 0;

        for (int i = 0; i < _cachedAnimators.Length; i++)
        {
            if (_cachedAnimators[i] != null) _cachedAnimators[i].enabled = animateThisFrame;
        }
    }

    // AJOUTÉ (2026-10-20, retour utilisateur : "quand il y a plein de tireur [...] c'est impossible d'esquiver et
    // c'est mort instantanément") - compteur global des ennemis À DISTANCE actuellement vivants (EnemyShooter/
    // EnemyBulb, voir leur IsRangedAttacker), tenu à jour ici plutôt que par une requête spatiale coûteuse. Lu par
    // EnemySpawner.SpawnEnemy() pour refuser un tireur supplémentaire une fois le plafond atteint (redessine un
    // ennemi de mêlée à la place) - la vraie source du problème n'est pas le dégât d'UN projectile mais le nombre
    // de tireurs actifs EN MÊME TEMPS autour du joueur, qui rend l'esquive géométriquement impossible une fois
    // encerclé de tous les côtés.
    protected virtual bool IsRangedAttacker => false;
    public static int ActiveRangedCount { get; private set; }

    protected virtual void OnEnable()
    {
        // AJOUTÉ (2026-10-04) - voir EnemySpatialGrid : s'enregistre pour la grille de séparation (remplace
        // Physics.OverlapSphereNonAlloc appelé individuellement par chaque ennemi chaque frame).
        EnemySpatialGrid.Register(this);

        if (IsRangedAttacker) ActiveRangedCount++;

        _currentHealth = _maxHealth * _healthMultiplier + _bonusHealth;
        _nextContactHitTime = 0f;
        _isDead = false; // nouvelle vie à la sortie du pool (voir TakeDamage/Die)
        Life++;
        _speedMultiplier = 1f;
        _speedMultiplierTarget = 1f;

        // AJOUTÉ (2026-10-03) - un ennemi qui revient du pool en pleine capture du flash de dégâts (mort juste après
        // un coup) ne doit pas repartir avec une teinte encore allumée sur sa prochaine vie.
        if (_hitFlashTimer > 0f && _hitFlashRenderer != null) SetHitFlashIntensity(0f);
        _hitFlashTimer = 0f;
        _knockbackVelocity = Vector3.zero;

        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        _currentTarget = _playerTransform;

        // AJOUTÉ (2026-10-24, retour utilisateur : "les golem [...] quand ils spawn ils font un 360 degré pour trouver
        // le joueur [...] il faudrait qu'ils soient déjà tourné dans la bonne direction") - un ennemi qui sortait du
        // pool gardait la rotation ET le _smoothedMoveDirection de sa vie précédente (ou Vector3.forward au tout
        // premier spawn) : Update() le faisait ensuite pivoter à 720°/s vers le joueur, bien visible quand toute une
        // vague apparaît en même temps. ObjectPool.Get pose la position AVANT SetActive(true), donc elle est déjà
        // la bonne ici : l'ennemi naît directement face à sa cible.
        FaceCurrentTargetInstantly();

        // MODIFIÉ (2026-10-24, session FPS) - chaque ennemi s'abonnait ici à PlayerController.OnPhantomDestroyed et
        // GameManager.OnGameEnded (et s'en désabonnait dans OnDisable) : en C#, chaque += / -= recopie TOUTE la liste
        // des abonnés dans un nouveau tableau - avec 150 ennemis qui apparaissent/disparaissent en continu, une
        // allocation mémoire qui grossit avec le nombre d'ennemis à chaque spawn et à chaque mort (relevé au
        // profileur). Remplacé par UN SEUL abonnement statique pour toute la classe + un registre des ennemis actifs
        // (voir RegisterActive / OnGameEndedStatic) : même comportement, plus aucune allocation par ennemi.
        RegisterActive();

        // AJOUTE - un ennemi qui revient du pool doit repartir avec son Animator a
        // vitesse normale, au cas ou il aurait ete fige (speed = 0) a la fin de sa
        // vie precedente via HandleGameEnded ci-dessous.
        if (_animatorController == null) _animatorController = GetComponentInChildren<EnemyAnimatorController>();
        if (_animatorController != null) _animatorController.SetAnimatorPaused(false);

        // AJOUTE - un ennemi qui revient du pool avec une brulure encore active de sa
        // vie precedente serait un bug (coroutine orpheline, teinte qui reste collee).
        // On repart toujours propre a chaque reutilisation.
        StopBurnAndResetTint();
    }

    private void FaceCurrentTargetInstantly()
    {
        if (_currentTarget == null) return;
        Vector3 toTarget = _currentTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;
        toTarget.Normalize();
        _smoothedMoveDirection = toTarget;
        transform.rotation = Quaternion.LookRotation(toTarget);
    }

    private void OnDisable()
    {
        EnemySpatialGrid.Unregister(this);

        if (IsRangedAttacker) ActiveRangedCount = Mathf.Max(0, ActiveRangedCount - 1);

        UnregisterActive();

        // AJOUTE - coupe la coroutine de brulure si l'ennemi est desactive/retourne
        // au pool en pleine brulure (ex: tue par une autre source de degats pendant
        // qu'il brule), pour eviter une coroutine qui tourne dans le vide.
        StopBurnAndResetTint();
    }

    // AJOUTE - appele UNE SEULE FOIS, exactement quand la partie se termine
    // (victoire ou game over), via GameManager.OnGameEnded. C'est ce qui manquait
    // pour que les ennemis arretent proprement leur animation de marche au lieu de
    // rester figes a mi-boucle : Update() (et donc UpdateBehaviour) s'arrete net a
    // IsGameOver, donc plus rien n'appelait jamais SetAttacking(false) ou
    // n'indiquait a l'Animator qu'il fallait s'arreter.
    // Registre des ennemis actifs (ajout/retrait en O(1) par échange avec le dernier) et abonnement unique aux 2
    // événements statiques - voir le commentaire dans OnEnable.
    private static readonly List<EnemyBase> _activeEnemies = new List<EnemyBase>(256);
    private int _activeIndex = -1;
    private static bool _staticHandlersHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveRegistry() => _activeEnemies.Clear();

    private void RegisterActive()
    {
        if (!_staticHandlersHooked)
        {
            _staticHandlersHooked = true;
            PlayerController.OnPhantomDestroyed += OnPhantomDestroyedStatic;
            GameManager.OnGameEnded += OnGameEndedStatic;
        }
        if (_activeIndex >= 0) return;
        _activeIndex = _activeEnemies.Count;
        _activeEnemies.Add(this);
    }

    private void UnregisterActive()
    {
        if (_activeIndex < 0) return;
        int last = _activeEnemies.Count - 1;
        EnemyBase moved = _activeEnemies[last];
        _activeEnemies[_activeIndex] = moved;
        moved._activeIndex = _activeIndex;
        _activeEnemies.RemoveAt(last);
        _activeIndex = -1;
    }

    private static void OnPhantomDestroyedStatic()
    {
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            if (_activeEnemies[i] != null) _activeEnemies[i].HandlePhantomDestroyed();
    }

    private static void OnGameEndedStatic()
    {
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            if (_activeEnemies[i] != null) _activeEnemies[i].HandleGameEnded();
    }

    private void HandleGameEnded()
    {
        if (_animatorController == null) _animatorController = GetComponentInChildren<EnemyAnimatorController>();
        if (_animatorController != null) _animatorController.SetAnimatorPaused(true);
    }

    private void HandlePhantomDestroyed()
    {
        if (_currentTarget != null && _currentTarget == PlayerController.ActivePhantomClone)
        {
            SetTarget(_playerTransform);
        }
    }

    private void Start()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }
        _currentTarget = _playerTransform;
    }

    // AJOUTÉ (2026-10-20, retour utilisateur : "dans les map sans bordure, difficulté 1 et 2, quand le max
    // d'ennemi à spawn, rien empêche le joueur de fuir à l'infini parce que plus rien ne peut spawn") - sur un axe
    // sans mur (Modéré : les 2 axes, Violent : X seul, voir MapBoundaryUtils/DifficultyTiers), rien ne ramenait
    // jamais un ennemi laissé loin derrière vers le joueur : une fois le plafond EnemySpatialGrid.Count atteint
    // (voir EnemySpawner.SpawnWave), ces ennemis "perdus" continuaient d'occuper le budget de spawn pour toujours,
    // empêchant tout nouvel ennemi d'apparaître près du joueur qui s'éloigne - Apocalyptique (boîte fermée des 2
    // côtés) n'a pas ce problème, les murs forcent naturellement l'affrontement. Recyclé directement au pool (PAS
    // un Die() : aucune XP/or/kill, ce n'est pas une mort) dès qu'un ennemi dépasse LeashDistance du VRAI joueur
    // (jamais du Clone Fantôme, qui n'est pas pertinent ici) - EnemySpawner en fait alors spawner un autre, plus
    // proche, à sa prochaine vague.
    private const float LeashDistance = 40f; // nettement au-delà du plus grand rayon de spawn (22, voir EnemySpawner._spawnRadius)
    private const float LeashDistanceSqr = LeashDistance * LeashDistance;

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        if (_playerTransform != null && (!MapBoundaryUtils.HasBoundX || !MapBoundaryUtils.HasBoundZ))
        {
            Vector3 toPlayerFlat = transform.position - _playerTransform.position;
            toPlayerFlat.y = 0f;
            if (toPlayerFlat.sqrMagnitude > LeashDistanceSqr)
            {
                if (ObjectPool.Instance != null) ObjectPool.Instance.ReturnToPool(_poolTag, gameObject);
                return;
            }
        }

        if (_currentTarget == null)
            SetTarget(_playerTransform);

        if (_currentTarget == null) return;

        if (PlayerController.ActivePhantomClone != null && _currentTarget == _playerTransform)
        {
            // le clone attire TOUS les ennemis (pas de rayon) ; seuls ceux proches gardent le bonus de vitesse
            float sqrDistance = Vector3.SqrMagnitude(transform.position - PlayerController.ActivePhantomClone.position);
            bool near = sqrDistance <= PlayerController.PhantomAttractRadius * PlayerController.PhantomAttractRadius;
            PlayerController.TryAttractToPhantom(this, near);
        }

        UpdateBehaviour(_currentTarget);
        UpdateKnockback();
        UpdateAnimatorLod();

        OnEnemyUpdate();

        // Après OnEnemyUpdate() : si un ennemi (ex. EnemyKaiju) gère sa propre émission dans son hook, elle garde la
        // main sur le Renderer. Ne s'exécute de toute façon jamais pour lui (SuppressHitFlash => true).
        UpdateHitFlash();
    }

    // AJOUTÉ (2026-10-03) - retour utilisateur : l'upgrade Orbitaux doit repousser légèrement un ennemi touché, pour
    // montrer qu'il s'est fait percuter. Vitesse qui s'amortit vite (pas de vrai "ragdoll" physique, juste un élan
    // bref) - APRÈS UpdateBehaviour() (qui recalcule la position vers la cible chaque frame) pour que le recul soit
    // visible plutôt qu'écrasé aussitôt par le déplacement normal.
    private Vector3 _knockbackVelocity;
    private const float KnockbackDamping = 13f; // plus haut = s'arrête plus vite
    // MODIFIÉ (2026-10-04) - retour utilisateur : "trop fort le repousse avec la vitesse actuelle des orbitaux".
    // Chaque orbe a SON PROPRE cooldown par ennemi (_lastHitTimestamps est un champ d'instance sur
    // OrbitalProjectile, pas partagé entre orbes) - avec plusieurs orbitaux maxés, 2-3 orbes DIFFÉRENTS peuvent
    // toucher le même ennemi à quelques frames d'intervalle, et les forces s'additionnaient sans limite
    // (_knockbackVelocity += force à chaque coup) - un ennemi encerclé par plusieurs orbes pouvait cumuler un élan
    // bien plus fort qu'un seul coup, d'où l'impression de repoussement incontrôlé. Plafonné à MaxKnockbackSpeed :
    // chaque coup pousse plus fort SI la vitesse actuelle est encore faible, mais un ennemi déjà en plein élan n'en
    // accumule plus - jamais un à-coup qui parte dans le décor.
    private const float MaxKnockbackSpeed = 4.5f;

    public void ApplyKnockback(Vector3 force)
    {
        force.y = 0f;
        _knockbackVelocity = Vector3.ClampMagnitude(_knockbackVelocity + force, MaxKnockbackSpeed);
    }

    // AJOUTÉ (2026-10-27, fusion Tourbière Magnétique) - attire l'ennemi vers un point au sol, appelé chaque frame par
    // la fusion tant qu'il est dans le rayon d'attraction. Déplacement direct (pas via _knockbackVelocity, plafonnée
    // et amortie pour des chocs brefs) : la vitesse d'attraction dépasse celle de marche, l'ennemi est donc entraîné
    // vers le centre même en marchant vers le joueur. S'arrête à minDistance du centre pour ne pas tout empiler.
    public void PullTowards(Vector3 center, float speed, float minDistance)
    {
        Vector3 toCenter = center - transform.position;
        toCenter.y = 0f;
        float distance = toCenter.magnitude;
        if (distance <= minDistance) return;
        float step = Mathf.Min(distance - minDistance, speed * Time.deltaTime);
        transform.position += toCenter / distance * step;
    }

    private void UpdateKnockback()
    {
        if (_knockbackVelocity.sqrMagnitude < 0.0001f) { _knockbackVelocity = Vector3.zero; return; }
        transform.position += _knockbackVelocity * Time.deltaTime;
        _knockbackVelocity = Vector3.MoveTowards(_knockbackVelocity, Vector3.zero, _knockbackVelocity.magnitude * KnockbackDamping * Time.deltaTime);
    }

    protected virtual void OnEnemyUpdate() { }

    private EnemyAnimatorController _animatorController;

    protected virtual void UpdateBehaviour(Transform target)
    {
        // MODIFIE - meme raisonnement que pour la direction de deplacement plus bas :
        // Vector3.Distance() mesurait la distance 3D COMPLETE, Y inclus. Tant que la
        // cible etait le joueur (meme hauteur), ca ne changeait rien. Mais avec le
        // Clone Fantome a une hauteur Y differente, un ennemi pouvait arriver pile
        // a cote horizontalement sans jamais que cette distance ne descende sous
        // _playerContactRadius (l'ecart vertical restait bloque dans le calcul) :
        // le seuil de contact n'etait donc jamais atteint, l'ennemi ne s'arretait
        // jamais et tournait indefiniment autour du Clone sans attaquer.
        Vector3 toTargetFlat = target.position - transform.position;
        toTargetFlat.y = 0f;
        float distanceToTarget = toTargetFlat.magnitude;
        bool isInContactWithTarget = distanceToTarget <= _playerContactRadius;

        if (_animatorController == null) _animatorController = GetComponentInChildren<EnemyAnimatorController>();

        if (isInContactWithTarget)
        {
            if (_animatorController != null) _animatorController.SetAttacking(true);

            if (target == _playerTransform)
            {
                _playerTransform.TryGetComponent(out HealthSystem playerHealth);
                if (playerHealth != null && Time.time >= _nextContactHitTime
                    && playerHealth.TakeContactHit(_contactDamage * GameModes.EnemyDamageScale))
                    _nextContactHitTime = Time.time + _contactDamageCooldown;
            }

            return;
        }

        if (_animatorController != null) _animatorController.SetAttacking(false);

        // Reutilise toTargetFlat calcule en haut de la methode (deja aplati en Y),
        // au lieu de recalculer target.position - transform.position ici.
        Vector3 direction = toTargetFlat.normalized;
        Vector3 separation = GetBaseSeparation();
        Vector3 desiredDirection = (direction + separation * 0.3f).normalized;

        if (desiredDirection.sqrMagnitude > 0.01f)
            _smoothedMoveDirection = Vector3.Slerp(_smoothedMoveDirection, desiredDirection, 10f * Time.deltaTime);

        Vector3 final = _smoothedMoveDirection;

        transform.position += final * MoveSpeed * _speedMultiplier * _speedMultiplierTarget * Time.deltaTime;

        if (final.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(final);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);
        }
    }

    public void SetTarget(Transform newTarget, float speedBoost = 1f)
    {
        _currentTarget = newTarget != null ? newTarget : _playerTransform;

        if (_currentTarget == _playerTransform)
        {
            _speedMultiplierTarget = 1f;
        }
        else
        {
            _speedMultiplierTarget = speedBoost;
        }
    }

    private static readonly EnemyBase[] _neighbourBuffer = new EnemyBase[16];

    // MODIFIE - pushDirection n'etait jamais aplati en Y. Meme cause que le bug de
    // sauts aleatoires en Y trouve sur les gobelins (EnemyShooter.GetSeparationForce,
    // qui a une copie separee de cette meme logique) : une composante Y residuelle
    // ici pouvait se propager dans desiredDirection puis final, faisant deriver
    // n'importe quel type d'ennemi verticalement des qu'il a des voisins proches -
    // corrige ici de facon preventive pour tous les types d'ennemis qui utilisent
    // EnemyBase.UpdateBehaviour() directement, pas seulement les gobelins.
    // MODIFIÉ (2026-10-04) - retour utilisateur : "toujours ~20 FPS avec beaucoup d'ennemis". Remplace
    // Physics.OverlapSphereNonAlloc (un vrai appel moteur physique PAR ENNEMI PAR FRAME) par EnemySpatialGrid
    // (grille reconstruite une fois par frame, interrogée ensuite en C# pur) - voir ce fichier pour le détail.
    private Vector3 GetBaseSeparation()
    {
        Vector3 force = Vector3.zero;
        float separationRadius = 1.5f;

        int count = EnemySpatialGrid.QueryNeighbors(transform.position, separationRadius, this, _neighbourBuffer);

        for (int i = 0; i < count; i++)
        {
            EnemyBase neighbour = _neighbourBuffer[i];

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            pushDirection.y = 0f;
            force += pushDirection.normalized;
        }

        return force.normalized * 0.5f;
    }

    // AJOUTE (2026-09-27) - DEBUG/TEST uniquement (voir DebugCheats.cs), meme principe que BossBase.DebugInvincible :
    // interrupteur global, un seul point a verifier partout ou un ennemi normal encaisse des degats.
    public static bool DebugInvincible = false;

    private bool _isDead;

    // AJOUTÉ (2026-10-28) - numéro de vie, +1 à chaque sortie du pool : un effet qui garde une référence vers un ennemi
    // pendant un moment (saut de la Chaîne de foudre) vérifie qu'il frappe toujours LE MÊME ennemi, et non l'objet
    // recyclé entre-temps en un nouvel ennemi apparu ailleurs.
    public int Life { get; private set; }
    public bool IsDead => _isDead;

    public void TakeDamage(float damage, Color color = default, bool fromNova = false)
    {
        if (DebugInvincible) return;
        // CORRIGE (2026-10-16, audit) - 2 coups dans la même frame (boule de feu + aura, éclair en chaîne, Nova...)
        // appelaient Die() 2 fois : kill, or et gemmes comptés en double, retour au pool en double. Et après la mort
        // du joueur (ou la victoire), projectiles/zones encore actifs continuaient de tuer pendant le délai avant
        // l'écran de fin : ces kills/cet or n'ont plus à compter (même règle que la Brûlure, voir BurnRoutine).
        if (_isDead) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        _currentHealth -= damage;

        if (DamageNumberSpawner.Instance != null)
        {
            Color c = color == default ? DamageNumberSpawner.ColorProjectile : color;
            DamageNumberSpawner.Instance.Spawn(transform.position, damage, c, transform, false);
        }

        if (_currentHealth <= 0)
            Die(fromNova);
        else
            // pas sur le coup fatal : EnemyDeathFX prend le relais tout de suite, le flash n'aurait pas le temps de se voir
            TriggerHitFlash(color == default ? Color.white : color);
    }

    // AJOUTE - point d'entree public de la Brulure, appele par ProjectileBasic quand
    // une upgrade Fireball avec Brulure debloquee touche cet ennemi (direct ou via
    // l'explosion). Redemarre la coroutine si une brulure est deja en cours plutot
    // que d'en empiler une deuxieme.
    public void ApplyBurn(float damagePerSecond, float duration)
    {
        if (damagePerSecond <= 0f || duration <= 0f) return;
        // CORRIGÉ (2026-10-05) - retour utilisateur, erreur console en jeu réel : "Coroutine couldn't be started
        // because the game object 'Spider(Clone)' is inactive!". Cause : ProjectileBasic.OnTriggerEnter appelle
        // ApplyDamage() PUIS ApplyBurnIfNeeded() sur le MÊME ennemi - si le coup est fatal, ApplyDamage() a déjà
        // appelé Die() -> ObjectPool.ReturnToPool() -> SetActive(false) avant que ApplyBurnIfNeeded() ne tente de
        // démarrer cette coroutine sur un GameObject désormais inactif (StartCoroutine échoue toujours dans ce
        // cas). Inoffensif en pratique (l'ennemi est déjà mort) mais polluait la console à chaque mise à mort par
        // Brûlure - gardé explicitement au lieu de laisser Unity émettre son avertissement.
        if (!gameObject.activeInHierarchy) return;

        CacheSpriteRenderer();

        if (_burnCoroutine != null)
            StopCoroutine(_burnCoroutine);

        _burnCoroutine = StartCoroutine(BurnRoutine(damagePerSecond, duration));
    }

    private void CacheSpriteRenderer()
    {
        if (_spriteRenderer != null) return;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null && !_spriteBaseColorCaptured)
        {
            _spriteBaseColor = _spriteRenderer.color;
            _spriteBaseColorCaptured = true;
        }
    }

    private IEnumerator BurnRoutine(float damagePerSecond, float duration)
    {
        float elapsed = 0f;
        float tickTimer = 0f;
        float tickDamage = damagePerSecond * _burnTickInterval;

        while (elapsed < duration)
        {
            // CORRIGÉ (2026-10-05) - retour utilisateur : "quand il y a la mort, certains dégâts comme la brûlure
            // de la boule de feu persiste, donc pendant le délai entre la mort et l'arrivée du panel, des fois on
            // voit encore des dégâts s'afficher". Cause : contrairement à PRESQUE TOUT LE RESTE du jeu (chaque
            // arme vérifie IsGameOver/IsPaused dans son Update()), cette coroutine n'avait AUCUNE garde - une
            // Brûlure déjà active sur un ennemi encore vivant continuait de tiquer (dégâts + chiffres affichés)
            // pendant tout le délai d'affichage du panel de fin, alors que la partie est déjà terminée. Même
            // garde que `MudPuddleZone.Update()` : rien n'avance tant que c'est vrai (dégâts ET durée gelés).
            if (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused))
            {
                yield return null;
                continue;
            }

            float dt = Time.deltaTime;
            elapsed += dt;
            tickTimer += dt;

            if (_spriteRenderer != null)
            {
                // Pulsation rapide plutot qu'une teinte fixe : plus lisible comme
                // "effet actif en ce moment" qu'une simple couleur statique.
                float pulse = (Mathf.Sin(elapsed * 10f) + 1f) * 0.5f;
                float blend = _burnTintIntensity * (0.6f + 0.4f * pulse);
                _spriteRenderer.color = Color.Lerp(_spriteBaseColor, BurnTintColor, blend);
            }

            if (tickTimer >= _burnTickInterval)
            {
                tickTimer -= _burnTickInterval;
                TakeDamage(tickDamage, BurnDamageNumberColor);
            }

            yield return null;
        }

        if (_spriteRenderer != null)
            _spriteRenderer.color = _spriteBaseColor;

        _burnCoroutine = null;
    }

    private void StopBurnAndResetTint()
    {
        if (_burnCoroutine != null)
        {
            StopCoroutine(_burnCoroutine);
            _burnCoroutine = null;
        }

        if (_spriteRenderer != null && _spriteBaseColorCaptured)
            _spriteRenderer.color = _spriteBaseColor;
    }

    // AJOUTÉ (2026-10-03) - couleur par défaut du pop de mort (EnemyDeathFX) quand l'ennemi n'a pas de SpriteRenderer
    // coloré à reprendre (la plupart des ennemis sont des modèles 3D, pas des sprites) : blanc chaud neutre, cohérent
    // avec la teinte "_hot" (Color.Lerp vers le blanc) déjà utilisée par BossFX/GroundTelegraphFX pour les éclats.
    private static readonly Color DefaultDeathFxColor = new Color(1f, 0.92f, 0.78f);

    private void Die(bool fromNova = false)
    {
        if (_isDead) return;
        _isDead = true;

        // AJOUTÉ (2026-10-03) - retour visuel à la mort : jusqu'ici instantané et muet (ReturnToPool direct), alors
        // que c'est l'événement le plus fréquent de toute la partie. Couleur reprise du sprite si l'ennemi en a un
        // (CacheSpriteRenderer, déjà utilisé par la Brûlure), sinon un blanc chaud neutre.
        CacheSpriteRenderer();
        Color fxColor = _spriteBaseColorCaptured ? _spriteBaseColor : DefaultDeathFxColor;
        EnemyDeathFX.Play(transform.position + Vector3.up * 0.5f, fxColor);

        // MODIFIE (2026-10-07, retour utilisateur : "au bout de plus de 8 min environ, les petits xp verts ne
        // donnent plus rien... j'suis arrivé qu'au niveau 18" en 15 min) - _xpValue restait fixe alors que
        // _healthMultiplier (même palier, voir EnemySpawner.SpawnEnemy) fait grossir les PV au fil du temps : un
        // ennemi tardif prend bien plus de coups à tuer mais rapportait exactement la même XP qu'au début de la
        // partie - l'XP par seconde de combat s'effondrait avec le temps. L'XP suit maintenant le même facteur que
        // les PV (healthMultiplier inclut déjà le scaling Sans Fin au-delà de 15 min, voir ObjectPool.Get).
        // CORRIGÉ (2026-10-11, retour utilisateur : "en fin de partie ça donne trop d'xp, comme il y a beaucoup
        // d'ennemis qui meurent, et que les ennemis donnent beaucoup d'xp, ça monte trop vite, il faudrait alléger
        // un peu le scale d'xp") - le correctif ci-dessus cumulait avec la hausse du NOMBRE d'ennemis tués par
        // seconde en fin de partie (plafond d'ennemis simultanés et cadence de spawn qui montent aussi avec le
        // temps, voir EnemySpawner._phasesDeJeu) : les deux ensemble faisaient grimper l'XP/seconde bien plus vite
        // que prévu. L'XP suit alors la MOITIÉ de la croissance des PV au lieu de la totalité (resté à 1x au
        // palier 0, seulement 3x au lieu de 5x au dernier palier) - toujours croissant (pas de retour au problème
        // de 2026-10-07), mais la pente allégée.
        // RETIRÉ (2026-10-22, retour utilisateur : "avant ils donnaient des xp doré, maintenant même en fin de
        // partie ça donne du vert [...] remette ce qu'il y avait avant") - cet amortissement empilé SUR le coût de
        // niveau déjà rallongé (voir XPSystem._baseXP/XPToNextLevel, suite 2026-10-21 juste en dessous) faisait que
        // les kills tardifs plafonnaient trop bas (3x) pour suivre la pente du coût (qui, lui, suit une vraie
        // exponentielle) : plus assez de grosses gemmes dorées en fin de partie. Pleine croissance restaurée (1x
        // au palier 0, jusqu'à 5x au dernier palier, comme "avant") - le ralentissement global reste porté par le
        // coût de niveau SEUL, pas cumulé une 2e fois ici.
        float xpBaseMultiplier = _xpHealthMultiplier >= 0f ? _xpHealthMultiplier : _healthMultiplier;
        float xpHealthMultiplier = xpBaseMultiplier;
        // RETIRÉ (2026-10-21, retour utilisateur : "tu as réduit l'xp que donnait les ennemis, mais [...] au début
        // de partie quand on tue un ennemi il ne donne pas dans tous les cas de l'xp, ce qui n'est pas normal dans
        // un survivor [...] on devrait rallonger l'xp qu'il faut selon le niveau de la barre, surtout dans les
        // hauts niveaux") - un multiplicateur global (jusqu'à x0,27, suites 17/19/20) faisait passer l'XP de
        // beaucoup de kills SOUS le seuil d'une gemme visible (10, voir XPGemSpawner.CalculateGems) : le kill ne
        // perdait rien en vrai (le reste était reporté via XPGemSpawner._xpCarry), mais AUCUNE gemme n'apparaissait
        // au sol la plupart du temps - cassait le geste de base d'un survivor. Le même ralentissement global est
        // désormais obtenu en rallongeant le COÛT d'un niveau plutôt que la RÉCOMPENSE d'un kill (voir
        // XPSystem._baseXP/XPToNextLevel) : chaque ennemi redonne sa pleine valeur, au moins 1 gemme visible par
        // kill la plupart du temps, ramasser de l'XP au sol reste aussi satisfaisant qu'avant.
        if (XPGemSpawner.Instance != null)
            XPGemSpawner.Instance.SpawnGems(transform.position, _xpValue * xpHealthMultiplier * _xpPacingMultiplier);

        GameManager.Instance.AddKill();
        // MODIFIÉ (2026-10-27, refonte de l'économie) - seule une petite part de _goldValue est versée (voir
        // MetaProgressionManager.AddKillGold) : l'or vient surtout des minutes, des boss et de la victoire.
        MetaProgressionManager.Instance.AddKillGold(_goldValue);

        // MODIFIE - reset du dash reserve au noeud "Impulsion Nova" (branche Fantome/Lyra).
        // Avant, ce reset se declenchait pour n'importe quel joueur des qu'une Nova
        // tuait un ennemi, sans verifier l'achat du noeud - l'effet payant etait donc
        // gratuit pour tout le monde. HasImpulsionNova() renvoie deja false hors branche
        // Fantome ou si le noeud n'est pas achete.
        if (fromNova
            && MetaProgressionManager.Instance != null
            && MetaProgressionManager.Instance.HasImpulsionNova())
        {
            if (_playerTransform != null)
            {
                _playerTransform.TryGetComponent(out PlayerController pc);
                if (pc != null) pc.ResetDashCooldown();
            }
        }

        ObjectPool.Instance.ReturnToPool(_poolTag, gameObject);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
}