using UnityEngine;

// AJOUTÉ (2026-11-24, cristaux du Boss 3 - lot 1 des modèles Blender) - rotation lente d'un visuel sur son axe vertical local,
// avec un léger flottement : les facettes du cristal accrochent la lumière tour à tour. Angle de départ aléatoire pour que
// les 6 cristaux du boss ne tournent pas en parfaite synchronisation. Purement visuel.
public class SpinY : MonoBehaviour
{
    [SerializeField] private float _degreesPerSecond = 40f;
    [SerializeField] private float _bobAmplitude = 0.08f;
    [SerializeField] private float _bobSpeed = 2.2f;
    private Vector3 _basePos;
    private float _phase;

    private void Awake()
    {
        _basePos = transform.localPosition;
        _phase = Random.value * 10f;
        transform.localRotation = Quaternion.AngleAxis(Random.value * 360f, Vector3.up) * transform.localRotation;
    }

    private void Update()
    {
        _phase += Time.deltaTime;
        transform.localRotation = Quaternion.AngleAxis(_degreesPerSecond * Time.deltaTime, Vector3.up) * transform.localRotation;
        transform.localPosition = _basePos + Vector3.up * (Mathf.Sin(_phase * _bobSpeed) * _bobAmplitude);
    }
}
