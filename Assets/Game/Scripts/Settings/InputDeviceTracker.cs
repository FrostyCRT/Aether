using UnityEngine;

// AJOUTÉ (2026-10-28, session manette) - objet persistant créé tout seul au lancement (comme CustomCursor) : met à
// jour chaque image le dernier appareil utilisé (GameInput.UsingGamepad / PadStyle) AVANT tous les autres scripts,
// pour que les icônes, le curseur et la visée réagissent dans la même image.
[DefaultExecutionOrder(-1000)]
public class InputDeviceTracker : MonoBehaviour
{
    private static InputDeviceTracker _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("[InputDeviceTracker]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InputDeviceTracker>();
    }

    private void Update()
    {
        GameInput.TrackDevice();
    }
}
