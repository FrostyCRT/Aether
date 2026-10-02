using UnityEngine;
using UnityEngine.EventSystems;

// AJOUTE (2026-09-27) - transmet les evenements de survol (entree/sortie souris) d'une tuile de carte vers
// PauseMenuUI, pour la fermeture en fondu du panneau de description quand la souris quitte la carte active (retour
// utilisateur). Ajoute dynamiquement par GameUI.ConfigureBuildGridSlot, uniquement quand un callback de clic est
// fourni (menu Pause) - jamais sur Victoire/Defaite.
public class UpgradeCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public UpgradeData Upgrade;
    public System.Action<UpgradeData> OnEnter;
    public System.Action<UpgradeData> OnExit;

    public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke(Upgrade);
    public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke(Upgrade);
}
