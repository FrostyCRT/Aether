using UnityEngine;

// Buffs offensifs DYNAMIQUES du joueur, centralisés en un seul multiplicateur de
// dégâts sortants lu par toutes les sources de dégâts du joueur (armes, projectiles
// persistants, zones). Un buff dynamique ne peut pas être « cuit » une fois au
// spawn comme les bonus de Réputation : sa valeur change en cours de partie et doit
// être relue au moment de chaque frappe.
//
// Aujourd'hui seul le nœud "Concentration" (branche Guerrier / Aether) l'alimente.
// Conçu pour accueillir d'autres buffs plus tard — notamment migrer "Surpuissance"
// ici, pour qu'il double TOUTES les armes et pas seulement le tir de base
// (limitation actuelle : CrystalSystem.OverpowerBuff n'appelle que WeaponBase).
//
// OutgoingDamageMultiplier est STATIQUE : il n'y a qu'un seul joueur, et c'est la
// seule façon simple pour les projectiles/zones détachés (OrbitalProjectile,
// MudPuddleZone, BouncingOrbProjectile...) de le lire à l'impact sans traîner une
// référence. Remis à 1 dans Awake() et OnDestroy().
[RequireComponent(typeof(HealthSystem))]
public class PlayerBuffs : MonoBehaviour
{
    [Header("Concentration (nœud Guerrier / Aether)")]
    [Tooltip("Gain de dégâts par seconde passée sans être touché.")]
    [SerializeField] private float _concentrationRampPerSecond = 0.08f;
    [Tooltip("Délai après un coup reçu avant que la montée reprenne (évite qu'un double-tap dans la même fraction de seconde ne soit ressenti comme une double punition).")]
    [SerializeField] private float _concentrationGraceAfterHit = 0.5f;

    private float _concentrationCap;      // 0 si hors branche Guerrier ou nœud non pris
    private float _concentrationCurrent;  // bonus actuel, 0..cap
    private float _cleanTimer;            // temps écoulé depuis le dernier coup
    private float _graceTimer;
    private int _lastHudPercent = -1;

    // Multiplicateur de dégâts sortants total (Concentration × futurs buffs).
    // 1f par défaut → aucun effet pour les personnages sans buff dynamique actif.
    public static float OutgoingDamageMultiplier { get; private set; } = 1f;

    // Exposé pour le HUD / debug.
    public float ConcentrationBonus => _concentrationCurrent;

    private void Awake()
    {
        OutgoingDamageMultiplier = 1f;
        _concentrationCap = MetaProgressionManager.Instance != null
            ? MetaProgressionManager.Instance.GetBonusConcentrationCap()
            : 0f;
    }

    private void Start()
    {
        if (GameUI.Instance != null)
            GameUI.Instance.SetConcentrationAvailable(_concentrationCap > 0f);
        PushHUD();
    }

    // Appelé par HealthSystem.TakeDamage() à chaque coup RÉELLEMENT encaissé
    // (y compris un coup atténué par Second Souffle) — même point que la
    // notification du défi "Sans-Faute".
    public void NotifyDamaged()
    {
        if (_concentrationCap <= 0f) return;

        _concentrationCurrent = 0f;
        _cleanTimer = 0f;
        _graceTimer = _concentrationGraceAfterHit;
        Recompute();
        PushHUD();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;
        if (_concentrationCap <= 0f) return;

        float dt = Time.deltaTime;

        if (_graceTimer > 0f)
        {
            _graceTimer -= dt;
            return;
        }

        if (_concentrationCurrent < _concentrationCap)
        {
            _cleanTimer += dt;
            float target = Mathf.Min(_cleanTimer * _concentrationRampPerSecond, _concentrationCap);
            if (target > _concentrationCurrent)
            {
                _concentrationCurrent = target;
                Recompute();
                PushHUD();
            }
        }
    }

    private void Recompute()
    {
        OutgoingDamageMultiplier = 1f + _concentrationCurrent;
    }

    private void PushHUD()
    {
        int pct = Mathf.RoundToInt(_concentrationCurrent * 100f);
        if (pct == _lastHudPercent) return;
        _lastHudPercent = pct;

        if (GameUI.Instance != null)
            GameUI.Instance.UpdateConcentration(_concentrationCurrent);
    }

    private void OnDestroy()
    {
        OutgoingDamageMultiplier = 1f;
    }
}
