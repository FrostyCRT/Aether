// Extrait les textes TextMeshPro / UI Text (m_text) des scènes et prefabs, et les champs texte des ScriptableObjects.
// Usage : node Tools/loc/extract_yaml.js Assets > out.tsv   (fichier \t texte décodé)
const fs = require("fs"), path = require("path");
const root = process.argv[2];
const files = [];
(function walk(d) {
  for (const f of fs.readdirSync(d)) {
    const p = path.join(d, f);
    if (fs.statSync(p).isDirectory()) { if (!/TextMesh Pro|TrailerCapture|Plugins|Samples/.test(p)) walk(p); }
    else if (/\.(unity|prefab|asset)$/.test(p)) files.push(p);
  }
})(root);

// Décode une valeur scalaire YAML (simple, double-quotée avec \uXXXX, ou repliée sur plusieurs lignes).
function decode(raw) {
  raw = raw.trim();
  if (raw.startsWith('"')) {
    let s = raw.slice(1, raw.lastIndexOf('"'));
    s = s.replace(/\\\r?\n\s*/g, "").replace(/\r?\n\s*/g, " ");
    return s.replace(/\\u([0-9A-Fa-f]{4})/g, (_, h) => String.fromCharCode(parseInt(h, 16)))
            .replace(/\\n/g, "\n").replace(/\\t/g, "\t").replace(/\\"/g, '"').replace(/\\\\/g, "\\");
  }
  if (raw.startsWith("'")) return raw.slice(1, raw.lastIndexOf("'")).replace(/''/g, "'").replace(/\r?\n\s*/g, " ");
  return raw.replace(/\r?\n\s*/g, " ");
}

const fields = /^(\s*)(m_text|upgradeName|description|displayName|skinName|title|label|nomPhase|nom|text|epithet|subtitle):\s?(.*)$/;
const out = [];
for (const f of files) {
  const lines = fs.readFileSync(f, "utf8").split("\n");
  for (let i = 0; i < lines.length; i++) {
    const m = fields.exec(lines[i]);
    if (!m) continue;
    let raw = m[3];
    const indent = m[1].length;
    // valeur sur plusieurs lignes (continuation plus indentée)
    while (i + 1 < lines.length && /^\s+/.test(lines[i + 1]) && lines[i + 1].search(/\S/) > indent && !/^\s*[A-Za-z_]+:\s/.test(lines[i + 1])) {
      raw += "\n" + lines[++i];
    }
    const v = decode(raw);
    if (!/[A-Za-zÀ-ÿ]{2}/.test(v)) continue;
    out.push([path.relative(root, f).replace(/\\/g, "/"), m[2], v.replace(/\n/g, "\\n")].join("\t"));
  }
}
console.log(out.join("\n"));
