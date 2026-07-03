using UnityEngine;
using UnityEngine.UI;

public class DashCircleUI : MonoBehaviour
{
    [SerializeField] private Image _dashFillImage;
    [SerializeField] private Image _dashBGImage;
    [SerializeField] private PlayerController _playerController;

    [Header("Couleurs")]
    [SerializeField] private Color _colorReady = new Color(0f, 0.9f, 1f, 1f);
    [SerializeField] private Color _colorCooldown = new Color(0f, 0.5f, 0.8f, 0.7f);

    private float _pulseTimer = 0f;
    private bool _wasReady = false;

    private void Update()
    {
        if (_playerController == null || _dashFillImage == null) return;

        float fill = _playerController.DashCooldownPercent;
        bool isReady = fill >= 1f;

        _dashFillImage.fillAmount = fill;

        if (isReady)
        {
            // Pulse subtil quand le dash est disponible
            _pulseTimer += Time.deltaTime * 3f;
            float pulse = 0.85f + Mathf.Sin(_pulseTimer) * 0.15f;
            _dashFillImage.color = new Color(
                _colorReady.r,
                _colorReady.g,
                _colorReady.b,
                _colorReady.a * pulse
            );
        }
        else
        {
            _pulseTimer = 0f;
            _dashFillImage.color = _colorCooldown;
        }

        // Fond légèrement plus visible pendant le cooldown
        if (_dashBGImage != null)
            _dashBGImage.color = isReady
                ? new Color(0.1f, 0.1f, 0.1f, 0.3f)
                : new Color(0.1f, 0.1f, 0.1f, 0.6f);

        _wasReady = isReady;
    }
}