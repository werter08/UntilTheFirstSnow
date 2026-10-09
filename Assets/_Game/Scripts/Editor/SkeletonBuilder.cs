using System.IO;
using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using Courtyard.Minigames;
using Courtyard.Story;
using Courtyard.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Courtyard.Editor
{
    /// <summary>
    /// M1 bootstrap: generates the placeholder sprite, sorting layers, UI and minigame prefabs, Day_01,
    /// the ending sequence and the Boot / Kitchen / Ending scenes.
    /// Safe to re-run: anything that already exists is kept, so hand-made changes are never overwritten.
    /// Delete an asset to have it regenerated.
    /// </summary>
    public static class SkeletonBuilder
    {
        private const string Root = "Assets/_Game";
        private const string SquarePath = Root + "/Art/Sprites/Placeholder_Square.png";
        private const string StoryBoxPath = Root + "/Prefabs/UI/StoryBox.prefab";
        private const string FaderPath = Root + "/Prefabs/UI/ScreenFader.prefab";
        private const string KettleBoilPath = Root + "/Prefabs/Minigames/KettleBoil.prefab";
        private const string InstantCoffeePath = Root + "/Prefabs/Minigames/InstantCoffee.prefab";
        private const string Day01Path = Root + "/Data/Levels/Day_01.asset";
        private const string EndingSequencePath = Root + "/Data/Story/Ending.asset";

        private static readonly string[] SortingLayers = { "Sky", "Courtyard", "Wall", "Counter", "Props", "Foreground" };

        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private const float CameraSize = 5.4f; // 1080 px at 100 px per unit

        private static Sprite _square;

        [MenuItem("Courtyard/Setup/Build M1 Skeleton")]
        public static void Build()
        {
            EnsureSortingLayers();
            _square = EnsureSquareSprite();

            // Everything is built in additive scratch scenes, so scenes the user has open are never touched.
            var previousActive = SceneManager.GetActiveScene();
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scratch);
            var storyBox = LoadOrCreatePrefab(StoryBoxPath, BuildStoryBox);
            var fader = LoadOrCreatePrefab(FaderPath, BuildFader);
            var kettleBoil = LoadOrCreatePrefab(KettleBoilPath, BuildKettleBoil);
            var instantCoffee = LoadOrCreatePrefab(InstantCoffeePath, BuildInstantCoffee);
            EditorSceneManager.CloseScene(scratch, true);

            var day01 = LoadOrCreateDay01(kettleBoil.GetComponent<MinigameStep>(), instantCoffee.GetComponent<MinigameStep>());
            var ending = LoadOrCreateEndingSequence();

            LoadOrCreateScene(BuildScript.Scenes[1], () => BuildKitchenScene(day01, storyBox, fader));
            LoadOrCreateScene(BuildScript.Scenes[2], () => BuildEndingScene(ending, storyBox, fader));
            LoadOrCreateScene(BuildScript.Scenes[0], () => BuildBootScene(day01));

            EditorBuildSettings.scenes = BuildScript.Scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            Debug.Log("[SkeletonBuilder] Done. Open Assets/_Game/Scenes/Boot.unity and press Play.");
        }

        // ---------------------------------------------------------------- project setup

        private static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");

            foreach (var layerName in SortingLayers)
            {
                var exists = Enumerable.Range(0, layers.arraySize)
                    .Any(i => layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layerName);
                if (exists) continue;

                layers.InsertArrayElementAtIndex(layers.arraySize);
                var layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                layer.FindPropertyRelative("name").stringValue = layerName;
                layer.FindPropertyRelative("uniqueID").uintValue = (uint)layerName.GetHashCode() | 1u;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite EnsureSquareSprite()
        {
            if (!File.Exists(SquarePath))
            {
                var texture = new Texture2D(4, 4);
                texture.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray());
                File.WriteAllBytes(SquarePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SquarePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(SquarePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4f;
            importer.filterMode = FilterMode.Point;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // required for Sliced draw mode
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        }

        // ---------------------------------------------------------------- data

        private static LevelDefinition LoadOrCreateDay01(MinigameStep kettleBoil, MinigameStep instantCoffee)
        {
            var existing = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Day01Path);
            if (existing != null) return existing;

            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(level, Day01Path);

            var so = new SerializedObject(level);
            so.FindProperty("dayNumber").intValue = 1;
            so.FindProperty("clockTime").stringValue = "06:40";
            so.FindProperty("timeOfDay").enumValueIndex = (int)TimeOfDay.Morning;
            so.FindProperty("weather").enumValueIndex = (int)Weather.LightRain;
            so.FindProperty("treeStage").enumValueIndex = (int)TreeStage.GreenYellow;
            so.FindProperty("miraWindow").enumValueIndex = (int)MiraWindowState.LitWarmSilhouette;
            so.FindProperty("props.plantStage").enumValueIndex = (int)PlantStage.Wilted;

            var steps = so.FindProperty("steps");
            steps.arraySize = 2;
            steps.GetArrayElementAtIndex(0).objectReferenceValue = kettleBoil;
            steps.GetArrayElementAtIndex(1).objectReferenceValue = instantCoffee;

            var beats = so.FindProperty("storyBeats");
            AddBeat(beats, BeatTrigger.LevelStart, 0, BeatStyle.Thought, "6:40. Again.");
            AddBeat(beats, BeatTrigger.AfterStep, 0, BeatStyle.Thought, "Instant coffee. It tastes like Monday.");
            AddBeat(beats, BeatTrigger.LevelEnd, 0, BeatStyle.Thought, "…Someone across the courtyard is already awake.");

            so.FindProperty("startWarmth").floatValue = 70f;
            so.FindProperty("drainPerSecond").floatValue = 1.5f;
            so.FindProperty("coldMultiplier").floatValue = 1f;
            so.FindProperty("warmthPerStep").floatValue = 12f;
            so.FindProperty("drinkWarmthBonus").floatValue = 25f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return level;
        }

        private static StorySequence LoadOrCreateEndingSequence()
        {
            var existing = AssetDatabase.LoadAssetAtPath<StorySequence>(EndingSequencePath);
            if (existing != null) return existing;

            var sequence = ScriptableObject.CreateInstance<StorySequence>();
            AssetDatabase.CreateAsset(sequence, EndingSequencePath);

            var so = new SerializedObject(sequence);
            AddBeat(so.FindProperty("beats"), BeatTrigger.LevelEnd, 0, BeatStyle.Note, "Thank you for playing.");
            so.ApplyModifiedPropertiesWithoutUndo();

            return sequence;
        }

        private static void AddBeat(SerializedProperty list, BeatTrigger trigger, int step, BeatStyle style, string text,
            string speaker = "", BeatCondition condition = BeatCondition.Always)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            var beat = list.GetArrayElementAtIndex(list.arraySize - 1);
            beat.FindPropertyRelative("trigger").enumValueIndex = (int)trigger;
            beat.FindPropertyRelative("stepIndex").intValue = step;
            beat.FindPropertyRelative("condition").enumValueIndex = (int)condition;
            beat.FindPropertyRelative("style").enumValueIndex = (int)style;
            beat.FindPropertyRelative("speaker").stringValue = speaker;
            beat.FindPropertyRelative("text").stringValue = text;
        }

        // ---------------------------------------------------------------- minigame prefabs

        private static GameObject BuildKettleBoil()
        {
            var root = new GameObject("KettleBoil");
            var step = root.AddComponent<KettleBoilStep>();

            Box(root.transform, "KettleBody", new Vector2(-3f, -1.3f), new Vector2(1.6f, 2f), Gray(0.7f), 0);
            var switchRenderer = Box(root.transform, "Switch", new Vector2(-3f, -2.55f), new Vector2(0.8f, 0.4f), Gray(0.35f), 1, true);
            var kettleSwitch = switchRenderer.gameObject.AddComponent<PointerTarget>();

            var thermometer = new GameObject("Thermometer").transform;
            thermometer.SetParent(root.transform, false);
            thermometer.localPosition = new Vector3(-1.5f, -2.4f, 0f);
            Box(thermometer, "Scale", new Vector2(0f, 1.5f), new Vector2(0.4f, 3f), Gray(0.2f), 0);
            var band = Box(thermometer, "GreenBand", Vector2.zero, new Vector2(0.4f, 0.2f), new Color(0.4f, 0.8f, 0.4f), 1);
            var needle = Box(thermometer, "Needle", Vector2.zero, new Vector2(0.8f, 0.08f), new Color(0.9f, 0.25f, 0.2f), 2);

            Wire(step, "hintText", "Click the switch. Click again when the needle is in the green.");
            Wire(step, "kettleSwitch", kettleSwitch);
            Wire(step, "switchLight", switchRenderer);
            Wire(step, "thermometer", thermometer);
            Wire(step, "needle", needle.transform);
            Wire(step, "greenBand", band);
            Wire(step, "scaleHeight", 3f);
            return root;
        }

        private static GameObject BuildInstantCoffee()
        {
            var root = new GameObject("InstantCoffee");
            var step = root.AddComponent<InstantCoffeeStep>();
            var coffee = new Color(0.35f, 0.2f, 0.1f);

            var jar = Box(root.transform, "Jar", new Vector2(-5f, -1.6f), new Vector2(1.1f, 1.4f), new Color(0.55f, 0.4f, 0.3f), 0, true);

            var mug = Box(root.transform, "Mug", new Vector2(0f, -1.7f), new Vector2(1.4f, 1.6f), new Color(0.9f, 0.88f, 0.82f), 0, true);
            var fillBar = new GameObject("MugFill").transform;
            fillBar.SetParent(root.transform, false);
            fillBar.localPosition = new Vector3(0f, -2.35f, 0f);
            var fill = Box(fillBar, "Fill", Vector2.zero, new Vector2(1.2f, 0f), new Color(0.75f, 0.85f, 0.95f), 1);
            var spriteBar = fillBar.gameObject.AddComponent<SpriteBar>();
            Wire(spriteBar, "fill", fill);
            Wire(spriteBar, "width", 1.2f);
            Wire(spriteBar, "maxHeight", 1.3f);
            var fillLine = Box(fillBar, "FillLine", new Vector2(0f, 1.1f), new Vector2(1.3f, 0.05f), Color.white, 3);

            var markers = new SpriteRenderer[3];
            for (var i = 0; i < markers.Length; i++)
                markers[i] = Box(root.transform, $"SpoonMarker{i}", new Vector2(-0.35f + 0.35f * i, -2.2f), new Vector2(0.22f, 0.22f), coffee, 2);

            var spoonRenderer = Box(root.transform, "Spoon", new Vector2(-3.2f, -2.6f), new Vector2(1.2f, 0.18f), Gray(0.8f), 5);
            spoonRenderer.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(1.4f, 0.6f); // easier to grab
            var spoon = spoonRenderer.gameObject.AddComponent<DragTarget>();
            var spoonLoad = Box(spoonRenderer.transform, "Load", new Vector2(0.45f, 0.1f), new Vector2(0.3f, 0.22f), coffee, 6);

            var kettleBody = Box(root.transform, "Kettle", new Vector2(3.2f, -1.3f), new Vector2(1.6f, 2f), Gray(0.6f), 0, true);
            var kettle = kettleBody.gameObject.AddComponent<PointerTarget>();

            Wire(step, "hintText", "Drag two spoons of coffee into the mug. Then hold the kettle to pour to the line.");
            Wire(step, "spoon", spoon);
            Wire(step, "spoonLoad", spoonLoad);
            Wire(step, "jar", jar.GetComponent<Collider2D>());
            Wire(step, "mug", mug.GetComponent<Collider2D>());
            WireArray(step, "spoonMarkers", markers.Cast<Object>().ToArray());
            Wire(step, "mugFill", spriteBar);
            Wire(step, "fillLine", fillLine.transform);
            Wire(step, "kettle", kettle);
            Wire(step, "kettleBody", kettleBody);
            return root;
        }

        // ---------------------------------------------------------------- UI prefabs

        private static GameObject BuildStoryBox()
        {
            var root = UiObject("StoryBox", null);
            Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1300f, 230f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Img(root, new Color(0.1f, 0.1f, 0.12f, 0.88f), true);

            var body = Label(root, "Body", "", 40f, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.93f, 0.88f));
            body.rectTransform.offsetMin = new Vector2(50f, 35f);
            body.rectTransform.offsetMax = new Vector2(-50f, -35f);

            var speaker = UiObject("Speaker", root);
            Place(speaker, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(30f, 0f), new Vector2(320f, 60f));
            Img(speaker, new Color(0.95f, 0.75f, 0.45f));
            var speakerLabel = Label(speaker, "Name", "", 32f, TextAlignmentOptions.Center, new Color(0.15f, 0.1f, 0.08f));

            var hint = Label(root, "ContinueHint", "click", 24f, TextAlignmentOptions.BottomRight, new Color(1f, 1f, 1f, 0.5f));
            hint.rectTransform.offsetMin = new Vector2(0f, 12f);
            hint.rectTransform.offsetMax = new Vector2(-20f, 0f);

            var player = root.gameObject.AddComponent<StoryPlayer>();
            Wire(player, "box", group);
            Wire(player, "body", body);
            Wire(player, "speakerRoot", speaker.gameObject);
            Wire(player, "speakerLabel", speakerLabel);
            Wire(player, "continueHint", hint.gameObject);
            return root.gameObject;
        }

        private static GameObject BuildFader()
        {
            var root = UiObject("ScreenFader", null);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            var background = Img(root, Color.black, true);
            var message = Label(root, "Message", "", 56f, TextAlignmentOptions.Center, Color.white);

            var fader = root.gameObject.AddComponent<ScreenFader>();
            Wire(fader, "group", group);
            Wire(fader, "background", background);
            Wire(fader, "message", message);
            return root.gameObject;
        }

        // ---------------------------------------------------------------- scenes

        private static void BuildKitchenScene(LevelDefinition fallbackLevel, GameObject storyBoxPrefab, GameObject faderPrefab)
        {
            var camera = CreateCamera(new Color(0.2f, 0.22f, 0.25f));
            camera.gameObject.AddComponent<Physics2DRaycaster>();

            // Placeholder kitchen: grey boxes until real art exists.
            var kitchen = new GameObject("Kitchen").transform;
            Box(kitchen, "Wall", Vector2.zero, new Vector2(19.2f, 10.8f), new Color(0.72f, 0.74f, 0.76f), 0, false, "Wall");
            Box(kitchen, "WindowFrame", new Vector2(0f, 1.7f), new Vector2(7.4f, 4.9f), new Color(0.5f, 0.38f, 0.28f), 1, false, "Wall");
            Box(kitchen, "Courtyard", new Vector2(0f, 1.7f), new Vector2(7f, 4.5f), new Color(0.45f, 0.5f, 0.58f), 2, false, "Wall");
            Box(kitchen, "MiraWindow", new Vector2(1.6f, 2.6f), new Vector2(1.1f, 1.3f), new Color(1f, 0.85f, 0.5f), 3, false, "Wall");
            Box(kitchen, "Counter", new Vector2(0f, -4.1f), new Vector2(19.2f, 2.6f), new Color(0.55f, 0.48f, 0.42f), 0, false, "Counter");
            Box(kitchen, "CounterEdge", new Vector2(0f, -2.85f), new Vector2(19.2f, 0.15f), new Color(0.4f, 0.34f, 0.3f), 1, false, "Counter");
            var stepRoot = new GameObject("StepRoot").transform;

            var canvas = CreateCanvas();
            var meterHim = BuildMeterView(canvas, "WarmthMeter_Him", new Vector2(40f, -40f));
            var meterMira = BuildMeterView(canvas, "WarmthMeter_Mira", new Vector2(40f, -100f));
            var hint = BuildHint(canvas);
            var story = ((GameObject)PrefabUtility.InstantiatePrefab(storyBoxPrefab, canvas)).GetComponent<StoryPlayer>();
            var fader = ((GameObject)PrefabUtility.InstantiatePrefab(faderPrefab, canvas)).GetComponent<ScreenFader>();
            CreateEventSystem();

            var runner = new GameObject("LevelRunner").AddComponent<LevelRunner>();
            Wire(runner, "fallbackLevel", fallbackLevel);
            Wire(runner, "worldCamera", camera);
            Wire(runner, "stepRoot", stepRoot);
            Wire(runner, "story", story);
            Wire(runner, "hint", hint);
            WireArray(runner, "meterViews", new Object[] { meterHim, meterMira });
            Wire(runner, "fader", fader);
        }

        private static void BuildEndingScene(StorySequence sequence, GameObject storyBoxPrefab, GameObject faderPrefab)
        {
            CreateCamera(new Color(0.1f, 0.12f, 0.2f));
            var canvas = CreateCanvas();
            var story = ((GameObject)PrefabUtility.InstantiatePrefab(storyBoxPrefab, canvas)).GetComponent<StoryPlayer>();
            var fader = ((GameObject)PrefabUtility.InstantiatePrefab(faderPrefab, canvas)).GetComponent<ScreenFader>();

            var ending = new GameObject("EndingScreen").AddComponent<EndingScreen>();
            Wire(ending, "sequence", sequence);
            Wire(ending, "story", story);
            Wire(ending, "fader", fader);
        }

        private static void BuildBootScene(LevelDefinition day01)
        {
            CreateCamera(new Color(0.12f, 0.13f, 0.18f));
            var canvas = CreateCanvas();

            var title = Label(canvas, "Title", "Until the First Snow", 96f, TextAlignmentOptions.Center, new Color(0.98f, 0.9f, 0.75f));
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1600f, 160f));

            var prompt = Label(canvas, "Prompt", "click to start", 40f, TextAlignmentOptions.Center, Color.white);
            Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(800f, 80f));
            var promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();

            var manager = new GameObject("GameManager").AddComponent<GameManager>();
            WireArray(manager, "levels", new Object[] { day01 });

            var boot = new GameObject("BootScreen").AddComponent<BootScreen>();
            Wire(boot, "prompt", promptGroup);
        }

        private static WarmthMeterView BuildMeterView(RectTransform canvas, string name, Vector2 position)
        {
            var frame = UiObject(name, canvas);
            Place(frame, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(460f, 44f));
            Img(frame, new Color(0.1f, 0.1f, 0.12f, 0.8f));

            var track = UiObject("Track", frame);
            Stretch(track);
            track.offsetMin = new Vector2(5f, 5f);
            track.offsetMax = new Vector2(-5f, -5f);

            var fill = UiObject("Fill", track);
            Stretch(fill);
            var fillImage = Img(fill, Color.white);

            var view = frame.gameObject.AddComponent<WarmthMeterView>();
            Wire(view, "fill", fill);
            Wire(view, "fillImage", fillImage);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.45f, 0.65f, 0.95f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.25f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var so = new SerializedObject(view);
            so.FindProperty("coldToWarm").gradientValue = gradient;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static HintView BuildHint(RectTransform canvas)
        {
            var root = UiObject("Hint", canvas);
            Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1300f, 70f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            Img(root, new Color(0f, 0f, 0f, 0.4f));
            var label = Label(root, "Label", "", 34f, TextAlignmentOptions.Center, Color.white);

            var view = root.gameObject.AddComponent<HintView>();
            Wire(view, "group", group);
            Wire(view, "label", label);
            return view;
        }

        private static Camera CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            return camera;
        }

        private static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject LoadOrCreatePrefab(string path, System.Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void LoadOrCreateScene(string path, System.Action build)
        {
            if (File.Exists(path)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            build();
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static SpriteRenderer Box(Transform parent, string name, Vector2 position, Vector2 size, Color color,
            int order, bool withCollider = false, string layer = "Props")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _square;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;

            if (withCollider) go.AddComponent<BoxCollider2D>().size = size;
            return renderer;
        }

        private static Color Gray(float value) => new(value, value, value);

        private static RectTransform UiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image Img(RectTransform rect, Color color, bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, string text, float size,
            TextAlignmentOptions alignment, Color color)
        {
            var rect = UiObject(name, parent);
            Stretch(rect);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            Property(so, field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Wire(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            Property(so, field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Wire(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            Property(so, field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var array = Property(so, field);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty Property(SerializedObject so, string field) =>
            so.FindProperty(field) ?? throw new System.ArgumentException($"{so.targetObject.GetType().Name} has no serialized field '{field}'.");
    }
}
