using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// AJOUTÉ (2026-10-05) - outil de cuisson pour "LA solution" perf (texture d'animation GPU / Vertex Animation
// Texture), voir NOTES.md. Précuit les animations d'un ennemi (SkinnedMeshRenderer + Animator) dans 2 textures
// (position + normale, une colonne par sommet, une ligne par frame), puis construit un mesh statique + matériau
// prêts à être rendus par VATLit.shader SANS Animator ni squelette en jeu - tout le travail de skinning se fait
// UNE SEULE FOIS ici, jamais à l'exécution.
//
// Pilote choisi : le Loup (2006 sommets, 48 os, clips Walk/Bite) - celui déjà utilisé pour le benchmark qui a
// prouvé le coût de l'Animator (~22% du temps de frame à 300 ennemis). Un seul type d'ennemi converti et validé
// (visuellement + en performance) avant d'étendre aux autres, pour ne pas risquer tout casser d'un coup.
public static class VATBaker
{
    public struct ClipBakeRange
    {
        public string clipName;
        public int startFrame;
        public int frameCount;
        public float length;
    }

    public class BakeResult
    {
        public Texture2D positionTexture;
        public Texture2D normalTexture;
        public Mesh staticMesh;
        public Material material;
        public List<ClipBakeRange> clipRanges;
        public VATClipSet clipSet;
        public int totalFrames;
        public int vertexCount;
        public VATBoneAttachmentData attachmentData; // null si attachmentBoneName n'a pas ete fourni/trouve
    }

    // CORRIGÉ (2026-10-05, extension aux autres ennemis) - certains prefabs (ex: Goblin) ont DEUX clips qui
    // portent EXACTEMENT le même nom brut ("mixamo.com", importé tel quel depuis Mixamo sans renommage) -
    // chercher par nom de CLIP est ambigu (seul le 1er match serait jamais trouvé). Résolu en cherchant par nom
    // d'ÉTAT de l'AnimatorController à la place ("Walk", "Shot"...) - toujours unique et porteur de sens, quel
    // que soit le nom du clip brut sous-jacent. Fonctionne pour tous les ennemis (Walk/Bite du Loup compris).
    public static BakeResult Bake(GameObject sourcePrefab, string[] stateNames, int sampleRate, string outputFolder, string assetNamePrefix, string attachmentBoneName = null, string attachmentPropName = null)
    {
        EnsureFolderExists(outputFolder);

        GameObject instance = Object.Instantiate(sourcePrefab);
        instance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            var animator = instance.GetComponentInChildren<Animator>();
            var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (animator == null || smr == null)
                throw new System.Exception("Le prefab source n'a pas d'Animator ou de SkinnedMeshRenderer.");

            AnimatorController ac = animator.runtimeAnimatorController as AnimatorController;
            if (ac == null)
                throw new System.Exception("runtimeAnimatorController n'est pas un AnimatorController direct (override controller non supporté).");
            var statesByName = ac.layers[0].stateMachine.states
                .ToDictionary(s => s.state.name, s => s.state.motion as AnimationClip);

            Mesh bindMesh = smr.sharedMesh;
            int vertexCount = bindMesh.vertexCount;

            // CORRIGÉ (2026-10-05, 2e régression critique - retour utilisateur : "il n'y a plus d'animation") -
            // les courbes d'un AnimationClip sont enregistrées avec des chemins RELATIFS AU GAMEOBJECT QUI PORTE
            // L'ANIMATOR (ex: "Skinned Mesh 0/root/Hips"), PAS à la racine du prefab instancié (qui est un ancêtre
            // AU-DESSUS de l'Animator - ex: Wolf > exported-model (1) [ANIMATOR ICI] > Skinned Mesh 0 > root >
            // Hips). Appeler clip.SampleAnimation(instance, t) avec `instance` (la racine) ne retrouve donc AUCUN
            // des chemins de courbe - echoue silencieusement, sans erreur ni exception, en laissant chaque os a sa
            // pose de bind. Résultat vérifié par mesure directe : la rotation des os ET la position des sommets
            // baked étaient rigoureusement IDENTIQUES à n'importe quel instant `t` échantillonné - aucune
            // animation n'a donc jamais été réellement cuite depuis le tout premier prototype (suite 23), malgré
            // un numéro de frame (`_VatFrame`) qui, lui, avançait bien côté C# - d'où l'illusion d'un pilote
            // fonctionnel dans toutes les vérifications précédentes (qui ne lisaient que ce numéro, jamais la pose
            // réelle). Corrigé en échantillonnant depuis `animator.gameObject` (le même point de référence que
            // l'Animator utiliserait lui-même normalement), qui correspond aux chemins enregistrés dans le clip.
            Transform sampleRoot = animator.gameObject.transform;

            // AJOUTÉ (2026-10-06, 3e régression critique - retour utilisateur : "le golem avance vite, recule, en
            // boucle" + "la taille du golem trop grande") - certains clips Walk (Golem, Goblin - PAS le Loup,
            // vérifié par mesure : hasRootCurves=False pour le Loup, True pour Golem/Goblin) contiennent du
            // VRAI MOUVEMENT DE RACINE ("root motion") - le clip déplace lui-même le GameObject de l'Animator
            // vers l'avant au fil de la marche (ex. Golem : ~1,18 unité/s vers l'avant, Goblin : ~1,66 u/s), prévu
            // à l'origine pour un usage avec "Apply Root Motion" classique de l'Animator. Notre pipeline VAT fait
            // AUTRE CHOSE : le déplacement réel de l'ennemi est géré exclusivement par le CODE (EnemyBase, basé
            // sur Time.deltaTime, totalement indépendant de l'animation) - sans ce correctif, le mouvement de
            // racine du clip s'ajoute EN PLUS, cuit directement dans les sommets : le maillage glisse en avant
            // tout au long du cycle (jusqu'à ~1,7 unité, une fraction significative de la taille du personnage)
            // PUIS SAUTE EN ARRIÈRE au bouclage du clip - exactement le "avance vite, recule" rapporté, et la
            // vraie boîte de collision (gérée par le code, jamais par l'animation) ne suit évidemment pas ce
            // glissement, d'où l'absence de dégâts quand le modèle visuel semble collé au joueur.
            Vector3 sampleRootBindLocalPos = sampleRoot.localPosition;
            Quaternion sampleRootBindLocalRot = sampleRoot.localRotation;

            // AJOUTÉ - pour les props tenus en main (sarbacane du Goblin...) : suit la MÊME frame que le corps
            // mais via une petite courbe position/rotation dédiée (voir VATBoneAttachment.cs), PAS la texture VAT
            // (qui ne concerne que les sommets du mesh principal). Relatif à la racine de l'instance (PAS à un os
            // parent intermédiaire) pour rester stable quel que soit l'endroit où le prop est reparenté ensuite.
            Transform attachmentBone = string.IsNullOrEmpty(attachmentBoneName)
                ? null
                : instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == attachmentBoneName);
            if (!string.IsNullOrEmpty(attachmentBoneName) && attachmentBone == null)
                Debug.LogWarning($"[VATBaker] Os d'attache '{attachmentBoneName}' introuvable sur {sourcePrefab.name}.");
            var attachmentFrames = new List<VATBoneAttachmentData.BoneFrame>();

