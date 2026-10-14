// Outils multi-langues. La table ANGLAISE (LocTableEN.cs) fait référence pour la liste et l'ordre des clés.
//
//   node Tools/loc/langs.js skeleton            -> Tools/loc/skeleton.txt : "id <TAB> français <TAB> anglais" (+ titres de sections)
//   node Tools/loc/langs.js build de            -> lit Tools/loc/tr_de.txt ("id <TAB> traduction", \n = retour à la ligne)
//                                                  et écrit Assets/Game/Scripts/Localization/LocTableDE.cs
//   node Tools/loc/langs.js check               -> contrôle chaque table LocTableXX.cs contre l'anglaise :
//                                                  clés manquantes / en trop, trous {0} différents, balises <...>,
//                                                  nombre de retours à la ligne, traductions identiques au français.
// Clés supplémentaires propres à une langue (ex. polonais "victoires#few") : lignes "+ clé| traduction" dans tr_xx.txt.
const fs = require("fs"), path = require("path");
const proj = path.resolve(__dirname, "../..");
const locDir = path.join(proj, "Assets/Game/Scripts/Localization");
const unesc = (s) => s.replace(/\\(.)/g, (_, c) => ({ n: "\n", t: "\t", '"': '"', "\\": "\\" }[c] ?? c));
const esc = (s) => s.replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\n/g, "\\n");
const show = (s) => s.replace(/\n/g, "\\n");

const LANGS = {
  de: { cls: "LocTableDE", name: "ALLEMANDE (Deutsch)" },
  es: { cls: "LocTableES", name: "ESPAGNOLE (Español)" },
  pt: { cls: "LocTablePT", name: "PORTUGAISE - Brésil (Português do Brasil)" },
  it: { cls: "LocTableIT", name: "ITALIENNE (Italiano)" },
  pl: { cls: "LocTablePL", name: "POLONAISE (Polski)" },
};

// Entrées de la table anglaise, dans l'ordre, avec les titres de sections (lignes de commentaire "// ====" ou "// ----").
function readEN() {
  const src = fs.readFileSync(path.join(locDir, "LocTableEN.cs"), "utf8");
  const items = [];
  for (const line of src.split(/\r?\n/)) {
    const sec = line.match(/^\s*\/\/ (?:=+|-+) (.*?)(?: - fait)?\s*$/);
    if (sec && !/SECTIONS SUIVANTES/.test(sec[1])) { items.push({ section: sec[1] }); continue; }
    for (const m of line.matchAll(/\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*\}/g))
      items.push({ fr: unesc(m[1]), en: unesc(m[2]) });
  }
  let id = 0;
  for (const it of items) if (it.fr !== undefined) it.id = ++id;
  return items;
}

function readTable(file) {
  const map = new Map();
  if (!fs.existsSync(file)) return null;
  for (const m of fs.readFileSync(file, "utf8").matchAll(/\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*\}/g))
    map.set(unesc(m[1]), unesc(m[2]));
  return map;
}

const holes = (s) => (s.match(/\{[^{}]*\}/g) || []).map((h) => h.replace(/:.*\}/, "}")).sort().join(" ");
const tags = (s) => (s.match(/<[^>]+>/g) || []).sort().join(" ");
const lines = (s) => s.split("\n").length;

