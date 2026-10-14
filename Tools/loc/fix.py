# Remplace des lignes "id| texte" dans un fichier tr_xx.txt.
# Usage : python Tools/loc/fix.py xx fichier_de_corrections.txt   (corrections au même format "id| texte")
import re, sys
lang, fixfile = sys.argv[1], sys.argv[2]
fixes = {}
for l in open(fixfile, encoding='utf-8').read().split('\n'):
    m = re.match(r'^(\d+)\| ?(.*)$', l)
    if m: fixes[int(m.group(1))] = m.group(2)
p = f'Tools/loc/tr_{lang}.txt'
lines = open(p, encoding='utf-8').read().split('\n')
for i, l in enumerate(lines):
    m = re.match(r'^(\d+)\|', l)
    if m and int(m.group(1)) in fixes:
        lines[i] = f"{m.group(1)}| {fixes.pop(int(m.group(1)))}"
if fixes: sys.exit(f"ids introuvables : {sorted(fixes)}")
open(p, 'w', encoding='utf-8').write('\n'.join(lines))
print(f"tr_{lang}.txt : corrections appliquées")
