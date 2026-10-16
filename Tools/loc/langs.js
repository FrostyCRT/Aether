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
  ru: { cls: "LocTableRU", name: "RUSSE (Русский)" },
  zh: { cls: "LocTableZH", name: "CHINOISE - simplifié (简体中文)" },
  ja: { cls: "LocTableJA", name: "JAPONAISE (日本語)" },
  ko: { cls: "LocTableKO", name: "CORÉENNE (한국어)" },
};

// Contrôles propres à une écriture (en plus des trous / balises / lignes communs à toutes les langues).
const CJK = /[　-〿぀-ヿ㐀-鿿＀-￯]/;   // ponctuation CJK, kana, idéogrammes, pleine chasse
const HANGUL = /[ᄀ-ᇿ㄰-㆏가-힯]/;

// Coréen : la ponctuation occidentale (. , : ! ? ( ) + — ‘ ’) existe dans Bangers, qui la dessinerait penchée et à sa
// taille à côté du hangeul droit de Noto Sans KR (« : » qui ressemble à « ; », « + » minuscule...). Le coréen s'écrit
// pourtant avec cette ponctuation-là (pas de pleine chasse). Donc tr_ko.txt garde la ponctuation normale, et la table
// générée la remplace par des caractères que Bangers N'A PAS ; Tools/build_language_fonts.py fait pointer ces caractères,
// dans la police coréenne réduite, vers le dessin normal (étroit) du signe d'origine. Résultat : ponctuation coréenne
// dessinée par la police coréenne, à la bonne taille. Ponctuation collée à du latin ou à un nombre ("Esc:", "x1.5",
// "+8%", "15:00") : inchangée (dessinée par Bangers comme le reste du mot).
// Même liste dans build_language_fonts.py (KO_PUNCT) : les modifier ensemble.
const KO_PUNCT = { ".": "．", ",": "，", ":": "：", "!": "！", "?": "？", "(": "（", ")": "）", "+": "＋", "—": "―", "‘": "", "’": "" };
const KO_OPENING = "(‘";
function koPunctuation(v) {
  if (!HANGUL.test(v) && !v.includes("{")) return v;         // ni coréen ni texte inséré (ex. "AZERTY (ZQSD)")
  const out = [...v];
  let inTag = false, inHole = false;
  const opened = [];                                          // décisions des "(" ouvertes : ")" suit la sienne
  const neighbour = (i, step) => {                            // caractère voisin significatif (espaces sautés)
    for (let j = i + step; j >= 0 && j < out.length; j += step) {
      if (out[j] === " ") continue;
      return out[j] === "{" || out[j] === "}" ? "0" : out[j];   // un trou {0} compte comme un nombre ("{0:00}:{1:00}")
    }
    return "";
  };
  for (let i = 0; i < out.length; i++) {
    const c = out[i];
    if (c === "<") inTag = true;
    if (inTag) { if (c === ">") inTag = false; continue; }
    if (c === "{") inHole = true;
    if (inHole) { if (c === "}") inHole = false; continue; }
    if (!(c in KO_PUNCT)) continue;
    let korean;
    if (c === "‘" || c === "’") korean = true;                // guillemets : toujours autour d'un texte coréen
    else if (c === ")") korean = opened.length ? opened.pop() : !/[A-Za-z0-9%]/.test(neighbour(i, -1));
    else if (c === "+" && /[0-9{]/.test(out[i + 1] || "")) korean = false;             // "+8%", "+{0}" : un nombre
    else if (c === "." && /[0-9]/.test(out[i - 1] || "") && /[0-9]/.test(out[i + 1] || "")) korean = false;   // "1.5"
    else korean = !/[A-Za-z0-9%]/.test(KO_OPENING.includes(c) ? neighbour(i, +1) : neighbour(i, -1));   // collée à du latin / un nombre : Bangers
    if (c === "(") opened.push(korean);
    if (korean) out[i] = KO_PUNCT[c];
  }
  return out.join("");
}
function scriptChecks(lang, v) {
  const out = [];
  const plain = v.replace(/<[^>]+>/g, " ").replace(/\{[^{}]*\}/g, " ");
  if (lang === "ru") {
    // mot mêlant lettres latines et cyrilliques (ex. "o" latin tapé dans un mot russe : invisible à l'œil, mal rendu)
    for (const w of plain.split(/[^A-Za-zЀ-ӿ]+/))
      if (/[A-Za-z]/.test(w) && /[Ѐ-ӿ]/.test(w)) out.push(`MÉLANGE latin/cyrillique dans "${w}"`);
    if (/"/.test(plain)) out.push("guillemets droits (le russe utilise « »)");
  }
  if (lang === "ko") {
    // particule variable (을/를, 이/가, 은/는, 와/과, 으로/로) collée à un trou {0} : sa forme dépend du mot inséré
    for (const m of v.matchAll(/\}['’]?(을|를|이|가|은|는|와|과|으로|로)(?![가-힯])/g))
      out.push(`particule variable "${m[1]}" juste après un trou {…} (dépend de la dernière lettre du mot inséré)`);
    // caractères chinois / japonais égarés dans une table coréenne
    if (/[぀-ヿ㐀-鿿]/.test(plain)) out.push("caractère chinois ou japonais dans le coréen");
    // ponctuation que Bangers dessinerait à côté du hangeul (table éditée à la main au lieu de "build ko" ?)
    if (koPunctuation(v) !== v) out.push('ponctuation dessinée par Bangers à côté du coréen (relancer "langs.js build ko")');
  }
  if (lang === "ja" && HANGUL.test(plain)) out.push("caractère coréen dans le japonais");
  if (lang === "zh" || lang === "ja") {
    // ponctuation occidentale collée à un caractère chinois (attendu : pleine chasse ，。：！？（）)
    for (let i = 0; i < plain.length; i++) {
      if (!/[,.:;!?()]/.test(plain[i]) || plain.slice(i - 2, i + 1) === "Lv.") continue;   // "Lv.12" : abréviation voulue
      if (CJK.test(plain[i - 1] || "") || CJK.test(plain[i + 1] || ""))
        out.push(`ponctuation occidentale "${plain[i]}" à côté du chinois (position ${i})`);
    }
    // signes que Bangers possède : il les dessinerait à la place de la police chinoise (tiret court, points en bas)
    for (const ch of ["—", "…", "“", "”", "..."]) if (plain.includes(ch)) out.push(`"${ch}" dessiné par Bangers (utiliser ，「」⋯⋯ ou des parenthèses)`);
    // (les trous {0} comptent comme des chiffres : "范围 +{0}" est un nombre, pas un « + » entre deux mots)
    const withNumbers = v.replace(/<[^>]+>/g, " ").replace(/\{[^{}]*\}/g, "0");
    if (new RegExp(CJK.source + " ?\\+ ?" + CJK.source).test(withNumbers)) out.push('"+" entre deux mots (utiliser ＋)');
    // "+" collé à un mot sans nombre derrière (ex. titre de carte "伤害+") : petit et penché en Bangers
    if (new RegExp(CJK.source + " ?\\+(?![ ]?[0-9])").test(withNumbers)) out.push('"+" après un mot (utiliser ＋)');
  }
  return out;
}

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
    tr.set(id, lang === "ko" ? koPunctuation(unesc(m[2])) : unesc(m[2]));
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
      for (const w of scriptChecks(lang, v)) out.push(`ÉCRITURE   #${it.id} ${w} : "${show(v)}"`);
    }
    for (const k of t.keys()) if (!en.some((i) => i.fr === k) && !k.endsWith("#few")) out.push(`EN TROP    "${show(k)}"`);
    problems += out.length;
    console.log(`${L.cls} : ${t.size} entrées, ${out.length} problème(s)` + (out.length ? "\n  " + out.join("\n  ") : ""));
  }
  process.exit(problems ? 1 : 0);
} else {
  console.log("usage : node Tools/loc/langs.js skeleton | build <" + Object.keys(LANGS).join("|") + "> | check");
}
