using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// PREMIÈRE FUSION (2026-09-27) : Boule de feu + Boue, toutes les deux au palier max → cette arme les remplace.
//
// REFONTE COMPLÈTE (2026-10-22, retour utilisateur : "il faut revoir cette fusion, il faut trouver une idée
// originale qui se démarque des autres fusions [...] météorite qui tombe du ciel ? [...] ça doit être plus fort que
// Boue au max et Boule de feu au max") - jusqu'ici cette fusion réutilisait tel quel le prefab de flaque "Maron" de
// la Boue (juste teinté orange, déployé en spirale) : visuellement identique à la Boue et à Marécage Maudit (l'autre
// fusion qui réutilise aussi ce prefab), aucune identité propre malgré 2 passes d'équilibrage. Remplacé par un vrai
// bombardement de météorites : à intervalle régulier, un point proche du joueur est choisi au hasard, un
// GroundTelegraphFX prévient le danger au sol (même composant que BossDeer pour ses sauts), un météore visible tombe
// du ciel en même temps (MeteorFallVFX, nouveau - billboard caméra, aucune autre arme/fusion du jeu n'a d'attaque
// qui tombe du ciel) ; à l'impact : déflagration (FireExplosionVFX, déjà au niveau de qualité visé pour la Boule de
// feu), dégât d'IMPACT instantané en zone (identité "burst" de la Boule de feu), PUIS un cratère de lave qui reste
// au sol et ralentit/brûle en continu (identité "contrôle de zone" de la Boue, même prefab MudPuddleZone que les
// autres flaques, teinté braises) - les 2 identités sources sont donc toutes les 2 représentées, mais dans une
// mécanique neuve plutôt qu'un simple reskin d'une des deux.
public class WeaponFusionScorchedEarth : MonoBehaviour
{
    [Header("Cadence des météorites")]
    [SerializeField] private float _strikeInterval = 0.9f;
    [SerializeField] private float _windupDuration = 0.85f;
    [SerializeField] private float _meteorStartHeight = 13f;

    // AJOUTÉ (2026-10-23, retour utilisateur : "en jeu c'est 6 flaques de boue et une boule de feu qui traverse les
    // ennemis [...] pour les ennemis je pense que c'est pas assez [une seule météorite] [...] faut pas qu'elles
    // focus le même ennemi") - une seule météorite par frappe ne couvrait qu'un point contre une horde, alors que
    // les 2 armes sources (Boue à 6 flaques simultanées, Boule de feu qui perce plusieurs ennemis) couvrent large.
    // Nombre ADAPTATIF (voir LaunchStrike/FindTargetEnemies) : jusqu'à ce nombre de météorites simultanées, chacune
    // sur un ennemi DIFFÉRENT, mais jamais plus que le nombre d'ennemis distincts réellement trouvés - contre un
    // boss seul (1 cible), ça retombe naturellement sur 1 météore qui prend tout le dégât, sans rien gaspiller au
    // sol pour rien (retour utilisateur : "contre les boss, 1 c'est bien").
    // MODIFIÉ (2026-10-27, retour utilisateur : "pluie de météorite est un peu trop fort [...] ça tire 2 et pas 3 en
    // même temps, et c'est sur des ennemis aléatoire sur la map, pas les plus proche") - 3 -> 2.
    [SerializeField] private int _maxSimultaneousMeteors = 2;

    // RENOMMÉ (2026-10-23) - ces 2 champs ne servent plus au ciblage lui-même (voir FindTargetEnemy, qui balaie
    // maintenant toute la scène sans limite de rayon) : uniquement le repli quand AUCUN ennemi n'existe nulle part.
    [Header("Repli si aucun ennemi sur la carte (zone proche du joueur)")]
    [SerializeField] private float _minDistance = 1.5f;
    [SerializeField] private float _maxDistance = 6f;

    [Header("Impact (burst instantané, identité Boule de feu)")]
    [SerializeField] private float _impactRadius = 2.2f;

    [Header("Cratère résiduel (DPS/ralentissement continu, identité Boue)")]
    [SerializeField] private float _craterDuration = 2.5f;
    [SerializeField] private float _craterSlowMultiplier = 0.55f;

    // MODIFIÉ (2026-10-23, retour utilisateur : "ça ressemble trop à une attaque de boss, rend le plus marron
    // boueux") - la teinte braise (0.85/0.35/0.08, quasi identique aux télégraphes de boss type BossDeer) remplacée
    // par un marron boueux repris du matériau "Maron" des flaques de Boue (Assets/Game/Materials/Maron.mat).
    [SerializeField] private Color _scorchedTint = new Color(0.52f, 0.31f, 0.17f);

