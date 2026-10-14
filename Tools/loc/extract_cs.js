// Extrait les chaînes littérales des scripts C# susceptibles d'être affichées au joueur.
// Ignore : commentaires, Debug.Log*, attributs ([Tooltip], [Header], [ContextMenu]...), identifiants évidents.
// Usage : node Tools/loc/extract_cs.js Assets/Game/Scripts > out.tsv   (fichier \t ligne \t chaîne)
const fs = require("fs"), path = require("path");
const root = process.argv[2];
const files = [];
(function walk(d) {
  for (const f of fs.readdirSync(d)) {
    const p = path.join(d, f);
    if (fs.statSync(p).isDirectory()) walk(p);
    else if (p.endsWith(".cs")) files.push(p);
  }
})(root);

const out = [];
const strRe = /\$?@?"((?:[^"\\]|\\.)*)"/g;
for (const f of files) {
  const src = fs.readFileSync(f, "utf8");
  const noBlock = src.replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, " "));
  noBlock.split("\n").forEach((line, i) => {
    // retire le commentaire de fin de ligne (hors chaînes)
    let inStr = false, code = "";
    for (let k = 0; k < line.length; k++) {
      const c = line[k];
      if (c === '"' && line[k - 1] !== "\\") inStr = !inStr;
      if (!inStr && c === "/" && line[k + 1] === "/") break;
      code += c;
    }
    if (/^\s*\[(Tooltip|Header|ContextMenu|MenuItem|Range|Space|TextArea|CreateAssetMenu|RequireComponent|InitializeOnLoad|DefaultExecutionOrder)/.test(code)) return;
    if (/Debug\.(Log|LogWarning|LogError|LogException|Assert)|throw new|Shader\.Find|Resources\.Load|PlayerPrefs\.|\.Find\(|FindWithTag|CompareTag|nameof|SetTrigger|Shader\.PropertyToID|LayerMask|\.tag\b/.test(code)) return;
    let m;
    strRe.lastIndex = 0;
    while ((m = strRe.exec(code))) {
      const s = m[1];
      if (!/[A-Za-zÀ-ÿ]{2}/.test(s)) continue;
      if (/^[A-Za-z0-9_.\/\-]+$/.test(s) && s === s.toLowerCase() && s.length < 40) continue;
      if (/^(_[A-Za-z0-9]+|[A-Z][a-zA-Z0-9]*(_[A-Za-z0-9]+)+)$/.test(s)) continue;
      out.push([path.relative(root, f).replace(/\\/g, "/"), i + 1, (m[0].startsWith("$") ? "$" : "") + s].join("\t"));
    }
  });
}
console.log(out.join("\n"));
