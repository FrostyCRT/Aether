using UnityEngine;

// AJOUTÉ (2026-10-05) - retour utilisateur : "fais en sorte que ce soit incroyable [...] comparable aux
// meilleurs du genre, voire meilleur". Le jeu a déjà des effets très travaillés (BossFX : flash, pilier de
// lumière, ralenti, embrasement...) mais AUCUN tremblement de caméra - un des ingrédients les plus attendus
// ("game feel") des bullet-heaven/action du genre (Vampire Survivors, Brotato, Halls of Torment...) pour donner
// du poids aux gros impacts. Volontairement réservé aux moments FORTS et peu fréquents (explosion de Boule de
// feu, arrivée/mort de boss) - jamais sur les dégâts courants (Orbitaux, Foudre...), pour ne jamais gêner la
// lisibilité en plein combat (voir retour "toujours voir ce qu'il se passe, pas bordélique").
//
// Technique "trauma" (Squirrel Eiserloh, GDC "Math for Game Programmers: Juicing Your Cameras With Math") :
// un trauma (0-1) monte instantanément à chaque secousse demandée (jamais cumulé, juste remonté au max), puis
// décroît tout seul : le déplacement réel appliqué est trauma², pas trauma - une petite secousse reste discrète,
// seule une grosse secousse donne le plein déplacement. Décale directement la caméra RENDUE (Camera.main,
// positionnée par CinemachineBrain) APRÈS que Cinemachine ait fini de la placer ce frame-là (ordre d'exécution
// tardif, voir [DefaultExecutionOrder]) - aucune dépendance à un asset de bruit Cinemachine, aucun risque
// d'interférer avec l'amortissement/le suivi du virtual camera.
[DefaultExecutionOrder(5000)]
public class CameraShakeFX : MonoBehaviour
{
    public static CameraShakeFX Instance { get; private set; }

    [SerializeField] private float _maxOffset = 0.55f;
    [SerializeField] private float _maxTiltDegrees = 2.2f;
    [SerializeField] private float _traumaDecayPerSecond = 2.4f;
    [SerializeField] private float _frequency = 20f;

    private float _trauma;
    private float _seed;
    private Camera _cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _seed = Random.Range(0f, 1000f);
    }

    // amount : 0-1, intensité de la secousse demandée. Jamais cumulée avec une secousse en cours - seul le MAX
    // compte (plusieurs petites secousses rapprochées ne doivent jamais s'additionner en une énorme).
    public static void Shake(float amount)
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("CameraShakeFX");
            go.AddComponent<CameraShakeFX>();
        }
        Instance._trauma = Mathf.Clamp01(Mathf.Max(Instance._trauma, amount));
    }

    private void LateUpdate()
    {
        if (_trauma <= 0f) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        float shake = _trauma * _trauma;
        float t = Time.time * _frequency;
        float offsetX = (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * _maxOffset * shake;
        float offsetY = (Mathf.PerlinNoise(_seed + 50f, t) * 2f - 1f) * _maxOffset * shake;
        float tilt = (Mathf.PerlinNoise(_seed + 100f, t) * 2f - 1f) * _maxTiltDegrees * shake;

        Transform camT = _cam.transform;
        camT.position += camT.right * offsetX + camT.up * offsetY;
        camT.rotation *= Quaternion.Euler(0f, 0f, tilt);

        _trauma = Mathf.Max(0f, _trauma - _traumaDecayPerSecond * Time.deltaTime);
    }
}
