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
    [Tooltip("Délai après un coup reçu avant que la montée reprenne (évite qu'un double-tap dans la même fraction de seconde ne soit ressenti comme une double punition).")]
    [SerializeField] private float _concentrationGraceAfterHit = 0.5f;

    // MODIFIE (2026-09-16) - retour utilisateur : le taux plat de 8%/s (ex-champ
    // serialisé ici) rendait le plafond de 50% trop facile a atteindre. Lu
    // depuis le palier via GetConcentrationRampPerSecond() (2/3/5 %/s aux
    // paliers 1/2/3) au lieu d'une valeur fixe.
    private float _concentrationRampPerSecond;
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
        _concentrationRampPerSecond = MetaProgressionManager.Instance != null
            ? MetaProgressionManager.Instance.GetConcentrationRampPerSecond()
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

        // MODIFIE (2026-09-16) - retour utilisateur : le HUD affichait "Concentration"
        // (trop long pour l'espace prévu) dès que le bonus retombait à 0 après un coup.
        // La valeur de JEU (dégâts) chute toujours instantanément - un coup reçu doit
        // rester une vraie punition - mais l'AFFICHAGE joue une descente animée rapide
        // jusqu'à 0% au lieu de sauter directement, pour que la perte se ressente
        // clairement au lieu d'un simple changement de texte.
        float previous = _concentrationCurrent;
        _concentrationCurrent = 0f;
        _cleanTimer = 0f;
        _graceTimer = _concentrationGraceAfterHit;
        Recompute();
        _lastHudPercent = 0;

        if (GameUI.Instance != null)
            GameUI.Instance.PlayConcentrationHitDrop(previous);
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
