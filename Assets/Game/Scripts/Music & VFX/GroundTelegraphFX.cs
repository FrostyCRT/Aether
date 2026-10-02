using UnityEngine;

// Signal de danger posé au sol, construit en code (remplace les prefabs de cercle de télégraphe).
//
// Style.Zone : zone de danger à rayon FIXE. Le contour complet est visible dès le début (le joueur voit toute la
//   zone touchée) et le remplissage grandit du centre vers le bord à mesure que SetProgress(0 -> 1) avance : le
//   temps restant se lit d'un coup d'oeil. Un anneau gradué tourne de plus en plus vite, tout pulse de plus en
//   plus fort. convergeTicks = true ajoute un anneau qui se resserre en boucle vers le centre (attaques qui ATTIRENT).
//   Impact() : éclat blanc + fondu (au moment où l'attaque touche).
// Style.Wave : onde dont le rayon est piloté frame par frame (SetRadius). Anneau net + léger halo.
// Dismiss() : fondu rapide puis destruction (attaque annulée / terminée sans impact).
//
// Le temps utilise Time.deltaTime : tout se fige tout seul en pause.
public class GroundTelegraphFX : MonoBehaviour
{
    public enum Style { Zone, Wave }
    private enum State { Alive, Impacting, Dismissing }

    private const float ImpactDuration = 0.3f;
    private const float DismissDuration = 0.15f;
    private const float RingBand = 1f / 0.94f;   // le trait de FXSprites.ThinRing() est à 94 % du rayon du sprite

    private Style _style;
    private State _state = State.Alive;
    private float _radius;
    private float _currentRadius;
    private float _progress;
    private bool _converge;
    private Color _main, _bright, _hot;

    private SpriteRenderer _fill, _ring, _ticks, _convergeRing, _flash;
    private SpriteRenderer[] _all;
    private Color[] _dismissStart;

    private float _t;
    private float _stateT;
    private float _tickAngle;

    // ------------------------------------------------------------------
    // Création / pilotage
    // ------------------------------------------------------------------

    public static GroundTelegraphFX Spawn(Vector3 position, float radius, Color color, Style style, bool convergeTicks = false)
    {
        GameObject go = new GameObject("GroundTelegraphFX");
        go.transform.position = new Vector3(position.x, 0f, position.z);
        GroundTelegraphFX fx = go.AddComponent<GroundTelegraphFX>();
        fx.Init(radius, color, style, convergeTicks);
        return fx;
    }

    private void Init(float radius, Color color, Style style, bool convergeTicks)
    {
        _radius = Mathf.Max(0.3f, radius);
        _currentRadius = 0.05f;
        _style = style;
        _converge = convergeTicks;
        _main = color;
        _bright = Color.Lerp(color, Color.white, 0.45f);
        _hot = Color.Lerp(color, Color.white, 0.85f);

        _fill = FXSprites.MakeRenderer(transform, "Fill", FXSprites.Disc(), 10, 0.04f);
        _ticks = FXSprites.MakeRenderer(transform, "Ticks", FXSprites.TickRing(), 11, 0.045f);
        _ring = FXSprites.MakeRenderer(transform, "Ring", FXSprites.ThinRing(), 12, 0.05f);
        _convergeRing = FXSprites.MakeRenderer(transform, "Converge", FXSprites.ThinRing(), 13, 0.06f);
        _flash = FXSprites.MakeRenderer(transform, "Flash", FXSprites.Glow(), 14, 0.07f);
        _flash.enabled = false;
        _convergeRing.enabled = _converge;

        _all = new SpriteRenderer[] { _fill, _ticks, _ring, _convergeRing, _flash };

        FXSprites.SetFlat(_fill, 0.05f, 0f);
        FXSprites.SetFlat(_ticks, 0.05f, 0f);
        FXSprites.SetFlat(_ring, 0.05f, 0f);
        FXSprites.SetFlat(_convergeRing, 0.05f, 0f);
        FXSprites.SetFlat(_flash, 0.05f, 0f);
    }

    // Style.Zone : avancement de l'attaque (0 = début, 1 = impact).
    public void SetProgress(float p)
    {
        _progress = Mathf.Clamp01(p);
    }

