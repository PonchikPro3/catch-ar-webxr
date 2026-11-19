using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using WebXR;

namespace CatchAR.Editor
{
    public static class PrototypeBuilder
    {
        const string ScenePath = "Assets/CatchAR/Scenes/CatchAR.unity";
        static readonly Color Navy = new Color(.035f, .065f, .10f, .96f);
        static readonly Color Mint = new Color(.48f, 1, .76f);
        static readonly Color Muted = new Color(.64f, .73f, .79f);
        static TMP_FontAsset font;
        static Material mint, gold, dark, grid;

        public static void ApplyGuidanceUpdate()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            if (game == null || game.hud == null) throw new Exception("Open the CatchAR scene first");
            ConfigureGuidance(game);
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        static void ConfigureGuidance(CatchGame game)
        {
            var safe = game.hud.transform.Find("Safe Area");
            if (safe == null) throw new Exception("Safe Area UI is missing");
            var layer = safe.Find("Offscreen Directions") as RectTransform;
            if (layer == null) layer = Rect("Offscreen Directions", safe, .015f, .075f, .985f, .92f);
            layer.SetAsLastSibling();
            var indicators = layer.GetComponent<OffscreenIndicators>();
            if (indicators == null) indicators = layer.gameObject.AddComponent<OffscreenIndicators>();
            indicators.game = game;
            indicators.bounds = layer;
            indicators.markers = new OffscreenArrow[5];
            for (int i = 0; i < indicators.markers.Length; i++)
            {
                string name = "Target Direction " + (i + 1);
                var pointer = layer.Find(name) as RectTransform;
                if (pointer == null) pointer = Rect(name, layer, .5f, .5f, .5f, .5f);
                pointer.sizeDelta = new Vector2(44, 44);
                var arrow = pointer.GetComponent<OffscreenArrow>();
                if (arrow == null) arrow = pointer.gameObject.AddComponent<OffscreenArrow>();
                arrow.color = Mint;
                arrow.raycastTarget = false;
                indicators.markers[i] = arrow;
                pointer.gameObject.SetActive(false);
                EditorUtility.SetDirty(arrow);
            }
            EditorUtility.SetDirty(indicators);
        }

        public static void ApplySpawnRingUpdate()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            if (game == null) throw new Exception("Open the CatchAR scene first");
            game.spawnDistance = 2.5f;
            game.spawnHeightAboveCamera = .5f;
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Catch AR/Apply falling objects update")]
        public static void ApplyFallingUpdate()
        {
            ConfigureSettings();
            var game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            if (game == null) throw new Exception("Open the CatchAR scene first");
            font = CreateFont();
            foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                label.font = font;
                if (label.name == "Description") label.text = "Лови падающие объекты вокруг себя.\nНе дай им долететь до земли!";
                if (label.name == "Hint") label.text = "СМОТРИ ВОКРУГ · ЛОВИ ДО КАСАНИЯ ЗЕМЛИ";
                EditorUtility.SetDirty(label);
            }
            game.arena.position = new Vector3(0, .13f, 3);
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Catch AR/1. Create prototype scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/CatchAR/Scenes");
            Directory.CreateDirectory("Assets/CatchAR/Art");
            Directory.CreateDirectory("Assets/CatchAR/Prefabs");
            Directory.CreateDirectory("Assets/XR/Settings");
            AssetDatabase.Refresh();
            ConfigureSettings();
            font = CreateFont();
            mint = MakeMaterial("Mint", Mint, true);
            gold = MakeMaterial("Gold", new Color(1, .66f, .22f), true);
            dark = MakeMaterial("Stage", new Color(.003f, .007f, .012f), true);
            grid = MakeMaterial("Grid", new Color(.012f, .040f, .05f), true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.6f, .7f, .8f);
            RenderSettings.fog = false;
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(45, -25, 0);
            light.shadows = LightShadows.None;

