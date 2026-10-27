using TMPro;
using UnityEngine;

// AJOUTÉ (2026-10-25) - pastille sombre derrière un message de page (Réputation, Personnages), même rendu que la consigne
// de l'arbre de compétences (SkillTreeUI.RefreshHeader : UI_RoundRect, largeur = texte + 56 px). La largeur suit le texte
// à chaque changement (langue comprise) ; le pivot de la pastille décide de quel côté elle s'étire.
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class HintPill : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _padding = 56f;

    private string _lastText;

    private void OnEnable() => _lastText = null;

    private void LateUpdate()
    {
        if (_text == null || _text.text == _lastText) return;
        _lastText = _text.text;
        RectTransform rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(_text.GetPreferredValues(_text.text).x + _padding, rt.sizeDelta.y);
    }
}
