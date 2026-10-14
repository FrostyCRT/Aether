// Vérifie que les textes stockés comme DONNÉES (traduits à l'affichage via une variable) ont tous leur traduction.
// Usage : node Tools/loc/check_data.js
const fs = require("fs"), path = require("path");
const root = path.resolve(__dirname, "../../Assets/Game/Scripts");
const unesc = (s) => s.replace(/\\(.)/g, (_, c) => ({ n: "\n", t: "\t", '"': '"', "\\": "\\" }[c] ?? c));
const keys = new Set();
for (const m of fs.readFileSync(path.join(root, "Localization/LocTableEN.cs"), "utf8").matchAll(/\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"(?:[^"\\]|\\.)*"\s*\}/g)) keys.add(unesc(m[1]));

// fichier -> filtre de lignes (regex) : seules les lignes qui correspondent sont contrôlées
const targets = {
  "Settings/SettingsSchema.cs": /label|title|subtitle|description|disabledHint|Toggle\(|Slider\(|Choice\(|Header\(|Note\(|Names =|options = new/,
  "Settings/InputBindings.cs": /PresetNames/,
  "Challenge/ChallengeDatabase.cs": /displayName/,
  "UI/SkillTreeData.cs": /displayName|description|level\dDesc/,
  "UI/CharacterSelectUI.cs": /tags|weaponName|weaponDescription|weaponUpgrades|branchName|branchFocus/,
  "Systems/SkinCatalog.cs": /displayName =/,
  "UI/GameUI.cs": /^\s+"[^"]+",\s*$/,
  "UI/GameOverMessagePool.cs": /^\s+"[^"]+",\s*$/,
  "Systems/MetaProgressionManager.cs": /reputationNames/,
  "UI/HudStyler.cs": /AddCaption\(/,
  "UI/HudKeyLegend.cs": /AddRow\(/,
  "Enemies/BossDeer.cs": /_bossName =/,
  "Enemies/BossCorruptedSource.cs": /_bossName =/,
  "Systems/WaveManager.cs": /ShowBossHP/,
};
const skip = new Set(["display", "graphics", "audio", "gameplay", "interface", "controls", "language", "Français", "English", "Deutsch", "Español", "Português (Brasil)", "Italiano", "Polski"]);
let missing = 0;
for (const [rel, filter] of Object.entries(targets)) {
  const lines = fs.readFileSync(path.join(root, rel), "utf8").split(/\r?\n/);
  lines.forEach((l, i) => {
    const code = l.replace(/\/\/.*$/, "");
    if (!filter.test(code) || /^\s*\[/.test(code)) return;
    for (const m of code.matchAll(/"((?:[^"\\]|\\.)*)"/g)) {
      const k = unesc(m[1]);
      if (!/[A-Za-zÀ-ÿ]{2}/.test(k) || skip.has(k) || /^[a-z][A-Za-z0-9]*$/.test(k)) continue;
      if (!keys.has(k)) { missing++; console.log(`${rel}:${i + 1} : "${k.replace(/\n/g, "\\n")}"`); }
    }
  });
}
console.log(missing ? `${missing} texte(s) de données sans traduction.` : "Données : tout est traduit.");
