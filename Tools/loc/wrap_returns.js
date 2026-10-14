// Enveloppe les `return "texte";` d'une plage de lignes dans Loc.T(...).
// Usage : node wrap_returns.js fichier.cs debut fin [--skip "a|b|c"]
// Ne touche pas aux chaînes sans lettres (symboles, "—", "").
const fs = require("fs");
const [file, a, b] = process.argv.slice(2);
const skipIdx = process.argv.indexOf("--skip");
const skip = skipIdx > 0 ? new Set(process.argv[skipIdx + 1].split("|")) : new Set();
const eol = fs.readFileSync(file, "utf8").includes("\r\n") ? "\r\n" : "\n";
const lines = fs.readFileSync(file, "utf8").split(/\r?\n/);
let n = 0;
for (let i = +a - 1; i <= +b - 1 && i < lines.length; i++) {
  lines[i] = lines[i].replace(/return "((?:[^"\\]|\\.)*)";/g, (m, s) => {
    if (!/[A-Za-zÀ-ÿ]{2}/.test(s) || skip.has(s)) return m;
    n++;
    return `return Loc.T("${s}");`;
  });
}
fs.writeFileSync(file, lines.join(eol));
console.log(`${file}: ${n} retours enveloppés`);
