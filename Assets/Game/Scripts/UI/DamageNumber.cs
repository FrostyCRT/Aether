using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 2f;
    // MODIFIÉ (2026-10-04) - 1 -> 0,7. Deux raisons liées : (1) retour utilisateur "beaucoup d'ennemis... c'est un
    // peu n'importe quoi à l'écran", des nombres qui s'effacent plus vite s'accumulent moins à l'écran en plein
    // combat dense ; (2) performance mesurée - avec Aura+Orbital maxés sur 300 ennemis, jusqu'à ~90 DamageNumber
    // actifs SIMULTANÉMENT (chacun avec son propre TextMeshPro mis à jour chaque frame) faisaient chuter le
    // framerate à ~46 FPS contre ~124 FPS sans ennemis en surnombre - réduire la durée de vie réduit directement
    // le nombre d'instances actives en même temps pour un même rythme de dégâts, sans rien retirer à l'info.
    [SerializeField] private float _lifetime = 0.7f;

    // AJOUTÉ (2026-10-04) - Camera.main refait une recherche interne à chaque appel ; avec des dizaines
    // d'instances actives simultanément lisant Camera.main CHACUNE à chaque frame (pour le billboard face caméra),
    // ce coût se multiplie. Mis en cache une seule fois au niveau classe (la caméra principale ne change jamais
    // en cours de partie dans ce projet).
    private static Camera _cachedMainCamera;

    private TextMeshPro _text;
    private float _timer = 0f;
    private Color _color;
    private float _totalDamage;
    private bool _isCritical;
    public float ElapsedTime => _timer;
    public Transform Target { get; private set; }

    private void Awake()
    {
        _text = GetComponent<TextMeshPro>();

        // AJOUTE (2026-09-16) - contour sombre autour du texte (retour
        // utilisateur : les couleurs sont devenues plus vives depuis la
        // migration Unity 6, ça ne dérange pas mais rend les lettres moins
        // lisibles - un outline règle ça sans toucher aux couleurs). TMP crée
        // automatiquement une instance de matériau dédiée dès qu'on modifie
        // ces propriétés, pas besoin de gérer le matériau à la main.
        _text.outlineWidth = 0.2f;
        _text.outlineColor = new Color32(0x1A, 0x14, 0x0D, 0xFF);

        // AJOUTÉ (2026-10-28, retour utilisateur : "les ennemis grands comme gobelin ou golem, la moitié du nombre est
        // sous l'ennemi, donc ça doit passer en priorité le nombre") - le texte est dans le monde 3D et le corps des
        // grands ennemis le masquait par la profondeur. Test de profondeur désactivé sur le matériau de CETTE instance
        // (déjà dédiée, voir le contour ci-dessus) : le nombre est toujours dessiné par-dessus les ennemis.
        _text.fontMaterial.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
    }

    public void Init(float damage, Color color, Transform target, bool isCritical = false)
    {
        _timer = 0f;
        _color = color;
        _totalDamage = damage;
        _isCritical = isCritical;
        Target = target;

        UpdateText();
        Pop();

        // Légère variation aléatoire sur l'axe X et Z pour espacer les textes
        transform.position += new Vector3(
            Random.Range(-0.3f, 0.3f),
            0f,
            Random.Range(-0.3f, 0.3f)
        );
    }

    // NOUVEAU — appelé quand un nouveau coup de LA MÊME ARME (même couleur, voir DamageNumberSpawner - la fusion
    // se fait maintenant par (cible, couleur)) arrive pendant la fenêtre de fusion.
    // MODIFIÉ (2026-10-04) - ne touche plus _color : avant, un coup critique en cours de fusion repeignait le
    // nombre en orange même si le canal fusionné était d'une autre arme (ex: Orbital blanc qui devient orange) -
    // cause du bug "la couleur ne va pas avec l'arme en question" signalé par l'utilisateur. La couleur est
    // désormais fixée une fois pour toutes par la clé de fusion (DamageNumberSpawner), jamais réécrite ici.
    public void AddDamage(float damage, bool isCritical)
    {
        _totalDamage += damage;
        if (isCritical) _isCritical = true;

        _timer = 0f; // relance la durée de vie pour laisser le temps de lire le total
        UpdateText();
        Pop();
    }

    private void UpdateText()
    {
        _text.color = _color;
        _text.text = _isCritical ? $"{Mathf.CeilToInt(_totalDamage)}!" : $"{Mathf.CeilToInt(_totalDamage)}";
        _text.fontSize = (_isCritical ? 11.5f : 9f) * GameSettings.GetFloat(GameSettings.DamageSize);
        _text.fontStyle = FontStyles.Bold;
    }

    // AJOUTÉ (2026-10-04) - retour utilisateur : affichage "plus fluide... coloré, dynamique". Un petit rebond
    // d'échelle (dépasse puis revient, FXSprites.EaseOutBack) à chaque apparition ET à chaque ajout de dégâts
    // dans la fenêtre de fusion - le nombre "réagit" visiblement à chaque coup au lieu de rester un texte statique
    // qui grandit juste tout seul en silence.
    private const float PopDuration = 0.18f;
    private float _popTimer = -1f;

    private void Pop()
    {
        _popTimer = 0f;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        transform.position += Vector3.up * _moveSpeed * Time.deltaTime;

        if (_cachedMainCamera == null) _cachedMainCamera = Camera.main;
        if (_cachedMainCamera != null)
            transform.rotation = _cachedMainCamera.transform.rotation;

        float alpha = Mathf.Clamp01(1f - (_timer / _lifetime));
        _text.color = new Color(_color.r, _color.g, _color.b, alpha);

        if (_popTimer >= 0f)
        {
            _popTimer += Time.deltaTime;
            float p = Mathf.Clamp01(_popTimer / PopDuration);
            transform.localScale = Vector3.one * FXSprites.EaseOutBack(p, 1.8f);
            if (_popTimer >= PopDuration) _popTimer = -1f;
        }

        // CORRIGÉ (2026-10-07, retour utilisateur : "les FPS baissent tout au long de la partie peu importe s'il y
        // a des ennemis ou pas... à 10fps au boss 3 alors qu'il n'y a que lui") - ce SetActive(false) direct ne
        // repasse JAMAIS par ObjectPool.ReturnToPool, contrairement à XPGem/ProjectileBasic/etc. L'objet quitte
        // juste la file du pool pour toujours (ObjectPool.Get ne le récupère que via ReturnToPool) : une fois le
        // pool pré-chauffé (125, voir Game.unity) épuisé - en quelques secondes de combat réel - CHAQUE coup
        // suivant de TOUTE la partie déclenche un Instantiate() tout neuf (texte TMP, maillage, matériau) qui ne
        // sera plus jamais réutilisé. Ces instances orphelines s'accumulent indéfiniment avec le nombre total de
        // coups portés depuis le début de la run - ça grossit avec le TEMPS ÉCOULÉ, pas avec le nombre d'ennemis
        // actuels à l'écran, exactement le symptôme signalé (FPS qui ne remonte jamais, même avec peu d'ennemis).
        if (_timer >= _lifetime)
        {
            if (ObjectPool.Instance != null) ObjectPool.Instance.ReturnToPool("DamageNumber", gameObject);
            else gameObject.SetActive(false); // filet de sécurité si le pool a déjà été détruit (fin de scène)
        }
    }
}