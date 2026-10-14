// Décodage des valeurs texte du YAML de Unity (double-quotées avec \xHH / \uXXXX, simples, ou repliées sur
// plusieurs lignes). Partagé par les outils de localisation.
function decode(raw) {
  raw = raw.trim();
  if (raw.startsWith('"')) {
    let s = raw.slice(1, raw.lastIndexOf('"'));
    s = s.replace(/\\\r?\n\s*/g, "");           // ligne repliée avec "\" : jointure sans espace
    s = s.replace(/\r?\n\s*/g, " ");            // ligne repliée simple : un espace
    let out = "";
    for (let i = 0; i < s.length; i++) {
      const c = s[i];
      if (c !== "\\") { out += c; continue; }
      const n = s[++i];
      if (n === "x") { out += String.fromCharCode(parseInt(s.substr(i + 1, 2), 16)); i += 2; }
      else if (n === "u") { out += String.fromCharCode(parseInt(s.substr(i + 1, 4), 16)); i += 4; }
      else if (n === "n") out += "\n";
      else if (n === "t") out += "\t";
      else if (n === "r") out += "\r";
      else if (n === "0") out += "\0";
      else if (n === "_") out += " ";
      else if (n === "N") out += "\u0085";
      else out += n;                              // \" \\ \/ ...
    }
    // Unity encode les caractères au-delà de l'ASCII en UTF-8 octet par octet dans \xHH : on recompose.
    try { return decodeURIComponent(escape(out)); } catch { return out; }
  }
  if (raw.startsWith("'")) return raw.slice(1, raw.lastIndexOf("'")).replace(/''/g, "'").replace(/\r?\n\s*/g, " ");
  return raw.replace(/\r?\n\s*/g, " ");
}

// Lit tous les champs `nom: valeur` (valeur éventuellement sur plusieurs lignes plus indentées) d'un fichier YAML.
function fields(text, names) {
  const lines = text.split(/\r?\n/);
  const re = new RegExp("^(\\s*)(" + names.join("|") + "):\\s?(.*)$");
  const out = [];
  for (let i = 0; i < lines.length; i++) {
    const m = re.exec(lines[i]);
    if (!m) continue;
    const indent = m[1].length;
    let raw = m[3];
    while (i + 1 < lines.length && lines[i + 1].search(/\S/) > indent && !/^\s*-?\s*[A-Za-z_]+:\s/.test(lines[i + 1])) raw += "\n" + lines[++i];
    out.push({ line: i + 1, field: m[2], value: decode(raw) });
  }
  return out;
}

module.exports = { decode, fields };
