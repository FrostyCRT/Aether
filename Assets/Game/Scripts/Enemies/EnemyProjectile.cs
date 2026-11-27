using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 6f;
    // AJOUTÉ (2026-10-21) - garde la valeur sérialisée d'origine (prefab partagé, UNE SEULE instance de pool pour
    // tous les tireurs) pour pouvoir la restaurer en Init() : sans ça, un projectile dont la vitesse a été modifiée
    // par un override (voir Init plus bas) garderait cette vitesse modifiée à vie une fois recyclé par le pool, même
    // pour un tireur différent qui n'a jamais demandé d'override.
    private float _baseSpeed = -1f;
    [SerializeField] private float _maxRange = 40f;
    // AJOUTÉ (2026-11-14, retour utilisateur : « faire en sorte que les projectiles des boss aillent plus loin ») - les 3 boss
    // tirent ce même projectile partagé (portée 20 sur le prefab, réglée pour les tireurs normaux) : seuls les tirs marqués
    // "boss" (BossBase, BossDeer, BossCorruptedSource) portent BossRangeMultiplier fois plus loin. Gobelins / Bulbes inchangés.
    private const float BossRangeMultiplier = 1.75f;
    private float _rangeThisShot;
    [SerializeField] private float _damage = 10f; // Ajout de la stat de dégâts manquante

    // AJOUTÉ (2026-11-22, lot 1 des modèles Blender, retour utilisateur : « on doit voir ce que c'est facilement même dans une
    // horde [...] je veux quelque chose de rond ») - la règle « rond + orange = projectile ennemi absorbable » est gardée, seule
    // la matière change selon le tireur : boule de braise fissurée (sarbacane du gobelin), spore duveteuse (Bulbe). Les boss
    // gardent la sphère orange d'origine (Plain) en attendant leurs propres modèles. Prefab partagé : Init remet Plain, le
    // tireur appelle SetLook juste après.
    // MODIFIÉ (2026-11-22, retour utilisateur : « un gobelin qui souffle une braise [...] on pourrait mettre du venin violet ?
    // [...] avec sa rotation actuelle on a l'impression que c'est une boule au sol qui avance ») - la braise devient un globe de
    // venin violet à cœur lumineux (visible de jour comme pendant l'Éclipse), et il ne roule plus : il est étiré dans le sens
    // du vol, ondule comme un liquide lancé et tourne sur l'axe de vol (comme une balle), ce qui se lit « en l'air ».
    // AJOUTÉ (2026-11-23, tirs des boss - retour utilisateur : « pas obligé que ce soit orange [...] il faut juste une couleur
    // pas trop proche du kit de sorts ») - un projectile par boss, dans une couleur absente des sorts du joueur : Dragon = flamme
    // draconique CRAMOISIE en comète (orientée dans le sens du tir), Cerf = larme d'AMBRE taillée (or/miel) qui tourne
    // lentement + feuilles, Source corrompue = éclat d'OBSIDIENNE hérissé, fissures vert poison, qui tournoie.
    // MODIFIÉ (2026-11-25, retours lot 1 groupe 4 : « les projectiles restent petits et nets », comète rouge du Dragon
    // rejetée, magma orange « sans rapport » avec le Cerf) - forme ET couleurs du boss, discrètes comme les tirs des ennemis
    // de base (le ROUGE est réservé aux zones au sol à esquiver : un premier essai en rouge a été jugé « trop rouge ») ;
    // halo léger + courte traînée aux bords fondus (« BossTrail ») :
    //  - Dragon de Mana (argent/cyan) = DARD D'ÉCAILLE en acier argenté, nervure cyan pâle, pointe dans le sens du tir ;
    //  - Cerf Ancestral (blanc, bois de cristal, mousse) = ÉPINE DE BOIS DE CRISTAL blanc givré à cœur vert mousse, pointe
    //    dans le sens du tir, qui tourne sur l'axe de vol (avant : culbute au hasard, on ne voyait pas d'où elle venait).
    // Fumée sombre du Dragon et pollen du Cerf retirés (encombraient l'écran).
    //  - Source corrompue (Serpent de la Faille, obsidienne) = ÉCLAT D'OBSIDIENNE effilé à cœur bordeaux sourd, pointe dans
    //    le sens du tir, vrille sur l'axe de vol ; ses cristaux de pulsation sont à cœur INDIGO (nuance voulue : on ne doit
    //    pas confondre les cristaux et leurs tirs). Vapeur verte retirée.
    public enum Look { Plain, Venom, Spore, BossDragon, BossDeer, BossSource }
    [SerializeField] private Transform _bossDragonVisual;
    [SerializeField] private Transform _bossDeerVisual;
    [SerializeField] private Transform _bossSourceVisual;
    [Header("Visuels par tireur")]
    [SerializeField] private Transform _venomVisual;
    [SerializeField] private Transform _sporeVisual;
    [SerializeField] private float _venomSpinSpeed = 300f;     // degrés/s autour de l'axe de vol
    [SerializeField] private float _venomStretch = 1.22f;      // allongement dans le sens du vol
    [SerializeField] private float _venomWobble = 0.07f;       // amplitude de l'ondulation du liquide
    [SerializeField] private float _sporeTumbleSpeed = 160f;   // la spore tourne lentement sur elle-même
    private Look _look = Look.Plain;
    private MeshRenderer _plainRenderer;
    private Vector3 _tumbleAxis = Vector3.up;
    // Spores : la luminescence suit la nuit de l'Éclipse (voile bleu très sombre) - réglée sur une COPIE du matériau, partagée
    // par toutes les spores (une seule modification par changement de nuit, l'asset n'est jamais touché).
    [SerializeField] private Color _sporeEmissionDay = new Color(0.9f, 0.78f, 0.65f);
    [SerializeField] private Color _sporeEmissionNight = new Color(1.75f, 1.15f, 0.7f);
    private static Material _sporeRuntimeMat;
    private static Material _venomCoreRuntimeMat;
    private static float _sporeNightApplied = -1f;
    private Vector3 _venomBaseScale;
    private Transform _venomMesh;
    private float _venomSpin;
    private float _venomPhase;

    private bool _renderPriorityDone;

    public void SetLook(Look look)
    {
        _look = look;
        if (_plainRenderer == null) _plainRenderer = GetComponent<MeshRenderer>();
        if (_plainRenderer != null) _plainRenderer.enabled = look == Look.Plain;
        ShowVisual(_venomVisual, look == Look.Venom);
        ShowVisual(_sporeVisual, look == Look.Spore);
        ShowVisual(_bossDragonVisual, look == Look.BossDragon);
        ShowVisual(_bossDeerVisual, look == Look.BossDeer);
        ShowVisual(_bossSourceVisual, look == Look.BossSource);
        _tumbleAxis = Random.onUnitSphere;
        // dard du Dragon / épine du Cerf : pointe (-Z du modèle) dans le sens du tir
        Vector3 aim = _direction.sqrMagnitude > 1e-4f ? _direction : Vector3.forward;
        if (look == Look.BossDragon && _bossDragonVisual != null)
            _bossDragonVisual.rotation = Quaternion.LookRotation(aim, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        if (look == Look.BossDeer && _bossDeerVisual != null)
            _bossDeerVisual.rotation = Quaternion.LookRotation(aim, Vector3.up) * Quaternion.Euler(0f, 180f, Random.value * 360f);
        if (look == Look.BossSource && _bossSourceVisual != null)
            _bossSourceVisual.rotation = Quaternion.LookRotation(aim, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);   // à plat (voir Update)
        if (look == Look.Venom && _venomVisual != null)
        {
            // le globe (enfant "Mesh") porte l'étirement ; le parent garde les particules et s'oriente dans le sens du vol
            if (_venomMesh == null)
            {
                _venomMesh = _venomVisual.Find("Mesh");
                if (_venomMesh != null)
                {
                    _venomBaseScale = _venomMesh.localScale;
                    // cœur lumineux (2e matériau) : copie partagée avec l'émission réellement active
                    Renderer vr = _venomMesh.GetComponent<Renderer>();
                    Material[] mats = vr != null ? vr.sharedMaterials : null;
                    if (mats != null && mats.Length > 1 && mats[1] != null)
                    {
                        if (_venomCoreRuntimeMat == null) _venomCoreRuntimeMat = EmissiveCopy(mats[1], " (jeu)");
                        mats[1] = _venomCoreRuntimeMat;
                        vr.sharedMaterials = mats;
                    }
                }
            }
            _venomVisual.rotation = Quaternion.LookRotation(_direction.sqrMagnitude > 1e-4f ? _direction : Vector3.forward, Vector3.up);
            _venomSpin = Random.value * 360f;
            _venomPhase = Random.value * 10f;
        }
    }

    private void UpdateSporeGlow()
    {
        if (_sporeRuntimeMat == null)
        {
            Renderer r = _sporeVisual.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return;
            _sporeRuntimeMat = EmissiveCopy(r.sharedMaterial, " (nuit)");
            _sporeNightApplied = -1f;
        }
        Renderer mine = _sporeVisual.GetComponent<Renderer>();
        if (mine != null && mine.sharedMaterial != _sporeRuntimeMat) mine.sharedMaterial = _sporeRuntimeMat;
        float k = NightOfShards.NightFactor;
        if (Mathf.Abs(k - _sporeNightApplied) < 0.02f) return;
        _sporeNightApplied = k;
        _sporeRuntimeMat.SetColor("_EmissionColor", Color.Lerp(_sporeEmissionDay, _sporeEmissionNight, k));
    }

    // Le mot-clé _EMISSION des matériaux URP Lit créés par script n'est pas conservé au chargement (vérifié en Play : fichier
    // correct, matériau chargé sans le mot-clé, donc aucune lueur) - même méthode que GoldenGoblinEvent/CrystalStormEvent :
    // une copie, mot-clé activé en jeu.
    private static Material EmissiveCopy(Material src, string suffix)
    {
        var m = new Material(src) { name = src.name + suffix };
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return m;
    }

    private static void ShowVisual(Transform visual, bool on)
    {
        if (visual == null) return;
        if (visual.gameObject.activeSelf != on) visual.gameObject.SetActive(on);
        if (!on) return;
        visual.rotation = Random.rotation;   // chaque tir montre une autre face des fissures / du duvet
        // traînée de particules en espace monde : vidée à chaque réemploi du pool (sinon un trait relierait l'ancienne
        // position du projectile à la nouvelle)
        foreach (ParticleSystem ps in visual.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }
        foreach (TrailRenderer tr in visual.GetComponentsInChildren<TrailRenderer>(true)) tr.Clear();   // BossTrail des boss
    }

    private Vector3 _direction;
    public Vector3 Direction => _direction;   // didacticiel : sens de déplacement
    private Vector3 _startPosition;
    private bool _hasHit = false;

    // AJOUTE (2026-10-16, audit) - cause de mort transmise au HealthSystem : "boss" pour les tirs de boss (sinon le
    // message de défaite parlait toujours de la horde, même tué par un projectile de boss). Projectile poolé : remis
    // à chaque Init.
    private string _damageSource = "horde";

    // AJOUTÉ (2026-10-21, retour utilisateur : "pour la cadence des bulbes [...] c'est pas la cadence mais la
    // vitesse du projectile lui-même pour se déplacer que je voulais ralentir" - correction du point 4 de la suite
    // 17, mal compris la 1ère fois : la cadence de tir avait été ralentie (3s -> 4,5s, voir EnemyBulb._fireInterval)
    // alors que la vraie demande portait sur la VITESSE DE DÉPLACEMENT du projectile. Cadence redescendue à 3s.
    // EnemyProjectile est PARTAGÉ (Bulb/EnemyShooter/BossDeer/BossCorruptedSource/BossBase) : un override optionnel
    // ici, plutôt que de changer _speed sur le prefab, pour ne ralentir QUE les tirs du Bulbe sans toucher aux
    // autres tireurs. speedOverride <= 0 = garde _speed par défaut (comportement inchangé pour tous les appelants
    // existants qui n'utilisent pas ce paramètre).
    public void Init(Vector3 direction, string damageSource = "horde", float speedOverride = -1f)
    {
        // AJOUTÉ (2026-11-27) - projectiles ennemis TOUJOURS visibles, au premier plan (voir RenderPriority)
        if (!_renderPriorityDone) { RenderPriority.ApplyEnemyProjectile(gameObject); _renderPriorityDone = true; }
        _damageSource = damageSource;
        if (_baseSpeed < 0f) _baseSpeed = _speed; // capturée une seule fois, à la toute première utilisation réelle
        _speed = speedOverride > 0f ? speedOverride : _baseSpeed;
        direction.y = 0f; // AJOUTÉ — sécurité, empêche toute trajectoire qui dériverait en hauteur
        _direction = direction.normalized;

        Vector3 spawnPos = transform.position;
        spawnPos.y = 1.5f;
        transform.position = spawnPos;

        _startPosition = transform.position;
        _hasHit = false;
        SetLook(Look.Plain);
        _rangeThisShot = damageSource == "boss" ? _maxRange * BossRangeMultiplier : _maxRange;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // CORRECTION PAUSE
        if (GameManager.Instance.IsGameOver || GameManager.Instance.IsPaused) return;
        if (EnemyBase.TutorialFreeze) return;

        transform.position += _direction * _speed * Time.deltaTime;

        if (_look == Look.Venom && _venomMesh != null)
        {
            _venomSpin += _venomSpinSpeed * Time.deltaTime;
            _venomPhase += Time.deltaTime;
            // l'enfant est tourné sur l'axe de vol (Z local du parent) puis étiré le long de cet axe ; l'ondulation alterne
            // allongé / aplati comme une goutte de liquide qui file dans l'air
            float w = Mathf.Sin(_venomPhase * 17f) * _venomWobble;
            _venomMesh.localRotation = Quaternion.AngleAxis(_venomSpin, Vector3.forward);
            _venomMesh.localScale = Vector3.Scale(_venomBaseScale, new Vector3(1f / Mathf.Sqrt(_venomStretch) - w * 0.5f, 1f / Mathf.Sqrt(_venomStretch) - w * 0.5f, _venomStretch + w));
        }
        else if (_look == Look.BossDeer && _bossDeerVisual != null && _direction.sqrMagnitude > 1e-4f)
            _bossDeerVisual.rotation = Quaternion.AngleAxis(240f * Time.deltaTime, _direction) * _bossDeerVisual.rotation;   // vrille sur l'axe de vol
        else if (_look == Look.BossSource && _bossSourceVisual != null && _direction.sqrMagnitude > 1e-4f)
        {
            // MODIFIÉ (2026-11-26, Archonte v2) - plume-lame de lumière PLATE : une vrille la montrait de profil (invisible)
            // la moitié du temps. Elle reste à plat face à la caméra et se balance doucement (±22°) autour de l'axe de vol.
            _venomPhase += Time.deltaTime;
            _bossSourceVisual.rotation = Quaternion.LookRotation(_direction, Vector3.up) * Quaternion.Euler(0f, 180f, Mathf.Sin(_venomPhase * 7f) * 22f);
        }
        else if (_look == Look.Spore && _sporeVisual != null)
        {
            _sporeVisual.rotation = Quaternion.AngleAxis(_sporeTumbleSpeed * Time.deltaTime, _tumbleAxis) * _sporeVisual.rotation;
            UpdateSporeGlow();
        }

        // OPTIMISATION : SqrMagnitude est plus performant que Vector3.Distance pour les vérifications de portée
        if (Vector3.SqrMagnitude(transform.position - _startPosition) >= _rangeThisShot * _rangeThisShot)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        if (other.CompareTag("Player"))
        {
            other.TryGetComponent(out PlayerController player);
            other.TryGetComponent(out HealthSystem playerHealthCheck);
            // CORRIGÉ (2026-10-07, retour utilisateur : "quand Lyra met son clone et qu'elle est invisible/invincible,
            // elle peut toujours se prendre des projectiles, même si elle ne se prend pas les dégâts") - ce bloc lisait
            // player.IsInvincible (PlayerController), qui ne vaut vrai QUE pendant le Dash (StartDash) ; l'invincibilité
            // du Clone Fantôme passe par HealthSystem.AddExternalInvincibility() et ne touche jamais ce flag. Résultat :
            // pendant le clone, on tombait dans la branche "dégâts normaux" plus bas - TakeDamage() ne faisait rien
            // (HealthSystem bloque bien les PV), mais le VFX d'impact rouge jouait quand même et le projectile était
            // détruit. Lit maintenant HealthSystem.IsInvincible (la vraie source de vérité, qui inclut le clone).
            if (playerHealthCheck != null && playerHealthCheck.IsInvincible)
            {
                if (player != null && player.CanAbsorb)
                {
                    other.TryGetComponent(out CrystalSystem crystal);
                    if (crystal != null) crystal.AbsorbProjectile();

                    // AJOUTÉ (2026-10-03) - absorption par le Dash/Cristal : jusqu'ici muette, comme tout impact de ce
                    // projectile. Même bleu que le chiffre de dégâts "Projectile" (DamageNumberSpawner.ColorProjectile).
                    ExpandingRingVFX.Spawn(transform.position, 0.9f, new Color(0f, 0.8f, 1f, 0.85f), 0.3f);

                    _hasHit = true;
                    Despawn();
                }
                return;
            }

            // AJOUTE - nœud capstone Gardien "Bouclier de Mana" : si le joueur n'est
            // pas déjà invincible et qu'une charge de bouclier est disponible, le
            // projectile est absorbé (0 dégât) et alimente le Cristal.
            other.TryGetComponent(out ManaShield shield);
            if (shield != null && shield.TryAbsorb())
            {
                // AJOUTÉ (2026-10-03) - absorption par le Bouclier de Mana : même vert que la bulle (#36D936).
                ExpandingRingVFX.Spawn(transform.position, 0.9f, new Color(0.212f, 0.851f, 0.212f, 0.85f), 0.3f);

                _hasHit = true;
                Despawn();
                return;
            }

            // CORRECTION LOGIQUE : Application des dégâts si le joueur n'est pas invincible
            // CORRIGÉ (2026-10-11, retour utilisateur : "je me fais one shot [...] si je suis dans lui, et qu'il
            // lance ses projectiles, je les prends tous en même temps") - TakeDamage() direct n'avait aucune
            // protection entre plusieurs projectiles distincts touchant la même frame (voir
            // HealthSystem.TakeDamageFromProjectile, qui a maintenant sa propre fenêtre anti-cumul).
            if (playerHealthCheck != null)
            {
                playerHealthCheck.TakeDamageFromProjectile(_damage * GameModes.EnemyDamageScale, _damageSource);

                // AJOUTÉ (2026-10-03) - jusqu'ici le projectile disparaissait sans rien au moment même où il touche le
                // joueur (seul le chiffre de dégâts, un peu après, signalait le coup). Même rouge que
                // DamageNumberSpawner.ColorPlayer, pour que l'œil associe tout de suite éclat ↔ dégât subi.
                ExpandingRingVFX.Spawn(transform.position, 0.7f, new Color(1f, 0.2f, 0.2f, 0.85f), 0.25f);
            }

            _hasHit = true;
            Despawn();
        }
    }

    // CORRECTION POOLING : Centralisation de la désactivation pour éviter de détruire l'entité
    private void Despawn()
    {
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool("EnemyProjectile", gameObject);
        }
        else
        {
            Destroy(gameObject); // Sécurité de secours
        }
    }
}