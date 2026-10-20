using UnityEngine;
using System.Collections.Generic;

// REFONTE (2026-10-04, plusieurs passes suite aux retours utilisateur) - l'XP est le cœur de la boucle de jeu
// (des centaines de ramassages par run), donc ça mérite un vrai traitement plutôt qu'une sphère Unity brute.
// 3 états qui s'enchaînent automatiquement : Popping (jaillit du point de mort en petit arc) -> Idle (flotte,
// respire) -> Attracted (accélère vers le joueur, traînée lumineuse). Au contact : éclate (XPGemCollectFX) au
// lieu de disparaître en silence.
//
// REFONTE (2026-10-05, suite 15) - retour utilisateur, 3 captures zoomées fournies (icônes vert/bleu/or) avec
// consigne explicite : "le relief blanc [...] plus fort en bas à gauche, et à l'opposé c'est un peu plus sombre
// [...] je veux que ça ressemble pixel à ça". Mesure pixel par pixel des 3 références (script Python, grille de
// couleurs + détection de silhouette) : ce n'est PAS un dégradé linéaire gauche/droite à ligne nette (suite 13/14)
// mais un DÉGRADÉ RADIAL DOUX, centré sur un point décalé bas-gauche (confirmé par mesure : le pixel le plus clair
// tombe systématiquement à x≈0,41-0,43 / y≈0,60-0,76 de la boîte englobante sur les 3 références), fondant vers la
// couleur de base puis vers un bord plus sombre côté opposé (haut-droite) - un dégradé radial linéaire par sommet
// ne peut pas reproduire ça. Passé en conséquence à une VRAIE TEXTURE générée (voir Tex_XPGem_*.png, silhouette +
// dégradé radial + liseré sombre cuits dedans) plaquée sur un quad simple, au lieu du mesh à 2 triangles à couleur
// de sommet bakée. Le contour (mesh de coque séparé, suites précédentes) est donc supprimé : le liseré est
// désormais cuit dans la texture elle-même, ce qui élimine du même coup toute la classe de bug "éclat noir"
// (tri de 2 meshes transparents quasi coïncidents, suite 11) puisqu'il n'y a plus qu'un seul quad à trier.
// - Orientation face-caméra par Quaternion.LookRotation, billboard DYNAMIQUE recalculé chaque frame (suite 14,
//   toujours valable) - sans quoi un quad plat mal aligné avec la caméra réelle se voit "de biais".
public class XPGem : MonoBehaviour
{
    public enum GemType { Small, Medium, Large }
    private enum GemState { Popping, Idle, Attracted }

    private GemType _gemType;
    private GemState _state;

    // AJOUTÉ (2026-10-20) - voir le commentaire sur perInstanceTieBreak dans Awake() : compteur partagé par TOUTES
    // les gemmes, jamais remis à zéro, utilisé uniquement modulo 7 (jamais lu en valeur absolue).
    private static int _spawnCounter = 0;

    private float _xpValue = 10f;
    private float _baseMoveSpeed = 8f;
    private float _maxMoveSpeed = 17f;
    private float _speedBoostMultiplier = 1f;
    private float _attractRampTimer = 0f;
    private const float AttractRampDuration = 0.45f;

    private Transform _playerTransform;
    private string _poolKey;
    private Color _gemColor = Color.white; // conservée pour XPGemCollectFX (voir Collect()) - plus utilisée pour le rendu du corps (voir note suite 13)
    private float _gemScale = 0.3f;

    private const float PopDuration = 0.3f;
    private const float PopArcHeight = 0.8f;
    private const float PopSpinDegrees = 360f; // flourish d'arrivée uniquement - pas de rotation continue ensuite

