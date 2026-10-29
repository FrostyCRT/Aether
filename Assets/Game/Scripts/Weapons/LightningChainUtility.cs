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
    // MODIFIÉ (2026-10-04) puis RE-MODIFIÉ (2026-10-04, suite) - redevient jaune/or (voir
    // WeaponLightningChain._lightningVfxColor pour le détail : retour utilisateur "pas assez de couleur chaude",
    // l'icône "Foudre" va être régénérée en jaune pour correspondre plutôt que l'inverse).
    private static readonly Color _lightningVfxColor = new Color(1f, 0.95f, 0.55f);

    // CORRIGÉ (2026-10-28, retour utilisateur : "la chaîne de foudre attaque trop tôt un ennemi alors qu'il n'a pas spawn,
    // et ça met quand même les dégâts") - entre deux sauts (0,05 s), la cible suivante pouvait mourir (autre arme) et son
    // objet être aussitôt recyclé par le pool en un NOUVEL ennemi apparu ailleurs (les Bulbes apparaissent tout près du
    // joueur) : la chaîne le frappait dès sa première image, à l'endroit où il venait d'apparaître. Chaque cible est
    // désormais retenue avec son numéro de vie (EnemyBase.Life) et la chaîne s'arrête si ce n'est plus le même ennemi
    // vivant. Les éclairs partent de la POSITION du coup précédent, plus de l'objet (qui pouvait avoir été déplacé).
    private static System.Collections.IEnumerator ChainRoutine(Vector3 origin, Transform firstTarget, float baseDamage, int maxChains, float chainRange)
    {
        List<GameObject> hit = new List<GameObject>();
        Transform current = firstTarget;
        int life = LifeOf(current);
        Vector3 from = origin;
        float buffedBase = baseDamage * PlayerBuffs.OutgoingDamageMultiplier;
        for (int i = 0; i <= maxChains; i++)
        {
            if (!SameLivingTarget(current, life)) break;
            float damage = buffedBase * Mathf.Pow(0.7f, i);
            Vector3 at = current.position;
            current.TryGetComponent(out EnemyBase eb);
            if (eb != null) eb.TakeDamage(damage, DamageNumberSpawner.ColorLightning);
            current.TryGetComponent(out BossBase boss);
            if (boss != null) boss.TakeDamage(damage, DamageNumberSpawner.ColorLightning);
            // MODIFIE (2026-09-27, retour utilisateur : "un effet... qu'on voit quand la foudre frappe, et
            // qu'on voit le ricochet entre ennemis") - remplace le Debug.DrawLine (invisible hors Scene view de
            // l'éditeur) par un vrai éclair visible en jeu (LightningBoltVFX) + un flash au point d'impact.
            LightningBoltVFX.SpawnBolt(from, at, _lightningVfxColor);
            LightningBoltVFX.SpawnStrikeFlash(at, _lightningVfxColor);
            hit.Add(current.gameObject);
            from = at;

            current = FindNextTarget(at, chainRange, hit);
            life = LifeOf(current);
            yield return new WaitForSeconds(0.05f);
        }
    }

    private static int LifeOf(Transform t) => t != null && t.TryGetComponent(out EnemyBase eb) ? eb.Life : 0;

    // Toujours le même ennemi, actif et vivant (un boss n'est jamais recyclé : actif suffit).
    private static bool SameLivingTarget(Transform t, int life)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        return !t.TryGetComponent(out EnemyBase eb) || (!eb.IsDead && eb.Life == life);
    }

    private static Transform FindNextTarget(Vector3 from, float chainRange, List<GameObject> alreadyHit)
    {
        int count = Physics.OverlapSphereNonAlloc(from, chainRange, _buffer, EnemyLayers.Mask);
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
