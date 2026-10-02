using UnityEngine;
using System.Collections.Generic;

// Algorithme de chaîne de foudre extrait de WeaponLightningChain (2026-09-27), pour être réutilisé tel quel par les
// fusions qui ajoutent la foudre à une autre arme (Orbes Foudroyants, Marécage Maudit) sans dupliquer la logique ni
// toucher à WeaponLightningChain lui-même. Statique et sans état : chaque appelant fournit son propre MonoBehaviour
// hôte (pour StartCoroutine) et ses propres réglages.
public static class LightningChainUtility
{
    private static readonly Collider[] _buffer = new Collider[64];

    // Déclenche une chaîne à partir de "firstTarget" déjà touché (il encaisse aussi les dégâts du 1er palier, comme
    // WeaponLightningChain). baseDamage = dégâts du 1er impact ; chaque saut suivant fait x0,7 le précédent.
    public static void Trigger(MonoBehaviour host, Transform firstTarget, float baseDamage, int maxChains, float chainRange)
    {
        if (host == null || !host.isActiveAndEnabled || firstTarget == null) return;
        host.StartCoroutine(ChainRoutine(host.transform.position, firstTarget, baseDamage, maxChains, chainRange));
    }

    // AJOUTE (2026-09-27, retour utilisateur) - même teinte que WeaponLightningChain (voir son commentaire) :
    // la foudre doit se reconnaître d'un coup d'œil peu importe l'arme/fusion qui la déclenche.
    private static readonly Color _lightningVfxColor = new Color(1f, 0.95f, 0.55f);

    private static System.Collections.IEnumerator ChainRoutine(Vector3 origin, Transform firstTarget, float baseDamage, int maxChains, float chainRange)
    {
        List<GameObject> hit = new List<GameObject>();
        Transform current = firstTarget;
        float buffedBase = baseDamage * PlayerBuffs.OutgoingDamageMultiplier;
        for (int i = 0; i <= maxChains; i++)
        {
            if (current == null) break;
            float damage = buffedBase * Mathf.Pow(0.7f, i);
            EnemyBase eb = current.GetComponent<EnemyBase>();
            if (eb != null) eb.TakeDamage(damage, DamageNumberSpawner.ColorCritical);
            BossBase boss = current.GetComponent<BossBase>();
            if (boss != null) boss.TakeDamage(damage, DamageNumberSpawner.ColorCritical);
            // MODIFIE (2026-09-27, retour utilisateur : "un effet... qu'on voit quand la foudre frappe, et
            // qu'on voit le ricochet entre ennemis") - remplace le Debug.DrawLine (invisible hors Scene view de
            // l'éditeur) par un vrai éclair visible en jeu (LightningBoltVFX) + un flash au point d'impact.
            LightningBoltVFX.SpawnBolt(hit.Count == 0 ? origin : hit[hit.Count - 1].transform.position, current.position, _lightningVfxColor);
            LightningBoltVFX.SpawnStrikeFlash(current.position, _lightningVfxColor);
            hit.Add(current.gameObject);

            current = FindNextTarget(current.position, chainRange, hit);
            yield return new WaitForSeconds(0.05f);
        }
    }

    private static Transform FindNextTarget(Vector3 from, float chainRange, List<GameObject> alreadyHit)
    {
        int count = Physics.OverlapSphereNonAlloc(from, chainRange, _buffer);
        Transform nearest = null;
        float minDistSqr = chainRange * chainRange;
        for (int i = 0; i < count; i++)
        {
            Collider col = _buffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            if (alreadyHit.Contains(col.gameObject)) continue;
            float distSqr = (col.transform.position - from).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                nearest = col.transform;
            }
        }
        return nearest;
    }
}
