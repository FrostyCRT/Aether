# Mode Escarmouche — document de conception

*Rédigé le 2026-11-13. Rien n'est codé. Les points ❓ sont des décisions à prendre par toi. Les chiffres marqués « à mesurer » seront réglés avec le bot de test une fois la première version jouable.*

---

## 1. Intention

Une partie de **5 minutes** pour les joueurs qui n'ont pas le temps d'en faire une de 15. Le mode n'est pas le cœur du jeu : il a le droit d'être **excessif**.

Le joueur doit :
- monter en puissance **très vite** et le sentir ;
- voir l'écran se remplir d'effets et d'ennemis qui fondent ;
- avoir quand même un vrai défi (on peut perdre) ;
- finir sur un **final marquant**, pas sur une coupure sèche.

Ce que le mode **ne change pas** : on commence toujours avec le **tir de base seul**. La courbe d'XP du jeu (80 × niveau²) reste la même ; l'Escarmouche accélère le **gain** d'XP, pas la courbe.

---

## 2. Ce qu'on sait du rythme actuel (mesures du bot, Classique, Modéré)

| Moment | Niveau atteint | Ennemis tués |
|---|---|---|
| 2:00 | 1 | ~35 |
| 3:00 | 2 à 5 | 75 à 180 |
| 5:00 | 6 à 8 | 340 à 690 |
| 10:00 | 16-17 | ~3 000 |
| 15:00 | 25 | ~7 000 |

*Le bot fuit les ennemis et ramasse peu de gemmes au début : un vrai joueur va un peu plus vite, mais l'ordre de grandeur est là.*

**Conclusion :** si on coupait simplement une partie Classique à 5:00, on finirait niveau 7, sans fusion, sans avoir vu l'Apocalypse finale. Il faut **compresser** la partie, pas la raccourcir.

---

## 3. Principe : une partie de 15 min compressée en 5

Tout ce qui dépend du temps tourne **3 fois plus vite** :

| Élément | Classique | Escarmouche |
|---|---|---|
| Phases d'apparition (Introduction → Apocalypse finale) | 0, 1, 3, 5, 9, 10, 12 min | 0:00, 0:20, 1:00, 1:40, 3:00, 3:20, 4:00 |
| Types d'ennemis (Intro → Brute) | 0, 2, 4, 8, 12 min | 0:00, 0:40, 1:20, 2:40, 4:00 |
| PV des ennemis par palier | ×1 → ×2 | mêmes valeurs, aux temps compressés |
| Fusions possibles à partir de | 10:00 | **3:00** |

Comme les ennemis arrivent 3 fois plus tôt et plus nombreux, l'**XP arrive aussi 3 fois plus vite**, sans toucher à la courbe.

**En plus**, un **multiplicateur d'XP** propre au mode, pour l'excès et la montée en puissance :

| Option | XP | Niveau visé à 5:00 | Ressenti |
|---|---|---|---|
| A | ×1 | ~25 (comme un Classique complet) | Rythme d'une vraie partie, en accéléré |
| **B (recommandée)** | **×1,5** | **~30** | Premiers niveaux en quelques secondes, build quasi complet vers 4:00, fusions dès 3:00 |
| C | ×2 | 35+ | Build complet tôt, risque de s'ennuyer la dernière minute |

❓ **Décision 1 :** multiplicateur d'XP (je recommande B, réglage final « à mesurer »).

---

## 4. Déroulé d'une partie

| Temps | Ce qui se passe |
|---|---|
| 0:00 | Tir de base seul. Premiers ennemis dès les premières secondes (pas de démarrage mou). |
| ~0:15 | Premier niveau. Puis un niveau toutes les ~10 s au début. |
| **2:30** | **Boss intermédiaire.** Le chrono s'arrête pendant le combat, comme en Classique. Son coffre donne des cartes gratuites : gros pic de puissance. |
| 3:00 | Les fusions deviennent possibles. |
| 4:00 | Apocalypse finale : densité maximale, Brutes. |
| **4:30** | **Frénésie** (voir §5) : 30 dernières secondes avant le boss final. |
| **5:00** | **Boss final.** Le battre = victoire. |

❓ **Décision 2 : quels boss ?**

| Option | Boss intermédiaire (2:30) | Boss final (5:00) | Remarque |
|---|---|---|---|
| **A (recommandée)** | Boss 1 | Boss 3 (la Source corrompue) | Le final le plus spectaculaire, celui qui conclut l'histoire |
| B | Boss 1 | Boss 2 | Plus simple, garde le Boss 3 pour le Classique |
| C | Un boss tiré au hasard | Un autre boss tiré au hasard | Plus de variété, mais un final moins prévisible à équilibrer |

