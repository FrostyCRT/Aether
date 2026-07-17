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

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 700f;

    [Header("Modèle 3D")]
    [SerializeField] private string _modelChildName = "stylized_character_3d_model";

    [Header("Clone Fantôme (touche dédiée)")] // AJOUTÉ — tout ce bloc
    [SerializeField] private KeyCode _phantomCloneKey = KeyCode.C;
    [SerializeField] private float _phantomCloneCooldown = 8f;
    [SerializeField] private float _phantomCloneDuration = 2f;
    [SerializeField] private float _phantomAttractRadius = 10f;
    [SerializeField] private int _phantomMaxAttracted = 14;
    [SerializeField] private float _phantomEscapeSpeedMultiplier = 1.5f; // +50%
    [SerializeField] private float _phantomEscapeSpeedDuration = 1.2f;
    [SerializeField] private Color _cloneTint = new Color(0.55f, 0.7f, 1f);
    [SerializeField] private float _cloneAlpha = 0.55f;
    [SerializeField] private float _phantomSelfAlpha = 0.45f;

    private float _phantomCloneCooldownTimer = 0f;
    public float PhantomCloneCooldownPercent => _phantomCloneCooldownTimer / _phantomCloneCooldown; // AJOUTÉ — à toi de le brancher sur une UI si tu veux l'afficher, pas d'équivalent existant côté GameUI

    private Rigidbody _rb;
    private HealthSystem _healthSystem;
    private Vector3 _moveDirection;
    private float _speedMultiplier = 1f;
    private float _escapeSpeedMultiplier = 1f; // AJOUTÉ — dédié, séparé de _speedMultiplier pour éviter le même bug que côté ennemi

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

    // AJOUTÉ — plafond partagé, utilisé à la fois par le burst initial ET par EnemyBase.OnEnable() pour les spawns tardifs
    public static float PhantomAttractRadius { get; private set; }
    public static int PhantomAttractedCount { get; private set; }
    public static int PhantomMaxAttracted { get; private set; }

    public static bool TryAttractToPhantom(EnemyBase enemy)
    {
        if (ActivePhantomClone == null) return false;
        if (PhantomAttractedCount >= PhantomMaxAttracted) return false;
        PhantomAttractedCount++;
        enemy.SetTarget(ActivePhantomClone, 2f);
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
    private Material[][] _originalMaterials;
    private Material[][] _ghostSelfMaterials;
    private Material[][] _ghostCloneMaterials;


    public void ActivateInvisibility(float duration)
    {
        _isInvisible = true;
        _invisibilityTimer = duration;
        _blinkTimer = 0f;

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (playerLayer != -1 && enemyLayer != -1)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        }
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
            {
                Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
            }
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

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _healthSystem = GetComponent<HealthSystem>();
        _crystalSystem = GetComponent<CrystalSystem>();
        _animatorController = GetComponent<PlayerAnimatorController>();

        _modelTransform = transform.Find(_modelChildName);
        if (_modelTransform != null)
        {
            _playerRenderers = _modelTransform.GetComponentsInChildren<Renderer>();
        }
        else
        {
            Debug.LogWarning($"PlayerController : enfant '{_modelChildName}' introuvable, fallback sur tout le Player (risque de bug capsule).");
            _playerRenderers = GetComponentsInChildren<Renderer>();
        }

        PrecomputeGhostMaterials();

        _moveSpeed += _moveSpeed * MetaProgressionManager.Instance.GetBonusAgility();
        _dashCooldown -= MetaProgressionManager.Instance.GetBonusDashCooldown();
        _dashCooldown = Mathf.Max(_dashCooldown, 1f);
    }

    private void PrecomputeGhostMaterials()
    {
        if (_playerRenderers == null) return;

        _originalMaterials = new Material[_playerRenderers.Length][];
        _ghostSelfMaterials = new Material[_playerRenderers.Length][];
        _ghostCloneMaterials = new Material[_playerRenderers.Length][];

        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            Material[] originals = _playerRenderers[i].sharedMaterials;
            _originalMaterials[i] = originals;

            Material[] ghostSelf = new Material[originals.Length];
            Material[] ghostClone = new Material[originals.Length];

            for (int j = 0; j < originals.Length; j++)
            {
                ghostSelf[j] = CreateGhostMaterial(originals[j], Color.white, _phantomSelfAlpha, 0f); // pas de teinte pour le joueur, juste l'alpha
                ghostClone[j] = CreateGhostMaterial(originals[j], _cloneTint, _cloneAlpha, 0.4f); // mélange doux vers le bleu pour le clone
            }

            _ghostSelfMaterials[i] = ghostSelf;
            _ghostCloneMaterials[i] = ghostClone;
        }
    }

    private Material CreateGhostMaterial(Material source, Color tintMultiply, float alpha, float tintBlend)
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
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetInt("_ZWrite", 1); // MODIFIÉ — voir point 2
        mat.SetShaderPassEnabled("DepthOnly", false);

        return mat;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        HandleMovementInput();
        HandleDash();
        HandlePhantomCloneInput(); // AJOUTÉ
        HandleAbsorptionWindow();
        UpdateDashCooldown();
        UpdatePhantomCloneCooldown(); // AJOUTÉ
        HandleInvisibilityTimer();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        _moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (_animatorController != null)
        {
            bool isMoving = _moveDirection.sqrMagnitude > 0.01f;
            _animatorController.SetWalking(isMoving);
        }
    }

    private void HandleDash()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && !_isDashing && _dashCooldownTimer <= 0f)
        {
            Vector3 direction = _moveDirection != Vector3.zero ? _moveDirection : transform.forward;
            StartDash(direction);
        }

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
                StopDash();
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

        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(true);
        if (GameUI.Instance != null) GameUI.Instance.UpdateDashCooldown(0f);

        // MODIFIÉ — le clone n'est plus déclenché par le dash, entièrement retiré d'ici
    }

    // AJOUTÉ — bloc entier, capacité autonome sur la touche C
    private void HandlePhantomCloneInput()
    {
        if (_phantomCloneCooldownTimer > 0f) return;
        if (!MetaProgressionManager.Instance.HasPhantomDash()) return; // note : nom hérité de l'ancien système, à renommer côté MetaProgressionManager quand tu auras le temps (HasPhantomClone() serait plus clair)

        if (Input.GetKeyDown(_phantomCloneKey))
        {
            _phantomCloneCooldownTimer = _phantomCloneCooldown;
            StartCoroutine(SpawnPhantomClone());
        }
    }

    private void UpdatePhantomCloneCooldown()
    {
        if (_phantomCloneCooldownTimer > 0f)
            _phantomCloneCooldownTimer -= Time.deltaTime;
    }

    private IEnumerator SpawnPhantomClone()
    {
        GameObject clone;

        if (_modelTransform != null)
        {
            clone = Instantiate(_modelTransform.gameObject, transform.position, transform.rotation);
        }
        else
        {
            clone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            clone.transform.position = transform.position;
        }

        clone.transform.localScale = _modelTransform != null ? _modelTransform.lossyScale : transform.localScale;
        SetLayerRecursively(clone, LayerMask.NameToLayer("PhantomClone"));

        ActivePhantomClone = clone.transform;
        PhantomAttractRadius = _phantomAttractRadius; // AJOUTÉ
        PhantomMaxAttracted = _phantomMaxAttracted;   // AJOUTÉ
        PhantomAttractedCount = 0;                    // AJOUTÉ

        Renderer[] cloneRenderers = clone.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < cloneRenderers.Length && i < _ghostCloneMaterials.Length; i++)
        {
            cloneRenderers[i].sharedMaterials = _ghostCloneMaterials[i];
        }

        foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;
        foreach (Collider col in clone.GetComponentsInChildren<Collider>()) // sécurité anti-collision fantôme, gardé même si le modèle n'a normalement pas de collider propre
            col.enabled = false;

        float duration = _phantomCloneDuration;

        // AJOUTÉ — les 3 effets d'échappement du joueur, tous indépendants les uns des autres
        StartCoroutine(PhantomSelfTransparency(duration));
        StartCoroutine(PhantomEscapeSpeedBoost());
        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(true);

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

        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(false); // AJOUTÉ — désactive l'immunité en même temps que le reste

        Destroy(clone);
    }

    // AJOUTÉ — boost de vitesse temporaire pour laisser le temps de fuir
    private IEnumerator PhantomEscapeSpeedBoost()
    {
        _escapeSpeedMultiplier = _phantomEscapeSpeedMultiplier;

        float elapsed = 0f;
        while (elapsed < _phantomEscapeSpeedDuration)
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused)
            {
                yield return null;
                continue;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        _escapeSpeedMultiplier = 1f;
    }

    private IEnumerator PhantomSelfTransparency(float duration)
    {
        if (_isInvisible) yield break; // évite un conflit visuel si Second Souffle est actif en même temps

        SwapPlayerMaterials(_ghostSelfMaterials);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused)
            {
                yield return null;
                continue;
            }
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
        if (_healthSystem != null) _healthSystem.SetInvincibleExternal(false);
    }

    private void HandleAbsorptionWindow()
    {
        if (!_canAbsorb) return;

        _absorptionTimer -= Time.deltaTime;
        if (_absorptionTimer <= 0f)
            _canAbsorb = false;
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
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

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
            nextPosition = _rb.position + _moveDirection * _moveSpeed * _speedMultiplier * _escapeSpeedMultiplier * Time.fixedDeltaTime; // MODIFIÉ — ajout du multiplicateur d'échappement

        nextPosition = MapBoundaryUtils.ClampToZone(nextPosition);

        _rb.MovePosition(nextPosition);
    }

    public static class MapBoundaryUtils
    {
        public const float ZoneHalfSize = 55f;

        public static Vector3 ClampToZone(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, -ZoneHalfSize, ZoneHalfSize);
            position.z = Mathf.Clamp(position.z, -ZoneHalfSize, ZoneHalfSize);
            return position;
        }
    }

    public void AddMoveSpeed(float value)
    {
        _moveSpeed += _moveSpeed * value;
    }

    public void ReduceDashCooldown(float value)
    {
        _dashCooldown = Mathf.Max(_dashCooldown - value, 0.5f);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }
}