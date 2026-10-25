using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CrystalSystem : MonoBehaviour
{
    [Header("Jauge")]
    [SerializeField] private int _maxCharges = 6;

    [Header("Ulti")]
    [SerializeField] private float _ultDamage = 500f;
    [SerializeField] private float _ultRange = 10f;
    [SerializeField] private float _slowFactor = 0.3f;
    [SerializeField] private float _slowDuration = 3f;

    [Header("Scaling de l'ultime dans le temps")]
    [SerializeField] private float _ultScaleRampDuration = 900f;
    [SerializeField] private float _ultScaleMaxMultiplier = 4f;

    [Header("Nova")]
    [SerializeField] private float _novaDamage = 100f;
    [SerializeField] private float _novaRadius = 3f;
    [SerializeField] private GameObject _novaVFXPrefab;
    [Tooltip("Durée de l'expansion visuelle de la Nova (0 -> rayon max). Historique : 0,3s d'origine jugé trop rapide, 0,7s presque parfait mais un chouïa trop lent, stabilisé à 0,5s. Redescendu à 0,35s (2026-10-21) sur retour utilisateur : \"rend la nova plus rapide au déclenchement, genre elle arrive à sa taille max en moins longtemps.\"")]
    [SerializeField] private float _novaVFXDuration = 0.35f;
    [Tooltip("Hauteur Y à laquelle le VFX de la Nova est instancié - même logique que WeaponMudPuddle._groundY : le pivot du joueur (transform.position) est à hauteur de torse (~1,5), pas au sol, donc un VFX plaqué au sol comme une onde de choc doit utiliser sa propre hauteur au lieu de suivre le pivot.")]
    [SerializeField] private float _novaGroundY = 0.2f;

    [Header("Ulti — VFX")]
    [SerializeField] private GameObject _ultVFXPrefab;
    [SerializeField] private float _ultVFXDuration = 0.55f;
    [SerializeField] private float _hitstopDuration = 0.08f;
    // AJOUTÉ (2026-10-06, "hiérarchie Y des effets au sol") - même bug que la Nova juste au-dessus (voir son
    // commentaire dans NovaRoutine, corrigé le 2026-09-17) : jamais appliqué ici. Le VFX de l'Ultime flottait
    // réellement à hauteur de torse (~1,5, mesuré en jeu) au lieu d'être une onde de choc au sol comme la Nova.
    [SerializeField] private float _ultGroundY = 0.2f;

    [Header("Ulti — Absorption XP")]
    [SerializeField] private float _gemAttractRange = 14f;

    private int _currentCharges = 0;
    private int _storedUlts = 0;
    private bool _overpowerActive = false;

    // AJOUTE - cache pour le soin du nœud "Récupération" (Gardien) sur absorption.
    private HealthSystem _healthSystem;

    public int CurrentCharges => _currentCharges;
    public int MaxCharges => _maxCharges;

    private void Start()
    {
        _healthSystem = GetComponent<HealthSystem>();

        float crystalBonus = MetaProgressionManager.Instance.GetBonusCrystalDamage();
        _ultDamage += _ultDamage * crystalBonus;
        _novaDamage += _novaDamage * crystalBonus;

        float novaBonus = MetaProgressionManager.Instance.GetBonusNovaRadius();
        _novaRadius += _novaRadius * novaBonus;

        if (MetaProgressionManager.Instance.HasCrystalMastery())
            _maxCharges = Mathf.Max(_maxCharges - 1, 2);

        GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);
        GameUI.Instance.UpdateUltStack(0);
    }

    private float GetUltDamageScale()
    {
        if (GameManager.Instance == null || _ultScaleRampDuration <= 0f) return 1f;

        float t = Mathf.Clamp01(GameManager.Instance.RunTimer / _ultScaleRampDuration);
        return Mathf.Lerp(1f, _ultScaleMaxMultiplier, t);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        // CORRIGE (2026-10-16, audit) - la touche d'Ultime était lue même en pause / pendant le choix de cartes /
        // l'annonce du défi : l'Ultime partait derrière le menu et son hitstop relançait le temps (voir HitstopRoutine).
        if (GameManager.Instance.IsPaused) return;

        if (_storedUlts >= 1 && GameInput.Down(GameAction.Ultimate))
            TriggerUlt();
    }

    public void AbsorbProjectile()
    {
        TriggerNova();

        // AJOUTE - nœud "Récupération" (Gardien) : chaque projectile absorbé au dash
        // rend des PV plats (20/50/80 selon le palier). GetBonusRecuperation() renvoie
        // 0 hors branche Gardien, donc l'appel est sûr pour tous les personnages.
        // Placé avant l'early-return ci-dessous : le soin s'applique même si la jauge
        // de Cristal est déjà pleine (le projectile est absorbé dans tous les cas).
        if (_healthSystem != null && MetaProgressionManager.Instance != null)
            _healthSystem.HealFlat(MetaProgressionManager.Instance.GetBonusRecuperation());

        if (_storedUlts >= 2) return;

        _currentCharges++;

        if (_currentCharges >= _maxCharges)
        {
            _storedUlts++;
            _currentCharges = 0;

            GameUI.Instance.UpdateCrystalCharge(_maxCharges, _maxCharges);
            GameUI.Instance.SetCrystalReady(_storedUlts);
            GameUI.Instance.UpdateUltStack(_storedUlts);
        }
        else
        {
            if (_storedUlts == 1)
                GameUI.Instance.SetCrystalReady(0);

            GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);
        }
    }

    private void TriggerUlt()
    {
        // AJOUTE - notifie le systeme de defis a chaque VRAI declenchement de
        // l'Ultime (defi "Sans Cristal" = ne jamais l'utiliser).
        if (ChallengeManager.Instance != null)
            ChallengeManager.Instance.NotifyUltimateUsed();

        bool isEmpowered = _storedUlts >= 2;

        if (isEmpowered)
        {
            _storedUlts = 0;
            _currentCharges = 0;
            GameUI.Instance.SetCrystalReady(0);
            GameUI.Instance.UpdateCrystalCharge(0, _maxCharges);
            GameUI.Instance.UpdateUltStack(0);
            TriggerEmpoweredUlt();
        }
        else
        {
            int savedCharges = _currentCharges;
            _storedUlts = 0;
            GameUI.Instance.SetCrystalReady(0);
            GameUI.Instance.UpdateUltStack(0);
            _currentCharges = savedCharges;
            GameUI.Instance.UpdateCrystalCharge(_currentCharges, _maxCharges);
            TriggerNormalUlt();
        }

        if (MetaProgressionManager.Instance.HasOverpower() && !_overpowerActive)
            StartCoroutine(OverpowerBuff());
    }

    private void TriggerNormalUlt()
    {
        DamageAllEnemies(GetUltDamageScale());
        StartCoroutine(SlowAllEnemies());
        AttractGems(_gemAttractRange, fast: false);
        StartCoroutine(ShowUltVFX());
        StartCoroutine(HitstopRoutine());
    }

    private void TriggerEmpoweredUlt()
    {
        float scale = GetUltDamageScale();
        // CORRIGÉ (2026-10-21) - même oubli que DamageAllEnemies ci-dessus (voir son commentaire) : l'Ultime
        // empouvoiré (2 charges) ne lisait pas non plus PlayerBuffs.OutgoingDamageMultiplier.
        float empoweredDamage = _ultDamage * 5f * scale * PlayerBuffs.OutgoingDamageMultiplier;

        EnemyBase[] allEnemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (EnemyBase enemy in allEnemies)
        {
            if (enemy != null)
                enemy.TakeDamage(empoweredDamage, DamageNumberSpawner.ColorUltimate);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange, EnemyLayers.Mask);
        foreach (Collider hit in hits)
        {
            hit.TryGetComponent(out BossBase boss);
            if (boss != null) boss.TakeDamage(_ultDamage * 2f * scale * PlayerBuffs.OutgoingDamageMultiplier, DamageNumberSpawner.ColorUltimate);
        }

        StartCoroutine(SlowAllEnemies());
        AttractGems(float.MaxValue, fast: true);
        StartCoroutine(ShowUltVFX());
        StartCoroutine(HitstopRoutine());
    }

    private void DamageAllEnemies(float multiplier = 1f)
    {
        // CORRIGÉ (2026-10-21, retour utilisateur : "la nova ne prend pas les bonus de dégâts, c'est pas normal")
        // - en auditant toutes les sources de dégâts (2026-10-16), CHAQUE arme (WeaponBase, WeaponAura,
        // WeaponFireball, les Orbitaux, la Boue, la Foudre, toutes les fusions...) avait été corrigée pour lire
        // PlayerBuffs.OutgoingDamageMultiplier (Concentration + Surpuissance) à l'impact - sauf l'Ultime et la Nova
        // (CrystalSystem), oubliés de cet audit car ils ne passent pas par WeaponBase. Concrètement : le nœud
        // "Surpuissance" ("après l'ultime, tes dégâts sont doublés pendant 5 secondes") ne doublait donc JAMAIS les
        // dégâts de l'Ultime lui-même ni de la Nova, seulement ceux des autres armes.
        float buffMultiplier = multiplier * PlayerBuffs.OutgoingDamageMultiplier;
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange, EnemyLayers.Mask);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                hit.TryGetComponent(out EnemyBase eb);
                if (eb != null) eb.TakeDamage(_ultDamage * buffMultiplier, DamageNumberSpawner.ColorUltimate);

                hit.TryGetComponent(out BossBase boss);
                if (boss != null) boss.TakeDamage(_ultDamage * buffMultiplier, DamageNumberSpawner.ColorUltimate);
            }
        }
    }

    private void AttractGems(float range, bool fast = false)
    {
        XPGem[] allGems = FindObjectsByType<XPGem>(FindObjectsSortMode.None);
        foreach (XPGem gem in allGems)
        {
            if (gem == null) continue;
            if (range >= float.MaxValue ||
                Vector3.Distance(transform.position, gem.transform.position) <= range)
            {
                if (fast) gem.ForceAttractFast();
                else gem.ForceAttract();
            }
        }
    }

    private void TriggerNova()
    {
        // MODIFIE (2026-09-16) - retour utilisateur : "les dégâts s'infligent
        // instant, alors que la zone n'a pas forcément atteint les ennemis".
        // Les dégâts étaient appliqués sur tout _novaRadius d'un coup, pendant
        // que le VFX grandissait séparément sur _novaVFXDuration - désynchro
        // visible. Les dégâts suivent maintenant le rayon réel du VFX, frame
        // par frame (voir NovaRoutine), un ennemi n'est touché qu'au moment où
        // l'anneau l'atteint visuellement.
        if (_novaVFXPrefab != null)
            StartCoroutine(NovaRoutine());
        else
            ApplyNovaDamage(_novaRadius, null); // pas de VFX à synchroniser : dégâts instantanés comme avant
    }

    private void ApplyNovaDamage(float radius, HashSet<Collider> alreadyHit)
    {
        // CORRIGÉ (2026-10-21, retour utilisateur : "la nova ne prend pas les bonus de dégâts, c'est pas normal") -
        // voir le commentaire détaillé de DamageAllEnemies plus haut : même oubli, la Nova ne lisait jamais
        // PlayerBuffs.OutgoingDamageMultiplier (Concentration + Surpuissance).
        float novaDamage = _novaDamage * PlayerBuffs.OutgoingDamageMultiplier;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, EnemyLayers.Mask);
        foreach (Collider hit in hits)
        {
            if (alreadyHit != null && !alreadyHit.Add(hit)) continue; // déjà touché par cette vague

            if (hit.CompareTag("Enemy"))
            {
                hit.TryGetComponent(out EnemyBase eb);
                if (eb != null) eb.TakeDamage(novaDamage, DamageNumberSpawner.ColorNova, true);

                hit.TryGetComponent(out BossBase boss);
                if (boss != null) boss.TakeDamage(novaDamage, DamageNumberSpawner.ColorNova);
            }
        }
    }

    private IEnumerator NovaRoutine()
    {
        // MODIFIE (2026-09-17) - retour utilisateur : "je veux que tu changes en
        // Y la position de la Nova, comme pour les flaques de boue". Le pivot du
        // joueur (transform.position.y) est à hauteur de torse (~1,5), pas au
        // sol - le VFX flottait donc en l'air au lieu d'être une onde de choc au
        // sol. Même logique que WeaponMudPuddle._groundY (spawnPos.y = _groundY).
        Vector3 vfxSpawnPos = transform.position;
        vfxSpawnPos.y = _novaGroundY;
        GameObject vfx = Instantiate(_novaVFXPrefab, vfxSpawnPos, Quaternion.identity);
        float elapsed = 0f;
        var alreadyHit = new HashSet<Collider>();

        while (elapsed < _novaVFXDuration)
        {
            elapsed += Time.deltaTime;
            float currentRadius = Mathf.Lerp(0f, _novaRadius, elapsed / _novaVFXDuration);
            vfx.transform.localScale = new Vector3(currentRadius * 2f, 0.05f, currentRadius * 2f);

            ApplyNovaDamage(currentRadius, alreadyHit);

            yield return null;
        }
        Destroy(vfx);
    }

    private IEnumerator ShowUltVFX()
    {
        if (_ultVFXPrefab == null) yield break;

        Vector3 vfxSpawnPos = transform.position;
        vfxSpawnPos.y = _ultGroundY;
        GameObject vfx = Instantiate(_ultVFXPrefab, vfxSpawnPos, Quaternion.identity);
        float elapsed = 0f;

        while (elapsed < _ultVFXDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float scale = Mathf.Lerp(0f, _ultRange * 2f, elapsed / _ultVFXDuration);
            vfx.transform.localScale = new Vector3(scale, 0.05f, scale);
            yield return null;
        }
        Destroy(vfx);
    }

    private IEnumerator HitstopRoutine()
    {
        const float HitstopScale = 0.05f;
        Time.timeScale = HitstopScale;
        yield return new WaitForSecondsRealtime(_hitstopDuration);
        // CORRIGE (2026-10-16, audit) - ne rend le temps que si personne d'autre ne l'a pris entre-temps (pause ou
        // level-up ouverts pendant le hitstop : timeScale = 0). Avant, le jeu repartait derrière le menu.
        if (Mathf.Approximately(Time.timeScale, HitstopScale))
            Time.timeScale = 1f;
    }

    private IEnumerator SlowAllEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _ultRange, EnemyLayers.Mask);
        foreach (Collider hit in hits)
        {
            hit.TryGetComponent(out EnemyBase eb);
            if (eb != null) eb.SetSpeedMultiplier(_slowFactor);

            hit.TryGetComponent(out BossBase boss);
            if (boss != null) boss.SetSpeedMultiplier(_slowFactor);
        }

        GameUI.Instance.ShowUltEffect(true);
        yield return new WaitForSeconds(_slowDuration);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            hit.TryGetComponent(out EnemyBase eb);
            if (eb != null) eb.SetSpeedMultiplier(1f);

            hit.TryGetComponent(out BossBase boss);
            if (boss != null) boss.SetSpeedMultiplier(1f);
        }

        GameUI.Instance.ShowUltEffect(false);
    }

    private IEnumerator OverpowerBuff()
    {
        // MODIFIÉ (2026-10-16, audit) - passe par le multiplicateur global (PlayerBuffs) au lieu de
        // WeaponBase.SetDamageMultiplier : double TOUTES les armes, pas seulement le tir de base.
        // AJOUTÉ (2026-10-21, retour utilisateur : "il faut mettre un indicateur de puissance pour le nœud
        // Surpuissance de Aether") - GameUI.SetOverpowerVisualActive suit EXACTEMENT la même durée que le
        // vrai multiplicateur (PlayerBuffs.SetOverpowerActive), jamais sa propre temporisation séparée.
        // MODIFIÉ (2026-10-22, retour utilisateur : "ça dure que 5 secondes, met 10 au lieu de 5, parce que 5
        // c'est vraiment pas assez") - 5s -> 10s.
        _overpowerActive = true;
        PlayerBuffs.SetOverpowerActive(true);
        if (GameUI.Instance != null) GameUI.Instance.SetOverpowerVisualActive(true);
        yield return new WaitForSeconds(10f);
        PlayerBuffs.SetOverpowerActive(false);
        if (GameUI.Instance != null) GameUI.Instance.SetOverpowerVisualActive(false);
        _overpowerActive = false;
    }

    private void OnDisable()
    {
        // coroutine interrompue (fin de partie, changement de scène) : ne jamais laisser le x2 actif
        if (_overpowerActive)
        {
            PlayerBuffs.SetOverpowerActive(false);
            if (GameUI.Instance != null) GameUI.Instance.SetOverpowerVisualActive(false);
            _overpowerActive = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _ultRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _novaRadius);
    }
}