using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-11-14, retour utilisateur : « le vent, on ne le voit pas, je veux qu'on le voie réellement, comme un dessin
// animé, genre on voit le blanc du vent léger en traits ») - un trait de vent façon dessin animé pour ForestAmbienceUI : un
// ruban blanc qui suit un tracé (ondulation douce terminée par une petite boucle) et dont seule la portion [Tail, Head] est
// dessinée. Head avance puis Tail le rattrape : le trait se dessine puis s'efface, comme un coup de pinceau. Ruban effilé
// aux deux bouts, bords adoucis (pas de crénelage sur un trait fin). Maillage reconstruit seulement quand Head / Tail changent.
[RequireComponent(typeof(CanvasRenderer))]
public class WindStreakGraphic : MaskableGraphic
{
    private Vector2[] _path = new Vector2[0];
    private float[] _lengths = new float[0];   // longueur cumulée le long du tracé
    private float _head, _tail;
    private float _width = 4f;
    private const int Samples = 56;

    public void SetPath(Vector2[] points, float width)
    {
        _path = points;
        _width = width;
        _lengths = new float[points.Length];
        for (int i = 1; i < points.Length; i++) _lengths[i] = _lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        SetVerticesDirty();
    }

    public void SetWindow(float tail, float head)
    {
        tail = Mathf.Clamp01(tail);
        head = Mathf.Clamp01(head);
        if (Mathf.Approximately(tail, _tail) && Mathf.Approximately(head, _head)) return;
        _tail = tail;
        _head = head;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_path.Length < 2 || _head - _tail < 0.005f) return;
        float total = _lengths[_lengths.Length - 1];
        Color32 c = color;
        for (int i = 0; i <= Samples; i++)
        {
            float u = (float)i / Samples;                         // position dans la portion visible
            float s = Mathf.Lerp(_tail, _head, u) * total;        // abscisse le long du tracé
            Vector2 p = PointAt(s, out Vector2 dir);
            Vector2 n = new Vector2(-dir.y, dir.x);
            // Max(0) : Sin(PI) vaut un tout petit négatif (-8,7e-8), et Pow(négatif, 0,7) = NaN -> « Invalid AABB » à chaque image
            float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)), 0.7f);   // effilé aux deux bouts, comme un trait de pinceau
            float w = _width * 0.5f * taper + 1f;                    // + 1 px de fondu même aux pointes
            float core = w * 0.45f;
            Color32 vc = c;
            vc.a = (byte)(c.a * Mathf.Clamp01(taper * 1.6f));
            Color32 edge = vc;
            edge.a = 0;
            vh.AddVert(p + n * w, edge, Vector2.zero);
            vh.AddVert(p + n * core, vc, Vector2.zero);
            vh.AddVert(p - n * core, vc, Vector2.zero);
            vh.AddVert(p - n * w, edge, Vector2.zero);
            if (i > 0)
            {
                int k = i * 4, j = k - 4;
                for (int band = 0; band < 3; band++)
                {
                    vh.AddTriangle(j + band, j + band + 1, k + band);
                    vh.AddTriangle(j + band + 1, k + band + 1, k + band);
                }
            }
        }
    }

    private Vector2 PointAt(float s, out Vector2 dir)
    {
        int i = 1;
        while (i < _lengths.Length - 1 && _lengths[i] < s) i++;
        float segment = Mathf.Max(0.0001f, _lengths[i] - _lengths[i - 1]);
        float t = Mathf.Clamp01((s - _lengths[i - 1]) / segment);
        dir = (_path[i] - _path[i - 1]).normalized;
        return Vector2.Lerp(_path[i - 1], _path[i], t);
    }
}
