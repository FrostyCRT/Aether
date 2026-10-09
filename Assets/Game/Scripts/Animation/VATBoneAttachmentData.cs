using UnityEngine;

// AJOUTÉ (2026-10-05) - correctif pour les objets tenus en main (sarbacane du Goblin...) : le corps entier est
// animé par texture (VATLit.shader), mais un PROP attaché à un os précis (main, dos...) ne peut pas suivre cette
// texture - il lui faut sa PROPRE petite courbe position/rotation, cuite une seule fois comme le reste. Beaucoup
// plus léger qu'un Animator complet (un seul os suivi, pas tout le squelette) - voir VATBoneAttachment.cs.
[CreateAssetMenu(fileName = "VATBoneAttachmentData", menuName = "Aether/VAT Bone Attachment Data")]
public class VATBoneAttachmentData : ScriptableObject
{
    [System.Serializable]
    public struct BoneFrame
    {
        public Vector3 localPosition; // relatif a la racine de l'ennemi (pas a un os parent - stable, jamais retaré)
        public Quaternion localRotation;
    }

    public BoneFrame[] frames; // meme indexation (meme longueur totale) que VATClipSet.clips sur le meme ennemi
}
