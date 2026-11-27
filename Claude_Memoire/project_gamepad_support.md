---
name: project-gamepad-support
description: "Manette jouable partout depuis le 2026-10-28 (Input System, curseur rond PadCursor dans les menus, icônes PadGlyph, vibrations PadRumble) ; architecture et choix"
metadata:
  node_type: memory
  type: project
  originSessionId: f76ab443-7a9d-4afa-9ab2-2c99702e0706
  modified: 2026-10-28T17:29:50.099Z
---

Session du 2026-10-28 : l'utilisateur a demandé la manette « dans tout le jeu », avec le nouveau système d'Unity (choix recommandé accepté).

- Package Input System 1.19.0, Active Input Handling = **Both** : le clavier et la souris restent lus par l'ancien système (KeyCode de la page Commandes), la manette par `Gamepad.current` (GameInput). Les 3 scènes utilisent InputSystemUIInputModule.
- Un bouton de manette par action (`InputBindings.GetPad`, enum `PadButton`, PlayerPrefs `padbind_<Action>`). Défauts : A Dash, Y Ultime, X Clone, LB/RB orbites, Start Pause. Déplacement = stick gauche + croix, cartes = navigation (non assignables). Visée manuelle = stick droit (2026-10-29 : WeaponBase tire dans GameInput.PadAimDirection, viseur en jeu AimReticle sous la souris ou devant le héros ; pas encore validé par l'utilisateur).
- `GameInput.UsingGamepad` / `PadStyle` (Xbox / PlayStation / Nintendo) mis à jour par InputDeviceTracker ; libellés texte via `GameInput.PadLabel`, icônes via PadGlyph. La page Commandes a 3 colonnes toujours visibles (2 clavier/souris + Manette, modifiable à la souris ; SettingsRow.PadSlot), le style des icônes suit la manette BRANCHÉE dès sa détection (pas seulement touchée) ; la légende du HUD bascule selon le dernier appareil utilisé.
- **Menus : curseur rond (PadCursor)**, demandé par l'utilisateur après essai (le cadre doré + navigation bouton par bouton « pas optimisé pour ce genre de jeu », supprimé). Stick gauche (croix = lent) déplace une SOURIS VIRTUELLE Input System, A clique : survol / clic / glisser natifs partout. Ralentit sur un bouton. Actif seulement si UsingGamepad et GameManager.NeedsCursor (en partie A = Dash). Sélection de l'EventSystem vidée chaque image (sinon A revalidait le dernier bouton). Zones qui défilent (Paramètres) : A sur le fond de la zone = mode défilement (stick haut/bas), touche retour (B / Rond) pour sortir, bulles « A Faire défiler » / « B Quitter le défilement » + liseré doré. `GameInput.PointerPosition` / `PointerClickDown` / `PadBackDown` (B consommé par la sortie du défilement). La détection clavier/souris ignore la souris virtuelle (RealMouseUsed).
- Menu principal : LB/RB = onglets ; Paramètres : LT/RT = catégories ; B = retour partout (`GameInput.BackDown`).

**How to apply :** un nouvel écran marche à la manette sans rien câbler (le curseur est une souris) ; tout code qui lit Input.mousePosition / GetMouseButtonDown doit passer par GameInput.PointerPosition / PointerClickDown. Testé avec une manette virtuelle (InputSystem.AddDevice + QueueStateEvent) ; à valider par l'utilisateur sur une vraie manette. Icônes (PadGlyph, dessinées en code, style Xbox / PlayStation / Switch : HUD, pastille du clone, page Commandes) et vibrations (PadRumble.Pulse ; coups reçus, dash, ultime, mort, boss vaincu ; réglages Vibrations + Intensité) faites le 2026-10-28. Le style PlayStation n'a pas pu être testé en Play (une DualShock virtuelle refuse les états Gamepad simulés) : rendu vérifié sur planche, détection à confirmer sur vraie manette. Voir aussi [[project-english-localization]].
