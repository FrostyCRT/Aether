using UnityEngine;

// AJOUTÉ (2026-11-22, Boule de feu v2 - retour utilisateur : « on doit distinguer que c'est de la 3D, et qu'elle est en l'air,
// donc pas de rotation comme une boule ») - purement visuel, posé sur le visuel de la boule de feu :
//  - léger flottement vertical (la boule « vole », elle ne roule pas) ;
//  - lueur chaude projetée AU SOL, toujours à la verticale de la boule : vue en plongée, elle apparaît décalée de la boule,
//    ce qui montre son altitude au-dessus du terrain (et c'est la lumière que le feu jette sur l'herbe).
public class FireballFlight : MonoBehaviour
{
    [SerializeField] private Transform _groundGlow;
    [SerializeField] private float _groundHeight = 0.04f;
    [SerializeField] private float _bobAmplitude = 0.06f;
    [SerializeField] private float _bobSpeed = 7f;

    private Vector3 _baseLocalPos;
    private float _phase;

    private void Awake() => _baseLocalPos = transform.localPosition;

    private void OnEnable() => _phase = 0f;

    private void LateUpdate()
    {
        _phase += Time.deltaTime * _bobSpeed;
        transform.localPosition = _baseLocalPos + Vector3.up * (Mathf.Sin(_phase) * _bobAmplitude);
        if (_groundGlow != null)
        {
            Vector3 p = transform.position;
            _groundGlow.position = new Vector3(p.x, _groundHeight, p.z);
            _groundGlow.rotation = Quaternion.Euler(90f, 0f, 0f);   // à plat sur le sol
        }
    }
}
