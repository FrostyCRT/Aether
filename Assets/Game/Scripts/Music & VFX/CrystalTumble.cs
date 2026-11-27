using UnityEngine;

// AJOUTÉ (2026-11-23, Orbe rebondissant) - MODIFIÉ le même jour (retour utilisateur : « mets la rotation comme une boule
// sur le sol, donc vers là où elle va ») : l'orbe ROULE dans le sens de son déplacement, à la vitesse d'une boule de ce
// rayon (axe = perpendiculaire au mouvement, à l'horizontale). Calculé à partir du déplacement réel de l'image, donc suit
// aussi les rebonds. Repart du même angle à chaque activation. Purement visuel.
public class CrystalTumble : MonoBehaviour
{
    [SerializeField] private float _radius = 1.25f;   // rayon visuel monde (mis à jour au besoin par l'échelle)
    private Quaternion _start;
    private Vector3 _lastPos;

    private void Awake() => _start = transform.localRotation;

    private void OnEnable()
    {
        transform.localRotation = _start;
        _lastPos = transform.position;
    }

    private void LateUpdate()
    {
        Vector3 delta = transform.position - _lastPos;
        _lastPos = transform.position;
        delta.y = 0f;
        float dist = delta.magnitude;
        if (dist < 1e-5f || dist > 5f) return;   // > 5 : téléportation (réemploi du pool) - pas de tour fou
        Vector3 axis = Vector3.Cross(Vector3.up, delta / dist);
        float r = Mathf.Max(0.05f, _radius * transform.lossyScale.x / Mathf.Max(0.0001f, transform.localScale.x));
        transform.rotation = Quaternion.AngleAxis(dist / r * Mathf.Rad2Deg, axis) * transform.rotation;
    }
}
