using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Remplace le squelette d'un SkinnedMeshRenderer (ex: modèle Tripo3D) par un autre
/// squelette (ex: rig Mixamo), en conservant le mesh ET sa texture d'origine.
///
/// Fonctionnement : on associe chaque os de l'ancien squelette à son équivalent dans le
/// nouveau squelette par nom, puis on recalcule les bindposes pour ce nouveau squelette.
/// Les boneWeights du mesh (quel vertex suit quel os, à quel poids) ne changent JAMAIS —
/// c'est justement pour ça qu'il faut construire le nouveau tableau d'os EXACTEMENT dans
/// le même ordre que l'ancien (les poids référencent des index, pas des noms).
///
/// À placer dans un dossier "Editor" (ex: Assets/Editor/SkeletonSwapTool.cs).
/// </summary>
public class SkeletonSwapTool : EditorWindow
{
    private SkinnedMeshRenderer _sourceRenderer; // mesh + texture à conserver (Tripo)
    private Transform _newRootBone;              // racine du nouveau squelette (Hips Mixamo)

    // Correspondance manuelle nomenclature Tripo -> nomenclature Mixamo standard.
    // Clés/valeurs déjà en minuscules (comparées après passage dans CleanName).
    // Les os de torsion (*Twist01/02), absents du rig Mixamo, pointent vers l'os principal
    // le plus proche : pratique standard quand le squelette cible est plus simple que la source.
    private static readonly Dictionary<string, string> BoneAliases = new Dictionary<string, string>
    {
        { "root", "hips" },
        { "hip", "hips" },
        { "pelvis", "hips" },
        { "l_thigh", "leftupleg" },
        { "l_thightwist01", "leftupleg" },
        { "l_thightwist02", "leftupleg" },
        { "l_calf", "leftleg" },
        { "l_calftwist01", "leftleg" },
        { "l_calftwist02", "leftleg" },
        { "l_foot", "leftfoot" },
        { "l_toebase", "lefttoebase" },
        { "r_thigh", "rightupleg" },
        { "r_thightwist01", "rightupleg" },
        { "r_thightwist02", "rightupleg" },
        { "r_calf", "rightleg" },
        { "r_calftwist01", "rightleg" },
        { "r_calftwist02", "rightleg" },
        { "r_foot", "rightfoot" },
        { "r_toebase", "righttoebase" },
        { "waist", "spine" },
        { "spine01", "spine1" },
        { "spine02", "spine2" },
        { "necktwist01", "neck" },
        { "necktwist02", "neck" },
        { "l_clavicle", "leftshoulder" },
        { "l_upperarm", "leftarm" },
        { "l_upperarmtwist01", "leftarm" },
        { "l_upperarmtwist02", "leftarm" },
        { "l_forearm", "leftforearm" },
        { "l_forearmtwist01", "leftforearm" },
        { "l_forearmtwist02", "leftforearm" },
        { "l_hand", "lefthand" },
        { "r_clavicle", "rightshoulder" },
        { "r_upperarm", "rightarm" },
        { "r_upperarmtwist01", "rightarm" },
        { "r_upperarmtwist02", "rightarm" },
        { "r_forearm", "rightforearm" },
        { "r_forearmtwist01", "rightforearm" },
        { "r_forearmtwist02", "rightforearm" },
        { "r_hand", "righthand" },
    };

    // Cherche d'abord une correspondance exacte par nom, puis se rabat sur le dictionnaire d'alias.
    private bool TryFindMatch(string oldBoneName, Dictionary<string, Transform> newMap, out Transform match)
    {
        string clean = CleanName(oldBoneName);

        if (newMap.TryGetValue(clean, out match))
            return true;

        if (BoneAliases.TryGetValue(clean, out string alias) && newMap.TryGetValue(alias, out match))
            return true;

        match = null;
        return false;
    }

    [MenuItem("Tools/AETHER/Swap Skeleton")]
    public static void ShowWindow()
    {
        GetWindow<SkeletonSwapTool>("Swap Skeleton");
    }

    private void OnGUI()
    {
        GUILayout.Label(
            "Remplace le squelette d'un SkinnedMeshRenderer par un autre,\n" +
            "en conservant le mesh et sa texture d'origine.",
            EditorStyles.wordWrappedLabel);

        EditorGUILayout.Space();

        _sourceRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
            "Mesh source (Tripo)", _sourceRenderer, typeof(SkinnedMeshRenderer), true);

        _newRootBone = (Transform)EditorGUILayout.ObjectField(
            "Racine du nouveau squelette (Hips Mixamo)", _newRootBone, typeof(Transform), true);

        EditorGUILayout.Space();

