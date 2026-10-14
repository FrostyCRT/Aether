const fs = require("fs"), path = require("path"), { fields } = require("./yaml_decode");
const dir = process.argv[2];
for (const f of fs.readdirSync(dir).filter((f) => f.endsWith(".asset")))
  for (const r of fields(fs.readFileSync(path.join(dir, f), "utf8"), ["upgradeName", "description"]))
    console.log(f.replace(".asset", "") + "\t" + r.field + "\t" + JSON.stringify(r.value));
