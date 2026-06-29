using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("Affichage")]
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private Toggle _shadowsToggle;
    [SerializeField] private Light _directionalLight;
    [SerializeField] private Image _fullscreenKnob;
    [SerializeField] private Image _fullscreenBackground;
    [SerializeField] private Image _shadowsKnob;
    [SerializeField] private Image _shadowsBackground;
    [SerializeField] private Image _autoFireKnob;
    [SerializeField] private Image _autoFireBackground;

    [Header("Gameplay")]
    [SerializeField] private Toggle _autoFireToggle;


    private const string MUSIC_KEY = "settings_music";
    private const string SFX_KEY = "settings_sfx";
    private const string FULLSCREEN_KEY = "settings_fullscreen";
    private const string SHADOWS_KEY = "settings_shadows";
    private const string AUTOFIRE_KEY = "settings_autofire";

    
    private void Start()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        float music = PlayerPrefs.GetFloat(MUSIC_KEY, 0.75f);
        float sfx = PlayerPrefs.GetFloat(SFX_KEY, 0.75f);
        bool fullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;
        bool shadows = PlayerPrefs.GetInt(SHADOWS_KEY, 0) == 1;
        bool autoFire = PlayerPrefs.GetInt(AUTOFIRE_KEY, 1) == 1;

        if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(music);
        if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(sfx);
        if (_fullscreenToggle != null) _fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        if (_shadowsToggle != null) _shadowsToggle.SetIsOnWithoutNotify(shadows);
        if (_autoFireToggle != null) _autoFireToggle.SetIsOnWithoutNotify(autoFire);

        ApplyMusicVolume(music);
        ApplySfxVolume(sfx);
        ApplyFullscreen(fullscreen);
        ApplyShadows(shadows);

        // AJOUTE CETTE LIGNE ICI pour rafraîchir l'affichage du bouton au démarrage :
        SetToggleVisual(_autoFireBackground, _autoFireKnob, autoFire);
    }

    public void OnMusicSliderChanged(float value)
    {
        PlayerPrefs.SetFloat(MUSIC_KEY, value);
        ApplyMusicVolume(value);
    }

    private void ApplyMusicVolume(float value)
    {
        if (_audioMixer == null) return;
        float dB = value > 0.0001f ? Mathf.Log10(value) * 20f : -80f;
        _audioMixer.SetFloat("MusicVolume", dB);
    }

    public void OnSfxSliderChanged(float value)
    {
        PlayerPrefs.SetFloat(SFX_KEY, value);
        ApplySfxVolume(value);
    }

    private void ApplySfxVolume(float value)
    {
        if (_audioMixer == null) return;
        float dB = value > 0.0001f ? Mathf.Log10(value) * 20f : -80f;
        _audioMixer.SetFloat("SFXVolume", dB);
    }

    public void OnFullscreenToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(FULLSCREEN_KEY, isOn ? 1 : 0);
        ApplyFullscreen(isOn);
    }

    private void ApplyFullscreen(bool isOn)
    {
        Screen.fullScreen = isOn;
        SetToggleVisual(_fullscreenBackground, _fullscreenKnob, isOn);
    }

    public void OnShadowsToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(SHADOWS_KEY, isOn ? 1 : 0);
        ApplyShadows(isOn);
    }

    private void ApplyShadows(bool isOn)
    {
        if (_directionalLight != null)
            _directionalLight.shadows = isOn ? LightShadows.Soft : LightShadows.None;

        SetToggleVisual(_shadowsBackground, _shadowsKnob, isOn);
    }

    public void OnAutoFireToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(AUTOFIRE_KEY, isOn ? 1 : 0);
        SetToggleVisual(_autoFireBackground, _autoFireKnob, isOn);
        
    }

    public static bool IsAutoFireEnabled()
    {
        int value = PlayerPrefs.GetInt(AUTOFIRE_KEY, 1);
        
        return value == 1;
    }
    public static bool AreShadowsEnabled()
    {
        return PlayerPrefs.GetInt(SHADOWS_KEY, 0) == 1;
    }
    private void SetToggleVisual(Image background, Image knob, bool isOn)
    {
        Debug.Log($"[SettingsManager] SetToggleVisual appelée — isOn:{isOn} — background null:{background == null} — knob null:{knob == null}");
        if (background != null)
            background.color = isOn ? new Color(0.36f, 0.48f, 0.58f) : new Color(0.24f, 0.26f, 0.28f);

        if (knob != null)
        {
            RectTransform knobRect = knob.rectTransform;
            knobRect.anchorMin = new Vector2(isOn ? 1f : 0f, 0.5f);
            knobRect.anchorMax = new Vector2(isOn ? 1f : 0f, 0.5f);
            knobRect.anchoredPosition = new Vector2(isOn ? -4f : 4f, 0f);
        }
    }
}