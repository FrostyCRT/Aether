using UnityEngine;

public class EnemyShooter : EnemyBase
{
    [Header("Shooter")]
    [SerializeField] private float _fireRate = 1.5f;
    [SerializeField] private float _preferredRange = 8f;
    [SerializeField] private float _fleeRange = 4f;
    [SerializeField] private float _rotationSpeed = 720f;

    [Header("Visuel")]
    [SerializeField] private GameObject _blowgunObject;
    [SerializeField] private Transform _projectileSpawnPoint;

    private float _fireTimer = 0f;
    private EnemyAnimatorController _shooterAnimatorController;

    // AJOUTÉ (2026-10-20) - voir EnemyBase.ActiveRangedCount/IsRangedAttacker (plafond de tireurs simultanés).
    protected override bool IsRangedAttacker => true;

    private static readonly EnemyBase[] _shooterNeighbourBuffer = new EnemyBase[8];

    // AJOUTÉ (2026-10-25) - retour utilisateur : "des fois ils avancent mais sur le côté". 3 causes corrigées
    // ensemble dans UpdateBehaviour / GetSeparationForce :
    //  1) le corps visait TOUJOURS le joueur alors que le déplacement mélangeait poursuite + écartement des voisins
    //     -> en marche, le corps regarde maintenant là où il marche, tourné vers le joueur d'au plus
    //        MaxFacingOffsetDegrees (assez pour garder le "regarde le joueur" du correctif du 06/10 quand l'écart est
    //        faible, sans jamais marcher en crabe) ; à l'arrêt pour tirer, il fait face au joueur comme avant ;
    //  2) l'écartement des voisins était normalisé (aussi fort que la poursuite dès qu'UN voisin était à < 3 m, et
    //     capable de l'annuler s'il se trouvait devant) -> il est maintenant proportionnel à la proximité et plafonné
    //     à SeparationWeight, toujours plus faible que la poursuite ;
    //  3) aucune marge autour de _preferredRange / _fleeRange : à ~8 m il alternait marche/tir plusieurs fois par
    //     seconde -> états avec hystérésis (RangeHysteresis / FleeExitMargin).
    // La fuite (< _fleeRange) se fait désormais en se retournant et en courant, au lieu de reculer face au joueur.
    private enum ShooterState { Chase, Hold, Flee }
    private ShooterState _state = ShooterState.Chase;

    private const float RangeHysteresis = 0.5f;       // s'approche au-delà de 8,5 m, se remet à tirer sous 7,5 m
    private const float FleeExitMargin = 1f;          // fuit sous 4 m, s'arrête de fuir au-delà de 5 m
    private const float SeparationRadius = 3f;
    private const float SeparationWeight = 0.6f;      // < 1 : l'écartement ne peut jamais annuler la poursuite
    private const float MaxFacingOffsetDegrees = 30f;

