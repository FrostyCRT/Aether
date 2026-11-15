using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
    private static readonly int WalkRateId = Animator.StringToHash("WalkRate");

    private Animator _animator;
    private bool _hasWalkRate;

    // AJOUTÉ (2026-11-15, retour utilisateur : « Lyra est tournée quand elle marche [...] mets son LyraTripo rotation en
    // y = 0 quand elle marche ») - le modèle de Lyra garde ses 20° de rotation (réglés pour l'attente) seulement à l'arrêt ;
    // en marche il revient à 0° (sinon la foulée part de travers, même souci qu'Aether avant). Fondu court (~0,15 s) pour
    // suivre la transition Attente <-> Marche de l'Animator. Coché seulement sur Player_Lyra.
    [SerializeField] private bool _straightenModelWhenWalking;
    private Transform _straightenModel;
    private float _idleYaw, _walkBlend;
    private bool _walking;
    private const float StraightenSpeed = 7f;

    void Awake()
    {
        // Cherche d'abord sur cet objet, puis dans les enfants
        // pour trouver celui qui est lié au modèle 3D Tripo
        _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
            Debug.LogError($"[PlayerAnimatorController] Aucun Animator trouvé sur {gameObject.name} ou ses enfants !");
        else
            foreach (AnimatorControllerParameter p in _animator.parameters)
                if (p.nameHash == WalkRateId) _hasWalkRate = true;

        // Player_Lyra porte 2 exemplaires de ce composant (racine + LyraTripo) : seul celui de la racine reçoit SetWalking ;
        // celui du modèle remettrait l'angle d'attente à chaque image, il ne s'en occupe donc pas.
        bool outermost = transform.parent == null || transform.parent.GetComponentInParent<PlayerAnimatorController>() == null;
        if (_straightenModelWhenWalking && outermost && _animator != null)
        {
            _straightenModel = _animator.transform;
            _idleYaw = _straightenModel.localEulerAngles.y;
        }
    }

    private void LateUpdate()
    {
        if (_straightenModel == null) return;
        _walkBlend = Mathf.MoveTowards(_walkBlend, _walking ? 1f : 0f, Time.deltaTime * StraightenSpeed);
        ApplyStraighten();
    }

    private void ApplyStraighten()
    {
        Vector3 e = _straightenModel.localEulerAngles;
        e.y = Mathf.LerpAngle(_idleYaw, 0f, _walkBlend);
        _straightenModel.localEulerAngles = e;
    }

    public void SetWalking(bool isMoving)
    {
        if (_animator == null) return;
        _animator.SetBool("IsWalking", isMoving);
        _walking = isMoving;
    }

    // AJOUTÉ (2026-11-08, retour utilisateur : "l'animation de walk [doit] être plus rapide par rapport à la vitesse du personnage,
    // qui va de plus en plus vite dans les améliorations de réputation, mais pas trop non plus") - le pas suit la vitesse, en moins
    // que proportionnel. speedRatio = vitesse actuelle / vitesse de base : ×1,20 sans bonus, ×1,44 avec +30 % de Réputation.
    // Seul Aether a le paramètre "WalkRate" dans son Animator.
    private const float WalkRateAtBaseSpeed = 1.2f, WalkRateExponent = 0.7f, WalkRateMin = 0.8f, WalkRateMax = 1.5f;

    public void SetWalkRate(float speedRatio)
    {
        if (!_hasWalkRate) return;
        float rate = WalkRateAtBaseSpeed * Mathf.Pow(Mathf.Max(speedRatio, 0.01f), WalkRateExponent);
        _animator.SetFloat(WalkRateId, Mathf.Clamp(rate, WalkRateMin, WalkRateMax));
    }

    public void TriggerDeath()
    {
        if (_animator == null) return;
        _animator.SetTrigger("IsDead");
    }

    // AJOUTE (2026-09-24) - force une pose Idle immédiate, sans blend ni délai, PUIS applique cette pose au maillage
    // sur-le-champ (Update(0) évalue l'Animator tout de suite au lieu d'attendre la prochaine image du moteur).
    // Sert à PlayerController.SpawnPhantomClone : le clone est un instantané figé du maillage (BakeMesh) - le
    // prendre pendant une image de marche donnait un clone "gelé en pleine foulée" au lieu d'un clone immobile.
    // Tout se passe dans la même image (avant tout rendu) : invisible pour le joueur, qui garde sa vraie pose juste
    // après (voir l'appel à SetWalking() qui suit, dans le même bloc de code).
    public void SnapToIdlePose()
    {
        if (_animator == null) return;
        _animator.Play("Idle", 0, 0f);
        _animator.Update(0f);
        if (_straightenModel != null) { _walkBlend = 0f; ApplyStraighten(); }
    }
}