    private float _burstDamage;
    private float _craterDps;
    private GameObject _puddlePrefab;
    private float _strikeTimer = 0f;
    private static readonly Collider[] _overlapBuffer = new Collider[32];

    // Appelé une fois par UpgradeData.ApplyFusionResult, juste après AddComponent : startingDamagePerSecond vient
    // de la somme des dégâts hérités des deux armes fusionnées (déjà boostés par la Réputation/les picks précédents,
    // donc PAS de second bonus de Réputation ici, contrairement aux autres armes qui le lisent dans leur Awake()).
    public void Init(GameObject puddlePrefab, float startingDamagePerSecond)
    {
        _puddlePrefab = puddlePrefab;
        // PREMIER JET (2026-10-22, refonte météorites) - non encore mesuré avec [[balance-testing-dummies]] au
        // moment d'écrire ceci (à retoucher après playtest réel, même traitement que les autres premiers jets du
        // projet). Répartition : la moitié de la "puissance héritée" en dégât d'impact ponctuel par météore, l'autre
        // moitié en DPS continu du cratère - un ennemi qui reste dans la zone de bombardement cumule les deux.
        float power = startingDamagePerSecond * 11f;
        _burstDamage = power * 0.5f;
        _craterDps = power * 0.5f;
    }

    public void AddDamage(float value)
    {
        _burstDamage += _burstDamage * value;
        _craterDps += _craterDps * value;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        _strikeTimer += Time.deltaTime;
        if (_strikeTimer >= _strikeInterval)
        {
            _strikeTimer = 0f;
            LaunchStrike();
        }
    }

    // CORRIGÉ (2026-10-23, retour utilisateur : "ça ne focus pas les ennemis le ciblage de la météorite") - la
    // première version limitait la recherche à Physics.OverlapSphereNonAlloc(_maxDistance=6) autour du joueur, LA
    // MÊME zone étroite que l'ancien point aléatoire de repli. En jeu réel les ennemis ne sont pas tous groupés à 6
    // unités du joueur en permanence (vérifié en test : plusieurs ennemis bien présents dans la scène mais à 16-22
    // unités du joueur au moment de la frappe) - du coup la recherche échouait la plupart du temps et retombait
    // systématiquement sur le point aléatoire, donnant l'impression que le ciblage ne marche jamais. Remplacé par
    // un vrai balayage de TOUTE la scène (FindObjectsByType, pas de rayon limitant).
    //
    // MODIFIÉ (2026-10-23, retour utilisateur multi-météores : "faut pas qu'elles focus le même ennemi") - renvoie
    // maintenant jusqu'à maxCount ennemis/boss DISTINCTS triés du plus proche au plus loin du joueur (un seul tri,
    // pas un appel répété par météore) - chaque météore de la salve prend une entrée différente de cette liste.
    //
    // MODIFIÉ (2026-10-27, même retour que _maxSimultaneousMeteors) - ennemis DISTINCTS tirés au hasard sur toute la
    // carte (tirage partiel de Fisher-Yates) au lieu des plus proches du joueur.
    private List<Transform> FindTargetEnemies(int maxCount)
    {
        List<Transform> candidates = new List<Transform>();

        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBase e = enemies[i];
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            candidates.Add(e.transform);
        }

