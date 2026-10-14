// Applique une liste de remplacements EXACTS (ancien -> nouveau) à des fichiers. Échoue si un ancien texte est absent
// (rien n'est écrit pour ce fichier). Respecte les fins de ligne du fichier (CRLF/LF).
// Usage : node Tools/loc/apply.js edits.json
// edits.json : { "chemin/fichier.cs": [ ["ancien", "nouveau"], ... ], ... }   (chemins relatifs à la racine du projet)
const fs = require("fs"), path = require("path");
const edits = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
let failed = false;
for (const [rel, pairs] of Object.entries(edits)) {
  const file = path.resolve(rel);
  let src = fs.readFileSync(file, "utf8");
  const crlf = src.includes("\r\n");
  if (crlf) src = src.replace(/\r\n/g, "\n");
  let ok = true, count = 0;
  for (const [from, to] of pairs) {
    const idx = src.indexOf(from);
    if (idx < 0) { console.error(`ABSENT dans ${rel} : ${from.slice(0, 90)}`); ok = false; continue; }
    if (src.indexOf(from, idx + from.length) >= 0) { console.error(`MULTIPLE dans ${rel} : ${from.slice(0, 90)}`); ok = false; continue; }
    src = src.replace(from, () => to);
    count++;
  }
  if (!ok) { failed = true; console.error(`-> ${rel} NON modifié`); continue; }
  if (crlf) src = src.replace(/\n/g, "\r\n");
  fs.writeFileSync(file, src);
  console.log(`${rel} : ${count} remplacements`);
}
process.exit(failed ? 1 : 0);
