using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// AJOUTÉ (2026-10-28, session manette) - vibrations de la manette.
//
//   PadRumble.Pulse(grave, aigu, durée)   grave = gros moteur (chocs sourds), aigu = petit moteur (secousses vives)
//
// Plusieurs secousses peuvent se chevaucher : à chaque image, chaque moteur prend la plus forte en cours, qui
// s'estompe sur sa durée. Rien ne vibre si le joueur joue au clavier, a coupé les vibrations (Paramètres >
// Commandes), pendant la pause (le jeu est figé : une vibration qui continue serait déroutante), ou quand la
// fenêtre perd le focus. Les moteurs sont toujours remis à zéro en quittant (sinon une manette peut rester vibrer).
// Temps réel (unscaled) : un coup fatal vibre aussi pendant l'écran de fin.
public class PadRumble : MonoBehaviour
{
    private struct Shake { public float low, high, start, end; }

    private static PadRumble _instance;
    private readonly List<Shake> _shakes = new List<Shake>();
    private Gamepad _pad;          // manette qui vibre actuellement (pour l'arrêter même si une autre devient « courante »)
    private float _low, _high;     // dernières vitesses envoyées

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("[PadRumble]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PadRumble>();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Stop();
    }

    public static void Pulse(float low, float high, float duration)
    {
        if (_instance == null || duration <= 0f || !Allowed()) return;
        float now = Time.unscaledTime;
        _instance._shakes.Add(new Shake { low = Mathf.Clamp01(low), high = Mathf.Clamp01(high), start = now, end = now + duration });
    }

    public static void Stop()
    {
        if (_instance == null) return;
        _instance._shakes.Clear();
        _instance._lastNonZero = Time.unscaledTime;   // l'arrêt sera renvoyé plusieurs fois (voir Apply)
        _instance.Apply(0f, 0f);
    }

    private static bool Allowed() =>
        GameInput.UsingGamepad && Gamepad.current != null && GameSettings.GetBool(GameSettings.PadRumble);

    private void Update()
    {
        if (!Allowed() || (GameManager.Instance != null && GameManager.Instance.IsPaused))
        {
            _shakes.Clear();
            Apply(0f, 0f);
            return;
        }

        float now = Time.unscaledTime, low = 0f, high = 0f;
        for (int i = _shakes.Count - 1; i >= 0; i--)
        {
            Shake s = _shakes[i];
            if (now >= s.end) { _shakes.RemoveAt(i); continue; }
            float fade = 1f - (now - s.start) / (s.end - s.start);   // décroissance linéaire : un choc, pas un bourdonnement
            low = Mathf.Max(low, s.low * fade);
            high = Mathf.Max(high, s.high * fade);
        }
        float strength = GameSettings.GetFloat(GameSettings.PadRumbleStrength);
        Apply(low * strength, high * strength);
    }

    // CORRIGÉ (2026-10-28, retour utilisateur : "ça vibre à des moments aléatoires [...] même hors de la partie") - deux
    // façons pour une manette de rester vibrer alors que le jeu la croit arrêtée :
    //  - Input System met les vibrations en pause quand la fenêtre perd le focus et RELANCE les dernières vitesses quand
    //    elle le retrouve : une secousse coupée par un changement de fenêtre repartait, et comme on croyait déjà avoir
    //    envoyé 0, on ne le renvoyait jamais. Le focus remet désormais tout à zéro (ResetHaptics, qui efface aussi ces
    //    vitesses mémorisées), à la perte comme au retour.
    //  - un ordre « stop » perdu (Bluetooth) : après chaque secousse, l'arrêt est renvoyé plusieurs fois (ZeroResend).
    private const float ZeroResendWindow = 2f;     // pendant 2 s après la dernière secousse...
    private const float ZeroResendInterval = 0.4f; // ...l'arrêt est renvoyé toutes les 0,4 s
    private float _lastNonZero = -999f, _lastZeroSent = -999f;

    private void Apply(float low, float high)
    {
        Gamepad current = Gamepad.current;
        if (_pad != null && _pad != current && (_low > 0f || _high > 0f)) _pad.SetMotorSpeeds(0f, 0f);
        _pad = current;
        if (_pad == null) { _low = _high = 0f; return; }

        float now = Time.unscaledTime;
        bool zero = low <= 0f && high <= 0f;
        if (!zero) _lastNonZero = now;
        bool changed = Mathf.Abs(low - _low) >= 0.01f || Mathf.Abs(high - _high) >= 0.01f;
        bool resendZero = zero && now - _lastNonZero < ZeroResendWindow && now - _lastZeroSent >= ZeroResendInterval;
        if (!changed && !resendZero) return;
        _low = low; _high = high;
        _pad.SetMotorSpeeds(low, high);
        if (zero) _lastZeroSent = now;
    }

    private void OnApplicationFocus(bool focus)
    {
        _shakes.Clear();
        InputSystem.ResetHaptics();
        _low = _high = 0f;
        _lastNonZero = Time.unscaledTime;   // renvoie encore l'arrêt quelques fois après le changement de fenêtre
    }

    private void OnApplicationQuit() => InputSystem.ResetHaptics();

    private void OnDisable()
    {
        _shakes.Clear();
        if (_pad != null) _pad.SetMotorSpeeds(0f, 0f);
        _low = _high = 0f;
    }
}
