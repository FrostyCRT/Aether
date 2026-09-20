using UnityEngine;
using UnityEngine.UI;

// AJOUTE (2026-09-19) - habillage du cadre "personnage verrouillé", teinté à la couleur du
// personnage (mêmes codes que la fiche : orange / vert / cyan). Refonte du cadre d'origine :
//  - plaque grise translucide à angles vifs -> voile sombre qui s'estompe sur les bords ;
//  - chaînes en grand X qui couvraient visage + logo -> X aplati resserré sur le torse ;
//  - cadenas nu au milieu du visage -> cadenas dans un médaillon (même langage que les
//    médaillons de l'arbre et de l'arme) ;
//  - portrait délavé par un alpha bas -> portrait désaturé mais net (shader
//    Aether/UI/Desaturate, appliqué par CharacterSelectUI).
// Les assets (chaînes, cadenas) sont ceux d'origine, seule leur composition change.
public class CharacterLockOverlay : MonoBehaviour
{
    [SerializeField] private Image _veil;
    [SerializeField] private Image[] _chains;
    [SerializeField] private Image _ring;
    [Range(0f, 1f)] [SerializeField] private float _veilAlpha = 0.5f;

    public void Apply(Color accent)
    {
        if (_veil != null)
        {
            Color veil = Color.Lerp(new Color(0.02f, 0.02f, 0.05f), accent, 0.10f);
            veil.a = _veilAlpha;
            _veil.color = veil;
        }

        if (_chains != null)
        {
            // argent légèrement teinté et assombri : moins bruyant que l'argent brillant d'origine
            Color chain = Color.Lerp(Color.white, accent, 0.22f) * 0.85f;
            chain.a = 0.95f;
            foreach (Image c in _chains)
                if (c != null) c.color = chain;
        }

        if (_ring != null) _ring.color = accent;
    }
}
