using UnityEngine;
using UnityEngine.UI;

// AJOUTÉ (2026-10-29, retour utilisateur : "l'option visée à la souris, il faut aussi la configurer sur manette, on peut
// prendre le joystick de droite, et donc on remet le curseur en jeu") - viseur affiché EN PARTIE quand la visée est
// manuelle (Paramètres > Jeu > Visée) : le curseur du jeu est caché pendant l'action (GameManager.NeedsCursor), on visait
// donc à l'aveugle à la souris.
//   - souris : le viseur suit la souris (les tirs partent vers lui, voir WeaponBase) ;
//   - manette : le viseur tourne autour du héros dans la direction du stick droit (GameInput.PadAimDirection), à
//     quelques mètres devant lui ; stick relâché, il reste dans la dernière direction visée.
// Caché en visée automatique, dans les menus, en pause, sur les panneaux (cartes de niveau, fin de partie).
public class AimReticle : MonoBehaviour
{
    private static AimReticle _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("[AimReticle]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AimReticle>();
    }

    private const float Size = 46f;           // pixels à 1080p
    private const float PadDistance = 4.5f;   // mètres devant le héros (manette)

    private RectTransform _reticle;
    private Image _image;
    private Transform _player;
    private Camera _camera;

    private void Awake()
    {
        GameObject canvasGo = new GameObject("AimReticleCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32005;

        GameObject r = new GameObject("Reticle", typeof(RectTransform));
        r.transform.SetParent(canvasGo.transform, false);
        _reticle = (RectTransform)r.transform;
        _reticle.anchorMin = _reticle.anchorMax = Vector2.zero;
        _reticle.pivot = new Vector2(0.5f, 0.5f);
        _image = r.AddComponent<Image>();
        _image.sprite = MakeSprite(96);
        _image.raycastTarget = false;
        r.SetActive(false);
    }

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        bool show = gm != null && !gm.NeedsCursor && !gm.IsGameOver && !gm.IsPaused && !GameSettings.AutoAimEnabled;
        Vector2 pos = Vector2.zero;
        if (show)
        {
            if (GameInput.UsingGamepad)
            {
                Vector2 dir = GameInput.PadAimDirection;
                if (_player == null) { GameObject p = GameObject.FindWithTag("Player"); _player = p != null ? p.transform : null; }
                if (_camera == null) _camera = Camera.main;
                show = dir.sqrMagnitude > 0f && _player != null && _camera != null;
                if (show)
                {
                    Vector3 world = _player.position + new Vector3(dir.x, 0f, dir.y) * PadDistance;
                    Vector3 screen = _camera.WorldToScreenPoint(world);
                    show = screen.z > 0f;
                    pos = screen;
                }
            }
            else pos = Input.mousePosition;
        }

        if (_reticle.gameObject.activeSelf != show) _reticle.gameObject.SetActive(show);
        if (!show) return;
        _reticle.anchoredPosition = pos;
        _reticle.sizeDelta = Vector2.one * Size * Screen.height / 1080f;
    }

    // Viseur : anneau blanc cerné de sombre, 4 encoches vers l'intérieur et un point central.
    private static Sprite MakeSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        Color32[] px = new Color32[size * size];
        float c = size * 0.5f, ringR = size * 0.34f, w = size * 0.035f, edge = size * 0.03f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float r = p.magnitude;
                float dRing = Mathf.Abs(r - ringR) - w;
                // encoches : segments sur les axes, de l'anneau vers le centre
                float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
                float dTickH = Mathf.Max(ay - w, Mathf.Max(ringR * 0.45f - ax, ax - ringR));
                float dTickV = Mathf.Max(ax - w, Mathf.Max(ringR * 0.45f - ay, ay - ringR));
                float dDot = r - size * 0.045f;
                float d = Mathf.Min(dRing, Mathf.Min(Mathf.Min(dTickH, dTickV), dDot));
                float white = Mathf.Clamp01(0.5f - d);
                float dark = Mathf.Clamp01(0.5f - (d - edge));
                float a = Mathf.Max(white, dark * 0.8f);
                Color col = Color.Lerp(new Color(0.08f, 0.07f, 0.1f), Color.white, a > 0f ? white / a : 0f);
                col.a = a;
                px[y * size + x] = col;
            }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        tex.hideFlags = HideFlags.HideAndDontSave;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