    // CORRIGE (2026-10-04, suite 14) - retour utilisateur : "le relief n'est pas le même, et il a l'air plus fin
    // [...] le contour aussi n'est pas le même". Cause : la suite 13 figeait l'orientation sur UNE SEULE mesure
    // de l'angle de Camera.main, prise une fois en Play. En vraie partie, la caméra suit le joueur (Cinemachine,
    // damping/décalage) et son angle réel varie légèrement frame par frame - un quad PLAT dont l'orientation ne
    // correspond plus exactement à cet angle se voit "de biais" (en partie de profil), ce qui réduit la largeur
    // apparente du losange (le "plus fin" du retour) ET déforme le ratio clair/sombre et le contour en conséquence
    // (même cause que la vraie photo de la caméra mal alignée fausse les deux à la fois). Corrigé : vrai billboard
    // DYNAMIQUE, recalculé CHAQUE frame depuis la direction réelle de Camera.main (jamais une valeur figée) - le
    // quad fait alors face à la caméra exactement, quoi qu'il arrive à son angle réel en jeu.
    private static Camera _cachedCamera;

    private static Quaternion GetBillboardRotation()
    {
        if (_cachedCamera == null) _cachedCamera = Camera.main;
        if (_cachedCamera == null) return Quaternion.identity;
        return Quaternion.LookRotation(-_cachedCamera.transform.forward, Vector3.up);
    }

    private Vector3 _popOrigin;
    private Vector3 _popTarget;
    private float _popTimer;

    private const float IdleBobAmplitude = 0.1f;
    private const float IdleBobSpeed = 2.6f;
    private const float BasePulseSpeed = 1.5f;
    private const float MinGlow = 1.0f;
    private const float MaxGlow = 1.3f; // resserré (suite 13) - la couleur finale est deja bakee dans la texture, le pulse ne doit que scintiller legerement, pas re-saturer
    private float _idlePhase;
    private float _pulseSpeedMul; // chaque gemme respire à son propre rythme, pas en choeur
    private float _timeAlive;

    private const float CollectDistSqr = 0.4f * 0.4f;

    private MeshRenderer _bodyRenderer;
    private TrailRenderer _trail;
    private MaterialPropertyBlock _propBlock;
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private static readonly Dictionary<GemType, Material> _gemMaterialByType = new Dictionary<GemType, Material>();
    private static readonly Dictionary<GemType, string> _gemTextureResourcePath = new Dictionary<GemType, string>
    {
        { GemType.Small, "Textures/XPGem/Tex_XPGem_Small" },
        { GemType.Medium, "Textures/XPGem/Tex_XPGem_Medium" },
        { GemType.Large, "Textures/XPGem/Tex_XPGem_Large" },
    };
    private static Mesh _gemQuadMesh;

    // Quad plat dans le plan XY local (Z=0), face tournée vers +Z - une fois tourné par GetBillboardRotation, ce
    // +Z local pointe exactement vers la caméra. Plus grand que le losange réellement visible dans la texture
    // (voir Tex_XPGem_*.png, généré avec une marge transparente de ~12%/5% pour l'anti-aliasing et le liseré),
    // pour que le losange rendu occupe la même taille à l'écran qu'aux passes précédentes (0,47 / 0,88 et
    // 0,46 / 0,95 - voir le script de génération des textures, AW/AH).
    private const float HalfWidth = 0.534f;
    private const float HalfHeight = 0.484f;

