using UnityEngine;

// MODIFIÉ (2026-10-19, diagnostic complet - refonte de la forme de carte par palier de difficulté) - ZoneHalfSize
// (un seul scalaire, carte carrée symétrique) ne peut plus décrire les 3 formes voulues : Modéré = infini sur les
// 2 axes, Violent = tunnel (mur sur Z seulement, X infini), Apocalyptique = boîte fermée sur les 4 côtés. Remplacé
// par 2 paires de bornes indépendantes (X et Z), chacune pouvant valoir ±Infinity pour un axe sans mur. Mathf.Clamp
// gère nativement les bornes infinies (aucune comparaison ne peut dépasser ±Infinity), donc ClampToZone n'a besoin
// d'aucun cas particulier. Initialisée une fois par GameManager.Awake() via ResetForNewRun(), avant que quoi que ce
// soit (spawner, boss, décor) n'en ait besoin.
public static class MapBoundaryUtils
{
    public static float MinX { get; private set; } = float.NegativeInfinity;
    public static float MaxX { get; private set; } = float.PositiveInfinity;
    public static float MinZ { get; private set; } = float.NegativeInfinity;
    public static float MaxZ { get; private set; } = float.PositiveInfinity;

    public static bool HasBoundX => !float.IsInfinity(MinX) || !float.IsInfinity(MaxX);
    public static bool HasBoundZ => !float.IsInfinity(MinZ) || !float.IsInfinity(MaxZ);

    // MODIFIÉ (2026-10-20, carrousel de difficulté - "les boss [...] dans les autres difficultés bénéficient du
    // scale [...] logique qu'il y ait un niveau de difficulté pour ça aussi") - le palier de difficulté (et donc sa
    // forme/taille de carte) s'applique maintenant aussi en Sans fin et Ruée de boss, pas seulement le Classique :
    // les 3 modes partagent désormais le même carrousel de difficulté (page Accueil), il serait incohérent que la
    // carte elle-même n'en tienne pas compte. Choc des titans garde son ancienne carte carrée fixe (pas de palier,
    // jamais proposé dans le carrousel - 3 boss positionnés par GameModes.TitansSpawnOffset sur une arène dont la
    // taille ne doit pas changer).
    public static void ResetForNewRun()
    {
        if (GameModes.IsTitans)
        {
            SetBounds(-55f, 55f, -55f, 55f);
            return;
        }

        DifficultyTiers.GetBounds(DifficultyTiers.Current, out float minX, out float maxX, out float minZ, out float maxZ);
        SetBounds(minX, maxX, minZ, maxZ);
    }

    private static void SetBounds(float minX, float maxX, float minZ, float maxZ)
    {
        MinX = minX;
        MaxX = maxX;
        MinZ = minZ;
        MaxZ = maxZ;
    }

    public static Vector3 ClampToZone(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, MinX, MaxX);
        position.z = Mathf.Clamp(position.z, MinZ, MaxZ);
        return position;
    }
}
