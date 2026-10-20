using System.Collections;
using UnityEngine;

// AJOUTÉ (2026-10-18, diagnostic complet - décision "mort de boss") : remplace le burst de gemmes d'XP à la mort
// d'un vrai boss (voir BossBase.Die, branche !IsSummoned) par ce coffre à ramasser. L'ancien système donnait de
// l'XP qui pouvait être "gâchée" (XP partielle sans level-up visible) ou au contraire déclencher une cascade de
// plusieurs cartes à la fois, peu lisible. Ce coffre accorde TOUJOURS exactement _freePicks tirages de carte
// complets (3 choix à chaque fois, via LevelUpManager.GrantFreePicks) sans jamais donner d'XP - une récompense
// fixe et cérémoniale plutôt qu'un gain variable. Instance poolée (ObjectPool, tag "BossChest"), suit le même
// patron que MudPuddleZone/BouncingOrbProjectile (Get/Init/retour au pool par l'objet lui-même).
public class BossChest : MonoBehaviour
{
    [Header("Récompense")]
    [SerializeField] private int _freePicks = 2;

    [Header("Apparition")]
    [SerializeField] private float _popDuration = 0.35f;

    [Header("Idle (avant ramassage)")]
    [SerializeField] private float _bobHeight = 0.18f;
    [SerializeField] private float _bobSpeed = 1.6f;
    [SerializeField] private float _spinSpeed = 35f; // degrés/s - tourne lentement sur lui-même pour bien se voir de dessus

    [Header("Ouverture")]
    [SerializeField] private float _hitstopDuration = 0.1f;
    [SerializeField] private float _hitstopScale = 0.3f;
    [SerializeField] private Color _openRingColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField] private float _openRingRadius = 3f;
    [SerializeField] private float _openRingDuration = 0.45f;
    [SerializeField] private float _cameraShakeAmount = 0.4f;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    // Teinte or/ambre distincte des gemmes d'XP normales (MAT_Gem partagé, blanc par défaut - voir
    // MudPuddleZone.SetColorTint pour le même principe de teinte via MaterialPropertyBlock sur un matériau commun).
    private static readonly Color ChestTint = new Color(1f, 0.78f, 0.25f);

    private MeshRenderer _meshRenderer;
    private MaterialPropertyBlock _propBlock;
    private Vector3 _baseScale;
    private float _bobPhase;
    private float _groundY;
    private bool _collected;
    private float _spawnTime;
    private string _poolKey;

    private void Awake()
    {
        _poolKey = name.Replace("(Clone)", "").Trim();
        _meshRenderer = GetComponentInChildren<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();
        if (_meshRenderer != null)
        {
            _meshRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorID, ChestTint);
            _meshRenderer.SetPropertyBlock(_propBlock);
        }
        _baseScale = transform.localScale;
        _bobPhase = Random.Range(0f, 6.28f);
    }

    // Appelé par BossBase.Die() juste après ObjectPool.Get(). groundY = même hauteur que celle déjà calculée pour
    // les gemmes de boss (BossBase.Die, gemSpawnPos.y), pour reposer visuellement au même niveau que le reste du loot.
    public void Init(float groundY)
    {
        _groundY = groundY;
    }

    private void OnEnable()
    {
        _collected = false;
        _spawnTime = Time.time;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        if (_collected) return;
        if (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused)) return;

        float t = Time.time - _spawnTime;

        if (t < _popDuration)
        {
            float p = Mathf.Clamp01(t / _popDuration);
            transform.localScale = _baseScale * EaseOutBack(p);
            return;
        }

        transform.localScale = _baseScale;
        float bob = Mathf.Sin((t - _popDuration) * _bobSpeed + _bobPhase) * _bobHeight;
        Vector3 pos = transform.position;
        pos.y = _groundY + 0.5f + bob;
        transform.position = pos;
        transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x1 = x - 1f;
        return 1f + c3 * x1 * x1 * x1 + c1 * x1 * x1;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player")) return;
        _collected = true;

        ExpandingRingVFX.Spawn(transform.position, _openRingRadius, _openRingColor, _openRingDuration);
        CameraShakeFX.Shake(_cameraShakeAmount);
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        // Même garde-fou que CrystalSystem.HitstopRoutine : ne remet timeScale à 1 que si rien d'autre (pause,
        // level-up) ne l'a entre-temps repris à 0 - sinon le hitstop pourrait "rendre" le jeu derrière un menu.
        Time.timeScale = _hitstopScale;
        yield return new WaitForSecondsRealtime(_hitstopDuration);
        if (Mathf.Approximately(Time.timeScale, _hitstopScale))
            Time.timeScale = 1f;

        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.GrantFreePicks(_freePicks);

        if (ObjectPool.Instance != null)
            ObjectPool.Instance.ReturnToPool(_poolKey, gameObject);
        else
            gameObject.SetActive(false);
    }
}

