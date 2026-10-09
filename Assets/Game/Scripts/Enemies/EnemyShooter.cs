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

    private static readonly EnemyBase[] _shooterNeighbourBuffer = new EnemyBase[8];

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

        Vector3 desiredDirection = Vector3.zero;
        bool isMoving;
        bool inFiringStance;

        if (distanceToTarget < _fleeRange)
        {
            Vector3 fleeDirection = transform.position - target.position;
            fleeDirection.y = 0f;
            fleeDirection = fleeDirection.normalized;
            desiredDirection = (fleeDirection + separationForce).normalized;
            isMoving = true;
            inFiringStance = false;
        }
        else if (distanceToTarget > _preferredRange)
        {
            Vector3 chaseDirection = target.position - transform.position;
            chaseDirection.y = 0f;
            chaseDirection = chaseDirection.normalized;
            desiredDirection = (chaseDirection + separationForce).normalized;
            isMoving = true;
            inFiringStance = false;
        }
        else
        {
            isMoving = false;
            inFiringStance = true;

            if (_fireTimer >= 1f / _fireRate)
            {
                Shoot(target);
                _fireTimer = 0f;
            }
        }

        // Même facteur de lissage (10/s) que EnemyBase.UpdateBehaviour, pour un ressenti cohérent entre tous les
        // types d'ennemis. Ne lisse QUE vers une direction voulue valide (sqrMagnitude > 0) - jamais vers zéro,
        // sans quoi un Shooter en pleine phase de tir (isMoving=false) ralentirait en trajectoire au lieu de
        // s'arrêter net.
        if (isMoving && desiredDirection.sqrMagnitude > 0.01f)
            _smoothedMoveDirection = Vector3.Slerp(_smoothedMoveDirection, desiredDirection, 10f * Time.deltaTime);

        Vector3 moveDirection = isMoving ? _smoothedMoveDirection : Vector3.zero;
        if (isMoving)
            transform.position += moveDirection * MoveSpeed * _speedMultiplier * Time.deltaTime;

        if (_shooterAnimatorController != null)
            _shooterAnimatorController.SetAttacking(inFiringStance);

        if (_blowgunObject != null)
            _blowgunObject.SetActive(inFiringStance);

        // CORRIGÉ (2026-10-06) - retour utilisateur : "quand il marche, il est mal orienté, mais quand il tire,
        // c'est bon" - mesuré et confirmé : avec plusieurs Shooters serrés en chasse (déplacement actif), l'écart
        // entre l'orientation visuelle et la vraie direction du joueur montait jusqu'à 77° (6 Goblins rapprochés,
        // 40 pas de simulation). Cause : en déplacement, l'orientation visait `moveDirection` (chasse + force de
        // séparation entre voisins MÉLANGÉES) au lieu du joueur directement - à l'arrêt pour tirer, seule la
        // direction PURE vers la cible était utilisée (pas de mélange), d'où la différence flagrante entre les 2
        // états. Le déplacement garde sa force de séparation (évite que les Shooters se chevauchent), mais
        // l'ORIENTATION VISUELLE se base désormais TOUJOURS sur la direction directe vers la cible, dans tous les
        // états (marche ou tir) - le corps fait face à l'ennemi même quand il se décale légèrement pour éviter un
        // voisin, exactement comme à l'arrêt.
        Vector3 facingDirection = target.position - transform.position;
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
    private Vector3 GetSeparationForce()
    {
        Vector3 force = Vector3.zero;
        float separationRadius = 3f;

        int count = EnemySpatialGrid.QueryNeighbors(transform.position, separationRadius, this, _shooterNeighbourBuffer);

        for (int i = 0; i < count; i++)
        {
            EnemyBase neighbour = _shooterNeighbourBuffer[i];

            Vector3 pushDirection = transform.position - neighbour.transform.position;
            pushDirection.y = 0f;
            force += pushDirection.normalized;
        }

        return force.normalized;
    }

    private void Shoot(Transform target)
    {
        if (ObjectPool.Instance == null) return;

        Vector3 origin = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;
        Vector3 direction = (target.position - origin).normalized;

        GameObject projectileGO = ObjectPool.Instance.Get("EnemyProjectile", origin, Quaternion.identity);
        if (projectileGO == null) return;

        EnemyProjectile projectile = projectileGO.GetComponent<EnemyProjectile>();
        if (projectile != null)
        {
            projectile.Init(direction);
        }
    }
}