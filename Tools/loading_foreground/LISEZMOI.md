# Premier plan de l'écran de chargement

`Assets/Game/Assets 2D/WaitingScreen/Chargement_PremierPlan.png` = l'illustration de chargement réduite aux personnages, bâtons,
lueurs et loups (alpha = détourage). `ForestAmbienceUI` la dessine par-dessus le vent et les feuilles, qui passent ainsi
derrière les personnages. À refaire seulement si l'illustration change.

Fabriqué le 2026-11-14 :
1. Environnement isolé : `python -m venv segenv`, puis `segenv/Scripts/python -m pip install onnxruntime pillow numpy`.
2. Modèle BiRefNet (détourage) : https://github.com/danielgatis/rembg/releases/download/v0.0.0/BiRefNet-general-epoch_244.onnx
3. Dans un dossier de travail contenant `seg/bg.png` (= l'illustration 1920 x 1080) :
   - `seg_run.py birefnet.onnx seg/bg.png seg/mask_bir.png birefnet` (image entière : héros, bâtons, gros loup de droite) ;
   - `seg_run.py birefnet.onnx seg/bg.png seg/mask_wA.png birefnet 240 260 880 640` (loups de gauche, recadrés : bien mieux détourés) ;
   - `seg_run.py birefnet.onnx seg/bg.png seg/mask_wB.png birefnet 1040 300 1280 500` (petit loup en haut à droite) ;
   - `seg_combine.py` : réunit les masques + les lueurs repérées par leur couleur (zones codées en dur pour CETTE illustration)
     -> `seg/mask_final.png` et un aperçu `seg/overlay_final.png`.
4. Recadrer l'image + alpha sur la zone utile et reporter le cadre dans `ForestAmbienceUI._foregroundAnchorMin/Max`
   (0-1, origine en bas à gauche). Import : Sprite, sans compression (même rendu que le fond), pas de mipmaps.
