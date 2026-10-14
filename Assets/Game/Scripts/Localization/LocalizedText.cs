using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// AJOUTÉ (2026-10-14) - traduit un texte posé à la main dans une scène ou un prefab (bouton, titre, onglet...).
//
// Le texte français d'origine est mémorisé (champ _french, rempli par l'outil Éditeur "Localisation > Poser les
// LocalizedText", ou à défaut lu au premier Awake). Le composant n'écrase JAMAIS un texte que le code a remplacé entre
// temps : il ne réécrit que si le texte affiché est encore le français d'origine ou sa dernière traduction posée par
// lui-même (ou une traduction de ce texte dans une autre langue). Les textes que le code réécrit en permanence passent,
// eux, par Loc.T() côté code.
// MODIFIÉ (2026-10-14) - TOUS les composants (actifs ou non) suivent les changements de langue : les MODÈLES inactifs
// que le code clone (lignes de la page Paramètres...) restaient sinon dans l'ancienne langue, et leurs clones aussi.
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [SerializeField, TextArea] private string _french;

    private TMP_Text _tmp;
    private string _lastApplied;

    private static readonly System.Collections.Generic.HashSet<LocalizedText> _all = new System.Collections.Generic.HashSet<LocalizedText>();

    public string French => _french;

    /// <summary>
    /// Pour un texte FIXE construit par le code (légende du HUD, libellé de bouton...) : à appeler juste après avoir
    /// écrit le texte français. Le texte est traduit tout de suite puis suit les changements de langue en direct.
    /// </summary>
    public static void Attach(TMP_Text text)
    {
        if (text == null) return;
        // Lu AVANT l'ajout : sur un objet actif, AddComponent déclenche Awake/OnEnable qui traduisent aussitôt.
        string french = text.text;
        LocalizedText lt = text.GetComponent<LocalizedText>();
        if (lt == null) lt = text.gameObject.AddComponent<LocalizedText>();
        _all.Add(lt);   // un objet inactif n'a pas encore reçu Awake
        lt._tmp = text;
        lt._french = french;
        lt._lastApplied = null;
        text.text = french;
        lt.Apply();
    }

    // Pose automatiquement le composant sur chaque texte de scène dont le contenu est un texte français connu de la
    // table (libellés fixes : boutons, titres, onglets...). Les scènes elles-mêmes ne sont jamais modifiées.
    // Les textes de remplissage ("New Text", "Valeur"...) ne sont pas des clés : ignorés, le code les réécrit.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookScenes()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Loc.Changed -= ApplyAll;
        Loc.Changed += ApplyAll;
    }

    private static void ApplyAll()
    {
        _all.RemoveWhere(lt => lt == null);
        foreach (LocalizedText lt in _all) lt.Apply();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) LocalizeTree(root);
    }

    /// <summary>Pose LocalizedText sur tous les textes FIXES (clés de la table) sous cet objet, actifs ou non.</summary>
    public static void LocalizeTree(GameObject root)
    {
        if (root == null) return;
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.GetComponent<LocalizedText>() != null || !Loc.Has(t.text)) continue;
            Attach(t);
        }
    }

    private void Awake()
    {
        _all.Add(this);
        _tmp = GetComponent<TMP_Text>();
        if (_tmp != null && string.IsNullOrEmpty(_french)) _french = _tmp.text;
    }

    private void OnEnable() => Apply();

    private void OnDestroy() => _all.Remove(this);

    public void Apply()
    {
        if (_tmp == null) _tmp = GetComponent<TMP_Text>();
        if (_tmp == null || string.IsNullOrEmpty(_french)) return;
        string current = _tmp.text;
        // Le code a écrit autre chose depuis : on ne touche à rien. Un clone (Instantiate) d'un modèle traduit porte
        // une traduction de _french : c'est toujours "notre" texte.
        if (current != _french && current != _lastApplied && !Loc.IsTranslationOf(_french, current)) return;
        string target = Loc.T(_french);
        _lastApplied = target;
        if (current != target) _tmp.text = target;
    }

#if UNITY_EDITOR
    // Utilisé par l'outil Éditeur qui pose ce composant sur les textes des scènes / prefabs.
    public void EditorSetFrench(string fr) => _french = fr;
#endif
}