        if (GUILayout.Button("1. Analyser la correspondance des os"))
        {
            AnalyzeMapping();
        }

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Avant d'appliquer : vérifie dans la Console qu'un maximum d'os matchent. " +
            "S'il en manque, dis-moi les noms exacts des deux côtés, je rajoute un mapping manuel.",
            MessageType.Info);

        if (GUILayout.Button("2. Appliquer le swap"))
        {
            ApplySwap();
        }
    }

    private Dictionary<string, Transform> BuildNameMap(Transform root)
    {
        var map = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string clean = CleanName(t.name);
            if (!map.ContainsKey(clean))
                map[clean] = t;
        }
        return map;
    }

    // Retire les préfixes courants (ex: "mixamorig:") et normalise la casse pour comparer les noms
    private string CleanName(string raw)
    {
        string n = raw.Replace("mixamorig:", "").Replace("mixamorig_", "");
        return n.Trim().ToLowerInvariant();
    }

    private void AnalyzeMapping()
    {
        if (_sourceRenderer == null || _newRootBone == null)
        {
            Debug.LogWarning("[SkeletonSwapTool] Assigne le Mesh source et la racine du nouveau squelette avant d'analyser.");
            return;
        }

        Transform[] oldBones = _sourceRenderer.bones;
        var newMap = BuildNameMap(_newRootBone);

        int matched = 0;
        List<string> unmatched = new List<string>();

        foreach (var b in oldBones)
        {
            if (b == null) continue;
            if (TryFindMatch(b.name, newMap, out _)) matched++;
            else unmatched.Add(b.name);
        }

        Debug.Log($"[SkeletonSwapTool] {matched}/{oldBones.Length} os correspondent par nom.");
        if (unmatched.Count > 0)
        {
            Debug.LogWarning("[SkeletonSwapTool] Os SANS correspondance trouvée (à mapper à la main si besoin) :\n"
                + string.Join(", ", unmatched));
        }
        else
        {
            Debug.Log("[SkeletonSwapTool] Tous les os matchent, tu peux appliquer le swap en sécurité.");
        }
    }

    private void ApplySwap()
    {
        if (_sourceRenderer == null || _newRootBone == null)
        {
            Debug.LogWarning("[SkeletonSwapTool] Assigne le Mesh source et la racine du nouveau squelette avant d'appliquer.");
            return;
        }

        Mesh originalMesh = _sourceRenderer.sharedMesh;
        if (originalMesh == null)
        {
            Debug.LogError("[SkeletonSwapTool] Le SkinnedMeshRenderer source n'a pas de mesh assigné.");
            return;
        }

        Transform[] oldBones = _sourceRenderer.bones;
        var newMap = BuildNameMap(_newRootBone);

        Transform[] newBones = new Transform[oldBones.Length];
        Matrix4x4[] newBindPoses = new Matrix4x4[oldBones.Length];

        for (int i = 0; i < oldBones.Length; i++)
        {
            if (oldBones[i] == null)
            {
                Debug.LogError($"[SkeletonSwapTool] Os manquant à l'index {i} sur le renderer source, abandon.");
                return;
            }

            if (!TryFindMatch(oldBones[i].name, newMap, out Transform matchedBone))
            {
                Debug.LogError($"[SkeletonSwapTool] Aucune correspondance trouvée pour l'os '{oldBones[i].name}'. "
                    + "Lance d'abord '1. Analyser la correspondance des os' et corrige les noms qui ne matchent pas.");
                return;
            }

            newBones[i] = matchedBone;
            // Formule standard de bindpose Unity : convertit l'espace du renderer vers l'espace local de l'os,
            // calculée à partir de la position ACTUELLE du nouvel os (peu importe qu'elle diffère spatialement
            // de l'ancien squelette : cette formule "annule" la pose actuelle pour reconstruire le mesh au repos).
            newBindPoses[i] = matchedBone.worldToLocalMatrix * _sourceRenderer.transform.localToWorldMatrix;
        }

        // On duplique le mesh pour ne jamais modifier l'asset FBX importé (lecture seule) directement.
        Mesh newMesh = Instantiate(originalMesh);
        newMesh.bindposes = newBindPoses;
        // newMesh.boneWeights (implicites, hérités de originalMesh) restent identiques : ce sont des index
        // dans le tableau bones, et newBones a été construit dans EXACTEMENT le même ordre que oldBones.

        // --- Diagnostic : détecte les bindposes dégénérées (souvent un souci d'échelle 0 sur un Transform) ---
        int degenerateCount = 0;
        for (int i = 0; i < newBindPoses.Length; i++)
        {
            if (Mathf.Approximately(newBindPoses[i].determinant, 0f))
            {
                degenerateCount++;
                Debug.LogWarning($"[SkeletonSwapTool] Bindpose dégénérée (déterminant ~0) sur l'os cible '{newBones[i].name}' "
                    + $"(mappé depuis '{oldBones[i].name}'). Vérifie l'échelle de ce Transform dans la scène.");
            }
        }
        if (degenerateCount > 0)
            Debug.LogError($"[SkeletonSwapTool] {degenerateCount} bindpose(s) dégénérée(s) détectée(s) — "
                + "c'est très probablement la cause d'une disparition du mesh. Corrige l'échelle des os concernés avant de continuer.");

        string dir = "Assets/AETHER_GeneratedMeshes/";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string assetPath = dir + _sourceRenderer.gameObject.name + "_SwappedSkeleton.asset";
        AssetDatabase.CreateAsset(newMesh, assetPath);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(_sourceRenderer, "Swap Skeleton");
        _sourceRenderer.sharedMesh = newMesh;
        _sourceRenderer.bones = newBones;
        _sourceRenderer.rootBone = _newRootBone;

        EditorUtility.SetDirty(_sourceRenderer);
        Debug.Log($"[SkeletonSwapTool] Swap terminé. Nouveau mesh sauvegardé : {assetPath}. "
            + "Le matériau/texture du SkinnedMeshRenderer n'a pas été touché.");
        Debug.Log($"[SkeletonSwapTool] Diagnostic — vertices: {newMesh.vertexCount}, "
            + $"bindposes: {newMesh.bindposes.Length}, bones assignés: {_sourceRenderer.bones.Length}, "
            + $"bounds locaux du mesh: {newMesh.bounds}");
    }
}
