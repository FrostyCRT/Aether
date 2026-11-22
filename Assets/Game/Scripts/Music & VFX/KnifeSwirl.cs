using UnityEngine;

// AJOUTÉ (2026-11-22, Couteaux de Lyra - lot 1 des modèles Blender) - le souffle magique menthe et rose de l'illustration
// de la carte : deux rubans (TrailRenderer, espace monde) accrochés de part et d'autre de la lame tournent autour de l'axe
// de vol, ce qui dessine une spirale derrière le couteau. La dague elle-même ne tournoie pas (pointe devant, aspect
// identique à chaque tir). Purement visuel. Les traînées sont vidées à chaque réemploi du pool (sinon un trait relierait
// l'ancienne position du couteau à la nouvelle).
public class KnifeSwirl : MonoBehaviour
{
    [SerializeField] private float _degreesPerSecond = 900f;
    private TrailRenderer[] _trails;
    private int _clearFrames;

    private void Awake() => _trails = GetComponentsInChildren<TrailRenderer>(true);

    private void OnEnable()
    {
        transform.localRotation = Quaternion.identity;
        _clearFrames = 2;
        ClearTrails();
    }

    private void ClearTrails()
    {
        if (_trails == null) return;
        foreach (TrailRenderer t in _trails) if (t != null) t.Clear();
    }

    private void Update()
    {
        // axe de vol = Z local du visuel (voir ProjectileBasic.OrientVisual)
        transform.localRotation = Quaternion.AngleAxis(_degreesPerSecond * Time.deltaTime, Vector3.forward) * transform.localRotation;
    }

    private void LateUpdate()
    {
        if (_clearFrames <= 0) return;
        _clearFrames--;
        ClearTrails();
    }
}
