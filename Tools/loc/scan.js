// Liste les chaînes littérales "texte" restantes (hors Loc.T/F, attributs, Debug, noms d'objets) de fichiers C#.
// Usage : node Tools/loc/scan.js fichier1.cs [fichier2.cs ...]
const fs = require("fs");
for (const f of process.argv.slice(2)) {
  const src = fs.readFileSync(f, "utf8").replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, " "));
  src.split(/\r?\n/).forEach((l, i) => {
    let inStr = false, code = "";
    for (let k = 0; k < l.length; k++) {
      if (l[k] === '"' && l[k - 1] !== "\\") inStr = !inStr;
      if (!inStr && l[k] === "/" && l[k + 1] === "/") break;
      code += l[k];
    }
    if (/^\s*\[|Debug\.|new GameObject\(|\.Find\(|Find<|FindWithTag|Resources\.Load|Shader\.|PlayerPrefs|nameof|throw new|case "|NewItem\(|NewRect\(|NewImage\(|NewText\(|Sliced\(|MakeBar\(|\.name = |name = "|AddComponent|GetComponent/.test(code)) return;
    const stripped = code.replace(/Loc\.(T|F|Plural)\((?:[^()]|\([^()]*\))*\)/g, "");
    const m = stripped.match(/\$?"(?:[^"\\]|\\.)*[A-Za-zÀ-ÿ]{3}(?:[^"\\]|\\.)*"/g);
    if (m) console.log(f.split(/[\\/]/).pop() + ":" + (i + 1) + "  " + m.join("  |  ").slice(0, 200));
  });
}