    private static Mesh BuildGemQuadMesh()
    {
        if (_gemQuadMesh != null) return _gemQuadMesh;

        var vertices = new List<Vector3>
        {
            new Vector3(-HalfWidth, -HalfHeight, 0f),
            new Vector3(HalfWidth, -HalfHeight, 0f),
            new Vector3(HalfWidth, HalfHeight, 0f),
            new Vector3(-HalfWidth, HalfHeight, 0f),
        };
        var uvs = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        var triangles = new List<int> { 0, 1, 2, 0, 2, 3 };

        Mesh mesh = new Mesh { name = "XPGemQuadMesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        _gemQuadMesh = mesh;
        return _gemQuadMesh;
    }

    // "Sprites/Default" (CGPROGRAM, déjà utilisé partout dans le projet - TrailRenderer, FXSprites) confirmé
    // fonctionnel en 3D sur un MeshRenderer dans cette config URP.
    private const int BodyRenderQueue = 3000;

    // Matériau par GemType (texture différente), mis en cache - la texture encode directement la silhouette
    // (alpha), le dégradé radial et le liseré sombre (voir suite 15 plus haut), donc un seul quad textué suffit
    // là où il fallait avant un corps + un contour séparés.
    private static Material BuildGemMaterial(GemType type)
    {
        if (_gemMaterialByType.TryGetValue(type, out Material cached) && cached != null) return cached;

        Texture2D tex = Resources.Load<Texture2D>(_gemTextureResourcePath[type]);
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        mat.renderQueue = BodyRenderQueue;
        _gemMaterialByType[type] = mat;
        return mat;
    }

    // Tag du pool, tel qu'utilisé PARTOUT pour l'obtenir (XPGemSpawner.SpawnGems/SpawnBossGemBurst, DebugCheats).
    // CORRIGE (2026-10-04) - BUG PRÉEXISTANT trouvé en testant la collecte : l'ancien calcul
    // `name.Replace("(Clone)", "")` donnait "PFB_XPGem" (le nom réel du prefab), qui ne correspond PAS au tag
    // "XPGem" sous lequel le pool est enregistré - ObjectPool.ReturnToPool ne trouvait jamais ce tag et DÉTRUISAIT
    // la gemme au lieu de la recycler, à CHAQUE ramassage d'XP de toute la partie, depuis le début.
    private const string PoolTag = "XPGem";

    private void Awake()
    {
        _poolKey = PoolTag;
        _propBlock = new MaterialPropertyBlock();

        _bodyRenderer = GetComponent<MeshRenderer>();
        if (_bodyRenderer != null)
        {
            _bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _bodyRenderer.receiveShadows = false;

            // MODIFIÉ (2026-10-20, retour utilisateur : "les xp passent devant les ennemis [...] ça devrait être
            // l'inverse", + "quand il y a plein d'xp qui se superpose [...] des fois il y a bug d'affichage entre
            // elles") - sortingOrder fixe à 1 pour TOUTES les gemmes alors qu'aucun script ennemi ne fixe de
            // sortingOrder (ils restent à 0, valeur par défaut) : en tri par ordre explicite (prioritaire sur le
            // tri par distance caméra, Default URP), une gemme à 1 gagnait TOUJOURS contre un ennemi à 0, quelle
            // que soit la position réelle des deux. Base descendue à -1 (toujours sous les ennemis, jamais l'ordre
            // inverse souhaité). Par ailleurs, TOUTES les gemmes partageant le MÊME sortingOrder (1 avant, -1
            // maintenant) retombaient entre elles sur le tri par distance caméra - instable/scintillant quand 2
            // gemmes sont quasi à la même profondeur (gemmes qui flottent superposées). Décalage stable PAR
            // INSTANCE ajouté pour départager les gemmes entre elles sans jamais dépasser 0 (toujours sous les
            // ennemis). ESSAYÉ PUIS ABANDONNÉ : dérivé de GetInstanceID() - les ID Unity attribués à des objets créés
            // dans la même rafale (plusieurs gemmes à la mort d'ennemis proches) avancent par PAS CONSTANT (16 au
            // test), donc un modulo d'une puissance de 2 (4) retombait quasi toujours sur le même reste - aucune
            // différenciation réelle, vérifié en Play (10 gemmes instanciées : 9 sur 10 au même sortingOrder).
            // Remplacé par un compteur statique propre à la classe, incrémenté à chaque Awake() : distribution
            // parfaitement round-robin, indépendante de tout detail d'implémentation d'Unity.
            int perInstanceTieBreak = _spawnCounter++ % 7;
            _bodyRenderer.sortingOrder = -1 - perInstanceTieBreak;
        }

        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter != null) filter.sharedMesh = BuildGemQuadMesh();

        // Traînée lumineuse, active UNIQUEMENT pendant l'aimantation (voir UpdateAttracted).
        _trail = gameObject.AddComponent<TrailRenderer>();
        _trail.time = 0.14f;
        _trail.minVertexDistance = 0.04f;
        _trail.widthMultiplier = 0.18f;
        _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        _trail.material = new Material(Shader.Find("Sprites/Default"));
        _trail.emitting = false;
        _trail.sortingOrder = 5;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
    }

