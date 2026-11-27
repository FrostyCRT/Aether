using UnityEngine;

// AJOUTÉ (2026-11-23, Couteaux de Lyra v2 - retour utilisateur : « on ne voit pas assez les reliefs, tu peux un peu les faire
// tourner ») - balancement de la dague autour de son axe de vol (±_amplitude), pas un tour complet : la lumière passe d'une
// facette à l'autre et révèle l'arête et les biseaux, sans jamais montrer la dague par la tranche. Le balancement repart
// de la même phase à chaque tir (aspect identique d'un tir à l'autre). Purement visuel.
public class KnifeRoll : MonoBehaviour
{
    [SerializeField] private float _amplitude = 40f;
    [SerializeField] private float _speed = 9f;   // radians/s
    private float _t;

    private void OnEnable() { _t = 0f; Apply(); }

    private void Update()
    {
        _t += Time.deltaTime;
        Apply();
    }

    // axe de vol = Z local (le parent "Visual" est orienté par ProjectileBasic.OrientVisual)
    private void Apply() => transform.localRotation = Quaternion.AngleAxis(Mathf.Sin(_t * _speed) * _amplitude, Vector3.forward);
}
