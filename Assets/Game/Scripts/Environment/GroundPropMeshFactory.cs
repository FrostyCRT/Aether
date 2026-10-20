using System;
using System.Collections.Generic;
using UnityEngine;

// AJOUTÉ (2026-10-20, chantier graphismes - "on peut faire des choses sans Tripo pour l'instant") - meshs
// PLACEHOLDER pour les props de sol scattered par GroundPropScatterer, générés entièrement par code (aucun asset
// importé nécessaire). But : valider tout de suite en jeu le système de dispersion/instancing décrit dans
// A_MODELISER_3D.md §7.2 sans attendre les vrais modèles Tripo3D. Remplacement futur : assigner le mesh Tripo3D
// importé au champ "meshOverride" de l'entrée correspondante dans GroundPropScatterer - aucune ligne de ce fichier
// à toucher, aucun autre changement de code.
//
// Tous les meshs ont leur pivot à la BASE (Y=0 au sol), prêts à être placés directement à la hauteur du sol.
public static class GroundPropMeshFactory
{
    private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

    public static Mesh GrassTuft(int variant) => GetOrBuild("GrassTuft" + variant, () => BuildGrassTuft(variant));
    public static Mesh Wildflower(int variant) => GetOrBuild("Wildflower" + variant, () => BuildWildflower(variant));
    public static Mesh ManaShard() => GetOrBuild("ManaShard", BuildManaShard);
    public static Mesh Pebble(int variant) => GetOrBuild("Pebble" + variant, () => BuildPebble(variant));
    public static Mesh Log() => GetOrBuild("Log", BuildLog);
    public static Mesh Mushroom(int variant) => GetOrBuild("Mushroom" + variant, () => BuildMushroom(variant));
    public static Mesh MossPatch(int variant) => GetOrBuild("MossPatch" + variant, () => BuildMossPatch(variant));

    private static Mesh GetOrBuild(string key, Func<Mesh> builder)
    {
        if (_cache.TryGetValue(key, out Mesh existing) && existing != null) return existing;
        Mesh built = builder();
        built.name = "Proc_" + key;
        _cache[key] = built;
        return built;
    }

    // --- Petits utilitaires de construction -------------------------------------------------------------------

    private class MeshBuilder
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<int> tris = new List<int>();
        public readonly List<Vector2> uvs = new List<Vector2>();

        // Triangle simple, ajouté dans les 2 sens d'enroulement (double face) - évite de dépendre d'un réglage
        // _Cull sur le matériau pour les brins d'herbe/pétales fins, vus des 2 côtés en vue du dessus inclinée.
        // CORRIGÉ (2026-10-20, test visuel immédiat - captures noires sur tous les props double-face) : la 1re
        // version référençait les 3 MÊMES sommets pour les 2 sens d'enroulement - RecalculateNormals (appelé une
        // fois à la fin, sur tout le mesh) moyenne alors la normale "avant" et la normale "arrière" (opposées) au
        // même sommet partagé, ce qui donne un vecteur quasi nul -> éclairage noir. Chaque face a maintenant SES
        // PROPRES sommets (dupliqués), comme AddQuad/AddDoubleSidedQuad le faisaient déjà correctement.
        public void AddDoubleSidedTri(Vector3 a, Vector3 b, Vector3 c)
        {
            AddSingleTri(a, b, c);
            AddSingleTri(a, c, b);
        }

