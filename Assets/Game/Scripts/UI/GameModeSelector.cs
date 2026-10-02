using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sélecteur de mode de jeu de la page Accueil du menu principal (juste au-dessus de JOUER) : deux flèches, le nom du mode,
// sa description et le meilleur résultat. Le choix est mémorisé (GameModes.Current) et lu au lancement d'une partie.
// Un mode verrouillé (Ruée de boss sans progression en Classique) reste consultable : son nom est grisé, la condition
// s'affiche à la place de la description et le bouton JOUER est désactivé tant qu'il est sélectionné.
// Ajouter un mode = une valeur dans GameMode + une entrée dans GameModes.Info / IsUnlocked : rien à changer ici.
public class GameModeSelector : MonoBehaviour
{
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;
    [SerializeField] private TextMeshProUGUI _modeLabelText;   // « MODE DE JEU »
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _recordText;
    [SerializeField] private RectTransform _punchTarget;      // élément qui « rebondit » au changement de mode
    [Tooltip("Bouton JOUER du menu : désactivé tant qu'un mode verrouillé est sélectionné.")]
    [SerializeField] private Button _playButton;

    [SerializeField] private Color _nameColor = new Color(1f, 0.79f, 0.30f, 1f);
    [SerializeField] private Color _lockedColor = new Color(0.6f, 0.58f, 0.55f, 1f);
    [SerializeField] private float _punchDuration = 0.25f;
    [SerializeField] private float _punchScale = 1.08f;

    private Coroutine _punchRoutine;

    private void Awake()
    {
        if (_leftButton != null)
        {
            _leftButton.onClick.RemoveAllListeners();
            _leftButton.onClick.AddListener(() => Step(-1));
        }
        if (_rightButton != null)
        {
            _rightButton.onClick.RemoveAllListeners();
            _rightButton.onClick.AddListener(() => Step(1));
        }
    }

    private void OnEnable()
    {
        GameModes.EnsureCurrentIsAvailable(CurrentData());
        Refresh(animate: false);
    }

    private static SaveData CurrentData()
    {
        return MetaProgressionManager.Instance != null ? MetaProgressionManager.Instance.Data : null;
    }

    private void Step(int direction)
    {
        int count = GameModes.Count;
        int next = ((int)GameModes.Current + direction + count) % count;
        GameModes.Current = (GameMode)next;
        Refresh(animate: true);
    }

    private void Refresh(bool animate)
    {
        SaveData data = CurrentData();
        GameMode mode = GameModes.Current;
        GameModes.ModeInfo info = GameModes.Info(mode, data);
        bool unlocked = GameModes.IsUnlocked(mode, data);

        if (_nameText != null)
        {
            _nameText.text = info.name;
            _nameText.color = unlocked ? _nameColor : _lockedColor;
        }
        if (_descriptionText != null) _descriptionText.text = info.description;
        RefreshRecord(mode, data, unlocked);

        // Un mode verrouillé ne peut pas être lancé.
        if (_playButton != null) _playButton.interactable = unlocked;

        bool multiple = GameModes.Count > 1;
        if (_leftButton != null) _leftButton.gameObject.SetActive(multiple);
        if (_rightButton != null) _rightButton.gameObject.SetActive(multiple);

        if (animate && isActiveAndEnabled)
        {
            if (_punchRoutine != null) StopCoroutine(_punchRoutine);
            _punchRoutine = StartCoroutine(Punch());
        }
    }

    private static string Clock(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    private void RefreshRecord(GameMode mode, SaveData data, bool unlocked)
    {
        if (_recordText == null) return;
        if (data == null || !unlocked) { _recordText.text = ""; return; }

        switch (mode)
        {
            case GameMode.Endless:
                _recordText.text = data.bestEndlessTime > 0f
                    ? $"Meilleur : {Clock(data.bestEndlessTime)}  ·  {data.bestEndlessBossKills} boss"
                    : "Aucune partie sans fin jouée";
                break;
            case GameMode.BossRush:
                _recordText.text = data.rushRuns > 0
                    ? $"Meilleur : {data.rushBestBossKills}/{GameModes.RushBossCount(data)} boss  ·  {data.rushWins} {GameModes.Plural(data.rushWins, "victoire", "victoires")}"
                    : "Aucune ruée jouée";
                break;
            case GameMode.Titans:
                _recordText.text = data.titansRuns > 0
                    ? (data.titansBestTime > 0f
                        ? $"Meilleur temps : {Clock(data.titansBestTime)}  ·  {data.titansWins} {GameModes.Plural(data.titansWins, "victoire", "victoires")}"
                        : $"Meilleur : {data.titansBestBossKills}/3 titans  ·  aucune victoire")
                    : "Aucune partie jouée";
                break;
            default:
                _recordText.text = data.bestTime > 0f ? $"Meilleur temps : {Clock(data.bestTime)}" : "";
                break;
        }
    }

    private IEnumerator Punch()
    {
        RectTransform target = _punchTarget != null ? _punchTarget : (RectTransform)transform;
        float t = 0f;
        while (t < _punchDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / _punchDuration);
            float bump = Mathf.Sin(k * Mathf.PI);                       // 0 -> 1 -> 0
            float s = 1f + (_punchScale - 1f) * bump;
            target.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        target.localScale = Vector3.one;
        _punchRoutine = null;
    }
}