            var rig = new GameObject("WebXR Camera Rig");
            rig.SetActive(false);
            rig.AddComponent<WebXRManager>();
            var head = new GameObject("Tracked Head");
            head.transform.SetParent(rig.transform, false);
            var tracker = head.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
            tracker.SetPoseSource(UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRDevice,
                UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.Center);
            tracker.trackingType = UnityEngine.SpatialTracking.TrackedPoseDriver.TrackingType.RotationAndPosition;
            tracker.updateType = UnityEngine.SpatialTracking.TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tracker.UseRelativeTransform = false;
            head.AddComponent<AudioListener>();
            var xr = head.AddComponent<WebXRCamera>();
            var cameras = new Camera[5];
            string[] names = { "Desktop Camera", "Left VR Camera", "Right VR Camera", "Left AR Camera", "Right AR Camera" };
            for (int i = 0; i < 5; i++)
            {
                var camera = new GameObject(names[i]).AddComponent<Camera>();
                camera.transform.SetParent(head.transform, false);
                camera.transform.localPosition = i == 0 ? new Vector3(0, 1.4f, 0) : Vector3.zero;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 30;
                camera.fieldOfView = 58;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = i >= 3 ? Color.clear : new Color(.025f, .045f, .065f, 1);
                camera.enabled = i == 0;
                camera.allowHDR = i == 0;
                var data = camera.GetUniversalAdditionalCameraData();
                // Keep AR alpha intact: opaque post-processing runs on the desktop preview only.
                data.renderPostProcessing = i == 0;
                data.volumeLayerMask = 1;
                if (i == 0) camera.tag = "MainCamera";
                cameras[i] = camera;
            }
            var serializedCamera = new SerializedObject(xr);
            string[] fields = { "cameraMain", "cameraL", "cameraR", "cameraARL", "cameraARR" };
            for (int i = 0; i < 5; i++) serializedCamera.FindProperty(fields[i]).objectReferenceValue = cameras[i];
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();
            var controllers = new WebXRController[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "WebXRController Left" : "WebXRController Right");
                go.transform.SetParent(rig.transform, false);
                controllers[i] = go.AddComponent<WebXRController>();
                controllers[i].hand = i == 0 ? WebXRControllerHand.LEFT : WebXRControllerHand.RIGHT;
            }
            rig.SetActive(true);
            CreateVolume();
            var demo = CreateEnvironment();
            var game = new GameObject("Catch Game").AddComponent<CatchGame>();
            game.xrCamera = xr;
            game.controllers = controllers;
            game.demoEnvironment = demo;
            game.arena = new GameObject("Arena · world anchored").transform;
            game.arena.position = new Vector3(0, 1.4f, 3);
            game.targetPrefab = CreateTargetPrefab();
            game.placementMarker = CreateRing("Surface placement reticle", null, .18f, .014f, mint);
            game.placementMarker.transform.rotation = Quaternion.Euler(90, 0, 0);
            game.placementMarker.SetActive(false);
            game.audioSource = game.gameObject.AddComponent<AudioSource>();
            game.audioSource.playOnAwake = false;
            game.catchSound = CreateSound();
            game.hud = CreateHud();
            ConfigureGuidance(game);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = game.gameObject;
            AssetDatabase.SaveAssets();
            Debug.Log("CATCHAR_SCENE_OK: WebXRCamera + 2 WebXRController + WebXRLoader");
        }

        public static void ConfigureSettings()
        {
            PlayerSettings.companyName = "AR Lab";
            PlayerSettings.productName = "Catch AR — Поймай объект";
            PlayerSettings.bundleVersion = "1.3.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 800;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.template = "PROJECT:CatchAR";
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 512;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            QualitySettings.SetQualityLevel(0, true);
            QualitySettings.vSyncCount = 0;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.supportsHDR = true;
                urp.msaaSampleCount = 2;
                urp.renderScale = 1;
                urp.supportsCameraDepthTexture = false;
                urp.supportsCameraOpaqueTexture = false;
                EditorUtility.SetDirty(urp);
            }
            var xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.WebGL);
            if (xrSettings == null)
            {
                var perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
                xrSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                AssetDatabase.AddObjectToAsset(xrSettings, perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.WebGL, xrSettings);
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                xrSettings.Manager = manager;
                EditorUtility.SetDirty(perTarget);
            }
            xrSettings.InitManagerOnStart = true;
            if (!xrSettings.Manager.activeLoaders.Any(loader => loader is WebXRLoader))
            {
                if (!XRPackageMetadataStore.AssignLoader(xrSettings.Manager, typeof(WebXRLoader).FullName, BuildTargetGroup.WebGL))
                    throw new Exception("Could not assign WebXRLoader to WebGL");
            }
            var settings = WebXRSettings.GetSettings();
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WebXRSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/XR/Settings/WebXR Settings.asset");
                EditorBuildSettings.AddConfigObject("WebXR.Settings", settings, true);
            }
            // The five-camera WebXRCamera path explicitly provides transparent AR cameras.
            settings.DisableXRDisplaySubsystem = true;
            settings.AutoLoadWebXRManager = false;
            settings.AutoLoadWebXRInputSystem = false;
            settings.ARRequiredReferenceSpace = WebXRSettings.ReferenceSpaceTypes.local;
            settings.AROptionalFeatures = WebXRSettings.ExtraFeatureTypes.hit_test;
            settings.VROptionalFeatures = 0;
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(xrSettings);
            EditorUtility.SetDirty(xrSettings.Manager);
            AssetDatabase.SaveAssets();
        }

        static TMP_FontAsset CreateFont()
        {
            const string fontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
            AssetDatabase.ImportAsset(fontPath);
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CatchAR/Art/Liberation Interface Font.asset");
            if (existing != null) return existing;
            var asset = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(fontPath), 64, 6,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = "Liberation Interface Font";
            asset.TryAddCharacters(string.Concat(Enumerable.Range(32, 95).Select(c => (char)c)) +
                string.Concat(Enumerable.Range(0x400, 256).Select(c => (char)c)) + "·—–×●");
            AssetDatabase.CreateAsset(asset, "Assets/CatchAR/Art/Liberation Interface Font.asset");
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static Material MakeMaterial(string name, Color color, bool unlit)
        {
            string path = "Assets/CatchAR/Art/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP shader missing");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color);
            if (!unlit) material.SetFloat("_Smoothness", .4f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateVolume()
        {
            string path = "Assets/CatchAR/Art/Preview Volume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.value = TonemappingMode.Neutral;
                AssetDatabase.AddObjectToAsset(tone, profile);
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.value = 1;
                bloom.intensity.value = .3f;
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            var volume = new GameObject("Preview Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static GameObject CreateEnvironment()
        {
            var root = new GameObject("Desktop Preview Stage · hidden in AR");
            Primitive("Ground", PrimitiveType.Cube, root.transform, new Vector3(0, .1f, 4), new Vector3(16, .05f, 14), dark);
            for (int i = -8; i <= 8; i++)
            {
                Primitive("Grid Z", PrimitiveType.Cube, root.transform, new Vector3(i, .131f, 4), new Vector3(.008f, .003f, 14), grid);
                Primitive("Grid X", PrimitiveType.Cube, root.transform, new Vector3(0, .131f, 4 + i), new Vector3(16, .003f, .008f), grid);
            }
            var halo = CreateRing("Arena perimeter", root.transform, 1.25f, .007f, grid);
            halo.transform.SetPositionAndRotation(new Vector3(0, .15f, 3), Quaternion.Euler(90, 0, 0));
            return root;
        }

        static CatchTarget CreateTargetPrefab()
        {
            var root = new GameObject("Catchable Energy");
            var target = root.AddComponent<CatchTarget>();
            root.AddComponent<SphereCollider>().radius = .20f;
            var core = Primitive("Energy Core", PrimitiveType.Cube, root.transform, Vector3.zero, Vector3.one * .19f, mint);
            core.transform.rotation = Quaternion.Euler(35, 35, 20);
            var orbit = CreateRing("Orbit", root.transform, .15f, .012f, mint);
            orbit.transform.localRotation = Quaternion.Euler(25, 35, 0);
            target.core = core.transform;
            target.ring = orbit.transform;
            target.coreRenderer = core.GetComponent<Renderer>();
            target.ringRenderer = orbit.GetComponent<Renderer>();
            target.mintMaterial = mint;
            target.goldMaterial = gold;
            target.mintCoreMaterial = MakeMaterial("Mint Core", new Color(.20f, .65f, .43f), false);
            target.goldCoreMaterial = MakeMaterial("Gold Core", new Color(.75f, .32f, .025f), false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/CatchAR/Prefabs/Catchable Energy.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<CatchTarget>();
        }

        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject CreateRing(string name, Transform parent, float radius, float width, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            string path = $"Assets/CatchAR/Art/Ring-{radius:0.000}-{width:0.000}.asset".Replace(',', '.');
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                const int count = 64;
                var verts = new Vector3[count * 2];
                var indices = new int[count * 6];
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2 / count;
                    var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    verts[i * 2] = direction * (radius - width);
                    verts[i * 2 + 1] = direction * (radius + width);
                    int j = i * 6, a = i * 2, b = ((i + 1) % count) * 2;
                    indices[j] = a; indices[j + 1] = b + 1; indices[j + 2] = a + 1;
                    indices[j + 3] = a; indices[j + 4] = b; indices[j + 5] = b + 1;
                }
                mesh = new Mesh { name = name, vertices = verts, triangles = indices };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            material.SetFloat("_Cull", 0);
            return go;
        }

        static AudioClip CreateSound()
        {
            const string path = "Assets/CatchAR/Art/Catch.wav";
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int count = 4410;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / 44100;
                    writer.Write((short)(Mathf.Sin(t * 2 * Mathf.PI * (750 + 2000 * t)) * Mathf.Pow(1 - (float)i / count, 2) * 12000));
                }
            }
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static CatchHud CreateHud()
        {
            var canvasGo = new GameObject("Game UI", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1080);
            scaler.matchWidthOrHeight = .5f;
            var safe = Rect("Safe Area", canvasGo.transform, 0, 0, 1, 1);
            safe.gameObject.AddComponent<SafeArea>();
            var hud = canvasGo.AddComponent<CatchHud>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Label("Brand", safe, "CATCH / AR", .04f, .915f, .52f, .98f, 28, Mint, TextAlignmentOptions.MidlineLeft);
            Label("Edition", safe, "МИНИ-ИГРА В ТВОЁМ МИРЕ", .53f, .915f, .96f, .98f, 15, Muted, TextAlignmentOptions.MidlineRight);
            var menu = Panel("Welcome", safe, .08f, .12f, .92f, .87f, Navy);
            hud.menu = menu.gameObject;
            Label("Eyebrow", menu, "01 / РЕАЛЬНОСТЬ СТАНОВИТСЯ ИГРОЙ", .07f, .86f, .93f, .94f, 19, Mint);
            Label("Title", menu, "Поймай\nмомент.", .07f, .58f, .93f, .85f, 84, Color.white);
            Label("Description", menu, "Лови падающие объекты вокруг себя.\nНе дай им долететь до земли!", .07f, .43f, .93f, .57f, 29, Muted);
            hud.playButton = Button("Demo", menu, "ИГРАТЬ В ДЕМО   →", .07f, .25f, .93f, .39f);
            Label("Rules", menu, "Обычный: +10  ·  Золотой: +25\nБыстрые поимки дают серию до ×5", .07f, .10f, .93f, .22f, 22, Muted);
            hud.bestText = Label("Best", menu, "ЛИЧНЫЙ РЕКОРД  0", .07f, .025f, .93f, .09f, 17, Mint);

            var play = Rect("Play HUD", safe, 0, 0, 1, .91f);
            hud.playHud = play.gameObject;
            var scoreCard = Panel("Score Card", play, .04f, .79f, .32f, .98f, Navy);
            Label("Score Label", scoreCard, "СЧЁТ", .08f, .63f, .92f, .92f, 18, Muted);
            hud.score = Label("Score", scoreCard, "000", .08f, .04f, .92f, .66f, 58, Color.white);
            var timeCard = Panel("Time Card", play, .70f, .79f, .96f, .98f, Navy);
            Label("Time Label", timeCard, "ОСТАЛОСЬ", .08f, .63f, .92f, .92f, 18, Muted);
            hud.timer = Label("Time", timeCard, "60с", .08f, .04f, .92f, .66f, 58, Color.white);
            hud.combo = Label("Combo", play, "ЛОВИ БЫСТРЕЕ", .34f, .86f, .68f, .96f, 20, Mint);
            Panel("Time Track", play, .04f, .763f, .96f, .77f, new Color(.15f, .22f, .26f, .7f));
            var fill = Panel("Time Fill", play, .04f, .763f, .96f, .77f, Mint).GetComponent<UnityEngine.UI.Image>();
            // A built-in white UI sprite enables Image.Type.Filled on all platforms.
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            hud.timeBar = fill;
            hud.status = Label("Mode", play, "● ДЕМО", .04f, .04f, .96f, .10f, 18, Mint);
            Label("Hint", play, "НАЖИМАЙ НА ОБЪЕКТЫ ДО КАСАНИЯ ЗЕМЛИ", .04f, .105f, .96f, .17f, 18, Color.white);
            hud.flash = Label("Feedback", play, "+10 ПОЙМАНО!", .15f, .57f, .85f, .67f, 32, Mint);

            var countdown = Rect("Countdown", safe, .25f, .32f, .75f, .67f);
            hud.countdown = countdown.gameObject;
            hud.countdownText = Label("Number", countdown, "3", 0, .15f, 1, 1, 180, Mint);
            Label("Get Ready", countdown, "ПРИГОТОВЬСЯ", 0, 0, 1, .2f, 24, Color.white);
            var placement = Panel("Placement", safe, .06f, .09f, .94f, .40f, Navy);
            hud.placement = placement.gameObject;
            hud.placementText = Label("Instruction", placement, "НАВЕДИ КАМЕРУ НА ПОВЕРХНОСТЬ", .05f, .42f, .95f, .94f, 28, Color.white);
            hud.placeButton = Button("Place", placement, "РАЗМЕСТИТЬ ЗОНУ ИГРЫ", .05f, .08f, .95f, .36f);

            var result = Panel("Result", safe, .08f, .12f, .92f, .87f, Navy);
            hud.result = result.gameObject;
            Label("Round Over", result, "РАУНД ЗАВЕРШЁН", .07f, .85f, .93f, .94f, 23, Mint);
            Label("Result Caption", result, "ТВОЙ СЧЁТ", .07f, .73f, .93f, .82f, 23, Muted);
            hud.resultScore = Label("Result Score", result, "0", .07f, .48f, .93f, .74f, 130, Color.white);
            hud.resultDetails = Label("Details", result, "", .07f, .34f, .93f, .48f, 25, Muted);
            hud.replayButton = Button("Replay", result, "ЕЩЁ ОДИН РАУНД   →", .07f, .17f, .93f, .30f);
            hud.menuButton = Button("Menu", result, "В МЕНЮ", .07f, .03f, .93f, .14f, true);
            Label("Footer", safe, "СВОБОДНОЕ МЕСТО ВОКРУГ ТЕБЯ — ЛУЧШАЯ ИГРОВАЯ ЗОНА", .04f, .015f, .96f, .065f, 15, Muted);
            play.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
            placement.gameObject.SetActive(false);
            countdown.gameObject.SetActive(false);
            return hud;
        }

        static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static RectTransform Panel(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static TMP_Text Label(string name, Transform parent, string text, float x0, float y0, float x1, float y1,
            float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.color = color;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = size * .45f;
            label.fontSizeMax = size;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        static UnityEngine.UI.Button Button(string name, Transform parent, string caption, float x0, float y0, float x1, float y1, bool secondary = false)
        {
            var rect = Panel(name, parent, x0, y0, x1, y1, secondary ? new Color(.12f, .19f, .23f) : Mint);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.8f, 1, .9f);
            colors.pressedColor = new Color(.6f, .8f, .7f);
            button.colors = colors;
            Label("Caption", rect, caption, .05f, .1f, .95f, .9f, 28, secondary ? Color.white : Navy);
            return button;
        }

        [MenuItem("Catch AR/2. Build WebGL")]
        public static void BuildWebGL()
        {
            ConfigureSettings();
            ValidateConfiguration();
            Directory.CreateDirectory("Builds/WebGL");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/WebGL", target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            var summary = report.summary;
            Directory.CreateDirectory("Evidence");
            File.WriteAllText("Evidence/build-report.json", JsonUtility.ToJson(new BuildEvidence
            {
                unityVersion = Application.unityVersion, result = summary.result.ToString(),
                bytes = summary.totalSize.ToString(), seconds = summary.totalTime.TotalSeconds,
                errors = summary.totalErrors, warnings = summary.totalWarnings,
                decompressionFallback = PlayerSettings.WebGL.decompressionFallback,
                multithread = PlayerSettings.WebGL.threadsSupport,
                compression = PlayerSettings.WebGL.compressionFormat.ToString(),
                webxrVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WebXRManager).Assembly).version,
                scene = ScenePath, template = PlayerSettings.WebGL.template
            }, true));
            if (summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed: " + summary.result);
            Debug.Log("CATCHAR_BUILD_OK");
        }

        public static string ValidateConfiguration()
        {
            var settings = WebXRSettings.GetSettings();
            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.WebGL);
            if (settings == null || xr == null || !xr.Manager.activeLoaders.Any(x => x is WebXRLoader)) throw new Exception("WebXR is not connected");
            if (PlayerSettings.WebGL.decompressionFallback || PlayerSettings.WebGL.threadsSupport) throw new Exception("Required WebGL settings are incorrect");
            if (UnityEngine.Object.FindObjectsByType<WebXRCamera>(FindObjectsSortMode.None).Length != 1) throw new Exception("Expected one WebXRCamera rig");
            if (UnityEngine.Object.FindObjectsByType<WebXRController>(FindObjectsSortMode.None).Length != 2) throw new Exception("Expected two WebXRController components");
            if (UnityEngine.Object.FindObjectsByType<UnityEngine.SpatialTracking.TrackedPoseDriver>(FindObjectsSortMode.None).Length != 1) throw new Exception("Expected XR head pose tracking");
            var game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            if (game == null || game.hud == null || game.targetPrefab == null || game.xrCamera.GetCamera(WebXRCamera.CameraID.LeftAR) == null) throw new Exception("Scene references are missing");
            return "Configuration OK: WebXR loader, cameras, controllers, game, fallback=false, threads=false";
        }

        [Serializable]
        sealed class BuildEvidence
        {
            public string unityVersion, result, bytes, webxrVersion, scene, template, compression;
            public double seconds;
            public int errors, warnings;
            public bool decompressionFallback, multithread;
        }
    }
}
