---
name: project-english-localization
description: "Jeu traduit en 11 langues (FR, EN, DE, ES, PT-BR, IT, PL, RU, ZH simplifié, JA, KO) + onglet Langue — terminé et testé ; comment ajouter un texte sans casser les traductions ni les polices"
metadata:
  node_type: memory
  type: project
  originSessionId: 330f2db8-4600-4ece-b773-321d7a8ff540
  modified: 2026-10-15T19:56:10.259Z
---

Le 2026-10-14, l'utilisateur a demandé l'anglais, puis l'allemand, l'espagnol, le portugais brésilien, l'italien et le polonais ; le 2026-10-15, le russe et le chinois simplifié (« tout doit être parfait »), puis le japonais et le coréen. Exigence constante : traductions naturelles, « pas Google Trad », relues en entier. Noms Aether / Kael / Lyra jamais traduits (gardés en latin, même en russe et en chinois). Langue au 1er lancement = langue de Windows (anglais si non gérée), clavier AZERTY seulement si Windows est en français.

**Why:** toucher un public international sur Steam (trailers EN déjà faits, voir [[project-trailer-pipeline]]). Langues à écriture difficile (arabe, hébreu, thaï) déconseillées : mal gérées par TextMeshPro.

**How to apply :** état au 2026-10-15 = 11 langues terminées, testées en Play (menus + partie), pas encore commité (voir [[feedback-commit-cadence]]).
- Nouveau texte affiché : français dans `Loc.T(...)`/`Loc.F(...)`, clé ajoutée EN FIN de `LocTableEN.cs` (section « Ajouts »), puis `node Tools/loc/langs.js skeleton`, ligne « numéro| traduction » dans chaque `Tools/loc/tr_xx.txt` (de, es, pt, it, pl, ru, zh, ja, ko), `langs.js build xx` pour chaque langue, `langs.js check`. Ne jamais éditer les `LocTableXX.cs` générés à la main.
- Si le chinois, le japonais ou le coréen ont de nouveaux caractères : `python Tools/build_language_fonts.py` puis Unity **Aether > Rebuild Language Fonts** (sinon erreur console). Ce script vérifie aussi que tout caractère de toutes les langues est dessinable.
- Chinois : ponctuation pleine chasse, 「」, ⋯⋯, ＋, jamais — … “ ” (Bangers les dessinerait). Russe : formulations neutres en genre, « ты », pas « ОЗ » (se lit « 03 » en Bangers). Japonais : mêmes règles de ponctuation que le chinois. Coréen : jamais de particule variable (을/를, 이/가...) juste après un trou {0} ; ponctuation écrite normalement dans tr_ko.txt, `langs.js build ko` la fait dessiner par la police coréenne (sinon Bangers la dessine penchée) — après un trou dont le texte est coréen, écrire le signe en pleine chasse (！ ．：). « + » collé à un nom : ＋ en chinois / japonais. La police de la langue courante passe en tête des replis (LanguageFonts.cs) : dessins chinois / japonais différents.
- Garder le même soin (tutoiement, formulations neutres en genre en polonais et en russe, glossaires en tête des tr_xx.txt et dans NOTES.md).
- Ne jamais renuméroter l'enum `GameLanguage` (valeur enregistrée dans les réglages).
- Ne jamais modifier un script pendant le Play (recompilation à chaud = fausses erreurs).
- Détails, polices (cyrillique ajouté DANS Bangers, chinois / japonais / coréen Noto Sans SC / JP / KR en repli statique), pièges : sections « Session 2026-10-14 » et « Session 2026-10-15 » de NOTES.md.
