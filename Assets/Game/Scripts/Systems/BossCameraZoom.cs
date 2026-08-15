using UnityEngine;
using Cinemachine;

public class BossCameraZoom : MonoBehaviour
{
    public static BossCameraZoom Instance { get; private set; }

    [SerializeField] private CinemachineVirtualCamera _virtualCamera;
    [SerializeField] private float _transitionSpeed = 3f;

    private float _defaultOrthoSize;
    private float _targetOrthoSize;

    private void Awake()
    {
        Instance = this;

        if (_virtualCamera != null)
        {
            _defaultOrthoSize = _virtualCamera.m_Lens.OrthographicSize;
            _targetOrthoSize = _defaultOrthoSize;
        }
    }

    private void LateUpdate()
    {
        if (_virtualCamera == null) return;
        if (Mathf.Abs(_virtualCamera.m_Lens.OrthographicSize - _targetOrthoSize) < 0.01f) return;

        LensSettings lens = _virtualCamera.m_Lens;
        lens.OrthographicSize = Mathf.MoveTowards(lens.OrthographicSize, _targetOrthoSize, _transitionSpeed * Time.deltaTime);
        _virtualCamera.m_Lens = lens;
    }

    public void SetBossZoom(float extraSize)
    {
        _targetOrthoSize = _defaultOrthoSize + extraSize;
    }

    public void ResetZoom()
    {
        _targetOrthoSize = _defaultOrthoSize;
    }
}