        BossBase[] bosses = FindObjectsByType<BossBase>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            BossBase b = bosses[i];
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            candidates.Add(b.transform);
        }

        int count = Mathf.Min(maxCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int j = Random.Range(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        candidates.RemoveRange(count, candidates.Count - count);
        return candidates;
    }

    private Vector3 RandomFallbackPosition()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = Random.Range(_minDistance, _maxDistance);
        Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
        pos = MapBoundaryUtils.ClampToZone(pos);
        pos.y = 0f;
        return pos;
    }

    // AJOUTÉ (2026-10-23) - dispatcher appelé par Update() à chaque tic de _strikeInterval : décide COMBIEN de
    // météorites partent cette fois-ci (adaptatif, voir FindTargetEnemies) et lance une coroutine indépendante par
    // météorite (SingleMeteorRoutine) plutôt qu'une seule coroutine séquentielle - les météorites de la même salve
    // tombent donc vraiment en même temps, chacune avec son propre télégraphe/suivi de cible.
    private void LaunchStrike()
    {
        List<Transform> targets = FindTargetEnemies(_maxSimultaneousMeteors);

        // Le dégât total de LA SALVE reste le même qu'avant (_burstDamage/_craterDps), simplement réparti entre les
        // météorites qui partent cette fois : contre un boss seul (1 cible trouvée), ce météore unique touche pour
        // 100% du dégât prévu, exactement comme avant l'ajout du multi-cible - rien n'est perdu ni gagné en passant
        // de 1 à plusieurs cibles, seule la COUVERTURE contre une horde augmente.
        int meteorCount = Mathf.Max(1, targets.Count);
        float burstShare = _burstDamage / meteorCount;
        float craterShare = _craterDps / meteorCount;

        if (targets.Count == 0)
        {
            StartCoroutine(SingleMeteorRoutine(RandomFallbackPosition(), null, burstShare, craterShare));
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Vector3 startPos = targets[i].position;
            startPos.y = 0f;
            StartCoroutine(SingleMeteorRoutine(startPos, targets[i], burstShare, craterShare));
        }
    }

    private IEnumerator SingleMeteorRoutine(Vector3 startPos, Transform targetEnemy, float burstDamage, float craterDps)
    {
        Vector3 targetPos = startPos;
        targetPos.y = 0f;

        GroundTelegraphFX telegraph = GroundTelegraphFX.Spawn(targetPos, _impactRadius, _scorchedTint, GroundTelegraphFX.Style.Zone);
        MeteorFallRunner meteor = MeteorFallVFX.Spawn(targetPos, _windupDuration, _meteorStartHeight);

        float t = 0f;
        while (t < _windupDuration)
        {
            // Partie en pause/fin de partie en plein windup : le télégraphe s'efface proprement au lieu de rester
            // figé à l'écran (même garde que les autres coroutines de VFX du projet).
            if (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver))
            {
                yield return null;
                continue;
            }

            // Cible en permanence : tant que l'ennemi verrouillé est vivant, le cercle au sol ET le météore qui
            // tombe suivent sa position courante (detruit/mort -> Transform Unity "null", on garde alors le
            // dernier point connu pour la suite de la frappe).
            if (targetEnemy != null)
            {
                targetPos = targetEnemy.position;
                targetPos.y = 0f;
                if (telegraph != null) telegraph.transform.position = new Vector3(targetPos.x, telegraph.transform.position.y, targetPos.z);
                if (meteor != null) meteor.UpdateLandPosition(targetPos);
            }

            t += Time.deltaTime;
            if (telegraph != null) telegraph.SetProgress(t / _windupDuration);
            yield return null;
        }

        if (this == null || !gameObject.activeInHierarchy) yield break; // arme détruite (fusion suivante) pendant le windup

        if (telegraph != null) telegraph.Impact();
        FireExplosionVFX.Spawn(targetPos, _impactRadius);
        CameraShakeFX.Shake(0.25f); // Trauma "au max", pas additif (CameraShakeFX.Shake) - sans danger même si 3 météores impactent la même frame.

        ApplyImpactDamage(targetPos, burstDamage);
        SpawnCrater(targetPos, craterDps);
    }

    private void ApplyImpactDamage(Vector3 position, float burstDamage)
    {
        int count = Physics.OverlapSphereNonAlloc(position, _impactRadius, _overlapBuffer, EnemyLayers.Mask);
        var hitBosses = new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit == null) continue;

            hit.TryGetComponent(out EnemyBase enemy);
            if (enemy != null)
            {
                // MODIFIÉ (2026-10-23) - cohérent avec la nouvelle teinte marron boueux de cette fusion (ColorMud),
                // ColorFireball (orange vif) ne correspond plus du tout au visuel une fois la refonte appliquée.
                enemy.TakeDamage(burstDamage, DamageNumberSpawner.ColorMud);
                continue;
            }

            BossBase boss = hit.GetComponentInParent<BossBase>();
            if (boss != null && hitBosses.Add(boss.GetInstanceID()))
                boss.TakeDamage(burstDamage, DamageNumberSpawner.ColorMud);
        }
    }

    private void SpawnCrater(Vector3 position, float craterDps)
    {
        if (ObjectPool.Instance == null || _puddlePrefab == null) return;

        GameObject puddleGO = ObjectPool.Instance.Get("MudPuddleZone", position, Quaternion.identity);
        if (puddleGO == null) return;

        puddleGO.TryGetComponent(out MudPuddleZone zone);
        if (zone != null)
        {
            zone.Init(_craterDuration, _impactRadius * 0.85f, _craterSlowMultiplier, craterDps);
            // Retour utilisateur d'origine (2026-10-05) : sans teinte, cette fusion affichait des flaques en tout
            // point identiques à la Boue normale - teinte distincte conservée (voir _scorchedTint, marron boueux
            // depuis le 2026-10-23).
            zone.SetColorTint(_scorchedTint);
        }
    }
}
