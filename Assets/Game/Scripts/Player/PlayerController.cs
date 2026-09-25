using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Déplacement")]
    [SerializeField] private float _moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float _dashSpeed = 15f;
    [SerializeField] private float _dashDuration = 0.15f;
    [SerializeField] private float _dashCooldown = 2f;
    [SerializeField] private float _absorptionWindow = 0.3f;

    [Header("Dash — Effet visuel")]
    // MODIFIE (2026-09-23) - remplace la 1ère version (traînées de vent seules, jugée trop discrète/"juste des
    // traits") par un vrai combo de Dash à la Hades/Dead Cells/Hollow Knight : silhouettes fantômes du personnage
    // qui restent un instant derrière lui (mêmes matériaux translucides que le double du Clone Fantôme, voir
    // PrecomputeGhostMaterials), un étirement/tassement du modèle façon animation (squash & stretch) pendant le
    // Dash puis un petit rebond élastique à l'arrivée, et un souffle d'air au point de départ (ExpandingRingVFX,
    // déjà utilisé par la Nova). Tout est procédural (aucun asset requis) et réglable ci-dessous.
    [SerializeField] private bool _dashVFXEnabled = true;
    [Tooltip("Teinte des silhouettes fantômes et du souffle de départ.")]
    [SerializeField] private Color _dashAccentColor = new Color(0.65f, 0.85f, 1f, 1f);
    [Range(0f, 1f)] [SerializeField] private float _dashGhostAlpha = 0.62f;
    [Tooltip("Nombre de silhouettes laissées derrière le joueur sur la durée du Dash.")]
    [SerializeField] private int _dashGhostCount = 6;
    [Tooltip("Durée du fondu de CHAQUE silhouette (elles se chevauchent : plus long = traînée plus dense).")]
    [SerializeField] private float _dashGhostFade = 0.18f;
    [Tooltip("Étirement du modèle dans le sens du Dash (1 = taille normale). Volontairement modéré : un personnage rigué (squelette/animations) supporte moins d'étirement qu'un sprite avant de se déformer bizarrement aux articulations.")]
    [SerializeField] private float _dashStretchForward = 1.14f;
    [Tooltip("Tassement latéral pendant l'étirement (conserve un effet de « volume »).")]
    [SerializeField] private float _dashSquashSide = 0.93f;
    [Tooltip("Petit rebond élastique au retour à la taille normale (1 = pas de rebond).")]
    [SerializeField] private float _dashReleaseOvershoot = 0.95f;
    [SerializeField] private float _dashReleaseDuration = 0.14f;
    [SerializeField] private bool _dashLaunchPuff = true;
    [SerializeField] private float _dashLaunchPuffRadius = 0.55f;
    [Tooltip("Hauteur Y du souffle de départ (même logique que WeaponMudPuddle._groundY : le pivot du joueur est à hauteur de torse, un effet au sol doit avoir sa propre hauteur).")]
    [SerializeField] private float _dashWindGroundY = 0.15f;
    [Tooltip("Son du Dash (optionnel) : laissé vide pour l'instant, à assigner plus tard sans toucher au code.")]
    [SerializeField] private AudioClip _dashSound;
    [Range(0f, 1f)] [SerializeField] private float _dashSoundVolume = 0.7f;

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 700f;

    [Header("Modèle 3D")]
    [SerializeField] private string _modelChildName = "stylized_character_3d_model";
    [SerializeField] private Transform _staffTransform;

    [Header("Clone Fantôme (touche dédiée)")]
    // OBSOLÈTE (2026-09-19) : la touche se règle désormais dans Paramètres > Commandes (GameAction.PhantomClone).
    // Le champ est conservé pour ne pas casser la sérialisation des prefabs joueur.
    //[SerializeField, HideInInspector] private KeyCode _phantomCloneKey = KeyCode.C;
    [SerializeField] private float _phantomCloneCooldown = 8f;
    [SerializeField] private float _phantomCloneDuration = 2f;
    [SerializeField] private float _phantomAttractRadius = 10f;
    [SerializeField] private int _phantomMaxAttracted = 14;
    [SerializeField] private float _phantomEscapeSpeedMultiplier = 1.5f;
    [SerializeField] private float _phantomEscapeSpeedDuration = 1.2f;
    [SerializeField] private Color _cloneTint = new Color(0.55f, 0.7f, 1f);
    // CORRIGE (2026-09-24) - retour utilisateur : "le clone aussi est invisible, ça n'a pas de sens, seule Lyra doit
    // l'être". Cette valeur (0,55) datait d'avant la correction du bug de mélange GPU de CreateGhostMaterial (voir
    // plus bas) : l'alpha n'avait AUCUN effet visuel avant ce correctif, donc le clone restait opaque à l'écran
    // quelle que soit cette valeur - personne ne s'en était rendu compte. Une fois l'alpha réellement fonctionnel,
    // 0,55 rendait le clone à moitié transparent, exactement comme Lyra en train de se rendre invisible : plus de
    // décoy visible. Le clone est maintenant pleinement opaque (1) ; seule la teinte (_cloneTint, mélange 0,4 plus
    // bas) lui donne un léger aspect fantomatique, sans jamais le rendre translucide.
    [SerializeField] private float _cloneAlpha = 1f;
    [SerializeField] private float _phantomSelfAlpha = 0.45f;

    [Header("Clone Fantôme — overrides par personnage")]
    [SerializeField] private Renderer _mainBodyRenderer;
    [SerializeField] private SkinnedMeshRenderer _cloneSourceRenderer;
    [SerializeField] private Vector3 _cloneScale = Vector3.one;
    [SerializeField] private Vector3 _cloneStaffScale = Vector3.one;

    private float _phantomCloneCooldownTimer = 0f;
    public float PhantomCloneCooldownPercent => _phantomCloneCooldownTimer / _phantomCloneCooldown;

    private Rigidbody _rb;
    private HealthSystem _healthSystem;
    private Vector3 _moveDirection;
    private float _speedMultiplier = 1f;
    private float _escapeSpeedMultiplier = 1f;

    private bool _isDashing = false;
    private bool _isInvincible = false;
    private float _dashTimer = 0f;
    private float _dashCooldownTimer = 0f;
    private bool _canAbsorb = false;
    private float _absorptionTimer = 0f;
    private Vector3 _dashDirection;

    private CrystalSystem _crystalSystem;
    private PlayerAnimatorController _animatorController;
    public static Transform ActivePhantomClone { get; private set; }
    public static System.Action OnPhantomDestroyed;
    public bool IsDashing => _isDashing;
    public bool IsInvincible => _isInvincible;
    public bool CanAbsorb => _canAbsorb;
    public float DashCooldownPercent => _dashCooldownTimer / _dashCooldown;

    public static float PhantomAttractRadius { get; private set; }
    public static int PhantomAttractedCount { get; private set; }
    public static int PhantomMaxAttracted { get; private set; }

    // MODIFIÉ (2026-09-24) - retour utilisateur : Lyra est invisible et invulnérable pendant le clone, il n'a donc plus de sens
    // qu'un ennemi hors de portée continue à la viser. TOUS les ennemis ciblent maintenant le clone tant qu'il existe (plus de
    // rayon d'attraction ni de plafond d'ennemis attirés). `near` = l'ennemi est dans l'ancien rayon : il garde le bonus de
    // vitesse x2 pour foncer sur le clone ; les plus lointains le rejoignent à vitesse normale (sinon toute la carte
    // accourrait deux fois plus vite).
    public static bool TryAttractToPhantom(EnemyBase enemy, bool near = true)
    {
        if (ActivePhantomClone == null) return false;
        PhantomAttractedCount++;
        enemy.SetTarget(ActivePhantomClone, near ? 2f : 1f);
        return true;
    }

    [Header("Effets Second Souffle")]
    private bool _isInvisible = false;
    private float _invisibilityTimer = 0f;
    private float _blinkTimer = 0f;
    [SerializeField] private float _blinkInterval = 0.1f;
    private Renderer[] _playerRenderers;
    private bool _renderersEnabled = true;

    private Transform _modelTransform;
    private Vector3 _modelBaseScale = Vector3.one;
    private Coroutine _dashStretchRoutine;
    private Material[][] _originalMaterials;
    private Material[][] _ghostSelfMaterials;
    private Material[][] _ghostCloneMaterials;
    private Material[][] _ghostDashMaterials;

    public void ActivateInvisibility(float duration)
    {
        _isInvisible = true;
        _invisibilityTimer = duration;
        _blinkTimer = 0f;

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (playerLayer != -1 && enemyLayer != -1)
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
    }

    private void HandleInvisibilityTimer()
    {
        if (!_isInvisible) return;

        _invisibilityTimer -= Time.deltaTime;
        _blinkTimer += Time.deltaTime;

        if (_blinkTimer >= _blinkInterval)
        {
            _blinkTimer = 0f;
            _renderersEnabled = !_renderersEnabled;
            SetRenderersEnabled(_renderersEnabled);
        }

        if (_invisibilityTimer <= 0f)
        {
            _isInvisible = false;
            SetRenderersEnabled(true);

            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (playerLayer != -1 && enemyLayer != -1)
                Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        }
    }

    private void SetRenderersEnabled(bool value)
    {
        if (_playerRenderers == null) return;
        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            if (_playerRenderers[i] != null)
                _playerRenderers[i].enabled = value;
        }
    }

    public void ResetDashCooldown()
    {
        _dashCooldownTimer = 0f;
        if (GameUI.Instance != null) GameUI.Instance.UpdateDashCooldown(1f);
    }

    public void HideStaff()
    {
        if (_staffTransform != null)
            _staffTransform.gameObject.SetActive(false);
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _healthSystem = GetComponent<HealthSystem>();
        _crystalSystem = GetComponent<CrystalSystem>();
        _animatorController = GetComponent<PlayerAnimatorController>();

        _modelTransform = transform.Find(_modelChildName);
        if (_modelTransform != null) _modelBaseScale = _modelTransform.localScale;

        if (_mainBodyRenderer != null)
        {
            _playerRenderers = new Renderer[] { _mainBodyRenderer };
        }
        else if (_modelTransform != null)
        {
            _playerRenderers = _modelTransform.GetComponentsInChildren<Renderer>();
        }
        else
        {
            Debug.LogWarning($"PlayerController : enfant '{_modelChildName}' introuvable, fallback sur tout le Player.");
            _playerRenderers = GetComponentsInChildren<Renderer>();
        }

        PrecomputeGhostMaterials();

        _moveSpeed += _moveSpeed * MetaProgressionManager.Instance.GetReputationBonusSpeed();
        _dashCooldown -= MetaProgressionManager.Instance.GetBonusDashCooldown();
        _dashCooldown = Mathf.Max(_dashCooldown, 1f);
    }

    private void Start()
    {
        if (GameUI.Instance != null)
        {
            GameUI.Instance.SetCloneAvailable(MetaProgressionManager.Instance.HasPhantomDash());
            GameUI.Instance.UpdateCloneCooldown(1f);
        }

        ResetDashCooldown();
    }

    private void PrecomputeGhostMaterials()
    {
        if (_playerRenderers == null) return;

        _originalMaterials = new Material[_playerRenderers.Length][];
        _ghostSelfMaterials = new Material[_playerRenderers.Length][];
        _ghostCloneMaterials = new Material[_playerRenderers.Length][];
        _ghostDashMaterials = new Material[_playerRenderers.Length][];

        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            Material[] originals = _playerRenderers[i].sharedMaterials;
            _originalMaterials[i] = originals;

            Material[] ghostSelf = new Material[originals.Length];
            Material[] ghostClone = new Material[originals.Length];
            Material[] ghostDash = new Material[originals.Length];

            for (int j = 0; j < originals.Length; j++)
            {
                ghostSelf[j] = CreateGhostMaterial(originals[j], Color.white, _phantomSelfAlpha, 0f);
                ghostClone[j] = CreateGhostMaterial(originals[j], _cloneTint, _cloneAlpha, 0.4f);
                // AJOUTE - silhouettes du Dash (voir StartDash/SpawnDashGhost) : même technique que les deux
                // au-dessus (matériau transparent URP dérivé du matériau réel, donc rendu garanti correct), teinte
                // dédiée réglable dans l'inspecteur (_dashAccentColor/_dashGhostAlpha).
                ghostDash[j] = CreateGhostMaterial(originals[j], _dashAccentColor, _dashGhostAlpha, 0.7f, zWrite: false);
            }

            _ghostSelfMaterials[i] = ghostSelf;
            _ghostCloneMaterials[i] = ghostClone;
            _ghostDashMaterials[i] = ghostDash;
        }
    }

    // MODIFIE (2026-09-23) - ajout de "zWrite" (true par défaut = comportement d'origine, inchangé pour les ghosts
    // du Clone Fantôme/Second Souffle qui n'existent jamais qu'un à la fois). Les silhouettes du Dash, elles,
    // se superposent PLUSIEURS À LA FOIS (voir SpawnDashGhost) : avec ZWrite actif, la première silhouette rendue
    // écrit la profondeur et empêche les suivantes de s'y fondre par transparence - tout le paquet ressort opaque
    // au lieu de se dégrader en fondu. zWrite=false règle ça (comportement standard pour empiler du alpha-blend).
    private Material CreateGhostMaterial(Material source, Color tintMultiply, float alpha, float tintBlend, bool zWrite = true)
    {
        Material mat = new Material(source);
        Color baseColor = mat.color;
        Color blended = Color.Lerp(baseColor, tintMultiply, tintBlend);
        mat.color = new Color(blended.r, blended.g, blended.b, alpha);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        // AJOUTE (2026-09-23) - manquait : sans ces deux-là, l'état de mélange GPU du shader Lit d'URP reste sur ses
        // valeurs par défaut (proches d'un rendu opaque) et fait totalement ignorer l'alpha, quels que soient les
        // mots-clés/_Surface ci-dessus - le ghost restait visuellement opaque en jeu malgré une couleur transparente.
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetInt("_ZWrite", zWrite ? 1 : 0);
        mat.SetShaderPassEnabled("DepthOnly", false);
        return mat;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        HandleMovementInput();
        HandleDash();
        HandlePhantomCloneInput();
        HandleAbsorptionWindow();
        UpdateDashCooldown();
        UpdatePhantomCloneCooldown();
        HandleInvisibilityTimer();
    }

    private void HandleMovementInput()
    {
        // Touches lues via la configuration du joueur (page Paramètres > Commandes) + stick de manette.
        Vector2 move = GameInput.Move();
        _moveDirection = new Vector3(move.x, 0f, move.y).normalized;

        if (_animatorController != null)
        {
            bool isMoving = _moveDirection.sqrMagnitude > 0.01f;
            _animatorController.SetWalking(isMoving);
        }
    }

    private void HandleDash()
    {
        if (GameInput.Down(GameAction.Dash) && !_isDashing && _dashCooldownTimer <= 0f)
        {
            Vector3 direction = _moveDirection != Vector3.zero ? _moveDirection : transform.forward;
            StartDash(direction);
        }

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f) StopDash();
        }
    }

    private void StartDash(Vector3 direction)
    {
        _dashDirection = direction.normalized;
        _isDashing = true;
        _isInvincible = true;
        _dashTimer = _dashDuration;
        _dashCooldownTimer = _dashCooldown;
        _canAbsorb = true;
        _absorptionTimer = _absorptionWindow;

        if (_healthSystem != null) _healthSystem.AddExternalInvincibility();
        if (GameUI.Instance != null) GameUI.Instance.UpdateDashCooldown(0f);

        // AJOUTE - habillage visuel du Dash (silhouettes fantômes + étirement + souffle de départ, voir le bloc de
        // champs juste au-dessus) + emplacement pour un son (silencieux tant qu'aucun clip n'est assigné).
        if (_dashVFXEnabled)
        {
            if (_dashLaunchPuff)
            {
                Vector3 puffPos = transform.position;
                puffPos.y = _dashWindGroundY;
                Color puffColor = _dashAccentColor;
                puffColor.a = 0.6f;
                ExpandingRingVFX.Spawn(puffPos, _dashLaunchPuffRadius, puffColor, 0.22f);
            }

            StartCoroutine(PlayDashAfterimages(_dashDuration));

            if (_modelTransform != null)
            {
                if (_dashStretchRoutine != null) StopCoroutine(_dashStretchRoutine);
                _dashStretchRoutine = StartCoroutine(DashStretchAndRelease(_dashDuration));
            }
        }
        if (_dashSound != null) AudioSource.PlayClipAtPoint(_dashSound, transform.position, _dashSoundVolume);

        // AJOUTE - notifie le systeme de defis a chaque VRAI declenchement du
        // Dash (defi "Toujours en Mouvement" = utiliser le Dash 20 fois).
        if (ChallengeManager.Instance != null) ChallengeManager.Instance.NotifyDashUsed();
    }

    // ---- Dash — silhouettes fantômes ------------------------------------------------------------------------------
    // Une poignée d'instantanés du modèle (même technique de bake que SpawnPhantomClone) sont laissés derrière le
    // joueur pendant le Dash et s'effacent chacun indépendamment - la traînée classique des jeux d'action (Hades,
    // Dead Cells, Hollow Knight...). Contrairement au Clone Fantôme, ces silhouettes n'ont ni script ni collider :
    // elles ne vivent qu'une fraction de seconde et ne doivent jamais interagir avec quoi que ce soit.
    private IEnumerator PlayDashAfterimages(float duration)
    {
        if (_dashGhostCount <= 0) yield break;
        float interval = duration / _dashGhostCount;

        for (int i = 0; i < _dashGhostCount; i++)
        {
            SpawnDashGhost();
            float t = 0f;
            while (t < interval)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    private void SpawnDashGhost()
    {
        if (_modelTransform == null) return;

        SkinnedMeshRenderer sourceSkinned = _cloneSourceRenderer != null
            ? _cloneSourceRenderer
            : _modelTransform.GetComponentInChildren<SkinnedMeshRenderer>();
        if (sourceSkinned == null) return;

        var ghost = new GameObject("DashGhost");
        Mesh baked = new Mesh();
        sourceSkinned.BakeMesh(baked);

        var mf = ghost.AddComponent<MeshFilter>();
        mf.sharedMesh = baked;

        var mr = ghost.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (_ghostDashMaterials != null && _ghostDashMaterials.Length > 0)
            mr.sharedMaterials = _ghostDashMaterials[0];

        // même astuce que SpawnPhantomClone : parenter un instant sous le rendu source pour récupérer exactement sa
        // position/rotation/échelle du monde au moment du bake, puis détacher (worldPositionStays = true).
        ghost.transform.SetParent(sourceSkinned.transform, false);
        ghost.transform.SetParent(null, true);
        // CORRIGE (2026-09-24) - même piège que SpawnPhantomClone (qui s'en protège déjà via _cloneScale) : détacher
        // avec worldPositionStays=true fait hériter l'échelle MONDE du parent quitté, presque jamais 1,1,1 (chaîne de
        // mise à l'échelle de l'import du modèle). Sans ce reset, chaque fantôme apparaissait bien plus gros que le
        // joueur, écrasant tout le reste de l'effet (impression d'un "clone" massif plutôt qu'une fine traînée).
        ghost.transform.localScale = Vector3.one;

        StartCoroutine(FadeAndDestroyGhost(ghost, baked));
    }

    private IEnumerator FadeAndDestroyGhost(GameObject ghost, Mesh bakedMesh)
    {
        MeshRenderer mr = ghost.GetComponent<MeshRenderer>();
        // .materials (et non .sharedMaterials) : instancie des copies propres à CE fantôme, pour faire varier leur
        // alpha sans toucher au modèle partagé _ghostDashMaterials (qui ferait clignoter tous les fantômes ensemble).
        Material[] mats = mr.materials;
        Color[] baseColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++) baseColors[i] = mats[i].color;

        float elapsed = 0f;
        while (elapsed < _dashGhostFade)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / _dashGhostFade);
            for (int i = 0; i < mats.Length; i++)
            {
                Color c = baseColors[i];
                mats[i].color = new Color(c.r, c.g, c.b, c.a * fade);
            }
            yield return null;
        }

        foreach (Material m in mats) Destroy(m);
        Destroy(bakedMesh);
        Destroy(ghost);
    }

    // ---- Dash — étirement / tassement (squash & stretch) -----------------------------------------------------------
    // Le modèle s'étire dans le sens du Dash pendant sa durée, puis revient à sa taille avec un petit rebond
    // élastique - le même principe d'animation que les jeux d'action utilisent pour "vendre" la vitesse d'un
    // mouvement instantané (Celeste, Hollow Knight...).
    private IEnumerator DashStretchAndRelease(float dashDuration)
    {
        if (_modelTransform == null) yield break;

        Vector3 baseScale = _modelBaseScale;
        Vector3 stretched = new Vector3(baseScale.x * _dashSquashSide, baseScale.y, baseScale.z * _dashStretchForward);

        float inDuration = Mathf.Min(0.05f, dashDuration * 0.35f);
        float t = 0f;
        while (t < inDuration)
        {
            t += Time.deltaTime;
            if (_modelTransform == null) yield break;
            _modelTransform.localScale = Vector3.Lerp(baseScale, stretched, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(inDuration, 0.0001f)));
            yield return null;
        }
        if (_modelTransform == null) yield break;
        _modelTransform.localScale = stretched;

        float hold = Mathf.Max(0f, dashDuration - inDuration);
        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // tassement bref à l'arrivée (sur X/Z, pas en hauteur) puis retour élastique à la taille normale
        Vector3 undershoot = Vector3.Scale(baseScale, new Vector3(_dashReleaseOvershoot, 1f, _dashReleaseOvershoot));
        t = 0f;
        while (t < _dashReleaseDuration)
        {
            t += Time.deltaTime;
            if (_modelTransform == null) yield break;
            float u = t / _dashReleaseDuration;
            Vector3 scale = u < 0.55f
                ? Vector3.Lerp(stretched, undershoot, Mathf.SmoothStep(0f, 1f, u / 0.55f))
                : Vector3.Lerp(undershoot, baseScale, Mathf.SmoothStep(0f, 1f, (u - 0.55f) / 0.45f));
            _modelTransform.localScale = scale;
            yield return null;
        }
        if (_modelTransform != null) _modelTransform.localScale = baseScale;
        _dashStretchRoutine = null;
    }

    private void HandlePhantomCloneInput()
    {
        if (_phantomCloneCooldownTimer > 0f) return;
        if (!MetaProgressionManager.Instance.HasPhantomDash()) return;

        if (GameInput.Down(GameAction.PhantomClone))
        {
            _phantomCloneCooldownTimer = _phantomCloneCooldown;
            if (GameUI.Instance != null) GameUI.Instance.UpdateCloneCooldown(0f);
            StartCoroutine(SpawnPhantomClone());
        }
    }

    private void UpdatePhantomCloneCooldown()
    {
        if (_phantomCloneCooldownTimer > 0f)
        {
            _phantomCloneCooldownTimer -= Time.deltaTime;
            if (GameUI.Instance != null)
                GameUI.Instance.UpdateCloneCooldown(1f - (_phantomCloneCooldownTimer / _phantomCloneCooldown));
        }
    }

    private IEnumerator SpawnPhantomClone()
    {
        GameObject clone;

        if (_modelTransform != null)
        {
            SkinnedMeshRenderer sourceSkinned = _cloneSourceRenderer != null
                ? _cloneSourceRenderer
                : _modelTransform.GetComponentInChildren<SkinnedMeshRenderer>();

            if (sourceSkinned != null)
            {
                clone = new GameObject("PhantomCloneSnapshot");

                // CORRIGE (2026-09-24) - retour utilisateur : "le clone est figé en pleine action" (pris en pleine
                // foulée de marche si Lyra bougeait au moment du sort). Le clone est un instantané FIGÉ du maillage
                // (BakeMesh) : il doit être pris sur une pose Idle, pas sur l'image de marche en cours. On force la
                // pose puis on l'applique tout de suite (Update(0)), on bake, et on restaure l'état réel juste après
                // - tout dans la même image, avant le moindre rendu : invisible pour la vraie Lyra, qui garde son
                // animation normale.
                bool wasMoving = _moveDirection != Vector3.zero;
                if (_animatorController != null) _animatorController.SnapToIdlePose();

                Mesh bakedMesh = new Mesh();
                sourceSkinned.BakeMesh(bakedMesh);

                if (_animatorController != null) _animatorController.SetWalking(wasMoving);

                MeshFilter mf = clone.AddComponent<MeshFilter>();
                mf.mesh = bakedMesh;

                MeshRenderer mr = clone.AddComponent<MeshRenderer>();
                mr.sharedMaterials = sourceSkinned.sharedMaterials;

                clone.transform.SetParent(sourceSkinned.transform, false);
                clone.transform.SetParent(null, true);

                clone.transform.localScale = _cloneScale;

                if (_staffTransform != null)
                {
                    GameObject staffClone = Instantiate(
                        _staffTransform.gameObject,
                        _staffTransform.position,
                        _staffTransform.rotation);

                    staffClone.transform.SetParent(clone.transform, true);
                    staffClone.transform.localScale = _cloneStaffScale;
                }
            }
            else
            {
                clone = Instantiate(_modelTransform.gameObject, transform.position, transform.rotation);
            }
        }
        else
        {
            clone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            clone.transform.position = transform.position;
        }

        SetLayerRecursively(clone, LayerMask.NameToLayer("PhantomClone"));

        ActivePhantomClone = clone.transform;
        PhantomAttractRadius = _phantomAttractRadius;
        PhantomMaxAttracted = _phantomMaxAttracted;
        PhantomAttractedCount = 0;

        MeshRenderer cloneMainRenderer = clone.GetComponent<MeshRenderer>();
        if (cloneMainRenderer != null && _ghostCloneMaterials.Length > 0)
            cloneMainRenderer.sharedMaterials = _ghostCloneMaterials[0];

        foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;
        foreach (Collider col in clone.GetComponentsInChildren<Collider>())
            col.enabled = false;

        float duration = _phantomCloneDuration;

        StartCoroutine(PhantomSelfTransparency(duration));
        StartCoroutine(PhantomEscapeSpeedBoost());
        if (_healthSystem != null) _healthSystem.AddExternalInvincibility();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        OnPhantomDestroyed?.Invoke();

        ActivePhantomClone = null;
        PhantomAttractedCount = 0;
        PhantomMaxAttracted = 0;

        if (_healthSystem != null) _healthSystem.RemoveExternalInvincibility();

        Destroy(clone);
    }

    private IEnumerator PhantomEscapeSpeedBoost()
    {
        _escapeSpeedMultiplier = _phantomEscapeSpeedMultiplier;

        float elapsed = 0f;
        while (elapsed < _phantomEscapeSpeedDuration)
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused)
            { yield return null; continue; }
            elapsed += Time.deltaTime;
            yield return null;
        }

        _escapeSpeedMultiplier = 1f;
    }

    private IEnumerator PhantomSelfTransparency(float duration)
    {
        if (_isInvisible) yield break;

        SwapPlayerMaterials(_ghostSelfMaterials);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused)
            { yield return null; continue; }
            elapsed += Time.deltaTime;
            yield return null;
        }

        SwapPlayerMaterials(_originalMaterials);
    }

    private void SwapPlayerMaterials(Material[][] materialSet)
    {
        if (_playerRenderers == null || materialSet == null) return;
        for (int i = 0; i < _playerRenderers.Length && i < materialSet.Length; i++)
        {
            if (_playerRenderers[i] != null)
                _playerRenderers[i].sharedMaterials = materialSet[i];
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void StopDash()
    {
        _isDashing = false;
        _isInvincible = false;
        if (_healthSystem != null) _healthSystem.RemoveExternalInvincibility();
    }

    private void HandleAbsorptionWindow()
    {
        if (!_canAbsorb) return;
        _absorptionTimer -= Time.deltaTime;
        if (_absorptionTimer <= 0f) _canAbsorb = false;
    }

    private void UpdateDashCooldown()
    {
        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer -= Time.deltaTime;
            if (GameUI.Instance != null)
                GameUI.Instance.UpdateDashCooldown(1f - (_dashCooldownTimer / _dashCooldown));
        }
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)) return;

        if (_moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
            Quaternion smoothedRotation = Quaternion.RotateTowards(_rb.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(smoothedRotation);
        }

        Vector3 nextPosition;
        if (_isDashing)
            nextPosition = _rb.position + _dashDirection * _dashSpeed * Time.fixedDeltaTime;
        else
            nextPosition = _rb.position + _moveDirection * _moveSpeed * _speedMultiplier * _escapeSpeedMultiplier * Time.fixedDeltaTime;

        nextPosition = MapBoundaryUtils.ClampToZone(nextPosition);
        _rb.MovePosition(nextPosition);
    }

    public void AddMoveSpeed(float value) => _moveSpeed += _moveSpeed * value;
    public void ReduceDashCooldown(float value) => _dashCooldown = Mathf.Max(_dashCooldown - value, 0.5f);
    public void SetSpeedMultiplier(float multiplier) => _speedMultiplier = multiplier;

    private Coroutine _slowCoroutine;

    public void ApplyTemporarySlow(float multiplier, float duration)
    {
        if (_slowCoroutine != null) StopCoroutine(_slowCoroutine);
        _slowCoroutine = StartCoroutine(TemporarySlowRoutine(multiplier, duration));
    }

    private IEnumerator TemporarySlowRoutine(float multiplier, float duration)
    {
        SetSpeedMultiplier(multiplier);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused)
            { yield return null; continue; }
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetSpeedMultiplier(1f);
        _slowCoroutine = null;
    }
}