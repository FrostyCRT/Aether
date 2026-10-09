using UnityEngine;

// AJOUTÉ (2026-10-05) - pilote d'exécution de "LA solution" perf (texture d'animation GPU), voir NOTES.md.
// Remplace Animator + SkinnedMeshRenderer : ce composant tourne sur un MeshRenderer/MeshFilter classiques (mesh
// statique cuit par VATBaker.cs), et se contente d'avancer un numéro de frame par instance (_VatFrame, lu par
// VATLit.shader) - aucun calcul de squelette, aucune évaluation d'Animator, à aucun moment.
//
// Déphasage aléatoire par instance (_instanceTimeOffset) : sans ça, 100 ennemis nés au même tic de spawn
// animeraient en parfaite synchronisation - visuellement une "armée de robots", pas une horde vivante.
[RequireComponent(typeof(MeshRenderer))]
public class VATAnimatedRenderer : MonoBehaviour
{
    [System.Serializable]
    public struct ClipRange
    {
        public string clipName;
        public int startFrame;
        public int frameCount;
        public float length;
    }

    [SerializeField] private VATClipSet _clipSet;
    // AJOUTÉ (2026-10-05, intégration jeu réel) - pont avec EnemyAnimatorController.SetAttacking(bool), qui
    // jusqu'ici ne parlait qu'à un Animator classique ("IsAttacking" bool). Noms configurables par ennemi car
    // les clips n'ont pas le même nom partout (Bite pour le Loup, Shot pour le Goblin, Tail Attack pour le
    // Kaiju...). Idle vide = retombe sur le 1er clip du jeu de clips (comportement déjà existant d'OnEnable).
    [SerializeField] private string _idleClipName = "";
    [SerializeField] private string _attackClipName = "Attack";
    // AJOUTÉ (2026-10-06) - retour utilisateur (Goblin uniquement) : "si le goblin est en position de tir, il est
    // orienté à Y=-30 pour son VAT_Mesh" - un choix de pose VISUELLE en position de tir (le corps se tourne un peu
    // plus, indépendamment de l'orientation réelle de visée déjà gérée par la racine/EnemyShooter). Valeur par
    // défaut 0 = AUCUN effet pour les 5 autres ennemis (champ générique, mais seul le prefab Goblin a cette valeur
    // à -30 dans son Inspector - rien d'autre n'y touche).
    [SerializeField] private float _attackLocalYRotation = 0f;

    private MeshRenderer _renderer;
    private MaterialPropertyBlock _props;
    private static readonly int VatFrameID = Shader.PropertyToID("_VatFrame");

    private ClipRange _currentClip;
    private bool _hasClip;
    private float _time;
    private float _instanceTimeOffset;
    private bool _paused;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _props = new MaterialPropertyBlock();
        _instanceTimeOffset = Random.Range(0f, 1f);
    }

    private void OnEnable()
    {
        _paused = false;
        _hasClip = false; // un ennemi qui revient du pool ne doit pas garder la frame de sa vie precedente
        if (_clipSet != null && _clipSet.clips != null && _clipSet.clips.Length > 0)
        {
            string idle = string.IsNullOrEmpty(_idleClipName) ? _clipSet.clips[0].clipName : _idleClipName;
            SetClip(idle);
            _time = _instanceTimeOffset * Mathf.Max(0.01f, _currentClip.length);
        }
    }

    // AJOUTÉ - equivalent VAT de EnemyAnimatorController.SetAttacking(bool) (qui appelait auparavant
    // Animator.SetBool("IsAttacking", ...)). Pont transparent : EnemyBase/EnemyShooter/etc. n'ont pas changé une
    // ligne, EnemyAnimatorController route juste vers l'un ou l'autre selon ce qui est present sur le prefab.
    public void SetAttacking(bool isAttacking)
    {
        string idle = string.IsNullOrEmpty(_idleClipName) && _clipSet != null && _clipSet.clips.Length > 0
            ? _clipSet.clips[0].clipName
            : _idleClipName;
        SetClip(isAttacking ? _attackClipName : idle);

        transform.localRotation = Quaternion.Euler(0f, isAttacking ? _attackLocalYRotation : 0f, 0f);
    }

    public void SetClip(string clipName)
    {
        if (_clipSet == null || _clipSet.clips == null) return;

        for (int i = 0; i < _clipSet.clips.Length; i++)
        {
            if (_clipSet.clips[i].clipName == clipName)
            {
                bool isNewClip = !_hasClip || _currentClip.clipName != clipName;
                _currentClip = _clipSet.clips[i];
                _hasClip = true;
                if (isNewClip) _time = 0f;
                return;
            }
        }
    }

    public void SetPaused(bool paused) => _paused = paused;

    // AJOUTÉ - expose la frame courante pour VATBoneAttachment (objets tenus en main - sarbacane du Goblin etc.)
    // qui doivent suivre la MÊME frame que le corps, sans dupliquer le calcul de temps/bouclage ci-dessus.
    public int CurrentFrame { get; private set; } = -1;

    private void Update()
    {
        if (!_hasClip || _currentClip.frameCount <= 0) return;

        if (!_paused)
            _time += Time.deltaTime;

        float length = Mathf.Max(0.0001f, _currentClip.length);
        float loopedTime = _time % length;
        float normalized = loopedTime / length;
        int frameInClip = Mathf.Clamp(Mathf.FloorToInt(normalized * _currentClip.frameCount), 0, _currentClip.frameCount - 1);
        int frame = _currentClip.startFrame + frameInClip;
        CurrentFrame = frame;

        _renderer.GetPropertyBlock(_props);
        _props.SetFloat(VatFrameID, frame);
        _renderer.SetPropertyBlock(_props);
    }
}
