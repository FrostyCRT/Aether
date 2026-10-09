using UnityEngine;

// AJOUTÉ (2026-10-05) - fait suivre un prop (sarbacane du Goblin...) la MÊME frame que le corps VAT, via une
// petite courbe position/rotation précuite (voir VATBoneAttachmentData.cs) au lieu de rester figé à la pose de
// bind comme le reste du squelette (désormais inerte - voir NOTES.md, "LA solution" perf). Coût négligeable :
// un seul Transform mis à jour par instance, uniquement pour les quelques ennemis qui portent un prop de ce
// genre (pas tous) - sans rapport avec le coût d'Animator qu'on a supprimé.
public class VATBoneAttachment : MonoBehaviour
{
    [SerializeField] private VATAnimatedRenderer _source;
    [SerializeField] private VATBoneAttachmentData _data;
    [SerializeField] private Transform _root; // l'ennemi entier (racine qui se déplace/tourne) - position relative a LUI, pas a un os parent

    private int _lastAppliedFrame = -1;

    private void LateUpdate()
    {
        if (_source == null || _data == null || _data.frames == null || _data.frames.Length == 0 || _root == null) return;

        int frame = Mathf.Clamp(_source.CurrentFrame, 0, _data.frames.Length - 1);
        if (frame == _lastAppliedFrame) return; // evite de reecrire le Transform plusieurs fois pour la meme frame
        _lastAppliedFrame = frame;

        var f = _data.frames[frame];
        transform.position = _root.TransformPoint(f.localPosition);
        transform.rotation = _root.rotation * f.localRotation;
    }
}