    // landPosition : position finale au sol (monde) où la gemme doit atterrir après son petit saut de "popping".
    // xpOverride : optionnel - utilisé par XPGemSpawner.SpawnBossGemBurst() pour faire porter plus d'XP à une
    // grosse gemme plutôt que de multiplier le nombre d'objets sur un gros total (voir ce fichier).
    public void Init(GemType type, Transform player, Vector3 landPosition, float? xpOverride = null)
    {
        _gemType = type;
        _playerTransform = player;
        _state = GemState.Popping;
        _speedBoostMultiplier = 1f;
        _attractRampTimer = 0f;
        _timeAlive = 0f;
        _idlePhase = Random.Range(0f, Mathf.PI * 2f);
        _pulseSpeedMul = Random.Range(0.7f, 1.4f); // chaque gemme respire à son propre rythme, pas en choeur

        _popOrigin = transform.position;
        _popTarget = landPosition;
        _popTimer = 0f;

        if (_trail != null) { _trail.emitting = false; _trail.Clear(); }

        // Palette calquée sur la référence Survivor.io (vert/bleu/or).
        switch (type)
        {
            case GemType.Small:
                _xpValue = 10f;
                _gemScale = 0.46f;
                _gemColor = new Color(0.25f, 0.95f, 0.35f); // vert
                break;

            case GemType.Medium:
                _xpValue = 20f;
                _gemScale = 0.6f;
                _gemColor = new Color(0.2f, 0.6f, 1f); // bleu
                break;

            case GemType.Large:
                _xpValue = 50f;
                _gemScale = 0.8f;
                _gemColor = new Color(1f, 0.8f, 0.15f); // or
                break;
        }

        if (xpOverride.HasValue) _xpValue = xpOverride.Value;

        if (_bodyRenderer != null) _bodyRenderer.sharedMaterial = BuildGemMaterial(type);

        transform.localScale = Vector3.zero; // popping fera grandir depuis 0 (rebond EaseOutBack)
        transform.rotation = GetBillboardRotation(); // face caméra dès l'apparition

        if (_trail != null) _trail.startColor = FXSprites.A(_gemColor, 0.55f);
        if (_trail != null) _trail.endColor = FXSprites.A(_gemColor, 0f);

        ApplyGlow(MinGlow);
    }

    // La couleur finale (dégradé radial + liseré) est BAKÉE DIRECTEMENT dans la texture (voir BuildGemMaterial,
    // suite 15). _Color ne sert donc qu'à moduler l'INTENSITÉ du scintillement (valeur de gris neutre, jamais une
    // teinte, multipliée avec la texture par le shader Sprites/Default) - sans quoi teinter une 2e fois
    // écraserait le dégradé déjà baké.
    private void ApplyGlow(float intensity)
    {
        if (_bodyRenderer != null)
        {
            _bodyRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(ColorID, new Color(intensity, intensity, intensity, 1f));
            _bodyRenderer.SetPropertyBlock(_propBlock);
        }
    }

    public void ForceAttract()
    {
        BeginAttraction(1f, 1f); // ramp naturel, pas de boost de vitesse
    }

    public void ForceAttractFast(float speedMultiplier = 1.6f)
    {
        BeginAttraction(1f, speedMultiplier); // deja a vitesse max immediatement, en plus boostee
    }

    private void BeginAttraction(float rampProgress, float speedBoost)
    {
        _state = GemState.Attracted;
        _attractRampTimer = AttractRampDuration * rampProgress;
        _speedBoostMultiplier = speedBoost;
        if (_trail != null) _trail.emitting = true;
    }

    private void Update()
    {
        if (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)) return;
        if (_playerTransform == null) return;

        _timeAlive += Time.deltaTime;

