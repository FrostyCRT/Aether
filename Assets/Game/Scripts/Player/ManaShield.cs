using UnityEngine;

// Nœud capstone de la branche Gardien : "Bouclier de Mana".
//
// Barrière rechargeable à charges. Chaque projectile ennemi qui toucherait le
// joueur (hors i-frames de dash) consomme 1 charge et voit ses dégâts TOTALEMENT
// annulés, puis alimente le Cristal exactement comme une absorption au dash
// (Nova + progression de la jauge + soin Récupération éventuel) — la défense
// devient une ressource offensive, cohérent avec l'identité du Cristal.
//
// Recharge 1 charge toutes les _rechargeInterval secondes, jusqu'à _maxCharges.
// Si les charges tombent à 0, la recharge est verrouillée _breakLockoutDuration
// secondes : se faire submerger casse vraiment le bouclier.
//
// Le composant vit sur les 3 prefabs Player mais s'auto-neutralise (_active =
// false) si le nœud n'est pas acheté / si le personnage joué n'est pas Gardien
// (MetaProgressionManager.HasManaShield() gère les deux conditions).
//
// Portée volontairement limitée aux projectiles ennemis (EnemyProjectile) : ni
// dégâts de contact, ni charge de boss, ni zones au sol. "La bulle bouffe les
// balles" — lisible, et ça ne trivialise pas le reste du danger.
[RequireComponent(typeof(CrystalSystem))]
public class ManaShield : MonoBehaviour
{
    [Header("Charges")]
    [SerializeField] private int _maxCharges = 3;
    [Tooltip("Temps pour regagner 1 charge (hors verrou de casse).")]
    [SerializeField] private float _rechargeInterval = 3f;
    [Tooltip("Durée pendant laquelle la recharge est bloquée après que les charges soient tombées à 0.")]
    [SerializeField] private float _breakLockoutDuration = 4f;

    [Header("Bulle (visuel procédural, aucun asset requis)")]
    [SerializeField] private Color _bubbleColor = new Color(0.176f, 0.831f, 0.812f, 0.22f); // #2DD4CF translucide
    [Tooltip("Rayon de la bulle en mètres. À ajuster selon la taille du modèle du personnage.")]
    [SerializeField] private float _bubbleRadius = 1.1f;
    [Tooltip("Décalage du centre de la bulle par rapport au pivot du joueur (généralement +Y pour centrer sur le torse).")]
    [SerializeField] private Vector3 _bubbleCenterOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("Alpha de la bulle quand il ne reste plus aucune charge (bouclier à terre mais toujours visible).")]
    [SerializeField] private float _bubbleEmptyAlpha = 0.05f;

    private bool _active;
    private int _charges;
    private float _rechargeTimer;
    private float _lockoutTimer;
    private float _hitFlash; // 0..1, décroît, pic à chaque projectile absorbé

    private CrystalSystem _crystal;

    private GameObject _bubble;
    private Renderer _bubbleRenderer;
    private MaterialPropertyBlock _bubbleBlock;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    public bool IsActive => _active;
    public int Charges => _charges;
    public int MaxCharges => _maxCharges;
    public bool IsLocked => _lockoutTimer > 0f;

    private void Awake()
    {
        _crystal = GetComponent<CrystalSystem>();
        _active = MetaProgressionManager.Instance != null
                  && MetaProgressionManager.Instance.HasManaShield();

        if (_active)
        {
            _charges = _maxCharges;
            BuildBubble();
        }
    }

    private void Start()
    {
        if (GameUI.Instance != null)
            GameUI.Instance.SetManaShieldAvailable(_active);

        if (_active)
            PushHUD();
    }

    private void Update()
    {
        if (!_active) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;

        float dt = Time.deltaTime;

        if (_lockoutTimer > 0f)
        {
            _lockoutTimer -= dt;
            if (_lockoutTimer <= 0f)
            {
                _lockoutTimer = 0f;
                _rechargeTimer = 0f;
                PushHUD(); // le verrou vient de sauter : le HUD repasse en couleur normale
            }
        }
        else if (_charges < _maxCharges)
        {
            _rechargeTimer += dt;
            if (_rechargeTimer >= _rechargeInterval)
            {
                _rechargeTimer -= _rechargeInterval;
                _charges++;
                PushHUD();
            }
        }

        if (_hitFlash > 0f)
            _hitFlash = Mathf.MoveTowards(_hitFlash, 0f, dt * 3f);

        UpdateBubble();
    }

    // Appelé par EnemyProjectile quand un tir toucherait le joueur NON invincible.
    // Retourne true si le projectile a été absorbé (l'appelant annule alors les
    // dégâts et despawn le projectile).
    public bool TryAbsorb()
    {
        if (!_active || _charges <= 0 || _lockoutTimer > 0f) return false;

        _charges--;
        _hitFlash = 1f;

        if (_charges == 0)
        {
            _lockoutTimer = _breakLockoutDuration;
            ExpandingRingVFX.Spawn(
                transform.position + _bubbleCenterOffset,
                _bubbleRadius * 2f,
                new Color(_bubbleColor.r, _bubbleColor.g, _bubbleColor.b, 0.8f),
                0.3f);
        }

        // Défense -> ressource : même traitement qu'une absorption au dash
        // (déclenche la Nova, fait progresser la jauge de Cristal, applique le
        // soin Récupération si le nœud Gardien correspondant est pris).
        if (_crystal != null)
            _crystal.AbsorbProjectile();

        PushHUD();
        return true;
    }

    private void PushHUD()
    {
        if (GameUI.Instance != null)
            GameUI.Instance.UpdateManaShield(_charges, _maxCharges, _lockoutTimer > 0f);
    }

    // ----- Bulle procédurale -----

    private void BuildBubble()
    {
        _bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _bubble.name = "ManaShieldBubble";

        Collider col = _bubble.GetComponent<Collider>();
        if (col != null) Destroy(col);

        _bubbleRenderer = _bubble.GetComponent<Renderer>();
        _bubbleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _bubbleRenderer.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.color = _bubbleColor;
        mat.SetFloat("_Surface", 1f); // 0 = opaque, 1 = transparent (URP/Unlit)
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _bubbleRenderer.material = mat;

        _bubbleBlock = new MaterialPropertyBlock();

        float d = _bubbleRadius * 2f;
        _bubble.transform.localScale = new Vector3(d, d, d);
        _bubble.transform.position = transform.position + _bubbleCenterOffset;
    }

    private void UpdateBubble()
    {
        if (_bubble == null) return;

        _bubble.transform.position = transform.position + _bubbleCenterOffset;

        float chargeRatio = _maxCharges > 0 ? (float)_charges / _maxCharges : 0f;
        float baseAlpha = Mathf.Lerp(_bubbleEmptyAlpha, _bubbleColor.a, chargeRatio);
        float alpha = Mathf.Clamp01(baseAlpha + _hitFlash * 0.5f);

        Color c = new Color(_bubbleColor.r, _bubbleColor.g, _bubbleColor.b, alpha);

        _bubbleRenderer.GetPropertyBlock(_bubbleBlock);
        _bubbleBlock.SetColor(BaseColorID, c);
        _bubbleBlock.SetColor(ColorID, c);
        _bubbleRenderer.SetPropertyBlock(_bubbleBlock);
    }

    private void OnDestroy()
    {
        if (_bubble != null) Destroy(_bubble);
    }
}