const cmd = process.argv[2];
if (cmd === "skeleton") {
  const out = [];
  for (const it of readEN()) {
    if (it.section) out.push(`\n## ${it.section}`);
    else out.push(`${it.id}\t${show(it.fr)}\t${show(it.en)}`);
  }
  fs.writeFileSync(path.join(__dirname, "skeleton.txt"), out.join("\n").trim() + "\n");
  console.log("skeleton.txt écrit (" + readEN().filter((i) => i.id).length + " entrées).");
} else if (cmd === "build") {
  const lang = process.argv[3], L = LANGS[lang];
  if (!L) throw new Error("langue inconnue : " + lang);
  const tr = new Map(), extra = [];
  const raw = fs.readFileSync(path.join(__dirname, `tr_${lang}.txt`), "utf8").split(/\r?\n/);
  raw.forEach((line, i) => {
    if (!line.trim() || line.startsWith("#")) return;
    let m = line.match(/^\+ (.+?)\|\s?(.*)$/);
    if (m) { extra.push([unesc(m[1]), unesc(m[2])]); return; }
    m = line.match(/^(\d+)\|\s?(.*)$/);
    if (!m) throw new Error(`tr_${lang}.txt:${i + 1} : ligne illisible : ${line}`);
    const id = +m[1];
    if (tr.has(id)) throw new Error(`tr_${lang}.txt:${i + 1} : id ${id} en double`);
    tr.set(id, unesc(m[2]));
  });
  const items = readEN(), missing = [];
  const body = [];
  let row = [];
  const flush = () => { if (row.length) body.push("        " + row.join(" ")); row = []; };
  for (const it of items) {
    if (it.section) { flush(); body.push("", `        // ======================================================== ${it.section}`); continue; }
    if (!tr.has(it.id)) { missing.push(it.id); continue; }
    row.push(`{ "${esc(it.fr)}", "${esc(tr.get(it.id))}" },`);
    if (row.join(" ").length > 110) flush();
  }
  flush();
  if (extra.length) {
    body.push("", "        // ======================================================== Formes propres à cette langue");
    for (const [k, v] of extra) body.push(`        { "${esc(k)}", "${esc(v)}" },`);
  }
  for (const id of tr.keys()) if (!items.some((i) => i.id === id)) missing.push("id inconnu " + id);
  if (missing.length) { console.log("ERREUR : ids manquants ou inconnus : " + missing.join(", ")); process.exit(1); }
  const cs = `using System.Collections.Generic;

// AJOUTÉ (2026-10-14) - table de traduction ${L.name}. Clé = texte français exact affiché par le jeu (voir Loc).
// Même liste de clés que LocTableEN (la référence). Générée par Tools/loc/langs.js depuis Tools/loc/tr_${lang}.txt :
// corriger une traduction dans tr_${lang}.txt puis relancer "node Tools/loc/langs.js build ${lang}" (ou éditer ici
// directement ET reporter dans tr_${lang}.txt).
public static class ${L.cls}
{
    public static readonly Dictionary<string, string> Table = new Dictionary<string, string>
    {${body.join("\n")}
    };
}
`;
  fs.writeFileSync(path.join(locDir, L.cls + ".cs"), cs.replace(/\n/g, "\r\n"));
  console.log(`${L.cls}.cs écrit : ${tr.size + extra.length} entrées.`);
} else if (cmd === "check") {
  const en = readEN().filter((i) => i.id);
  let problems = 0;
  for (const [lang, L] of Object.entries(LANGS)) {
    const t = readTable(path.join(locDir, L.cls + ".cs"));
    if (!t) { console.log(`${L.cls} : absente`); continue; }
    const out = [];
    for (const it of en) {
      if (!t.has(it.fr)) { out.push(`MANQUANTE  #${it.id} "${show(it.fr)}"`); continue; }
      const v = t.get(it.fr);
      if (holes(v) !== holes(it.fr)) out.push(`TROUS      #${it.id} "${show(it.fr)}" -> "${show(v)}"`);
      if (tags(v) !== tags(it.fr)) out.push(`BALISES    #${it.id} "${show(v)}"`);
      if (lines(v) !== lines(it.fr)) out.push(`LIGNES     #${it.id} "${show(v)}"`);
      if (v.trim() === "") out.push(`VIDE       #${it.id}`);
    }
    for (const k of t.keys()) if (!en.some((i) => i.fr === k) && !k.endsWith("#few")) out.push(`EN TROP    "${show(k)}"`);
    problems += out.length;
    console.log(`${L.cls} : ${t.size} entrées, ${out.length} problème(s)` + (out.length ? "\n  " + out.join("\n  ") : ""));
  }
  process.exit(problems ? 1 : 0);
} else {
  console.log("usage : node Tools/loc/langs.js skeleton | build <de|es|pt|it|pl> | check");
}