**PV des boss** : fixés d'après la puissance attendue du joueur à ce moment, comme pour la Ruée. Premier jet, à mesurer :
- boss à 2:30 : ~45 000 PV (le Classique en a 20 000 au Boss 1, mais le joueur aura le niveau d'un joueur à 7:30) ;
- boss final : ~150 000 PV (180 000 au Boss 3 du Classique).

❓ **Décision 3 :** l'Escarmouche ouvre-t-elle **tous les boss dès le départ**, ou seulement ceux déjà battus en Classique, comme la Ruée ? Je recommande **tous dès le départ** : c'est le mode des joueurs pressés, les en priver irait contre l'intention. Mais ça leur montre le Boss 3 avant le Classique.

---

## 5. Les excès qui rendent le mode satisfaisant

À choisir, à combiner ou à rejeter :

| # | Excès | Effet | Risque |
|---|---|---|---|
| 1 | **Frénésie finale** (4:30 → 5:00) | Apparitions ×2, gemmes d'XP ×2, bandeau « FRÉNÉSIE ! », musique qui s'accélère | Performances (à mesurer avec beaucoup d'ennemis) |
| 2 | **Coffre généreux** | Le coffre du boss à 2:30 donne 4 cartes au lieu de 2 | Aucun, simple à régler |
| 3 | **Gemmes plus grosses** | Gemmes d'une taille au-dessus (plus d'XP chacune, plus visibles) | Fait doublon avec le multiplicateur d'XP : l'un ou l'autre |
| 4 | **Chiffres de dégâts plus gros** pendant la Frénésie | Ressenti de puissance | Lisibilité |

Je recommande **1 + 2**. Les gemmes ne sont jamais plafonnées ni fusionnées (règle du jeu), même en Frénésie.

❓ **Décision 4 :** quels excès ?

---

## 6. Récompenses (ne doivent pas rendre le mode plus rentable que le Classique)

Règle : **par minute jouée, l'Escarmouche rapporte autant ou un peu moins que le Classique**. Sinon tout le monde ferait des Escarmouches pour progresser, et le Classique deviendrait inutile.

| Source | Classique (15 min) | Escarmouche (5 min), proposition |
|---|---|---|
| Or de survie | 300 / min | 300 / min (même taux) |
| Or des ennemis | rendement décroissant | identique (le rendement décroissant limite déjà la densité ×3) |
| Or des boss | 900 / 1 800 / 3 000 | ×0,5 (450 et 1 500) |
| Prime de victoire | 3 700 or + 60 éclats | 1 200 or + 20 éclats (un tiers) |
| Éclats de survie | 2 toutes les 45 s | identique |
| Éclats des boss | 15 / 30 / 45 | ×0,5 |
| Prime de 1re victoire | 300 / 600 / 1 000 | aucune |
| Multiplicateur de difficulté | ×1 / ×1,5 / ×2 | identique |

---

## 7. Interactions avec le reste du jeu (tout ce qu'il faut anticiper)

| Système | Question | Proposition |
|---|---|---|
| **Paliers de difficulté** | Modéré / Violent / Apocalyptique disponibles ? | Oui, mêmes règles (carte, PV, récompenses). ❓ Ou Modéré seul pour garder le mode simple. |
| **Déblocage du mode** | Disponible dès la 1re partie ? | Oui (il sert les joueurs pressés). |
| **Défis de l'heure** | Comptent-ils ? | Non (comme Sans fin et Ruée). Ils sont faits pour 15 min (« niveau 20 en 10 min », « vaincre 2 boss »). |
| **Records** | Mélangés au Classique ? | Non : records à part (meilleur temps de victoire, plus de kills), comme les autres modes. |
| **Déblocage de Kael / Lyra** | Kael = atteindre le Boss 2, Lyra = gagner une partie | Comptent seulement en Classique (sinon l'Escarmouche les débloquerait en 5 min). |
| **Arbre de compétences, Réputation** | S'appliquent ? | Oui, comme partout. |
| **Ultime, Clone, Bouclier** | Recharges en secondes : 3 fois moins d'utilisations par partie | Pas de changement au départ. À surveiller : le Clone (8 s) pèse plus lourd sur une partie courte. |
| **Second Souffle** | Une fois par partie | Inchangé. |
| **Plafond de coup 50 %** | — | Inchangé. |
| **Dernière partie, écrans de fin** | Nom du mode, détail de l'or et des éclats | Ajout du mode dans les textes (11 langues). |
| **Performances** | La densité maximale arrive à 4:00 au lieu de 12:00 | Même nombre d'ennemis max (160) : déjà mesuré. La Frénésie (×2) est à mesurer. |
| **Manette, menu, sélection du mode** | Nouveau mode dans le sélecteur | Même sélecteur que les autres modes, avec description et meilleur score. |
| **Sauvegarde** | Nouveaux champs | Compteurs et records du mode, ajoutés sans casser les anciennes sauvegardes. |

---

## 8. Comment on vérifiera que c'est réussi

Bot de test, 3 parties par personnage, sans arbre puis avec l'arbre au maximum. Cibles :

| Mesure | Cible |
|---|---|
| Niveau à 2:30 / 5:00 | ~15 / ~30 |
| Fusion obtenue avant 5:00 | dans la plupart des parties |
| Boss final | battu par un joueur correct, pas à coup sûr (le bot doit perdre parfois sans arbre) |
| Or et éclats par minute | ≤ Classique |
| Images par seconde pendant la Frénésie | pas de chute visible |

Puis ta partie à toi, pour le ressenti : c'est le seul vrai juge de « satisfaisant ».

---

## 9. Récapitulatif des décisions à prendre

1. Multiplicateur d'XP : A ×1, **B ×1,5 (recommandé)**, C ×2.
2. Boss : **A Boss 1 puis Boss 3 (recommandé)**, B Boss 1 puis Boss 2, C au hasard.
3. Boss accessibles : **tous dès le départ (recommandé)** ou seulement ceux battus en Classique.
4. Excès : **Frénésie finale + coffre généreux (recommandé)**, et/ou gemmes plus grosses, chiffres plus gros.
5. Paliers de difficulté : les trois, ou Modéré seul.
6. Le nom « Escarmouche » te convient-il ? (autres pistes : « Partie éclair », « Assaut »)
