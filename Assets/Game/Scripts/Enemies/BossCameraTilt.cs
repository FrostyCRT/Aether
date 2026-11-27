using UnityEngine;

// AJOUTÉ (2026-11-26, Archonte de la Source - retour utilisateur : « quand il apparaît on dirait juste un tas
// d'éléments, on ne comprend pas ce que c'est ») - la caméra du jeu regarde à 65° vers le bas : un personnage DEBOUT
// n'y montre qu'environ 40 % de sa hauteur (on voit surtout le dessus de sa tête et de ses épaules). Le Dragon et le
// Cerf, eux, sont allongés : vus d'en haut, ils se lisent tout de suite.
// Ce composant, posé sur le modèle (enfant du boss), le BASCULE EN ARRIÈRE (le haut s'éloigne de la caméra) d'un angle
// fixe : son axe devient presque perpendiculaire au regard de la caméra, il se présente DE FACE à l'écran comme un
// personnage peint (pencher VERS la caméra, essayé d'abord, alignait au contraire le corps sur le regard : on ne voyait
// plus que le dessus de la tête). Il se tourne vers le joueur mais jamais à plus de _maxYawFromCamera de la caméra
// (vu de dos, on ne reconnaît rien). Purement visuel : pivot, déplacements et attaques du boss inchangés.
[DisallowMultipleComponent]
public class BossCameraTilt : MonoBehaviour
{
    [Tooltip("Bascule en arrière (le haut s'éloigne de la caméra), en degrés. Caméra à 65° : ~50° = vu presque de face.")]
    [SerializeField, Range(0f, 70f)] private float _tilt = 50f;

    private Quaternion _baseLocalRotation;
    private Camera _cam;

    private void Awake() { _baseLocalRotation = transform.localRotation; }

    [Tooltip("Écart maximal (degrés) entre l'orientation du modèle et la caméra : le boss se tourne vers le joueur mais ne "
           + "montre jamais son dos à l'écran (vu de dos, on ne reconnaît plus rien). 180 = aucune limite.")]
    [SerializeField, Range(0f, 180f)] private float _maxYawFromCamera = 70f;

    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        Transform parent = transform.parent;
        Quaternion parentRot = parent != null ? parent.rotation : Quaternion.identity;
        if (_cam != null && _maxYawFromCamera < 179f)
        {
            Vector3 fwd = parentRot * Vector3.forward; fwd.y = 0f;
            Vector3 toCam = -_cam.transform.forward; toCam.y = 0f;
            if (fwd.sqrMagnitude > 1e-4f && toCam.sqrMagnitude > 1e-4f)
            {
                float a = Mathf.Clamp(Vector3.SignedAngle(toCam, fwd, Vector3.up), -_maxYawFromCamera, _maxYawFromCamera);
                parentRot = Quaternion.LookRotation(Quaternion.AngleAxis(a, Vector3.up) * toCam.normalized, Vector3.up);
            }
        }
        Quaternion baseWorld = parentRot * _baseLocalRotation;
        Vector3 axis = _cam != null ? _cam.transform.right : Vector3.right;
        axis.y = 0f;
        if (axis.sqrMagnitude < 1e-4f) { transform.rotation = baseWorld; return; }
        // angle positif autour de la droite de la caméra : le haut du modèle s'éloigne de la caméra
        transform.rotation = Quaternion.AngleAxis(_tilt, axis.normalized) * baseWorld;
    }
}
