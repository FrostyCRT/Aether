using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-10-04) - optimisation performance : retour utilisateur "toujours ~20 FPS avec beaucoup d'ennemis,
// alors que j'ai un bon PC... que font les meilleurs jeux du genre pour n'avoir aucun problème de latence ?".
//
// Cause trouvée en lisant le code (pas supposée) : EnemyBase.GetBaseSeparation() ET sa copie quasi-identique
// EnemyShooter.GetSeparationForce() (force de répulsion pour ne pas que les ennemis se chevauchent visuellement)
// appelaient CHACUNE Physics.OverlapSphereNonAlloc() - un vrai appel au moteur physique (broadphase + narrowphase)
// - À CHAQUE FRAME, POUR CHAQUE ENNEMI. Avec 300 ennemis, ça fait jusqu'à 300 requêtes physiques séparées par
// frame, rien que pour cette répulsion cosmétique, AVANT même de compter l'IA, les dégâts ou le rendu - cause la
// plus probable du ralentissement observé (confirmé par benchmark : même SANS aucune arme équipée, 300 ennemis
// faisaient déjà chuter le framerate significativement par rapport à 150, alors qu'aucune autre logique ne tourne
// à cette fréquence pour chaque ennemi).
//
// Solution standard dans les jeux à forte densité d'unités (ce que font les horde-survivors qui encaissent des
// centaines d'ennemis sans ralentir) : une grille spatiale (spatial hash) reconstruite UNE SEULE FOIS par frame
// (coût total O(n), pas O(n) requêtes séparées), interrogée ensuite en O(1) amorti par chaque ennemi - aucun appel
// au moteur physique nécessaire pour un simple "qui est près de moi".
//
// Reconstruction paresseuse (premier appel de la frame) plutôt qu'un MonoBehaviour dédié avec un ordre d'exécution
// précis à garantir : plus simple, et correct quel que soit l'ordre d'Update() entre ennemis.
public static class EnemySpatialGrid
{
    private const float CellSize = 2.5f;

    private static readonly Dictionary<(int, int), List<EnemyBase>> _cells = new Dictionary<(int, int), List<EnemyBase>>(256);
    private static readonly List<EnemyBase> _registered = new List<EnemyBase>(512);
    private static int _builtFrame = -1;

    public static void Register(EnemyBase enemy)
    {
        if (!_registered.Contains(enemy)) _registered.Add(enemy);
    }

    public static void Unregister(EnemyBase enemy)
    {
        _registered.Remove(enemy);
    }

    private static (int, int) CellKey(Vector3 pos) =>
        (Mathf.FloorToInt(pos.x / CellSize), Mathf.FloorToInt(pos.z / CellSize));

    private static void BuildIfNeeded()
    {
        if (_builtFrame == Time.frameCount) return;
        _builtFrame = Time.frameCount;

        // Vide les listes existantes sans les réallouer (pas de pression GC frame après frame) - seules de
        // NOUVELLES cellules (rare une fois la horde stabilisée) coûtent une allocation.
        foreach (List<EnemyBase> list in _cells.Values) list.Clear();

        for (int i = 0; i < _registered.Count; i++)
        {
            EnemyBase e = _registered[i];
            if (e == null || !e.isActiveAndEnabled) continue;

            var key = CellKey(e.transform.position);
            if (!_cells.TryGetValue(key, out List<EnemyBase> list))
            {
                list = new List<EnemyBase>(8);
                _cells[key] = list;
            }
            list.Add(e);
        }
    }

    // Remplit 'results' avec les ennemis à moins de 'radius' de 'position' (sauf 'self'), jamais d'allocation ici
    // (buffer fourni par l'appelant, même convention que Physics.OverlapSphereNonAlloc qu'elle remplace).
    public static int QueryNeighbors(Vector3 position, float radius, EnemyBase self, EnemyBase[] results)
    {
        BuildIfNeeded();

        int count = 0;
        float sqrRadius = radius * radius;
        int reach = Mathf.CeilToInt(radius / CellSize);
        int cx = Mathf.FloorToInt(position.x / CellSize);
        int cz = Mathf.FloorToInt(position.z / CellSize);

        for (int dx = -reach; dx <= reach && count < results.Length; dx++)
        {
            for (int dz = -reach; dz <= reach && count < results.Length; dz++)
            {
                if (!_cells.TryGetValue((cx + dx, cz + dz), out List<EnemyBase> list)) continue;

                for (int i = 0; i < list.Count && count < results.Length; i++)
                {
                    EnemyBase e = list[i];
                    if (e == null || e == self) continue;

                    Vector3 d = e.transform.position - position;
                    d.y = 0f;
                    if (d.sqrMagnitude <= sqrRadius) results[count++] = e;
                }
            }
        }
        return count;
    }
}