    // Style.Wave : rayon courant de l'onde.
    public void SetRadius(float r)
    {
        _currentRadius = Mathf.Max(0.05f, r);
    }

    // L'attaque touche : éclat puis fondu, puis le signal se détruit tout seul.
    public void Impact()
    {
        if (_state != State.Alive) return;
        _state = State.Impacting;
        _stateT = 0f;
        _flash.enabled = true;
    }

    // L'attaque se termine sans impact visuel particulier : fondu rapide puis destruction.
    public void Dismiss()
    {
        if (_state != State.Alive) return;
        _state = State.Dismissing;
        _stateT = 0f;

        _dismissStart = new Color[_all.Length];
        for (int i = 0; i < _all.Length; i++) _dismissStart[i] = _all[i].color;
    }

    // ------------------------------------------------------------------
    // Boucle
    // ------------------------------------------------------------------

    private void Update()
    {
        float dt = Time.deltaTime;

        if (_state == State.Impacting) { UpdateImpact(dt); return; }
        if (_state == State.Dismissing) { UpdateDismiss(dt); return; }

        _t += dt;
        float appear = Mathf.Clamp01(_t / 0.2f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * Mathf.Lerp(5f, 22f, _progress));

        if (_style == Style.Zone)
        {
            float fillRadius = _radius * FXSprites.EaseOutQuad(_progress);
            FXSprites.SetFlat(_fill, Mathf.Max(0.05f, fillRadius * 2f), 0f);
            _fill.color = FXSprites.A(_main, Mathf.Lerp(0.16f, 0.48f, _progress) * (0.85f + 0.15f * pulse) * appear);

            FXSprites.SetFlat(_ring, _radius * 2f * RingBand, 0f);
            _ring.color = FXSprites.A(_bright, (0.55f + 0.35f * pulse + 0.1f * _progress) * appear);

            _tickAngle += Mathf.Lerp(20f, 110f, _progress) * dt;
            FXSprites.SetFlat(_ticks, _radius * 2f * 1.04f, _tickAngle);
            _ticks.color = FXSprites.A(_hot, (0.30f + 0.45f * _progress) * appear);
        }
        else
        {
            FXSprites.SetFlat(_fill, _currentRadius * 2f, 0f);
            _fill.color = FXSprites.A(_main, 0.12f * appear);

            FXSprites.SetFlat(_ring, _currentRadius * 2f * RingBand, 0f);
            _ring.color = FXSprites.A(_bright, 0.95f * appear);

            _ticks.color = Color.clear;
        }

        if (_converge)
        {
            // Anneau qui se resserre en boucle vers le centre : "ça tire vers l'intérieur".
            float c = Mathf.Repeat(_t * 1.6f, 1f);
            FXSprites.SetFlat(_convergeRing, _radius * 2f * RingBand * Mathf.Lerp(1.5f, 0.2f, c), 0f);
            _convergeRing.color = FXSprites.A(_hot, Mathf.Sin(c * Mathf.PI) * 0.7f * appear);
        }
    }

    private void UpdateImpact(float dt)
    {
        _stateT += dt;
        float q = Mathf.Clamp01(_stateT / ImpactDuration);
        float inv = 1f - q;

        float r = _style == Style.Zone ? _radius : _currentRadius;

        FXSprites.SetFlat(_fill, r * 2f, 0f);
        _fill.color = FXSprites.A(_main, 0.8f * inv);

        FXSprites.SetFlat(_ring, r * 2f * RingBand * (1f + 0.2f * q), 0f);
        _ring.color = FXSprites.A(_hot, inv);

        _ticks.color = FXSprites.A(_hot, 0.6f * inv);
        _convergeRing.color = Color.clear;

        FXSprites.SetFlat(_flash, r * (2f + 1.2f * q), 0f);
        _flash.color = FXSprites.A(_hot, inv * inv);

        if (q >= 1f) Destroy(gameObject);
    }

    private void UpdateDismiss(float dt)
    {
        _stateT += dt;
        float q = Mathf.Clamp01(_stateT / DismissDuration);
        float inv = 1f - q;

        for (int i = 0; i < _all.Length; i++)
        {
            Color c = _dismissStart[i];
            c.a *= inv;
            _all[i].color = c;
        }

        if (q >= 1f) Destroy(gameObject);
    }
}