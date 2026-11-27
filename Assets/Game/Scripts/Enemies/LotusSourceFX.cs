using UnityEngine;

// AJOUTÉ (2026-11-26, Lotus de la Source - passe « qualité ») - mise en scène du boss 3, purement visuelle, posée sur la
// racine du boss :
//  - APPARITION : le bassin de la Source s'étend depuis le centre (temps réel : l'entrée du boss fige le jeu) ;
//  - CHARGE (paramètre IsCoiling de l'Animator) : le cœur de cristal s'illumine, les ondes du bassin s'accélèrent et se
//    renforcent, la lumière qui monte du cœur devient un jet ;
//  - IMPLOSION (état Dance) : les ondes s'INVERSENT et convergent vers la fleur qui se referme ;
//  - les feuilles flottantes dérivent et ondulent doucement sur l'eau ;
//  - CORRIGÉ (2026-11-27, retour utilisateur : « la mare est impactée par la rotation du lotus ») : le bassin garde une
//    orientation FIXE dans le monde (seule la fleur se tourne vers le joueur) ; l'encre de corruption suit le côté corrompu
//    de la fleur (direction recalculée à chaque image depuis _corruptAnchor).
// Aucune règle de jeu ne dépend de ce composant.
public class LotusSourceFX : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private SkinnedMeshRenderer _body;
    [SerializeField] private string _crystalMaterialName = "M_ArchonV2Crystal";
    [SerializeField] private Transform _pond;
    [SerializeField] private Renderer _water;
    [SerializeField] private Transform[] _pads;
    [SerializeField] private ParticleSystem _heartMotes;
    [SerializeField] private float _pondGrowTime = 1.4f;
    [Tooltip("Os du pétale corrompu : l'encre de corruption du bassin s'oriente vers lui")]
    [SerializeField] private Transform _corruptAnchor;

    private static readonly int GlowId = Shader.PropertyToID("_Glow");
    private static readonly int PhaseId = Shader.PropertyToID("_RipplePhase");
    private static readonly int RippleStrengthId = Shader.PropertyToID("_RippleStrength");
    private static readonly int CorruptDirId = Shader.PropertyToID("_CorruptDir");
    private static readonly int ZoneMinId = Shader.PropertyToID("_ZoneMin");
    private static readonly int ZoneMaxId = Shader.PropertyToID("_ZoneMax");
    private Quaternion _pondWorldRotation = Quaternion.identity;
    private static readonly int CoilingId = Animator.StringToHash("IsCoiling");
    private static readonly int DanceStateId = Animator.StringToHash("Dance");

    private MaterialPropertyBlock _mpb;
    private int _crystalIndex = -1;
    private float _charge, _implode, _phase, _grow;
    private Vector3 _pondScale;
    private Vector3[] _padBase;
    private float _motesBaseRate = 9f;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (_body != null)
        {
            var mats = _body.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name == _crystalMaterialName) _crystalIndex = i;
        }
        if (_pond != null) _pondScale = _pond.localScale;
        if (_pads != null) { _padBase = new Vector3[_pads.Length]; for (int i = 0; i < _pads.Length; i++) if (_pads[i]) _padBase[i] = _pads[i].localPosition; }
        if (_heartMotes != null) _motesBaseRate = _heartMotes.emission.rateOverTime.constant;
    }

    private void OnEnable() { _grow = 0f; if (_pond != null) _pond.localScale = Vector3.zero; }

    private void LateUpdate()
    {
        // après la rotation du boss vers le joueur : le bassin reste immobile
        if (_pond != null) _pond.rotation = _pondWorldRotation;
    }

    private void Update()
    {
        float udt = Time.unscaledDeltaTime, dt = Time.deltaTime;
        // apparition du bassin (léger dépassement puis retour, comme une onde qui s'étale)
        if (_pond != null && _grow < 1f)
        {
            _grow = Mathf.Min(1f, _grow + udt / Mathf.Max(0.01f, _pondGrowTime));
            float g = 1f - Mathf.Pow(1f - _grow, 3f); float over = 1f + 0.06f * Mathf.Sin(_grow * Mathf.PI);
            _pond.localScale = _pondScale * g * over;
        }
        bool coiling = _animator != null && _animator.GetBool(CoilingId);
        bool dance = _animator != null && _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == DanceStateId;
        _charge = Mathf.MoveTowards(_charge, coiling ? 1f : 0f, dt * 2.5f);
        _implode = Mathf.MoveTowards(_implode, dance ? 1f : 0f, dt * 2f);

        // cœur de cristal qui s'illumine
        if (_body != null && _crystalIndex >= 0)
        {
            _body.GetPropertyBlock(_mpb, _crystalIndex);
            _mpb.SetFloat(GlowId, 1.0f + 1.3f * Mathf.Max(_charge, _implode * 0.7f) + 0.08f * Mathf.Sin(Time.time * 2f));
            _body.SetPropertyBlock(_mpb, _crystalIndex);
        }
        // ondes : plus rapides à la charge, inversées pendant l'implosion
        _phase += dt * (0.6f * _charge - 0.9f * _implode);
        if (_water != null)
        {
            _water.GetPropertyBlock(_mpb);
            _mpb.SetFloat(PhaseId, _phase);
            _mpb.SetFloat(RippleStrengthId, 0.35f + 0.4f * Mathf.Max(_charge, _implode));
            // AJOUTÉ (2026-11-27) - l'eau s'arrête aux murs de l'arène (le décor d'arbres est au-delà des murs)
            _mpb.SetVector(ZoneMinId, new Vector4(MapBoundaryUtils.HasBoundX ? MapBoundaryUtils.MinX : -1e5f, MapBoundaryUtils.HasBoundZ ? MapBoundaryUtils.MinZ : -1e5f, 0f, 0f));
            _mpb.SetVector(ZoneMaxId, new Vector4(MapBoundaryUtils.HasBoundX ? MapBoundaryUtils.MaxX : 1e5f, MapBoundaryUtils.HasBoundZ ? MapBoundaryUtils.MaxZ : 1e5f, 0f, 0f));
            if (_corruptAnchor != null && _pond != null)
            {
                // uv du bassin : x = x local, y = z local du bassin (le quad est couché de 90° autour de X)
                Vector3 d = _pond.InverseTransformDirection(_corruptAnchor.position - _pond.position);
                if (d.x * d.x + d.z * d.z > 1e-4f) _mpb.SetVector(CorruptDirId, new Vector4(d.x, d.z, 0f, 0f));
            }
            _water.SetPropertyBlock(_mpb);
        }
        if (_heartMotes != null) { var em = _heartMotes.emission; em.rateOverTime = _motesBaseRate * (1f + 3.5f * _charge); }
        // feuilles flottantes : petite dérive circulaire et ondulation
        if (_pads != null)
            for (int i = 0; i < _pads.Length; i++)
            {
                if (_pads[i] == null) continue;
                Vector3 pw = _pads[i].position;   // feuille hors de l'arène (au-delà des murs) : masquée
                bool inZone = pw.x > MapBoundaryUtils.MinX + 0.6f && pw.x < MapBoundaryUtils.MaxX - 0.6f && pw.z > MapBoundaryUtils.MinZ + 0.6f && pw.z < MapBoundaryUtils.MaxZ - 0.6f;
                if (_pads[i].gameObject.activeSelf != inZone) _pads[i].gameObject.SetActive(inZone);
                float ph = i * 1.7f + Time.time * 0.6f;
                _pads[i].localPosition = _padBase[i] + new Vector3(Mathf.Sin(ph) * 0.03f, Mathf.Sin(ph * 1.3f) * 0.004f, Mathf.Cos(ph * 0.8f) * 0.03f);
                _pads[i].Rotate(0f, 4f * dt * (i % 2 == 0 ? 1f : -1f), 0f, Space.Self);
            }
    }
}
