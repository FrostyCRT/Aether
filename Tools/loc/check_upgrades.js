// Vérifie que le nom et la description de chaque carte d'amélioration (.asset avec upgradeName) sont des clés de la
// table anglaise (donc traduits dans toutes les langues). Usage : node Tools/loc/check_upgrades.js
const fs = require("fs"), path = require("path"), { fields } = require("./yaml_decode");
const proj = path.resolve(__dirname, "../..");
const unesc = (s) => s.replace(/\\(.)/g, (_, c) => ({ n: "\n", t: "\t", '"': '"', "\\": "\\" }[c] ?? c));
const keys = new Set();
for (const m of fs.readFileSync(path.join(proj, "Assets/Game/Scripts/Localization/LocTableEN.cs"), "utf8").matchAll(/\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"(?:[^"\\]|\\.)*"\s*\}/g)) keys.add(unesc(m[1]));
let n = 0, miss = 0;
(function walk(d) {
  for (const f of fs.readdirSync(d)) {
    const p = path.join(d, f);
    if (fs.statSync(p).isDirectory()) { walk(p); continue; }
    if (!p.endsWith(".asset")) continue;
    const src = fs.readFileSync(p, "utf8");
    if (!src.includes("upgradeName:")) continue;
    for (const r of fields(src, ["upgradeName", "description"])) {
      n++;
      if (r.value && !keys.has(r.value)) { miss++; console.log("MANQUE " + f + " " + r.field + " " + JSON.stringify(r.value)); }
    }
  }
})(path.join(proj, "Assets/Game"));
console.log(`${n} textes de cartes contrôlés, ${miss} sans traduction.`);