    // CORRIGÉ (2026-10-05) - retour utilisateur, partie réelle de 15 min : "les ennemis shooter ont des bugs de
    // déplacement, des fois ils tremblent car ils changent de direction tout le temps quand ils sont proches les
    // uns des autres [...] une méthode de déplacement plus simple, sans risque de tremblement". Cause trouvée :
    // contrairement à EnemyBase.UpdateBehaviour() (qui lisse sa direction via _smoothedMoveDirection + Slerp),
    // cette méthode appliquait sa direction VOULUE brute directement à la position CHAQUE FRAME, sans aucun
    // lissage. Or la force de séparation (GetSeparationForce) se normalise à partir d'un vecteur quasi nul quand
    // 2 Shooters sont collés l'un à l'autre - un vecteur quasi nul est extrêmement sensible au moindre bruit de
    // position une fois normalisé, donc sa direction peut sauter radicalement d'une frame à l'autre. Appliquée
    // sans lissage, cette direction qui saute = le tremblement observé. Corrigé en réutilisant SIMPLEMENT le même
    // mécanisme de lissage que la classe de base (_smoothedMoveDirection, maintenant protected) plutôt que
    // d'inventer un nouveau système : la direction VOULUE peut continuer à sauter d'une frame à l'autre si besoin,
    // seule la direction RÉELLEMENT appliquée au mouvement est désormais lissée dans le temps.
    protected override void UpdateBehaviour(Transform target)
    {
        if (target == null) return;

        if (_shooterAnimatorController == null)
            _shooterAnimatorController = GetComponentInChildren<EnemyAnimatorController>();

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        Vector3 separationForce = GetSeparationForce();
        _fireTimer += Time.deltaTime;

        // Transitions avec marge (voir commentaire au-dessus de ShooterState). Pas besoin de réinitialiser _state au
        // retour du pool : un état périmé se corrige de lui-même en 1 ou 2 frames via ces mêmes transitions.
        if (distanceToTarget < _fleeRange)
            _state = ShooterState.Flee;
        else if (_state == ShooterState.Flee && distanceToTarget > _fleeRange + FleeExitMargin)
            _state = ShooterState.Hold;
        else if (_state == ShooterState.Hold && distanceToTarget > _preferredRange + RangeHysteresis)
            _state = ShooterState.Chase;
        else if (_state == ShooterState.Chase && distanceToTarget <= _preferredRange - RangeHysteresis)
            _state = ShooterState.Hold;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        toTarget = toTarget.normalized;

        Vector3 desiredDirection = Vector3.zero;
        bool isMoving = _state != ShooterState.Hold;
        bool inFiringStance = !isMoving;

        if (_state == ShooterState.Flee)
            desiredDirection = (-toTarget + separationForce).normalized;
        else if (_state == ShooterState.Chase)
            desiredDirection = (toTarget + separationForce).normalized;
        else if (_fireTimer >= 1f / _fireRate)
        {
            Shoot(target);
            _fireTimer = 0f;
        }

        // Même facteur de lissage (10/s) que EnemyBase.UpdateBehaviour, pour un ressenti cohérent entre tous les
        // types d'ennemis. Ne lisse QUE vers une direction voulue valide (sqrMagnitude > 0) - jamais vers zéro,
        // sans quoi un Shooter en pleine phase de tir (isMoving=false) ralentirait en trajectoire au lieu de
        // s'arrêter net.
        if (isMoving && desiredDirection.sqrMagnitude > 0.01f)
            _smoothedMoveDirection = Vector3.Slerp(_smoothedMoveDirection, desiredDirection, 10f * Time.deltaTime);

        Vector3 moveDirection = isMoving ? _smoothedMoveDirection : Vector3.zero;
        if (isMoving)
        {
            // AJOUTÉ (2026-10-25) - vitesse selon l'alignement du corps avec la marche : pleine vitesse jusqu'à
            // MaxFacingOffsetDegrees d'écart, nulle s'il est encore de dos/de côté. Sans ça, au début d'une fuite il
            // reculait face au joueur pendant tout son demi-tour (mesuré : ~2,5 % des frames de marche à plus de 45°
            // d'écart) - désormais il pivote sur place puis court.
            Vector3 bodyForward = transform.forward;
            bodyForward.y = 0f;
            float alignment = Vector3.Dot(bodyForward.normalized, moveDirection);
            float speedFactor = Mathf.Clamp01(alignment / Mathf.Cos(MaxFacingOffsetDegrees * Mathf.Deg2Rad));
            transform.position += moveDirection * MoveSpeed * _speedMultiplier * speedFactor * Time.deltaTime;
        }

        if (_shooterAnimatorController != null)
            _shooterAnimatorController.SetAttacking(inFiringStance);

        if (_blowgunObject != null)
            _blowgunObject.SetActive(inFiringStance);

        // CORRIGÉ (2026-10-06) - retour utilisateur : "quand il marche, il est mal orienté, mais quand il tire,
        // c'est bon" - l'orientation visait `moveDirection` (chasse + séparation mélangées, jusqu'à 77° d'écart avec
        // le joueur mesurés à l'époque) ; elle avait alors été basée TOUJOURS sur le joueur.
        // MODIFIÉ (2026-10-25) - mais un corps qui regarde le joueur pendant que le déplacement part en biais = la
        // marche "sur le côté" signalée. Désormais (voir ShooterState) :
        //  - au tir : face au joueur ;
        //  - en poursuite : direction de marche, tournée vers le joueur d'au plus MaxFacingOffsetDegrees - comme
        //    l'écartement est maintenant faible, l'écart reste en général sous ce seuil et le corps fait face au
        //    joueur exactement, comme le voulait le correctif du 06/10 ;
        //  - en fuite : direction de marche (il tourne le dos et court).
        Vector3 facingDirection;
        if (!isMoving)
            facingDirection = toTarget;
        else if (_state == ShooterState.Chase)
            facingDirection = Vector3.RotateTowards(moveDirection, toTarget, MaxFacingOffsetDegrees * Mathf.Deg2Rad, 0f);
        else
            facingDirection = moveDirection;
        facingDirection.y = 0f;

        if (facingDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }

    // MODIFIE - pushDirection n'etait jamais aplati en Y, contrairement a
    // fleeDirection/chaseDirection juste a cote dans UpdateBehaviour. Comme
    // moveDirection = (fleeDirection + separationForce).normalized, une
    // composante Y residuelle ici pouvait reintroduire une derive verticale meme
    // si fleeDirection/chaseDirection etaient propres - d'autant plus visible avec
    // beaucoup de voisins proches, puisque chaque terme non-aplati s'additionne
    // (force += ...). C'est la cause des sauts aleatoires en Y signales, plus
    // frequents avec beaucoup d'ennemis en meme temps.
    // MODIFIÉ (2026-10-04) - même correctif que EnemyBase.GetBaseSeparation() : EnemySpatialGrid au lieu de
    // Physics.OverlapSphereNonAlloc (voir EnemySpatialGrid.cs pour le détail du problème/de la solution).
    // MODIFIÉ (2026-10-25) - chaque voisin pousse proportionnellement à sa proximité (1 s'il est collé, 0 à
    // SeparationRadius) au lieu d'une poussée pleine dès qu'il entre dans le rayon, et le total est plafonné à
    // SeparationWeight au lieu d'être normalisé à 1 : un voisin à 2,9 m ne dévie presque plus la trajectoire, et
    // l'écartement ne peut plus annuler la poursuite (voir commentaire au-dessus de ShooterState).
    private Vector3 GetSeparationForce()
    {
        Vector3 force = Vector3.zero;

        int count = EnemySpatialGrid.QueryNeighbors(transform.position, SeparationRadius, this, _shooterNeighbourBuffer);

        for (int i = 0; i < count; i++)
        {
            EnemyBase neighbour = _shooterNeighbourBuffer[i];

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            pushDirection.y = 0f;
            float distance = pushDirection.magnitude;
            if (distance < 0.0001f || distance >= SeparationRadius) continue;
            force += pushDirection / distance * (1f - distance / SeparationRadius);
        }

        return Vector3.ClampMagnitude(force, 1f) * SeparationWeight;
    }

    private void Shoot(Transform target)
    {
        if (ObjectPool.Instance == null) return;

        Vector3 origin = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;
        Vector3 direction = (target.position - origin).normalized;

        GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", origin, Quaternion.identity);
        if (projectileGO == null) return;

        projectileGO.TryGetComponent(out EnemyProjectile projectile);
        if (projectile != null)
        {
            projectile.Init(direction);
        }
    }
}