using UnityEngine;

// AJOUTÉ (2026-10-16, audit version finale) - masque de couche partagé par toutes les recherches d'ennemis des
// armes (Physics.OverlapSphereNonAlloc). Sans masque, les tampons de taille fixe (50/64) se remplissaient aussi avec
// les gemmes d'XP, les projectiles, le sol et le décor : avec beaucoup de gemmes au sol, des ennemis bien présents
// dans la zone n'étaient plus vus (Aura, Boue, Foudre, ciblage...). Ennemis ET boss sont sur la couche "Enemy"
// (vérifié sur tous les prefabs) - c'est déjà ce que fait le tir de base (WeaponBase._enemyLayer).
public static class EnemyLayers
{
    private static int _mask = -1;

    public static int Mask
    {
        get
        {
            if (_mask == -1)
            {
                _mask = LayerMask.GetMask("Enemy");
                if (_mask == 0) _mask = Physics.DefaultRaycastLayers; // couche absente : comportement d'avant
            }
            return _mask;
        }
    }
}
