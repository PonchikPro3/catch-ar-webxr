using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CatchAR.Editor
{
    public static class SpriteUiBuilder
    {
        const string Art = "Assets/CatchAR/Art/UI";
        static TMP_FontAsset font;
        static Sprite frame, clock, upperButton, lowerButton, burst;
        static Material hudType, buttonMaterial;

        public static void ConfigureScanProgress(CatchHud hud)
        {
            if (hud == null || hud.scanInfo == null)
                throw new Exception("Scan HUD references are missing");
            if (hud.scanProgress == null)
            {
                var bar = Rect("ScanProgress", hud.scanInfo, .07f, .37f, .93f, .49f);
                var back = Rect("Track", bar, 0, 0, 1, 1).gameObject.AddComponent<TimerFillGraphic>();
                back.color = WarmPalette.Peach; back.raycastTarget = false;
                var area = Rect("FillArea", bar, .012f, .12f, .988f, .88f);
                var fill = Rect("Fill", area, 0, 0, 1, 1).gameObject.AddComponent<TimerFillGraphic>();
                fill.color = WarmPalette.Red; fill.raycastTarget = false;
                hud.scanProgress = bar.gameObject.AddComponent<UnityEngine.UI.Slider>();
                hud.scanProgress.fillRect = fill.rectTransform;
                hud.scanProgress.interactable = false;
                hud.scanProgress.transition = UnityEngine.UI.Selectable.Transition.None;
                hud.scanProgress.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            }
            hud.scanProgress.SetValueWithoutNotify(0);
            foreach (string name in new[] { "Backing", "ScanTitle", "Counts", "Source" })
            {
                var child = hud.scanInfo.Find(name);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            var status = hud.scanStatus.rectTransform;
            Transform oldHint = status.parent;
            status.SetParent(hud.scanInfo, false);
            status.anchorMin = new Vector2(0, 0); status.anchorMax = new Vector2(1, .4f);
            status.offsetMin = status.offsetMax = Vector2.zero;
            hud.scanStatus.text = "Сканируй комнату вокруг себя";
            hud.scanStatus.color = WarmPalette.Cream;
            if (oldHint != hud.scanInfo && oldHint.name == "ScanInstructions") UnityEngine.Object.DestroyImmediate(oldHint.gameObject);
            hud.scanInfo.sizeDelta = new Vector2(840, 160);
            hud.scanInfo.anchoredPosition = new Vector2(0, -40);
            var progress = (RectTransform)hud.scanProgress.transform;
            progress.anchorMin = new Vector2(0, .6f); progress.anchorMax = new Vector2(1, .6f);
            progress.sizeDelta = new Vector2(0, 62); progress.anchoredPosition = Vector2.zero;
            var timerOutline = hud.timeSlider.transform.Find("Outline").GetComponent<UnityEngine.UI.Image>();
            if (progress.Find("Outline") == null)
                Image("Outline", progress, 0, 0, 1, 1, timerOutline.sprite, WarmPalette.Orange);
            ConfigureFill(hud.scanProgress);
            ConfigureFill(hud.timeSlider);
            EditorUtility.SetDirty(hud);
        }

        public static void ConfigureFill(UnityEngine.UI.Slider slider)
        {
            slider.enabled = false;
            var area = (RectTransform)slider.transform.Find("FillArea");
            area.anchorMin = new Vector2(0, .12f); area.anchorMax = new Vector2(1, .88f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var fill = slider.fillRect.GetComponent<TimerFillGraphic>();
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.color = WarmPalette.Peach;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var track = slider.transform.Find("Track").GetComponent<TimerFillGraphic>();
            track.type = UnityEngine.UI.Image.Type.Simple;
            track.color = new Color(WarmPalette.Ink.r, WarmPalette.Ink.g, WarmPalette.Ink.b, .9f);
            slider.enabled = true;
            EditorUtility.SetDirty(slider);
        }

        [MenuItem("Catch AR/Apply sprite UI")]
        public static void Apply()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            if (game == null || game.hud == null) throw new Exception("Open CatchAR scene first");
            var old = game.hud.gameObject;
            var actualFont = game.hud.GetComponentInChildren<TMP_Text>(true).font;
            game.hud = CreateHud(actualFont);
            ConfigureScanner(game);
            UnityEngine.Object.DestroyImmediate(old);
            PrototypeBuilder.ConfigureGuidance(game);
            Recolor("Mint.mat", WarmPalette.Orange);
            Recolor("Mint Core.mat", WarmPalette.Red);
            Recolor("Gold.mat", WarmPalette.Cream);
            Recolor("Gold Core.mat", WarmPalette.Peach);
            Recolor("Stage.mat", new Color(.045f, .018f, .022f));
            Recolor("Grid.mat", new Color(.13f, .065f, .06f));
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene, "Assets/CatchAR/Scenes/CatchAR.unity");
            AssetDatabase.SaveAssets();
        }

        static void Recolor(string name, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/CatchAR/Art/" + name);
            if (mat == null) return;
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }

        public static void ConfigureScanner(CatchGame game)
        {
            if (game.scanner == null) game.scanner = game.GetComponent<RoomSurfaceScanner>();
            if (game.scanner == null) game.scanner = game.gameObject.AddComponent<RoomSurfaceScanner>();
            string fillPath = Art + "/ScanFill.mat", linePath = Art + "/ScanLine.mat";
            var fill = AssetDatabase.LoadAssetAtPath<Material>(fillPath);
            if (fill == null) { fill = new Material(Shader.Find("CatchAR/Scan Surface")); AssetDatabase.CreateAsset(fill, fillPath); }
            var line = AssetDatabase.LoadAssetAtPath<Material>(linePath);
            if (line == null)
            {
                line = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                line.SetColor("_BaseColor", Color.white);
                line.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(line, linePath);
            }
            game.scanner.surfaceMaterial = fill;
            game.scanner.lineMaterial = line;
            EditorUtility.SetDirty(game.scanner);
        }

        static void LoadSprites()
        {
            Directory.CreateDirectory(Art);
            AssetDatabase.ImportAsset("Assets/UI.png", ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/UI.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI.png");
            frame = Slice(texture, "TimerOutline", new Rect(30, 902, 840, 80));
            clock = Slice(texture, "Clock", new Rect(885, 890, 100, 110));
            upperButton = Slice(texture, "PlayButton", new Rect(60, 400, 718, 278),
                new[] { new Vector2(62, 392), new Vector2(775, 347), new Vector2(733, 623), new Vector2(63, 568) });
            lowerButton = Slice(texture, "ExitButton", new Rect(57, 140, 684, 274),
                new[] { new Vector2(65, 610), new Vector2(723, 664), new Vector2(740, 816), new Vector2(58, 884) });
            burst = Slice(texture, "ThreeStrokeBurst", new Rect(829, 200, 188, 270));
        }

        static Sprite Slice(Texture2D texture, string name, Rect rect, Vector2[] polygon = null)
        {
            string path = Art + "/" + name + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        public static CatchHud CreateHud(TMP_FontAsset existingFont)
        {
            font = existingFont;
            LoadSprites();
            string buttonPath = Art + "/PaletteButtons.mat";
            buttonMaterial = AssetDatabase.LoadAssetAtPath<Material>(buttonPath);
            if (buttonMaterial == null)
            {
                buttonMaterial = new Material(Shader.Find("CatchAR/Palette Sprite UI"));
                buttonMaterial.SetColor("_BorderColor", WarmPalette.Cream);
                AssetDatabase.CreateAsset(buttonMaterial, buttonPath);
            }
            string typePath = Art + "/OutlinedType.mat";
            hudType = AssetDatabase.LoadAssetAtPath<Material>(typePath);
            if (hudType == null)
            {
                hudType = new Material(font.material);
                hudType.EnableKeyword("OUTLINE_ON");
                hudType.SetFloat("_OutlineWidth", .16f);
                hudType.SetColor("_OutlineColor", WarmPalette.Ink);
                AssetDatabase.CreateAsset(hudType, typePath);
            }
            var root = new GameObject("Game UI", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1080);
            scaler.matchWidthOrHeight = .5f;
            var hud = root.AddComponent<CatchHud>();
            var safe = Rect("Safe Area", root.transform, 0, 0, 1, 1);
            safe.gameObject.AddComponent<SafeArea>();
            hud.safeArea = safe;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var menu = Image("Menu", safe, 0, 0, 1, 1, null, WarmPalette.Cream);
            hud.menu = menu.gameObject;
            hud.menuStack = Center("MenuButtons", menu.transform, 660, 540);
            hud.playButton = Button("Play", hud.menuStack, "Играть", .0f, .53f, 1, 1, upperButton, WarmPalette.Red, WarmPalette.Cream);
            hud.exitButton = Button("Exit", hud.menuStack, "Выход", .0f, .0f, 1, .47f, lowerButton, WarmPalette.Peach, WarmPalette.Ink);
            hud.menuNotice = Label("ARNotice", menu.transform, "", .1f, .08f, .9f, .22f, 27, WarmPalette.Ink);

            var modes = Image("ModeSelection", safe, 0, 0, 1, 1, null, WarmPalette.Cream);
            hud.modes = modes.gameObject;
            hud.modeStack = Center("ModeButtons", modes.transform, 660, 766);
            Label("ChooseMode", hud.modeStack, "Выбери режим", 0, .89f, 1, 1, 42, WarmPalette.Ink);
            hud.fallingModeButton = Button("FallingMode", hud.modeStack, "Падающие", 0, .59f, 1, .87f, upperButton, WarmPalette.Red, WarmPalette.Cream);
            hud.scanModeButton = Button("ScanMode", hud.modeStack, "Скан комнаты", 0, .29f, 1, .57f, lowerButton, WarmPalette.Red, WarmPalette.Cream);
            hud.modeBackButton = Button("Back", hud.modeStack, "Назад", 0, 0, 1, .25f, lowerButton, WarmPalette.Peach, WarmPalette.Ink);

            var scan = Rect("Scan HUD", safe, 0, 0, 1, 1);
            hud.scanHud = scan.gameObject;
            hud.scanInfo = Center("ScanInformation", scan, 900, 260);
            hud.scanInfo.anchorMin = hud.scanInfo.anchorMax = new Vector2(.5f, 1);
            hud.scanInfo.pivot = new Vector2(.5f, 1);
            hud.scanInfo.anchoredPosition = new Vector2(0, -24);
            Image("Backing", hud.scanInfo, 0, 0, 1, 1, null, WarmPalette.Cream);
            Label("ScanTitle", hud.scanInfo, "СКАН КОМНАТЫ · ОТЛАДКА", .03f, .8f, .97f, .98f, 32, WarmPalette.Ink);
            hud.scanCounts = Label("Counts", hud.scanInfo, "Поверхностей: 0", .03f, .4f, .97f, .78f, 28, WarmPalette.Ink);
            hud.scanSource = Label("Source", hud.scanInfo, "Ожидание данных устройства", .03f, .05f, .97f, .37f, 25, WarmPalette.Ink);
            hud.scanControls = Center("ScanControls", scan, 800, 250);
            hud.scanControls.anchorMin = hud.scanControls.anchorMax = new Vector2(.5f, 0);
            hud.scanControls.pivot = new Vector2(.5f, 0);
            hud.scanControls.anchoredPosition = new Vector2(0, 28);
            var hint = Image("ScanInstructions", hud.scanControls, 0, 1, 1, 1, null, WarmPalette.Cream).rectTransform;
            hint.pivot = new Vector2(.5f, 0); hint.sizeDelta = new Vector2(0, 90); hint.anchoredPosition = new Vector2(0, 12);
            hud.scanStatus = Label("Status", hint, "Наведи камеру на пол или стол. Двигай телефон медленно.", .03f, .12f, .97f, .88f, 28, WarmPalette.Ink);
            hud.scanPauseButton = Button("PauseScan", hud.scanControls, "Зафиксировать", 0, .48f, 1, 1, upperButton, WarmPalette.Red, WarmPalette.Cream);
            hud.scanPauseCaption = hud.scanPauseButton.GetComponentInChildren<TMP_Text>();
            hud.scanResetButton = Button("ResetScan", hud.scanControls, "Заново", 0, 0, .48f, .43f, lowerButton, WarmPalette.Peach, WarmPalette.Ink);
            hud.scanBackButton = Button("LeaveScan", hud.scanControls, "В меню", .52f, 0, 1, .43f, lowerButton, WarmPalette.Peach, WarmPalette.Ink);
            modes.gameObject.SetActive(false);
            scan.gameObject.SetActive(false);

            var play = Rect("Play HUD", safe, 0, 0, 1, 1);
            hud.playHud = play.gameObject;
            hud.timerRow = Center("TimerRow", play, 840, 80);
            hud.timerRow.anchorMin = hud.timerRow.anchorMax = new Vector2(.5f, 1);
            hud.timerRow.anchoredPosition = new Vector2(0, -72);
            var bar = Rect("TimeSlider", hud.timerRow, 0, .5f, 1, .5f);
            bar.sizeDelta = new Vector2(-100, 62);
            bar.anchoredPosition = new Vector2(-50, 0);
            var back = Rect("Track", bar, 0, .12f, 1, .88f).gameObject.AddComponent<TimerFillGraphic>();
            back.color = new Color(WarmPalette.Ink.r, WarmPalette.Ink.g, WarmPalette.Ink.b, .9f);
            back.raycastTarget = false;
            var area = Rect("FillArea", bar, .025f, .18f, .975f, .82f);
            var fill = Rect("Fill", area, 0, 0, 1, 1).gameObject.AddComponent<TimerFillGraphic>();
            fill.color = WarmPalette.Peach;
            fill.raycastTarget = false;
            hud.timerFill = fill;
            hud.timeSlider = bar.gameObject.AddComponent<UnityEngine.UI.Slider>();
            hud.timeSlider.fillRect = fill.rectTransform;
            hud.timeSlider.minValue = 0;
            hud.timeSlider.maxValue = 1;
            hud.timeSlider.value = 1;
            hud.timeSlider.interactable = false;
            hud.timeSlider.transition = UnityEngine.UI.Selectable.Transition.None;
            hud.timeSlider.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            Image("Outline", bar, 0, 0, 1, 1, frame, WarmPalette.Orange);
            hud.clockIcon = Rect("Clock", hud.timerRow, 1, .5f, 1, .5f);
            hud.clockIcon.sizeDelta = new Vector2(76, 84);
            hud.clockIcon.anchoredPosition = new Vector2(-38, 0);
            var icon = hud.clockIcon.gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.sprite = clock; icon.color = WarmPalette.Cream; icon.preserveAspect = true; icon.raycastTarget = false;
            hud.score = Label("Score", play, "0", .5f, 1, .5f, 1, 86, WarmPalette.Cream, true);
            hud.scoreRect = (RectTransform)hud.score.transform;
            hud.scoreRect.sizeDelta = new Vector2(420, 110);
            hud.scoreRect.anchoredPosition = new Vector2(0, -168);

            var placement = Rect("Placement", safe, 0, 0, 1, 1);
            hud.placement = placement.gameObject;
            hud.placementStack = Center("PlacementControls", placement, 760, 340);
            hud.placementStack.anchorMin = hud.placementStack.anchorMax = new Vector2(.5f, 0);
            hud.placementStack.pivot = new Vector2(.5f, 0);
            hud.placementStack.anchoredPosition = new Vector2(0, 48);
            var plate = Image("InstructionBacking", hud.placementStack, 0, .5f, 1, 1, null, WarmPalette.Cream);
            hud.placementText = Label("Instruction", plate.transform, "Наведи камеру на пол\nи медленно двигай телефон.", .05f, .08f, .95f, .92f, 30, WarmPalette.Ink);
            hud.placeButton = Button("Place", hud.placementStack, "Разместить", .04f, 0, .96f, .48f, lowerButton, WarmPalette.Red, WarmPalette.Cream);

            var countdown = Rect("Countdown", safe, .2f, .32f, .8f, .68f);
            hud.countdown = countdown.gameObject;
            hud.countdownText = Label("Number", countdown, "3", 0, 0, 1, 1, 180, WarmPalette.Cream, true);
            var result = Image("Result", safe, 0, 0, 1, 1, null, WarmPalette.Cream);
            hud.result = result.gameObject;
            hud.resultStack = Center("ResultControls", result.transform, 660, 740);
            Label("Finished", hud.resultStack, "Время вышло", 0, .88f, 1, 1, 40, WarmPalette.Ink);
            hud.resultScore = Label("FinalScore", hud.resultStack, "0", 0, .62f, 1, .88f, 120, WarmPalette.Red);
            hud.replayButton = Button("Replay", hud.resultStack, "Ещё раз", 0, .31f, 1, .60f, upperButton, WarmPalette.Red, WarmPalette.Cream);
            hud.menuButton = Button("Menu", hud.resultStack, "В меню", 0, 0, 1, .29f, lowerButton, WarmPalette.Peach, WarmPalette.Ink);
            ConfigureScanProgress(hud);
            hud.effects = Rect("TouchEffects", safe, 0, 0, 1, 1);
            hud.touchBurst = burst;
            play.gameObject.SetActive(false);
            placement.gameObject.SetActive(false);
            countdown.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
            return hud;
        }

        static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }
        static RectTransform Center(string name, Transform parent, float w, float h)
        {
            var r = Rect(name, parent, .5f, .5f, .5f, .5f); r.sizeDelta = new Vector2(w, h); return r;
        }
        static UnityEngine.UI.Image Image(string name, Transform parent, float x0, float y0, float x1, float y1, Sprite sprite, Color color)
        {
            var img = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            return img;
        }
        static TMP_Text Label(string name, Transform parent, string text, float x0, float y0, float x1, float y1, float size, Color color, bool outline = false)
        {
            var label = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true; label.fontSizeMin = size * .7f; label.fontSizeMax = size;
            label.alignment = TextAlignmentOptions.Center; label.color = color; label.raycastTarget = false;
            if (outline) label.fontSharedMaterial = hudType;
            return label;
        }
        static UnityEngine.UI.Button Button(string name, Transform parent, string caption, float x0, float y0, float x1, float y1, Sprite sprite, Color color, Color textColor)
        {
            var image = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<SheetButtonImage>();
            image.sprite = sprite;
            image.color = color;
            image.material = buttonMaterial;
            Vector2[] polygon = sprite == upperButton
                ? new[] { new Vector2(62, 392), new Vector2(775, 347), new Vector2(733, 623), new Vector2(63, 568) }
                : new[] { new Vector2(65, 610), new Vector2(723, 664), new Vector2(740, 816), new Vector2(58, 884) };
            image.outline = new Vector2[4];
            for (int i = 0; i < 4; i++)
                image.outline[i] = new Vector2((polygon[i].x - sprite.rect.xMin) / sprite.rect.width,
                    (1024 - polygon[i].y - sprite.rect.yMin) / sprite.rect.height);
            image.raycastTarget = true;
            image.useSpriteMesh = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1, .94f, .85f);
            colors.pressedColor = new Color(.8f, .72f, .65f);
            colors.disabledColor = new Color(.65f, .55f, .5f, .55f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            Label("Caption", image.transform, caption, .12f, .18f, .88f, .82f, 62, textColor);
            image.gameObject.AddComponent<UiButtonMotion>();
            return button;
        }
    }
}