        switch (_state)
        {
            case GemState.Popping: UpdatePopping(); break;
            case GemState.Idle: UpdateIdle(); break;
            case GemState.Attracted: UpdateAttracted(); break;
        }
    }

    private void UpdatePopping()
    {
        _popTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_popTimer / PopDuration);

        Vector3 flat = Vector3.Lerp(_popOrigin, _popTarget, t);
        float arc = Mathf.Sin(t * Mathf.PI) * PopArcHeight;
        transform.position = flat + Vector3.up * arc;

        float scaleT = FXSprites.EaseOutBack(t, 1.5f);
        transform.localScale = Vector3.one * _gemScale * Mathf.Max(0f, scaleT);
        // Flourish d'arrivée : rotation calculée directement depuis la progression `t` (absolue, jamais
        // accumulée via transform.Rotate) - un tour complet autour de l'axe face-caméra (recalculé chaque frame,
        // voir GetBillboardRotation), retombant pile sur l'orientation face-caméra au dixième de degré près, quel
        // que soit le nombre de frames écoulées (un calcul accumulé par frame dérive selon le framerate réel -
        // bug trouvé et corrigé en suite 11).
        transform.rotation = GetBillboardRotation() * Quaternion.Euler(0f, 0f, PopSpinDegrees * t);

        ApplyGlow(Mathf.Lerp(MinGlow, MaxGlow, 0.6f)); // leger eclat pendant le vol, stabilise des l'atterrissage

        if (t >= 1f)
        {
            transform.position = _popTarget;
            transform.localScale = Vector3.one * _gemScale;
            transform.rotation = GetBillboardRotation(); // figé sur l'angle face-caméra du moment
            _state = GemState.Idle;
        }
    }

    private void UpdateIdle()
    {
        float bob = Mathf.Sin(_timeAlive * IdleBobSpeed + _idlePhase) * IdleBobAmplitude;
        transform.position = _popTarget + Vector3.up * Mathf.Max(0f, bob);
        transform.rotation = GetBillboardRotation(); // toujours exactement face caméra (voir note suite 14)

        float pulse = 0.5f + 0.5f * Mathf.Sin(_timeAlive * BasePulseSpeed * _pulseSpeedMul + _idlePhase);
        ApplyGlow(Mathf.Lerp(MinGlow, MaxGlow, pulse));

        float currentRadius = XPGemSpawner.Instance != null ? XPGemSpawner.Instance.AttractionRadius : 0f;
        if (currentRadius > 0f)
        {
            float sqrDist = (_playerTransform.position - transform.position).sqrMagnitude;
            if (sqrDist <= currentRadius * currentRadius)
                BeginAttraction(0f, 1f);
        }
    }

    private void UpdateAttracted()
    {
        _attractRampTimer += Time.deltaTime;
        float rampT = Mathf.Clamp01(_attractRampTimer / AttractRampDuration);
        float eased = rampT * (2f - rampT); // EaseOutQuad maison
        float speed = Mathf.Lerp(_baseMoveSpeed, _maxMoveSpeed, eased) * _speedBoostMultiplier;

        Vector3 offset = _playerTransform.position - transform.position;
        float sqrDist = offset.sqrMagnitude;
        Vector3 dir = offset.normalized;
        transform.position += dir * speed * Time.deltaTime;
        transform.rotation = GetBillboardRotation(); // toujours exactement face caméra (voir note suite 14)

        float pulse = 0.5f + 0.5f * Mathf.Sin(_timeAlive * BasePulseSpeed * _pulseSpeedMul * 1.6f);
        ApplyGlow(Mathf.Lerp(MinGlow, MaxGlow, pulse));

        if (sqrDist <= CollectDistSqr)
            Collect();
    }

    // MODIFIE - garde-fou ajoute : ce chemin (contact physique direct) ne
    // verifiait auparavant AUCUN etat de pause, contrairement a Update(). Empeche
    // une collecte (et donc potentiellement un nouveau LevelUp en cascade) de se
    // declencher si le jeu est deja en pause au moment du contact. Ne resout pas
    // le tout premier pas de physique qui declenche le TOUT PREMIER level-up
    // (limite Unity : un changement de pause en plein milieu d'un callback
    // physique ne peut pas annuler retroactivement ce meme pas de physique), mais
    // protege contre tout ramassage en cascade APRES coup, pendant que le panel
    // est deja ouvert.
    private void OnTriggerEnter(Collider other)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (other.CompareTag("Player"))
            Collect();
    }

    private void Collect()
    {
        if (!gameObject.activeSelf) return;

        // AJOUTÉ - retour visuel au ramassage (avant : disparition silencieuse, "pas satisfaisant" comme demandé).
        XPGemCollectFX.Play(transform.position, _gemColor, _gemScale);

        if (XPSystem.Instance != null)
        {
            XPSystem.Instance.AddXP(_xpValue);
        }

        if (_trail != null) { _trail.emitting = false; _trail.Clear(); }

        gameObject.SetActive(false);
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool(_poolKey, gameObject);
        }
    }
}
