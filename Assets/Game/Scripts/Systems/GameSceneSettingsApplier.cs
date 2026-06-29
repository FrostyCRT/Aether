using UnityEngine;

public class GameSceneSettingsApplier : MonoBehaviour
{
    [SerializeField] private Light _directionalLight;

    private void Start()
    {
        bool shadowsOn = SettingsManager.AreShadowsEnabled();
        if (_directionalLight != null)
            _directionalLight.shadows = shadowsOn ? LightShadows.Soft : LightShadows.None;
    }
}