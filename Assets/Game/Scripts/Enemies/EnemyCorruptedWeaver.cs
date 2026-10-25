using UnityEngine;
using System.Collections;

/// <summary>
/// Tisseuse Corrompue — ennemi additionnel palier 8 min.
/// Comportement par défaut (marche vers le joueur, dégâts de contact) hérité de EnemyBase.
/// Ajout : un saut vers une position FIGÉE au moment du déclenchement (pas de poursuite en
/// vol, pas de télégraphe visuel — la distance elle-même est le signal, une fois le pattern
/// appris) quand le joueur est à portée, qui applique un ralentissement à l'atterrissage
/// (réutilise PlayerController.SetSpeedMultiplier(), déjà utilisé côté Boss).
/// </summary>
public class EnemyCorruptedWeaver : EnemyBase
{
    [Header("Saut d'attaque")]
    [SerializeField] private float _jumpTriggerRange = 5f;
    [SerializeField] private float _jumpAnticipationDuration = 0.15f;
    [SerializeField] private float _jumpDuration = 0.4f;
    [SerializeField] private float _jumpCooldown = 3f;
    [SerializeField] private float _jumpImpactRadius = 1.5f;
    [SerializeField] private float _jumpDamage = 10f;
    [SerializeField] private float _jumpArcHeight = 1.5f;
    [SerializeField] private float _jumpSlowMultiplier = 0.5f;
    [SerializeField] private float _jumpSlowDuration = 1.5f;
    [SerializeField] private Animator _weaverAnimator;

    private bool _isJumping = false;
    private float _jumpCooldownTimer = 0f;

    protected override void OnEnable()
    {
        base.OnEnable();

        _isJumping = false;
        _jumpCooldownTimer = 0f;

        // AJOUTE - securite au cas ou l'objet reviendrait du pool avec IsJumping
        // encore a true depuis sa vie precedente (ex: desactive en plein saut).
        if (_weaverAnimator != null)
            _weaverAnimator.SetBool("IsJumping", false);
    }

    protected override void UpdateBehaviour(Transform target)
    {
        if (_isJumping) return;
        base.UpdateBehaviour(target);
    }

    protected override void OnEnemyUpdate()
    {
        if (_isJumping) return;

        if (_jumpCooldownTimer > 0f)
            _jumpCooldownTimer -= Time.deltaTime;

        // CORRIGÉ (2026-10-11, retour utilisateur : "les araignées sautent sur le Lyra même si elle est invisible
        // en lâchant son clone") - visait auparavant une référence joueur mise en cache à part (_weaverPlayerTransform),
        // jamais redirigée vers le Clone Fantôme contrairement au reste du ciblage (voir EnemyBase.CurrentTarget) :
        // l'araignée sautait donc toujours sur la vraie position de Lyra, invisible ou pas. CurrentTarget pointe
        // maintenant vers le clone tant qu'il existe, comme tous les autres ennemis.
        if (_jumpCooldownTimer <= 0f && CurrentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, CurrentTarget.position);
            if (dist <= _jumpTriggerRange && dist > _jumpImpactRadius)
            {
                StartCoroutine(JumpAttack());
            }
        }
    }

    private IEnumerator JumpAttack()
    {
        _isJumping = true;
        _jumpCooldownTimer = _jumpCooldown;

        // CORRIGÉ (2026-10-21, retour utilisateur : "les araignées ne prennent pas les dégâts des orbitaux [...]
        // regarde si c'est pas la même raison que pour les bulbes") - même famille de bug que le Bulb (hauteur Y
        // héritée du mauvais pivot), mais ici le mécanisme est différent : `targetPos` venait de
        // `CurrentTarget.position` SANS retirer sa composante Y, qui est la hauteur de TORSE du joueur (~1,5, voir
        // [[player-pivot-is-torso-height-not-ground]]), pas le sol. `transform.position = targetPos;` plus bas
        // plantait donc l'araignée à Y≈1,5 de façon PERMANENTE après son premier saut (pas seulement pendant l'arc
        // du saut) - hors de portée de tous les effets au sol (Orbitaux, Aura, Boue, Nova), pas seulement les
        // Orbitaux. Hauteur Y forcée à celle du sol (sa propre position Y d'avant le saut, jamais celle du joueur).
        float groundY = transform.position.y;
        Vector3 targetPos = CurrentTarget != null ? CurrentTarget.position : transform.position;
        targetPos.y = groundY;

        yield return new WaitForSeconds(_jumpAnticipationDuration);

        if (_weaverAnimator != null)
        {
            // AJOUTE - IsJumping passe a true EN MEME TEMPS que le Trigger Jump.
            // C'est cette valeur que la transition de retour vers Locomotion dans
            // l'Animator Controller doit utiliser comme condition (IsJumping ==
            // false), plutot que de compter uniquement sur un Exit Time qui peut se
            // desynchroniser de la duree reelle du saut.
            _weaverAnimator.SetBool("IsJumping", true);
            _weaverAnimator.SetTrigger("Jump");
        }

        Vector3 startPos = transform.position;
        float jumpElapsed = 0f;
        while (jumpElapsed < _jumpDuration)
        {
            jumpElapsed += Time.deltaTime;
            float progress = jumpElapsed / _jumpDuration;
            Vector3 flatPos = Vector3.Lerp(startPos, targetPos, progress);
            float arc = Mathf.Sin(progress * Mathf.PI) * _jumpArcHeight;
            transform.position = flatPos + Vector3.up * arc;
            yield return null;
        }

        transform.position = targetPos;

        bool hitPlayer = false;

        Collider[] hits = Physics.OverlapSphere(transform.position, _jumpImpactRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                hitPlayer = true;

                hit.TryGetComponent(out HealthSystem health);
                if (health != null) health.TakeDamage(_jumpDamage);

                hit.TryGetComponent(out PlayerController player);
                if (player != null) player.ApplyTemporarySlow(_jumpSlowMultiplier, _jumpSlowDuration);
            }
        }
        if (_weaverAnimator != null && hitPlayer)
            _weaverAnimator.SetTrigger("Attack");

        // AJOUTE - repasse IsJumping a false a la toute fin, une fois l'atterrissage
        // et l'eventuel Attack geres - c'est le signal que la transition de retour
        // vers Locomotion attend dans l'Animator Controller.
        if (_weaverAnimator != null)
            _weaverAnimator.SetBool("IsJumping", false);

        _isJumping = false;
    }

}