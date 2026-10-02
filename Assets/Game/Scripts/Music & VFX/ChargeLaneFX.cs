using UnityEngine;

// Couloir de charge d'un boss (remplace le simple cube rouge). Construit en code.
//   - tout le couloir est visible dès le début (le joueur voit où la charge va passer) ;
//   - un remplissage avance depuis le boss selon la progression du windup (le temps restant se lit d'un coup d'oeil) ;
//   - deux bords nets, des chevrons qui défilent vers l'avant de plus en plus vite, et un anneau qui marque l'arrivée.
// BossBase l'appelle à chaque frame de windup via UpdateLane(). Le temps utilise Time.deltaTime (figé en pause).
public class ChargeLaneFX : MonoBehaviour
{
    private const int ChevronCount = 6;

    private SpriteRenderer _base, _fill, _edgeLeft, _edgeRight, _endRing;
    private SpriteRenderer[] _chevrons;
    private Color _main, _bright, _hot;
    private float _t;

    public static ChargeLaneFX Spawn(Color color)
    {
        GameObject go = new GameObject("ChargeLaneFX");
        ChargeLaneFX fx = go.AddComponent<ChargeLaneFX>();
        fx.Init(color);
        return fx;
    }

    private void Init(Color color)
    {
        _main = new Color(color.r, color.g, color.b, 1f);
        _bright = Color.Lerp(_main, Color.white, 0.4f);
        _hot = Color.Lerp(_main, Color.white, 0.8f);

        _base = FXSprites.MakeRenderer(transform, "Base", FXSprites.Square(), 10, 0f);
        _fill = FXSprites.MakeRenderer(transform, "Fill", FXSprites.Square(), 11, 0f);
        _edgeLeft = FXSprites.MakeRenderer(transform, "EdgeL", FXSprites.Square(), 12, 0f);
        _edgeRight = FXSprites.MakeRenderer(transform, "EdgeR", FXSprites.Square(), 12, 0f);
        _endRing = FXSprites.MakeRenderer(transform, "EndRing", FXSprites.ThinRing(), 13, 0f);

        _chevrons = new SpriteRenderer[ChevronCount];
        for (int i = 0; i < ChevronCount; i++)
            _chevrons[i] = FXSprites.MakeRenderer(transform, "Chevron", FXSprites.Chevron(), 14, 0f);
    }

    // origin : point de départ au sol (Y = hauteur du télégraphe). dir : direction de la charge. length/width : taille
    // réelle du couloir. progress : avancement du windup (0 -> 1).
    public void UpdateLane(Vector3 origin, Vector3 dir, float length, float width, float progress)
    {
        _t += Time.deltaTime;
        progress = Mathf.Clamp01(progress);

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
        Vector3 right = Vector3.Cross(Vector3.up, dir);

        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * Mathf.Lerp(6f, 24f, progress));

        // Fond du couloir sur toute sa longueur.
        Place(_base, origin + dir * (length * 0.5f), rot, width, length);
        _base.color = FXSprites.A(_main, 0.10f + 0.05f * pulse + 0.06f * progress);

        // Remplissage qui avance depuis le boss.
        float fillLength = Mathf.Max(0.05f, length * progress);
        Place(_fill, origin + dir * (fillLength * 0.5f), rot, width, fillLength);
        _fill.color = FXSprites.A(_main, 0.18f + 0.40f * progress);

        // Bords nets.
        float edgeAlpha = 0.5f + 0.4f * progress;
        Place(_edgeLeft, origin + dir * (length * 0.5f) - right * (width * 0.5f), rot, 0.1f, length);
        Place(_edgeRight, origin + dir * (length * 0.5f) + right * (width * 0.5f), rot, 0.1f, length);
        _edgeLeft.color = FXSprites.A(_bright, edgeAlpha);
        _edgeRight.color = FXSprites.A(_bright, edgeAlpha);

        // Chevrons qui défilent vers l'avant, de plus en plus vite.
        float speed = Mathf.Lerp(0.6f, 2.4f, progress);
        float chevronWidth = width * 0.55f;
        for (int i = 0; i < ChevronCount; i++)
        {
            float phase = Mathf.Repeat(_t * speed + (float)i / ChevronCount, 1f);
            Place(_chevrons[i], origin + dir * (phase * length), rot, chevronWidth, chevronWidth * 0.65f);
            _chevrons[i].color = FXSprites.A(_hot, Mathf.Sin(phase * Mathf.PI) * (0.35f + 0.5f * progress));
        }

        // Anneau d'arrivée.
        Place(_endRing, origin + dir * length, rot, width / 0.94f, width / 0.94f);
        _endRing.color = FXSprites.A(_hot, (0.35f + 0.5f * progress) * (0.8f + 0.2f * pulse));
    }

    private static void Place(SpriteRenderer sr, Vector3 worldPos, Quaternion rot, float sizeX, float sizeY)
    {
        Vector3 b = sr.sprite.bounds.size;
        sr.transform.position = worldPos;
        sr.transform.rotation = rot;
        sr.transform.localScale = new Vector3(sizeX / Mathf.Max(0.001f, b.x), sizeY / Mathf.Max(0.001f, b.y), 1f);
    }
}