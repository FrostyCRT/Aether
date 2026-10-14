// Liste les textes TMP (m_text) des scènes / prefabs du jeu absents de la table anglaise.
// Usage : node Tools/loc/scene_texts.js [--all]
const fs = require("fs"), path = require("path");
const { decode } = require("./yaml_decode");
const proj = path.resolve(__dirname, "../..");
const unesc = (s) => s.replace(/\\(.)/g, (_, c) => ({ n: "\n", t: "\t", '"': '"', "\\": "\\" }[c] ?? c));
const keys = new Set();
for (const m of fs.readFileSync(path.join(proj, "Assets/Game/Scripts/Localization/LocTableEN.cs"), "utf8").matchAll(/\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"(?:[^"\\]|\\.)*"\s*\}/g)) keys.add(unesc(m[1]));
const files = [];
(function walk(d) { for (const f of fs.readdirSync(d)) { const p = path.join(d, f); if (fs.statSync(p).isDirectory()) walk(p); else if (/\.(unity|prefab)$/.test(p)) files.push(p); } })(path.join(proj, "Assets/Game"));
const all = process.argv.includes("--all");
const seen = new Map();
for (const f of files) {
  const src = fs.readFileSync(f, "utf8");
  // m_text: valeur (éventuellement repliée sur plusieurs lignes plus indentées)
  for (const m of src.matchAll(/\n  m_text: (.*(?:\r?\n {4,}.*)*)/g)) {
    const t = decode(m[1]);
    if (!/\p{L}{2}/u.test(t)) continue;
    if (!all && keys.has(t)) continue;
    if (!seen.has(t)) seen.set(t, new Set());
    seen.get(t).add(path.basename(f));
  }
}
for (const [t, fl] of seen) console.log(JSON.stringify(t) + "\t" + [...fl].join(","));
console.log(seen.size + " texte(s).");
