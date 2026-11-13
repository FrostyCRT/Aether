# Bot d'équilibrage des branches (BranchBot)

Bot utilisé le 2026-11-11 pour comparer les 3 personnages, sans arbre puis avec leur branche au maximum (résultats dans `resultats_2026-11-11/`, synthèse dans `NOTES.md`, section « Étape 5 »).

## Lancer
1. Unity : ouvrir `MainMenu.unity`, passer en Play (rien ne doit être modifié pendant le Play).
2. Remplacer dans `BranchBot.cs` la valeur de `bot.dir` (fin du fichier) par le dossier temporaire (scratchpad) de la session.
3. Envoyer TOUT le contenu de `BranchBot.cs` à l'outil `Unity_RunCommand` (la classe `CommandScript` à la fin crée le bot, une manette virtuelle, et lance la 1re partie).
4. Attendre le fichier `bb_FIN.txt` dans `dir` (boucle bash en arrière-plan), puis lire les `bb_<config>.txt` et arrêter le Play dans la même réponse.

## Ce qu'il fait
- Configurations : pour chaque personnage (0 Aether, 1 Kael, 2 Lyra), une partie « sans » arbre et une « arbre » (branche du perso au max, autres branches à 0) ; `repeat` parties par config. Les vrais niveaux de l'arbre sont remis à la fin. `SaveSystem.SuppressWrites = true`.
- Pilotage : fuit les ennemis proches, va au coffre de boss, dash vers un projectile qui arrive, dash de secours si encerclé, Ultime si ≥ 15 ennemis ou boss, Clone si ≥ 5 ennemis proches, toujours la carte 0, timeScale 3.
- PV remis au max sous 40 % = « morts évitées » (recharges). Journal toutes les 60 s de chrono ; ligne FIN (mort) ou FIN15 (15:00 ou 600 s réelles).
- Durée : ~5 min réelles par partie (18 parties ≈ 1 h 30).
