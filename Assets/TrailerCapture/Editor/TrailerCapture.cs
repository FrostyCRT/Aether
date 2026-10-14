using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// OUTIL TRAILER (2026-10-14) - côté éditeur de TrailerShotDriver : passe la Game View en 1920x1080 fixe, lance un
// plan, puis assemble les images capturées en MP4 (H.264, 60 i/s) avec le ffmpeg fourni par Remotion.
// Sortie : ../Aether Storm Trailer/public/shots/<plan>.mp4 (projet Remotion, à côté du projet Unity).
[InitializeOnLoad]
public static class TrailerCapture
{
    // ---- Liste de plans enchaînés automatiquement (Play -> plan -> Stop -> plan suivant) ----
    // Conservée dans SessionState car l'entrée en Play Mode recharge le domaine C# (statiques remis à zéro).
    private const string QueueKey = "TrailerCapture.Queue";

    static TrailerCapture()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static string RunBatch(string shotsCsv)
    {
        SessionState.SetString(QueueKey, shotsCsv);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else EditorApplication.EnterPlaymode();
        return "File d'attente : " + shotsCsv;
    }

    public static string Queue => SessionState.GetString(QueueKey, "");

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        string queue = Queue;
        if (string.IsNullOrEmpty(queue)) return;
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            string[] parts = queue.Split(',');
            SessionState.SetString(QueueKey, string.Join(",", parts, 1, parts.Length - 1));
            string shot = parts[0].Trim();
            EditorApplication.delayCall += () =>
            {
                Debug.Log("[Trailer] " + Run(shot, exitWhenDone: true));
            };
        }
    }
    private static string TrailerRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Aether Storm Trailer"));
    private static string ShotsDir => Path.Combine(TrailerRoot, "public", "shots");
    private static string FramesDir => Path.Combine(TrailerRoot, "captures", "frames");
    private static string Ffmpeg => Path.Combine(TrailerRoot, "node_modules", "@remotion", "compositor-win32-x64-msvc", "ffmpeg.exe");

    public static string LastEncode = "";

    // Appelé (via RunCommand) une fois en Play Mode.
    public static string Run(string shot, bool exitWhenDone = false)
    {
        if (!EditorApplication.isPlaying) return "Pas en Play Mode";
        if (!TrailerShotDriver.Exists(shot)) return "Plan inconnu : " + shot;
        Vector2Int res = TrailerShotDriver.Resolution(shot);
        string size = SetGameViewSize(res.x, res.y);
        TrailerShotDriver.FramesRoot = FramesDir;
        TrailerShotDriver.OnShotCaptured = (s, dir, n) =>
        {
            Encode(s, dir, n);
            if (exitWhenDone) EditorApplication.delayCall += () => EditorApplication.ExitPlaymode();
        };
        TrailerShotDriver.Launch(shot);
        return $"Plan lancé : {shot} (Game View {size})";
    }

    // ffmpeg tourne dans un cmd indépendant (survit au rechargement de domaine de l'entrée en Play suivante) et
    // supprime lui-même le dossier d'images une fois le MP4 écrit avec succès.
    private static void Encode(string shot, string framesDir, int frames)
    {
        Directory.CreateDirectory(ShotsDir);
        string output = Path.Combine(ShotsDir, shot + ".mp4");
        string ff = $"\"{Ffmpeg}\" -y -v error -framerate {TrailerShotDriver.Fps} -i \"{Path.Combine(framesDir, "f_%05d.jpg")}\" " +
                    $"-vf scale={TrailerShotDriver.Resolution(shot).x}:{TrailerShotDriver.Resolution(shot).y}:flags=lanczos -c:v libx264 -preset slow -crf 15 -pix_fmt yuv420p -movflags +faststart \"{output}\"";
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{ff} && rmdir /s /q \"{framesDir}\"\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process.Start(psi);
        LastEncode = $"encodage lancé {shot} ({frames} images)";
        TrailerShotDriver.Status = "done " + shot;
    }

    // Game View en résolution fixe (taille personnalisée "Trailer LxH" ajoutée une seule fois par résolution).
    public static string SetGameViewSize(int w, int h)
    {
        try
        {
            Assembly asm = typeof(Editor).Assembly;
            Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)GameViewSizeGroupType.Standalone });
            Type groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);

            int index = -1;
            for (int i = 0; i < total; i++)
            {
                object s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                Type st = s.GetType();
                if ((int)st.GetProperty("width").GetValue(s) == w && (int)st.GetProperty("height").GetValue(s) == h &&
                    st.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution") { index = i; break; }
            }
            if (index < 0)
            {
                Type sizeType = asm.GetType("UnityEditor.GameViewSize");
                Type kindType = asm.GetType("UnityEditor.GameViewSizeType");
                object newSize = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) })
                    .Invoke(new object[] { Enum.Parse(kindType, "FixedResolution"), w, h, $"Trailer {w}x{h}" });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                index = total;
            }

            Type gvType = asm.GetType("UnityEditor.GameView");
            EditorWindow gv = EditorWindow.GetWindow(gvType, false, null, false);
            PropertyInfo sel = gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sel != null) sel.SetValue(gv, index);
            else gvType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Invoke(gv, new object[] { index, null });
            gv.Repaint();
            return $"{w}x{h} (index {index})";
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Trailer] Impossible de régler la Game View : " + e.Message);
            return "inchangée";
        }
    }
}
