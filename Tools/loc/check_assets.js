// Contrôle les textes stockés dans les assets Unity (ScriptableObjects, prefabs, scènes) hors TMP m_text :
// tout champ sérialisé "_nom: valeur" dont la valeur ressemble à une phrase (lettres + espace ou accent) et qui n'est
// pas une clé de la table anglaise est listé, pour vérifier à la main qu'il n'est jamais affiché tel quel.
// Usage : node Tools/loc/check_assets.js
const fs = require("fs"), path = require("path");
const { decode } = require("./yaml_decode");
const proj = path.resolve(__dirname, "../..");
const unesc = (s) => s.replace(/\\(.)/g, (_, c) => ({ n: "\n", t: "\t", '"': '"', "\\": "\\" }[c] ?? c));
const keys = new Set();
for (const m of fs.readFileSync(path.join(proj, "Assets/Game/Scripts/Localization/LocTableEN.cs"), "utf8").matchAll(/\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"(?:[^"\\]|\\.)*"\s*\}/g)) keys.add(unesc(m[1]));
const files = [];
(function walk(d) { for (const f of fs.readdirSync(d)) { const p = path.join(d, f); if (fs.statSync(p).isDirectory()) walk(p); else if (/\.(asset|prefab|unity)$/.test(p)) files.push(p); } })(path.join(proj, "Assets/Game"));
const skipField = /^(m_Name|m_text|m_TagString|m_Script|guid|m_EditorClassIdentifier|propertyPath|m_SortingLayer|m_Material|m_FontData|_french)$/;
const seen = new Map();
for (const f of files) {
  const src = fs.readFileSync(f, "utf8");
  for (const m of src.matchAll(/\n\s+-?\s*([A-Za-z_][A-Za-z0-9_]*): ((?:"[^\n]*|'[^\n]*|[^\n{[]*[A-Za-zÀ-ÿ][^\n]*)(?:\r?\n {6,}[^\n:]*)*)/g)) {
    if (skipField.test(m[1])) continue;
    const v = decode(m[2]);
    if (!/[A-Za-zÀ-ÿ]{2}/.test(v) || !/[ àâçéèêëîïôûùüÿœÉÈÀÇ]/.test(v) || v.length > 400) continue;
    if (keys.has(v)) continue;
    const k = m[1] + " = " + JSON.stringify(v);
    if (!seen.has(k)) seen.set(k, new Set());
    seen.get(k).add(path.basename(f));
  }
}
for (const [k, fl] of seen) console.log(k + "    [" + [...fl].slice(0, 3).join(", ") + "]");
console.log(seen.size + " valeur(s) hors table.");
