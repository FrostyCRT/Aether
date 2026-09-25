using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Légende des touches en jeu (AJOUTÉ 2026-09-24) : colonne à gauche de l'écran, une ligne par action utile en partie
// (Dash, Ultime, Clone, Orbites, Pause) — le déplacement est exclu. Chaque ligne = pastille de touche (cadre du HUD)
// + nom de l'action. Les touches affichées sont celles réellement configurées (InputBindings) et se mettent à jour
// quand le joueur les change ; le tout peut être masqué dans Paramètres > Interface (GameSettings.ShowKeyLegend).
// Créé par HudStyler (police et matériau Bangers repris du chrono). Ignoré par les layouts et les raycasts.
public class HudKeyLegend : MonoBehaviour
{
    private sealed class Row
    {
        public GameAction[] actions;
        public string label;
        public TextMeshProUGUI keyText;
        public RectTransform cap;
        public RectTransform root;
        public Func<bool> available;   // null = toujours affichée
    }

    private readonly List<Row> _rows = new List<Row>();
    private TextMeshProUGUI _fontRef;
    private RectTransform _rt;        // racine (toujours active : écoute les réglages)
    private RectTransform _content;   // contenu affiché / masqué selon le réglage

    // Marges / tailles (unités du canvas 1920×1080)
    private const float LeftMargin = 26f, RowHeight = 48f, RowGap = 10f, CapMinWidth = 56f, CapPad = 20f, LabelGap = 12f;
    private static readonly Color CapFill = new Color(0.05f, 0.06f, 0.08f, 0.78f);
    private static readonly Color CapFrame = new Color(1f, 1f, 1f, 1f);
    private static readonly Color KeyColor = new Color32(0xF5, 0xC1, 0x4A, 255);    // or, comme le texte de l'or
    private static readonly Color LabelColor = new Color(0.96f, 0.89f, 0.72f, 1f);  // ivoire, comme les légendes du HUD

    public static HudKeyLegend Create(Transform hudParent, TextMeshProUGUI fontRef)
    {
        var go = new GameObject("KeyLegend", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(hudParent, false);
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(LeftMargin, -60f);
        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var legend = go.AddComponent<HudKeyLegend>();
        legend._rt = rt;
        legend._fontRef = fontRef;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        legend._content = (RectTransform)contentGo.transform;
        legend._content.SetParent(rt, false);
        legend._content.anchorMin = Vector2.zero;
        legend._content.anchorMax = Vector2.one;
        legend._content.offsetMin = legend._content.offsetMax = Vector2.zero;

        legend.Build();
        legend.ApplyVisibility();
        return legend;
    }

    private void Build()
    {
        AddRow("Dash", null, GameAction.Dash);
        AddRow("Ultime", null, GameAction.Ultimate);
        // Clone : seulement si la branche Fantôme (Lyra) est active ET la compétence achetée
        AddRow("Clone", () => MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.HasPhantomDash(), GameAction.PhantomClone);
        // Orbites : seulement une fois l'arme orbitale obtenue (carte de montée de niveau choisie)
        AddRow("Orbites", HasOrbital, GameAction.OrbitShrink, GameAction.OrbitGrow);
        AddRow("Pause", null, GameAction.Pause);

        Refresh();
        Relayout();
    }

    private void AddRow(string label, Func<bool> available, params GameAction[] actions)
    {
        var rowGo = new GameObject("Row_" + label, typeof(RectTransform));
        var rowRt = (RectTransform)rowGo.transform;
        rowRt.SetParent(_content, false);
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0f, 1f);
        rowRt.pivot = new Vector2(0f, 1f);
        rowRt.sizeDelta = new Vector2(300f, RowHeight);

        // pastille de touche : fond sombre + cadre bronze du HUD
        var capGo = new GameObject("Key", typeof(RectTransform));
        var capRt = (RectTransform)capGo.transform;
        capRt.SetParent(rowRt, false);
        capRt.anchorMin = capRt.anchorMax = new Vector2(0f, 0.5f);
        capRt.pivot = new Vector2(0f, 0.5f);
        capRt.anchoredPosition = Vector2.zero;
        capRt.sizeDelta = new Vector2(CapMinWidth, RowHeight);

        var fill = capGo.AddComponent<Image>();
        fill.sprite = HudSprites.Pill;
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 1.5f;
        fill.color = CapFill;
        fill.raycastTarget = false;

        var frameGo = new GameObject("Frame", typeof(RectTransform));
        var frameRt = (RectTransform)frameGo.transform;
        frameRt.SetParent(capRt, false);
        frameRt.anchorMin = Vector2.zero;
        frameRt.anchorMax = Vector2.one;
        frameRt.offsetMin = frameRt.offsetMax = Vector2.zero;
        var frame = frameGo.AddComponent<Image>();
        frame.sprite = HudSprites.PillOutline;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.5f;
        frame.color = CapFrame;
        frame.raycastTarget = false;

        var keyText = NewText("KeyText", capRt, 29f, KeyColor, TextAlignmentOptions.Center, 0.28f);
        var keyRt = keyText.rectTransform;
        keyRt.anchorMin = Vector2.zero;
        keyRt.anchorMax = Vector2.one;
        keyRt.offsetMin = new Vector2(4f, 0f);
        keyRt.offsetMax = new Vector2(-4f, 0f);

        // nom de l'action, à droite de la pastille (positionné dans Refresh, selon la largeur de la pastille)
        var labelText = NewText("Label", rowRt, 29f, LabelColor, TextAlignmentOptions.MidlineLeft, 0.32f);
        labelText.text = label;
        labelText.characterSpacing = 2f;
        var lrt = labelText.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.sizeDelta = new Vector2(200f, RowHeight);

        _rows.Add(new Row { actions = actions, label = label, keyText = keyText, cap = capRt, root = rowRt, available = available });
    }