            // CORRIGÉ (2026-10-05, 3e régression - retour utilisateur : "la sarbacane du goblin est pas bien
            // orientée") - l'ancien code cuisait directement la rotation/position MONDE de l'OS d'attache
            // (attachmentBone) comme étant celles du PROP lui-même. Faux : dans le rig d'origine, le prop (ex.
            // "Sarbacane") est enfant de l'os avec son PROPRE décalage fixe de position/rotation (choisi par
            // l'artiste pour que l'arme soit bien calée dans la main) - un os et l'objet qu'il tient ne partagent
            // JAMAIS exactement les mêmes axes. En ignorant ce décalage, le prop se retrouvait recalé sur les axes
            // bruts de l'os à chaque frame, d'où une orientation visiblement fausse. Ce décalage est RIGIDE (ne
            // change jamais avec l'animation - c'est un simple point de montage) : capturé UNE SEULE FOIS ici, à
            // la pose de bind (avant tout SampleAnimation), puis réappliqué à chaque frame ci-dessous.
            Transform attachmentProp = (attachmentBone != null && !string.IsNullOrEmpty(attachmentPropName))
                ? instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == attachmentPropName)
                : null;
            bool hasAttachmentOffset = attachmentBone != null && attachmentProp != null;
            Quaternion attachmentLocalOffsetRot = Quaternion.identity;
            Vector3 attachmentLocalOffsetPos = Vector3.zero;
            if (hasAttachmentOffset)
            {
                attachmentLocalOffsetRot = Quaternion.Inverse(attachmentBone.rotation) * attachmentProp.rotation;
                attachmentLocalOffsetPos = attachmentBone.InverseTransformPoint(attachmentProp.position);
            }
            else if (attachmentBone != null && !string.IsNullOrEmpty(attachmentPropName))
            {
                Debug.LogWarning($"[VATBaker] Prop d'attache '{attachmentPropName}' introuvable sur {sourcePrefab.name}.");
            }

            // AJOUTÉ (2026-10-05, 2e régression critique - retour utilisateur : "certains [ennemis] ne sont même
            // pas bien tourné") - BakeMesh() produit des sommets dans l'espace local du SkinnedMeshRenderer
            // (confirmé déjà correct en échelle/position SANS conversion, voir le commentaire plus bas dans la
            // boucle de cuisson - mais PAS en rotation : le GameObject qui porte le SkinnedMeshRenderer a, pour
            // certains ennemis (Golem, Goblin - modèles Tripo), une rotation locale de -90° en X par rapport à la
            // racine du prefab (correction d'axe appliquée par l'import glTF, Z-up -> Y-up, posée sur ce noeud -
            // PAS cuite dans les données du mesh/squelette lui-même, contrairement à l'échelle). Comme le nouveau
            // "VAT_Mesh" est placé à l'IDENTITÉ stricte sous la racine (voir ApplyToPrefab), cette rotation doit
            // être réappliquée manuellement aux données baked, sans quoi l'ennemi apparaît couché/tordu au lieu de
            // debout. Les ennemis dont le squelette n'a aucune rotation corrective (Loup/Corbeau/Araignée/Kaiju)
            // ont une valeur identité ici - aucun effet pour eux, confirmé par mesure (voir suite "26" de NOTES.md).
            Quaternion smrToRootRotation = Quaternion.Inverse(instance.transform.rotation) * smr.transform.rotation;

            var clipRanges = new List<ClipBakeRange>();
            var allPositions = new List<Vector3[]>(); // une entrée par frame baked, chacune un tableau par-sommet
            var allNormals = new List<Vector3[]>();

            int currentRow = 0;
            Mesh bakedMesh = new Mesh();

            foreach (string clipName in stateNames)
            {
                if (!statesByName.TryGetValue(clipName, out AnimationClip clip) || clip == null)
                {
                    Debug.LogWarning($"[VATBaker] État '{clipName}' introuvable sur {sourcePrefab.name}, ignoré.");
                    continue;
                }

                int frameCount = Mathf.Max(2, Mathf.RoundToInt(clip.length * sampleRate));
                var range = new ClipBakeRange { clipName = clipName, startFrame = currentRow, frameCount = frameCount, length = clip.length };
                clipRanges.Add(range);

                for (int f = 0; f < frameCount; f++)
                {
                    float t = (frameCount <= 1) ? 0f : (f / (float)frameCount) * clip.length;
                    // CORRIGÉ (2026-10-05, 2e régression critique) - echantillonne depuis le GameObject de
                    // l'Animator (voir commentaire sur `sampleRoot` plus haut), pas depuis la racine du prefab -
                    // sans quoi aucune courbe ne correspond et la pose reste figee au bind pose pour toujours.
                    clip.SampleAnimation(sampleRoot.gameObject, t);
                    // Annule le mouvement de racine QUE CE CLIP vient d'écrire sur sampleRoot lui-même (voir
                    // commentaire plus haut) - remet sampleRoot exactement à sa position/rotation de référence
                    // AVANT la cuisson, SANS toucher aux os enfants (Hips, bras, jambes...) dont la pose relative
                    // à sampleRoot reste, elle, correctement celle de cette frame du cycle de marche. Résultat :
                    // le personnage anime "sur place" dans la texture VAT, exactement comme le Loup (dont le clip
                    // n'a jamais eu de mouvement de racine) - le déplacement réel reste entièrement au code.
                    sampleRoot.localPosition = sampleRootBindLocalPos;
                    sampleRoot.localRotation = sampleRootBindLocalRot;
                    smr.BakeMesh(bakedMesh);

                    // CORRIGÉ (2026-10-05, 2e passage - le 1er correctif n'a RIEN changé en pratique, vérifié par
                    // mesure directe) - j'avais ajouté ici un passage par l'espace monde (smrTransform -> monde ->
                    // rootTransform) en supposant que BakeMesh() exclut l'échelle du GameObject du
                    // SkinnedMeshRenderer. Mesure directe après coup : le résultat final (mesh.bounds du nouveau
                    // VAT_Mesh, placé à l'identité stricte - lossyScale=1 vérifié) était EXACTEMENT la même
                    // taille, trop grande, qu'avant ce 1er correctif - preuve que BakeMesh() produit ICI une
                    // donnée DÉJÀ à la bonne échelle finale (vraisemblablement parce que les poses de repère des
                    // os sont elles-mêmes définies à l'échelle du modèle importé, pas à celle, réduite, du seul
                    // espace local du renderer) - mon passage par smrTransform (qui réintroduit l'échelle des
                    // ancêtres) la multipliait donc UNE SECONDE FOIS. Revenu à la sortie BRUTE de BakeMesh(), sans
                    // aucune conversion d'espace : combinée à VAT_Mesh placé à l'identité stricte (déjà en place,
                    // voir ApplyToPrefab/FixAlreadyConvertedPrefab), c'est cette donnée brute qui donne la bonne
                    // taille finale. SEULE la ROTATION (smrToRootRotation, voir plus haut) doit encore être
                    // réappliquée - elle n'était, elle, jamais cuite nulle part (contrairement à l'échelle).
                    Vector3[] bakedVerts = bakedMesh.vertices;
                    Vector3[] bakedNorms = bakedMesh.normals;
                    for (int bv = 0; bv < bakedVerts.Length; bv++)
                    {
                        bakedVerts[bv] = smrToRootRotation * bakedVerts[bv];
                        bakedNorms[bv] = smrToRootRotation * bakedNorms[bv];
                    }

                    Vector3[] verts = bakedVerts.Length == vertexCount ? bakedVerts : ResampleToCount(bakedVerts, vertexCount);
                    Vector3[] norms = bakedNorms.Length == vertexCount ? bakedNorms : ResampleToCount(bakedNorms, vertexCount);

                    allPositions.Add(verts);
                    allNormals.Add(norms);

                    if (hasAttachmentOffset)
                    {
                        // Recombine la pose de l'os CETTE frame avec le décalage fixe os->prop capturé plus haut,
                        // au lieu d'utiliser directement les axes bruts de l'os (voir commentaire plus haut).
                        Vector3 propWorldPos = attachmentBone.TransformPoint(attachmentLocalOffsetPos);
                        Quaternion propWorldRot = attachmentBone.rotation * attachmentLocalOffsetRot;
                        attachmentFrames.Add(new VATBoneAttachmentData.BoneFrame
                        {
                            localPosition = instance.transform.InverseTransformPoint(propWorldPos),
                            localRotation = Quaternion.Inverse(instance.transform.rotation) * propWorldRot
                        });
                    }
                }

                // AJOUTÉ (2026-10-06) - vraie cause du "mouvement saccadé" (suite 29 de NOTES.md) : les clips
                // Humanoid de Mixamo (Golem/Goblin) avancent le corps vers l'avant pendant le cycle (2,98 unités
                // pour le Walk du Golem, 1,83 pour le Goblin). L'Animator extrayait ce déplacement comme root motion
                // (puis le jetait, applyRootMotion = false) - SampleAnimation, lui, le cuisait dans les sommets :
                // l'ennemi avançait dans son propre mesh puis se TÉLÉPORTAIT en arrière à chaque bouclage.
                if (clip.humanMotion)
                    RemoveRootDriftXZ(allPositions, attachmentFrames, range.startFrame, frameCount);

                currentRow += frameCount;
            }

            int totalFrames = currentRow;
            if (totalFrames == 0) throw new System.Exception("Aucune frame cuite - vérifie les noms de clips.");

            // --- Textures VAT (RGBAHalf, Point filtering obligatoire - un filtrage bilineaire melangerait les
            // positions de sommets voisins SANS AUCUN RAPPORT entre eux, corrompant le mesh anime). ---
            Texture2D positionTex = new Texture2D(vertexCount, totalFrames, TextureFormat.RGBAHalf, false, true);
            Texture2D normalTex = new Texture2D(vertexCount, totalFrames, TextureFormat.RGBAHalf, false, true);
            positionTex.filterMode = FilterMode.Point;
            normalTex.filterMode = FilterMode.Point;
            positionTex.wrapMode = TextureWrapMode.Clamp;
            normalTex.wrapMode = TextureWrapMode.Clamp;

            Color[] posPixels = new Color[vertexCount * totalFrames];
            Color[] normPixels = new Color[vertexCount * totalFrames];
            Vector3 boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int row = 0; row < totalFrames; row++)
            {
                Vector3[] verts = allPositions[row];
                Vector3[] norms = allNormals[row];
                for (int v = 0; v < vertexCount; v++)
                {
                    int idx = row * vertexCount + v;
                    posPixels[idx] = new Color(verts[v].x, verts[v].y, verts[v].z, 1f);
                    normPixels[idx] = new Color(norms[v].x, norms[v].y, norms[v].z, 0f);
                    boundsMin = Vector3.Min(boundsMin, verts[v]);
                    boundsMax = Vector3.Max(boundsMax, verts[v]);
                }
            }

            positionTex.SetPixels(posPixels);
            normalTex.SetPixels(normPixels);
            positionTex.Apply(false, false);
            normalTex.Apply(false, false);

            string posPath = $"{outputFolder}/{assetNamePrefix}_VatPosition.asset";
            string normPath = $"{outputFolder}/{assetNamePrefix}_VatNormal.asset";
            AssetDatabase.DeleteAsset(posPath);
            AssetDatabase.DeleteAsset(normPath);
            AssetDatabase.CreateAsset(positionTex, posPath);
            AssetDatabase.CreateAsset(normalTex, normPath);

            // --- Mesh statique (topologie du bind pose, + canal UV dédié "index de sommet" pour le shader) ---
            Mesh staticMesh = new Mesh();
            staticMesh.name = $"{assetNamePrefix}_VatMesh";
            staticMesh.indexFormat = vertexCount > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            staticMesh.vertices = bindMesh.vertices;
            staticMesh.normals = bindMesh.normals;
            staticMesh.uv = bindMesh.uv;
            staticMesh.triangles = bindMesh.triangles;

            Vector2[] vatUV = new Vector2[vertexCount];
            for (int v = 0; v < vertexCount; v++)
                vatUV[v] = new Vector2((v + 0.5f) / vertexCount, 0f);
            staticMesh.SetUVs(3, vatUV); // canal uv4 Unity = TEXCOORD3 dans Attributes (voir VATLitForwardPass.hlsl)

            // Bounds englobant TOUTES les frames (pas seulement le bind pose) - sinon le culling frustum
            // utiliserait une boîte trop petite et l'ennemi disparaîtrait au bord de l'écran en pleine animation.
            Vector3 center = (boundsMin + boundsMax) * 0.5f;
            Vector3 size = (boundsMax - boundsMin);
            staticMesh.bounds = new Bounds(center, size * 1.1f); // marge 10%

            string meshPath = $"{outputFolder}/{assetNamePrefix}_VatMesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(staticMesh, meshPath);

            // --- Matériau (Aether/VATLit, réutilise les textures d'origine pour l'albédo). 2 familles de shader
            // source rencontrées dans ce projet (voir NOTES.md) : "Shader Graphs/glTF-pbrMetallicRoughness"
            // (modèles importés de Tripo - Loup/Corbeau/Araignée/Kaiju) nécessite une conversion de canal
            // (metallicRoughnessTexture: G=roughness,B=metallic -> _MetallicGlossMap: R=metallic,A=smoothness) ;
            // "Universal Render Pipeline/Lit" (Golem/Goblin) utilise DÉJÀ la convention Unity - copie directe,
            // aucune conversion nécessaire. ---
            Material sourceMat = smr.sharedMaterial;
            Shader vatShader = Shader.Find("Aether/VATLit");
            if (vatShader == null) throw new System.Exception("Shader 'Aether/VATLit' introuvable - vérifie qu'il compile sans erreur.");

            Material mat = new Material(vatShader);
            mat.name = $"{assetNamePrefix}_VatMat";
            mat.enableInstancing = true; // OBLIGATOIRE - _VatFrame est une propriete par-instance (voir VATCommon.hlsl)

            bool isGltfShader = sourceMat.shader.name.Contains("glTF");

            if (isGltfShader)
            {
                if (sourceMat.HasProperty("baseColorTexture"))
                    mat.SetTexture("_BaseMap", sourceMat.GetTexture("baseColorTexture"));
                if (sourceMat.HasProperty("baseColorFactor"))
                    mat.SetColor("_BaseColor", sourceMat.GetColor("baseColorFactor"));

                if (sourceMat.HasProperty("metallicRoughnessTexture") && sourceMat.GetTexture("metallicRoughnessTexture") != null)
                {
                    Texture2D converted = ConvertGltfMetallicRoughness(sourceMat.GetTexture("metallicRoughnessTexture"));
                    string metalPath = $"{outputFolder}/{assetNamePrefix}_MetallicSmoothness.asset";
                    AssetDatabase.DeleteAsset(metalPath);
                    AssetDatabase.CreateAsset(converted, metalPath);
                    mat.SetTexture("_MetallicGlossMap", converted);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                if (sourceMat.HasProperty("metallicFactor"))
                    mat.SetFloat("_Metallic", sourceMat.GetFloat("metallicFactor"));
                if (sourceMat.HasProperty("roughnessFactor"))
                    mat.SetFloat("_Smoothness", 1f - sourceMat.GetFloat("roughnessFactor"));
                if (sourceMat.HasProperty("emissiveFactor"))
                {
                    Color emissive = sourceMat.GetColor("emissiveFactor");
                    if (emissive.maxColorComponent > 0f)
                    {
                        mat.SetColor("_EmissionColor", emissive);
                        mat.EnableKeyword("_EMISSION");
                    }
                }
            }
            else
            {
                // Déjà en convention Unity (Universal Render Pipeline/Lit) - copie directe des propriétés.
                if (sourceMat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", sourceMat.GetTexture("_BaseMap"));
                if (sourceMat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", sourceMat.GetColor("_BaseColor"));
                if (sourceMat.HasProperty("_MetallicGlossMap") && sourceMat.GetTexture("_MetallicGlossMap") != null)
                {
                    mat.SetTexture("_MetallicGlossMap", sourceMat.GetTexture("_MetallicGlossMap"));
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                if (sourceMat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", sourceMat.GetFloat("_Metallic"));
                if (sourceMat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", sourceMat.GetFloat("_Smoothness"));
                if (sourceMat.HasProperty("_EmissionColor"))
                {
                    Color emissive = sourceMat.GetColor("_EmissionColor");
                    if (emissive.maxColorComponent > 0f)
                    {
                        mat.SetColor("_EmissionColor", emissive);
                        mat.EnableKeyword("_EMISSION");
                    }
                }
            }

            mat.SetTexture("_VatPositionTex", positionTex);
            mat.SetTexture("_VatNormalTex", normalTex);

            string matPath = $"{outputFolder}/{assetNamePrefix}_VatMat.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            // --- VATClipSet (metadata runtime : ou commence/combien de frames dure chaque clip dans la texture) ---
            VATClipSet clipSet = ScriptableObject.CreateInstance<VATClipSet>();
            clipSet.clips = clipRanges.Select(r => new VATAnimatedRenderer.ClipRange
            {
                clipName = r.clipName,
                startFrame = r.startFrame,
                frameCount = r.frameCount,
                length = r.length
            }).ToArray();
            string clipSetPath = $"{outputFolder}/{assetNamePrefix}_VatClipSet.asset";
            AssetDatabase.DeleteAsset(clipSetPath);
            AssetDatabase.CreateAsset(clipSet, clipSetPath);

            // --- Donnees d'attache (sarbacane etc.) - uniquement si un os d'attache a ete demande ET trouve ---
            VATBoneAttachmentData attachmentData = null;
            if (hasAttachmentOffset && attachmentFrames.Count == totalFrames)
            {
                attachmentData = ScriptableObject.CreateInstance<VATBoneAttachmentData>();
                attachmentData.frames = attachmentFrames.ToArray();
                string attachPath = $"{outputFolder}/{assetNamePrefix}_VatAttachment.asset";
                AssetDatabase.DeleteAsset(attachPath);
                AssetDatabase.CreateAsset(attachmentData, attachPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new BakeResult
            {
                positionTexture = positionTex,
                normalTexture = normalTex,
                staticMesh = staticMesh,
                material = mat,
                clipRanges = clipRanges,
                clipSet = clipSet,
                totalFrames = totalFrames,
                vertexCount = vertexCount,
                attachmentData = attachmentData
            };
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // AJOUTÉ (2026-10-05, intégration jeu réel) - bascule un prefab d'ennemi existant du pipeline classique
    // (SkinnedMeshRenderer + Animator) vers VAT : repère le GameObject qui porte le SkinnedMeshRenderer, le
    // transforme en MeshRenderer/MeshFilter + VATAnimatedRenderer à la même place dans la hiérarchie (rien
    // d'autre ne bouge - colliders, Rigidbody, EnemyBase/EnemyShooter/etc. restent intacts), retire l'Animator.
    // Tout le reste du jeu (EnemyAnimatorController, EnemyBase.Awake shadows/culling, le flash de dégâts par
    // MaterialPropertyBlock) continue de fonctionner sans modification - voir EnemyAnimatorController.cs.
    // Recale chaque frame de [startFrame, startFrame+frameCount) pour que le centre de masse (moyenne des sommets)
    // garde la position XZ de la 1re frame du clip - équivalent "en place" de l'extraction de root motion Humanoid
    // d'Unity (elle aussi basée sur le centre de masse). Y intact : rebond vertical de la marche conservé.
    // attachmentFrames peut être vide (ennemi sans objet tenu) - sinon recalé du même décalage que le corps.
    public static void RemoveRootDriftXZ(List<Vector3[]> frames, List<VATBoneAttachmentData.BoneFrame> attachmentFrames, int startFrame, int frameCount)
    {
        Vector3 reference = Vector3.zero;
        for (int f = 0; f < frameCount; f++)
        {
            Vector3[] verts = frames[startFrame + f];
            Vector3 centroid = Vector3.zero;
            for (int v = 0; v < verts.Length; v++) centroid += verts[v];
            centroid /= Mathf.Max(1, verts.Length);
            if (f == 0) reference = centroid;

            Vector3 offset = new Vector3(centroid.x - reference.x, 0f, centroid.z - reference.z);
            for (int v = 0; v < verts.Length; v++) verts[v] -= offset;

            int a = startFrame + f;
            if (attachmentFrames != null && a < attachmentFrames.Count)
            {
                VATBoneAttachmentData.BoneFrame bf = attachmentFrames[a];
                bf.localPosition -= offset;
                attachmentFrames[a] = bf;
            }
        }
    }

    public static void ApplyToPrefab(string prefabPath, BakeResult bake, string idleClipName, string attackClipName, string attachmentPropName = null)
        => ApplyToPrefab(prefabPath, bake.staticMesh, bake.material, bake.clipSet, idleClipName, attackClipName, attachmentPropName, bake.attachmentData);

    // Surcharge indépendante d'un appel Bake() dans le même run - charge les assets déjà cuits sur disque (utile
    // pour appliquer après coup, dans une commande séparée de celle qui a cuit).
    public static void ApplyToPrefab(string prefabPath, Mesh mesh, Material material, VATClipSet clipSet, string idleClipName, string attackClipName,
        string attachmentPropName = null, VATBoneAttachmentData attachmentData = null)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var animator = root.GetComponentInChildren<Animator>(true);
            if (smr == null) throw new System.Exception($"Pas de SkinnedMeshRenderer dans {prefabPath}.");

            // AJOUTÉ - repère le prop tenu en main (sarbacane...) AVANT de retirer l'Animator/les os, pendant
            // qu'il est encore dans sa pose de bind connue - voir VATBoneAttachment.cs pour pourquoi il a besoin
            // d'une courbe à part (le corps anime par texture, mais un prop enfant d'un os ne peut pas la lire).
            Transform attachmentProp = string.IsNullOrEmpty(attachmentPropName)
                ? null
                : root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == attachmentPropName);

            Object.DestroyImmediate(smr);
            if (animator != null) Object.DestroyImmediate(animator);

            // CORRIGÉ (2026-10-05, retour utilisateur critique sur la taille) - le mesh VAT est maintenant cuit
            // dans l'espace de la RACINE de l'ennemi (voir Bake() ci-dessus), PAS dans celui du GameObject qui
            // portait le SkinnedMeshRenderer (qui a presque toujours sa propre échelle héritée du rig importé -
            // 2x, 2.3x, 2.5x selon l'ennemi). Le nouveau renderer doit donc être un enfant DIRECT de la racine, à
            // l'identité (position/rotation nulles, échelle 1) - sinon cette échelle s'applique UNE SECONDE FOIS
            // par-dessus une texture qui contient déjà la bonne taille finale, d'où les ennemis 2 à 8x trop
            // grands observés. L'ancien GameObject (Armature, EnemyAnimatorController...) reste en place, intact
            // mais vidé de son SkinnedMeshRenderer/Animator.
            GameObject meshGO = new GameObject("VAT_Mesh");
            meshGO.transform.SetParent(root.transform, false); // false = force position/rotation/scale locales a zero/identite/1

            var mf = meshGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = meshGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;

            var vatAnim = meshGO.AddComponent<VATAnimatedRenderer>();
            var so = new SerializedObject(vatAnim);
            so.FindProperty("_clipSet").objectReferenceValue = clipSet;
            so.FindProperty("_idleClipName").stringValue = idleClipName;
            so.FindProperty("_attackClipName").stringValue = attackClipName;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (attachmentProp != null && attachmentData != null)
            {
                // Sort le prop de la chaine d'os (desormais figee) - SetParent(true) garde sa position/rotation
                // MONDE actuelle (pose de bind) inchangee a l'instant du changement ; VATBoneAttachment reprend la
                // main DES LA PROCHAINE frame pour le faire suivre la bonne pose a chaque instant.
                attachmentProp.SetParent(root.transform, true);
                var attach = attachmentProp.gameObject.AddComponent<VATBoneAttachment>();
                var attachSo = new SerializedObject(attach);
                attachSo.FindProperty("_source").objectReferenceValue = vatAnim;
                attachSo.FindProperty("_data").objectReferenceValue = attachmentData;
                attachSo.FindProperty("_root").objectReferenceValue = root.transform;
                attachSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // AJOUTÉ (2026-10-05, correctif d'urgence - retour utilisateur : "les ennemis sont devenus bien plus grand,
    // plus d'animation, ils glissent") - les 6 prefabs étaient déjà convertis (plus de SkinnedMeshRenderer) quand
    // le bug d'échelle a été trouvé : `ApplyToPrefab` normal ne peut plus s'appliquer dessus (il cherche un
    // SkinnedMeshRenderer à retirer, qui n'existe plus). Cette variante corrige un prefab DÉJÀ converti : retrouve
    // le `VATAnimatedRenderer` existant, remplace son mesh/matériau par la version recuite (espace racine,
    // échelle correcte), et le replace en enfant DIRECT de la racine à l'identité - exactement comme le ferait
    // ApplyToPrefab sur un prefab neuf, mais sans dépendre d'un SkinnedMeshRenderer encore présent.
    public static void FixAlreadyConvertedPrefab(string prefabPath, BakeResult bake)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var vatAnim = root.GetComponentInChildren<VATAnimatedRenderer>(true);
            if (vatAnim == null) throw new System.Exception($"Pas de VATAnimatedRenderer dans {prefabPath} - ce prefab n'a jamais ete converti ?");

            // CORRIGÉ - l'ancien GameObject (reutilise du SkinnedMeshRenderer d'origine) appartient a une
            // instance de prefab IMBRIQUEE (le modele 3D importe) - Unity refuse d'en changer le parent
            // ("Setting the parent of a transform which resides in a Prefab instance is not possible"). Au lieu
            // de le deplacer, cree un NOUVEAU GameObject (jamais imbrique, deplacement libre) comme enfant direct
            // de la racine, y copie les reglages, et vide l'ancien de ses composants de rendu.
            var oldSo = new SerializedObject(vatAnim);
            string idleClipName = oldSo.FindProperty("_idleClipName").stringValue;
            string attackClipName = oldSo.FindProperty("_attackClipName").stringValue;

            GameObject oldMeshGO = vatAnim.gameObject;
            Object.DestroyImmediate(vatAnim);
            Object.DestroyImmediate(oldMeshGO.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(oldMeshGO.GetComponent<MeshFilter>());

            GameObject meshGO = new GameObject("VAT_Mesh");
            meshGO.transform.SetParent(root.transform, false);

            var mf = meshGO.AddComponent<MeshFilter>();
            mf.sharedMesh = bake.staticMesh;
            var mr = meshGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = bake.material;

            var newVatAnim = meshGO.AddComponent<VATAnimatedRenderer>();
            var so = new SerializedObject(newVatAnim);
            so.FindProperty("_clipSet").objectReferenceValue = bake.clipSet;
            so.FindProperty("_idleClipName").stringValue = idleClipName;
            so.FindProperty("_attackClipName").stringValue = attackClipName;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Les props attaches (sarbacane...) referencent VATAnimatedRenderer par instance - le composant ayant
            // change d'objet, il faut re-pointer toute VATBoneAttachment existante dessus.
            foreach (var attach in root.GetComponentsInChildren<VATBoneAttachment>(true))
            {
                var attachSo = new SerializedObject(attach);
                attachSo.FindProperty("_source").objectReferenceValue = newVatAnim;
                attachSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Cree chaque niveau manquant du chemin (AssetDatabase.CreateFolder ne cree qu'UN niveau a la fois et
    // echoue silencieusement si le parent immediat n'existe pas deja).
    private static void EnsureFolderExists(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Vector3[] ResampleToCount(Vector3[] source, int targetCount)
    {
        // Filet de sécurité si BakeMesh retourne un nombre de sommets différent (ne devrait pas arriver pour un
        // import glTF standard) - évite un crash, la qualité de ce cas limite n'est pas prioritaire.
        Vector3[] result = new Vector3[targetCount];
        for (int i = 0; i < targetCount; i++)
            result[i] = source[Mathf.Min(i, source.Length - 1)];
        return result;
    }

    // glTF (metallicRoughnessTexture) : G=roughness, B=metallic. Unity URP (_MetallicGlossMap) : R=metallic,
    // A=smoothness(=1-roughness). Passe par un Blit + ReadPixels (fonctionne même si la texture source n'a pas
    // "Read/Write Enabled" dans ses import settings, contrairement a GetPixels direct).
    private static Texture2D ConvertGltfMetallicRoughness(Texture source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] src = readable.GetPixels32();
        Color32[] dst = new Color32[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            byte metallic = src[i].b;
            byte roughness = src[i].g;
            byte smoothness = (byte)(255 - roughness);
            dst[i] = new Color32(metallic, metallic, metallic, smoothness);
        }

        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, true);
        result.SetPixels32(dst);
        result.Apply(true, false);

        Object.DestroyImmediate(readable);
        return result;
    }
}
