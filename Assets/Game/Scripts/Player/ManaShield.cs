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
    // MODIFIE (2026-09-16) - vert plutôt que le cyan #2DD4CF d'origine (retour
    // utilisateur : "c'est représentatif de Kael").
    [SerializeField] private Color _bubbleColor = new Color(0.212f, 0.851f, 0.212f, 0.16f); // #36D936 translucide
    // MODIFIE (2026-09-16) - valeurs reprises telles quelles des reglages
    // trouves par l'utilisateur en testant en jeu (Inspector de la capsule :
    // Scale (1.7, 1.6, 1.7) -> rayon = 1.7/2, hauteur = 1.6*2).
    [Tooltip("Rayon de la capsule en mètres. À ajuster selon la taille du modèle du personnage.")]
    [SerializeField] private float _bubbleRadius = 0.85f;
    [Tooltip("Hauteur totale de la capsule en mètres (bout à bout, extrémités rondes comprises). À ajuster selon la taille du modèle.")]
    [SerializeField] private float _bubbleHeight = 3.2f;
    // CORRIGE (2026-09-16) - retour utilisateur : "la bulle est en Y = 3 au
    // lieu de 1.5". Le pivot racine du joueur (transform.position) est DEJA a
    // Y=1.5 en jeu (confirme sur les 3 prefabs : localPosition=(0,1.5,0), et
    // en live testing ou la bulle reelle atterrissait a Y=2.99 = 1.5 (pivot) +
    // 1.49 (ancien offset)) - ce n'est pas un pivot au sol comme suppose a
    // tort precedemment. Offset a 0 pour que la bulle reste exactement au
    // pivot du joueur (~Y=1.5, hauteur torse).
    [Tooltip("Décalage du centre de la bulle par rapport au pivot du joueur. Le pivot racine du joueur est déjà à hauteur de torse (~Y=1.5) en jeu, donc 0 par défaut.")]
    [SerializeField] private Vector3 _bubbleCenterOffset = Vector3.zero;
    // MODIFIE (2026-09-16) - retour utilisateur : incertain de vouloir garder
    // la bulle visible en PERMANENCE pendant toute la run - "une option serait
    // de l'afficher que si il prend un projectile, là ce serait bien". La
    // bulle est desormais invisible au repos (plus de _bubbleEmptyAlpha/alpha
    // de base lie aux charges restantes) et ne flashe que reactivement au
    // moment ou elle absorbe un coup (voir _hitFlash dans UpdateBubble),
    // avant de s'effacer a nouveau - le HUD (pips) reste la seule indication
    // permanente du nombre de charges restantes.
    [Tooltip("Alpha maximum de la bulle au pic du flash d'absorption (juste après avoir bloqué un projectile), avant qu'elle ne s'efface à nouveau.")]
    [SerializeField] private float _hitFlashPeakAlpha = 0.35f;
    [Tooltip("Durée totale du flash (pic -> invisible) après une absorption. Retour utilisateur : ~0,3s d'origine disparaissait trop vite.")]
    [SerializeField] private float _hitFlashDuration = 2.5f;

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
            // MODIFIE (2026-09-16) - vitesse de decroissance derivee de
            // _hitFlashDuration (avant : dt*3f, un decroissance fixe en ~0,33s
            // - retour utilisateur, "elle devrait aussi rester au moins 2 sec
            // de plus quand elle est declenchee").
            _hitFlash = Mathf.MoveTowards(_hitFlash, 0f, dt / _hitFlashDuration);

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
        // MODIFIE (2026-09-16) - capsule plutôt que sphère (retour utilisateur) :
        // enveloppe le gabarit debout d'un personnage bien mieux qu'une boule
        // uniforme. Capsule primitive Unity : rayon 0,5 et hauteur 2 à l'échelle
        // 1 - voir le calcul de localScale plus bas pour _bubbleRadius/_bubbleHeight.
        _bubble = GameObject.CreatePrimitive(PrimitiveType.Capsule);
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
        // CORRIGE (2026-09-16) - retour utilisateur : "elle n'est pas
        // transparente, elle va dessus Kael et on ne voit que la sphère".
        // _Surface seul ne suffit pas a activer le blend alpha sur le shader
        // URP/Unlit - il manquait _ALPHABLEND_ON (et _Blend), sans quoi le
        // shader ignore le canal alpha et rend en quasi-opaque quelle que soit
        // sa valeur. Meme reglage complet que PlayerController.CreateGhostMaterial()
        // (seul autre endroit du projet qui cree un materiau URP transparent a
        // la volée, et qui fonctionne correctement) plutôt que redeviner.
        mat.SetFloat("_Surface", 1f); // 0 = opaque, 1 = transparent (URP/Unlit)
        mat.SetFloat("_Blend", 0f); // 0 = Alpha
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        // CORRIGE (2026-09-16) - LA vraie cause du "pas transparente du tout" :
        // _Surface/_Blend/les mots-cles ci-dessus sont juste des SELECTEURS
        // logiques - l'etat de blend GPU reel est lu sur _SrcBlend/_DstBlend,
        // que rien ne derive automatiquement d'eux en dehors du ShaderGUI de
        // l'Inspector (qui ne s'execute jamais pour un materiau cree par
        // script). Sans ce reglage explicite, ils restaient a leur valeur par
        // defaut "One/Zero" (= opaque, ecrase completement le pixel derriere)
        // quel que soit l'alpha demande - d'ou la bulle qui masquait
        // totalement Kael au lieu d'etre translucide (retour utilisateur).
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetShaderPassEnabled("DepthOnly", false);
        _bubbleRenderer.material = mat;

        _bubbleBlock = new MaterialPropertyBlock();

        ApplyBubbleScale();
        _bubble.transform.position = transform.position + _bubbleCenterOffset;
    }

    // AJOUTE (2026-09-16) - la capsule primitive Unity a un rayon de 0,5 et une
    // hauteur de 2 a l'echelle (1,1,1) - convertit _bubbleRadius/_bubbleHeight
    // (en metres, tels qu'exposes dans l'Inspector) vers le localScale
    // correspondant. X/Z pilotent le rayon, Y pilote la hauteur totale.
    private void ApplyBubbleScale()
    {
        _bubble.transform.localScale = new Vector3(_bubbleRadius * 2f, _bubbleHeight * 0.5f, _bubbleRadius * 2f);
    }

    private void UpdateBubble()
    {
        if (_bubble == null) return;

        _bubble.transform.position = transform.position + _bubbleCenterOffset;

        // MODIFIE (2026-09-16) - la bulle n'existe plus visuellement qu'au
        // moment du flash d'absorption (_hitFlash, remis à 1 dans TryAbsorb()
        // puis redescend vers 0) - voir la note sur _hitFlashPeakAlpha plus haut.
        float alpha = Mathf.Clamp01(_hitFlash * _hitFlashPeakAlpha);

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
