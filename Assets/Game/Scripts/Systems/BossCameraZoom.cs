using UnityEngine;
using Cinemachine;

public class BossCameraZoom : MonoBehaviour
{
    public static BossCameraZoom Instance { get; private set; }

    [SerializeField] private CinemachineVirtualCamera _virtualCamera;
    [SerializeField] private Vector3 _defaultFollowOffset; // AJOUTÉ — à remplir avec la valeur actuelle de ta Follow Offset dans l'inspector Cinemachine
    [SerializeField] private float _transitionSpeed = 3f;

    private Vector3 _targetOffset;
    private CinemachineTransposer _transposer; // AJOUTÉ — le composant qui gère le Follow Offset sur une Virtual Camera classique

    private void Awake()
    {
        Instance = this;

        if (_virtualCamera != null)
            _transposer = _virtualCamera.GetCinemachineComponent<CinemachineTransposer>();

        _targetOffset = _defaultFollowOffset;
    }

    private void Update()
    {
        if (_transposer == null) return;

        _transposer.m_FollowOffset = Vector3.MoveTowards(_transposer.m_FollowOffset, _targetOffset, _transitionSpeed * Time.deltaTime);
    }

    // extraOffset : décalage additionnel, typiquement en avançant/reculant sur l'axe Z ou en montant en Y selon ton setup
    public void SetBossOffset(Vector3 extraOffset)
    {
        _targetOffset = _defaultFollowOffset + extraOffset;
    }

    public void ResetOffset()
    {
        _targetOffset = _defaultFollowOffset;
    }
}