    private TextMeshProUGUI NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions align, float outline)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_fontRef != null)
        {
            t.font = _fontRef.font;
            t.fontSharedMaterial = HudBar.OutlinedMaterial(_fontRef.fontSharedMaterial, outline, new Color32(14, 9, 5, 255));
        }
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private Transform _player;
    private float _pollTimer;
    private int _lastMask = -1;

    private bool HasOrbital()
    {
        if (_player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
        }
        return _player != null && _player.GetComponent<WeaponOrbital>() != null;
    }

    private void Update()
    {
        _pollTimer -= Time.unscaledDeltaTime;
        if (_pollTimer > 0f) return;
        _pollTimer = 0.25f;
        Relayout();
    }

    // Affiche seulement les lignes disponibles et les empile sans trou.
    private void Relayout()
    {
        int mask = 0, shown = 0;
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i].available == null || _rows[i].available()) mask |= 1 << i;
        if (mask == _lastMask) return;
        _lastMask = mask;

        for (int i = 0; i < _rows.Count; i++)
        {
            bool on = (mask & (1 << i)) != 0;
            _rows[i].root.gameObject.SetActive(on);
            if (!on) continue;
            _rows[i].root.anchoredPosition = new Vector2(0f, -shown * (RowHeight + RowGap));
            shown++;
        }
        _rt.sizeDelta = new Vector2(300f, Mathf.Max(0f, shown * (RowHeight + RowGap) - RowGap));
    }

    // Réécrit les touches affichées (et ajuste la largeur des pastilles) d'après les commandes actuelles.
    private void Refresh()
    {
        foreach (var r in _rows)
        {
            string txt = "";
            foreach (var a in r.actions)
            {
                string k = GameInput.ShortBindingLabel(a);
                if (string.IsNullOrEmpty(k)) continue;
                txt += (txt.Length > 0 ? " / " : "") + k;
            }
            if (txt.Length == 0) txt = "—";
            r.keyText.text = txt;

            float w = Mathf.Max(CapMinWidth, r.keyText.GetPreferredValues(txt).x + CapPad * 2f);
            r.cap.sizeDelta = new Vector2(w, RowHeight);

            var label = r.cap.parent.Find("Label") as RectTransform;
            if (label != null) label.anchoredPosition = new Vector2(w + LabelGap, 0f);
        }
    }

    private void ApplyVisibility()
    {
        if (_content != null) _content.gameObject.SetActive(GameSettings.GetBool(GameSettings.ShowKeyLegend));
    }

    private void OnEnable()
    {
        InputBindings.Changed += OnBindingChanged;
        GameSettings.Changed += OnSettingChanged;
        ApplyVisibility();
        Refresh();
    }

    private void OnDisable()
    {
        InputBindings.Changed -= OnBindingChanged;
        GameSettings.Changed -= OnSettingChanged;
    }

    private void OnSettingChanged(string key)
    {
        if (key == GameSettings.ShowKeyLegend) ApplyVisibility();
    }

    private void OnBindingChanged(GameAction a) => Refresh();
}