        private void AddSingleTri(Vector3 a, Vector3 b, Vector3 c)
        {
            int start = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0.5f, 1));
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }

        public void AddDoubleSidedQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddQuad(a, b, c, d);
            AddQuad(a, d, c, b);
        }

        public void AddTriFan(Vector3 center, IReadOnlyList<Vector3> rim, bool faceUp)
        {
            int centerIndex = verts.Count;
            verts.Add(center);
            uvs.Add(new Vector2(0.5f, 0.5f));
            int firstRim = verts.Count;
            for (int i = 0; i < rim.Count; i++)
            {
                verts.Add(rim[i]);
                uvs.Add(new Vector2(0.5f + rim[i].x * 0.5f, 0.5f + rim[i].z * 0.5f));
            }
            for (int i = 0; i < rim.Count; i++)
            {
                int a = firstRim + i;
                int b = firstRim + (i + 1) % rim.Count;
                if (faceUp) { tris.Add(centerIndex); tris.Add(a); tris.Add(b); }
                else { tris.Add(centerIndex); tris.Add(b); tris.Add(a); }
            }
        }

        public Mesh Build()
        {
            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(true); // plus jamais modifié après construction - libère la copie côté CPU
            return mesh;
        }
    }

    private static float Hash01(int seed)
    {
        unchecked
        {
            int h = seed * 374761393;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / 2147483647f;
        }
    }

    // --- Touffe d'herbe : 3 brins en croix, légèrement inclinés vers l'extérieur --------------------------------
    private static Mesh BuildGrassTuft(int variant)
    {
        MeshBuilder b = new MeshBuilder();
        float height = 0.3f + Hash01(variant * 11 + 1) * 0.15f;
        float width = 0.05f + Hash01(variant * 11 + 2) * 0.03f;
        float lean = 0.05f + Hash01(variant * 11 + 3) * 0.05f;

        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120f + Hash01(variant * 11 + 10 + i) * 30f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 side = Vector3.Cross(Vector3.up, dir);

            Vector3 baseA = -side * width;
            Vector3 baseB = side * width;
            Vector3 tip = dir * lean + Vector3.up * height;

            b.AddDoubleSidedTri(baseA, baseB, tip);
        }

        return b.Build();
    }

    // --- Fleur sauvage : tige fine + bouton en croix au sommet --------------------------------------------------
    private static Mesh BuildWildflower(int variant)
    {
        MeshBuilder b = new MeshBuilder();
        float stemHeight = 0.22f + Hash01(variant * 17 + 1) * 0.1f;
        float stemWidth = 0.02f;
        float bloomSize = 0.07f + Hash01(variant * 17 + 2) * 0.03f;

        b.AddDoubleSidedQuad(
            new Vector3(-stemWidth, 0f, 0f),
            new Vector3(stemWidth, 0f, 0f),
            new Vector3(stemWidth, stemHeight, 0f),
            new Vector3(-stemWidth, stemHeight, 0f));

        Vector3 bloomCenter = Vector3.up * stemHeight;
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            Vector3 petalTip = bloomCenter + new Vector3(Mathf.Sin(angle), 0.15f, Mathf.Cos(angle)) * bloomSize;
            Vector3 petalA = bloomCenter + new Vector3(Mathf.Sin(angle - 0.6f), 0f, Mathf.Cos(angle - 0.6f)) * bloomSize * 0.5f;
            Vector3 petalB = bloomCenter + new Vector3(Mathf.Sin(angle + 0.6f), 0f, Mathf.Cos(angle + 0.6f)) * bloomSize * 0.5f;
            b.AddDoubleSidedTri(petalA, petalB, petalTip);
        }

        return b.Build();
    }

    // --- Éclat de mana : bipyramide à base carrée (cristal effilé) ----------------------------------------------
    private static Mesh BuildManaShard()
    {
        MeshBuilder b = new MeshBuilder();
        float height = 0.4f;
        float midHeight = height * 0.4f;
        float radius = 0.06f;

        Vector3 top = Vector3.up * height;
        Vector3 bottom = Vector3.zero;
        Vector3[] equator = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            float angle = (45f + i * 90f) * Mathf.Deg2Rad;
            equator[i] = new Vector3(Mathf.Sin(angle) * radius, midHeight, Mathf.Cos(angle) * radius);
        }

        for (int i = 0; i < 4; i++)
        {
            Vector3 a = equator[i];
            Vector3 c = equator[(i + 1) % 4];
            b.AddDoubleSidedTri(a, c, top);
            b.AddDoubleSidedTri(c, a, bottom);
        }

        return b.Build();
    }

    // --- Caillou : octaèdre aplati et déformé (variantes via un bruit déterministe par sommet) ------------------
    private static Mesh BuildPebble(int variant)
    {
        MeshBuilder b = new MeshBuilder();

        Vector3[] baseDirs =
        {
            Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back,
            Vector3.left, Vector3.right
        };

        Vector3[] p = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float jitterX = (Hash01(variant * 29 + i * 7 + 1) - 0.5f) * 0.3f;
            float jitterY = (Hash01(variant * 29 + i * 7 + 2) - 0.5f) * 0.15f;
            float jitterZ = (Hash01(variant * 29 + i * 7 + 3) - 0.5f) * 0.3f;
            Vector3 dir = baseDirs[i];
            float radius = dir.y != 0f ? 0.1f : 0.16f; // aplati : rayon vertical plus court que l'horizontal
            p[i] = dir * radius + new Vector3(jitterX, jitterY * 0.5f, jitterZ);
        }
        // Base posée au sol : relève tout le bloc pour que le point le plus bas touche Y=0.
        float minY = Mathf.Min(p[0].y, p[1].y, p[2].y, p[3].y, p[4].y, p[5].y);
        for (int i = 0; i < 6; i++) p[i].y -= minY;

        Vector3 top = p[0], bottom = p[1], front = p[2], back = p[3], left = p[4], right = p[5];
        b.AddDoubleSidedTri(front, right, top);
        b.AddDoubleSidedTri(right, back, top);
        b.AddDoubleSidedTri(back, left, top);
        b.AddDoubleSidedTri(left, front, top);
        b.AddDoubleSidedTri(right, front, bottom);
        b.AddDoubleSidedTri(back, right, bottom);
        b.AddDoubleSidedTri(left, back, bottom);
        b.AddDoubleSidedTri(front, left, bottom);

        return b.Build();
    }

    // --- Bûche couchée : prisme hexagonal le long de l'axe X, avec bouchons aux 2 bouts -------------------------
    private static Mesh BuildLog()
    {
        MeshBuilder b = new MeshBuilder();
        const int segments = 6;
        float radius = 0.16f;
        float length = 1.1f;

        Vector3[] ring = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            ring[i] = new Vector3(0f, radius + Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        for (int i = 0; i < segments; i++)
        {
            Vector3 a0 = ring[i] + Vector3.right * -length * 0.5f;
            Vector3 a1 = ring[(i + 1) % segments] + Vector3.right * -length * 0.5f;
            Vector3 b0 = ring[i] + Vector3.right * length * 0.5f;
            Vector3 b1 = ring[(i + 1) % segments] + Vector3.right * length * 0.5f;
            b.AddQuad(a0, a1, b1, b0);
        }

        List<Vector3> rimNear = new List<Vector3>();
        List<Vector3> rimFar = new List<Vector3>();
        for (int i = segments - 1; i >= 0; i--) rimNear.Add(ring[i] + Vector3.right * -length * 0.5f);
        for (int i = 0; i < segments; i++) rimFar.Add(ring[i] + Vector3.right * length * 0.5f);

        Vector3 centerNear = new Vector3(-length * 0.5f, radius, 0f);
        Vector3 centerFar = new Vector3(length * 0.5f, radius, 0f);
        b.AddTriFan(centerNear, rimNear, faceUp: false);
        b.AddTriFan(centerFar, rimFar, faceUp: true);

        return b.Build();
    }

    // --- Champignon : chapeau conique + pied cylindrique -----------------------------------------------------
    private static Mesh BuildMushroom(int variant)
    {
        MeshBuilder b = new MeshBuilder();
        const int segments = 7;
        float stemHeight = 0.12f + Hash01(variant * 31 + 1) * 0.05f;
        float stemRadius = 0.02f;
        float capRadius = 0.07f + Hash01(variant * 31 + 2) * 0.03f;
        float capHeight = 0.06f;

        Vector3[] stemRing = new Vector3[segments];
        Vector3[] capRimRing = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            stemRing[i] = dir * stemRadius + Vector3.up * stemHeight;
            capRimRing[i] = dir * capRadius + Vector3.up * stemHeight;
        }

        // Pied (petit cylindre, pas de bouchon en bas : invisible, planté dans le sol).
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 baseP = dir * stemRadius;
            Vector3 baseN = new Vector3(Mathf.Sin((i + 1) * (360f / segments) * Mathf.Deg2Rad), 0f, Mathf.Cos((i + 1) * (360f / segments) * Mathf.Deg2Rad)) * stemRadius;
            b.AddQuad(baseP, baseN, stemRing[(i + 1) % segments], stemRing[i]);
        }

        // Chapeau : anneau du rebord + sommet, forme conique simple.
        Vector3 capTop = Vector3.up * (stemHeight + capHeight);
        for (int i = 0; i < segments; i++)
            b.AddDoubleSidedTri(capRimRing[i], capRimRing[(i + 1) % segments], capTop);

        List<Vector3> underCap = new List<Vector3>();
        for (int i = segments - 1; i >= 0; i--) underCap.Add(capRimRing[i]);
        b.AddTriFan(Vector3.up * stemHeight, underCap, faceUp: false);

        return b.Build();
    }

    // --- Patch de mousse : disque irrégulier, quasiment plat ---------------------------------------------------
    private static Mesh BuildMossPatch(int variant)
    {
        MeshBuilder b = new MeshBuilder();
        const int segments = 8;
        float baseRadius = 0.22f + Hash01(variant * 41 + 1) * 0.08f;

        List<Vector3> rim = new List<Vector3>();
        for (int i = 0; i < segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            float r = baseRadius * (0.75f + Hash01(variant * 41 + 10 + i) * 0.5f);
            rim.Add(new Vector3(Mathf.Sin(angle) * r, 0.015f, Mathf.Cos(angle) * r));
        }

        b.AddTriFan(new Vector3(0f, 0.015f, 0f), rim, faceUp: true);
        return b.Build();
    }
}
