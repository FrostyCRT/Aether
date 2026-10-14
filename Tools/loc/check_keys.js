// Vérifie que chaque texte littéral passé à Loc.T / Loc.F / Loc.Plural / GameModes.Plural dans le code existe dans la
// table anglaise (LocTableEN.cs). Affiche les clés manquantes et les clés de la table jamais utilisées (info).
// Usage : node Tools/loc/check_keys.js
const fs = require("fs"), path = require("path");
const root = path.resolve(__dirname, "../../Assets/Game/Scripts");
const tableSrc = fs.readFileSync(path.join(root, "Localization/LocTableEN.cs"), "utf8");

// Décodage d'un littéral C# "..." (échappements simples)
const unesc = (s) => s.replace(/\\(.)/g, (_, c) => ({ n: "\n", t: "\t", '"': '"', "\\": "\\" }[c] ?? c));
const keys = new Set();
for (const m of tableSrc.matchAll(/\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"(?:[^"\\]|\\.)*"\s*\}/g)) keys.add(unesc(m[1]));

const files = [];
(function walk(d) { for (const f of fs.readdirSync(d)) { const p = path.join(d, f); if (fs.statSync(p).isDirectory()) walk(p); else if (p.endsWith(".cs")) files.push(p); } })(root);

const missing = [], used = new Set();
const callRe = /(?:Loc\.(?:T|F)|GameModes\.Plural\([^,]+,|Loc\.Plural\([^,]+,)\s*\(?\s*((?:"(?:[^"\\]|\\.)*"\s*(?:\?|:|,|\))?\s*)+)/g;
for (const f of files) {
  if (f.includes("Localization")) continue;
  const src = fs.readFileSync(f, "utf8");
  for (const m of src.matchAll(/Loc\.(T|F)\(\s*((?:[^()"]|"(?:[^"\\]|\\.)*"|\([^()]*\))*)\)/g)) {
    // tous les littéraux du PREMIER argument (y compris dans un ternaire "a" : "b")
    const arg = m[2];
    let depth = 0, first = "";
    for (let i = 0, inS = false; i < arg.length; i++) {
      const c = arg[i];
      if (c === '"' && arg[i - 1] !== "\\") inS = !inS;
      if (!inS && (c === "(" )) depth++;
      if (!inS && (c === ")" )) depth--;
      if (!inS && depth === 0 && c === "," && m[1] === "F") break;
      first += c;
    }
    for (const s of first.matchAll(/"((?:[^"\\]|\\.)*)"/g)) {
      const k = unesc(s[1]);
      if (!/[A-Za-zÀ-ÿ]{2}/.test(k)) continue;
      used.add(k);
      if (!keys.has(k)) missing.push(`${path.relative(root, f)} : "${k.replace(/\n/g, "\\n")}"`);
    }
  }
  for (const m of src.matchAll(/(?:GameModes|Loc)\.Plural\([^,]+,\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"/g))
    for (const k of [unesc(m[1]), unesc(m[2])]) { used.add(k); if (!keys.has(k)) missing.push(`${path.relative(root, f)} : "${k}" (Plural)`); }
}
console.log(`Table : ${keys.size} clés. Littéraux Loc dans le code : ${used.size}.`);
console.log(missing.length ? "MANQUANTES :\n" + [...new Set(missing)].join("\n") : "Aucune clé manquante.");
