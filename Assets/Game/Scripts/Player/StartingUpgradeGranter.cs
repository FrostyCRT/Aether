using UnityEngine;

// MODIFIE - vide de tout contenu suite au changement de design : les armes
// exclusives (Fireball/Aura/Knives) ne s'equipent plus automatiquement au spawn.
// Elles passent desormais par le systeme de cartes a deblocage separe, comme
// Orbital/Lightning/MudPuddle - voir UpgradeData.cs et LevelUpManager.cs
// (garantie de premiere apparition au niveau 2). Composant laisse en place sur
// les 3 prefabs de personnage plutot que de te demander de le retirer partout :
// il ne fait simplement plus rien pour l'instant. Si tu changes d'avis plus tard,
// c'est ici qu'il faudrait remettre la logique d'equipement au spawn.
public class StartingUpgradeGranter : MonoBehaviour
{
}