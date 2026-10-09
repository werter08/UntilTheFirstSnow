using System.Collections.Generic;
using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using Courtyard.Story;
using Courtyard.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// Generates placeholder content: sorting layers, UI and minigame prefabs, level data and the Boot / Kitchen / Ending scenes.
    /// Safe to re-run: anything that already exists is kept, so hand-made changes are never overwritten.
    /// Delete an asset to have it regenerated.
    /// </summary>
    public static class ContentBuilder
    {
        private const string StoryBoxPath = "Assets/_Game/Prefabs/UI/StoryBox.prefab";
        private const string FaderPath = "Assets/_Game/Prefabs/UI/ScreenFader.prefab";

        private static readonly string[] SortingLayerNames = { "Sky", "Courtyard", "Wall", "Counter", "Props", "Foreground" };
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private const float CameraSize = 5.4f; // 1080 px at 100 px per unit

        [MenuItem("Courtyard/Setup/Build Content")]
        public static void Build()
        {
            EnsureSortingLayers(SortingLayerNames);
            EnsureSquareSprite();

            var storyBox = LoadOrCreatePrefab(StoryBoxPath, BuildStoryBox);
            UpgradeStoryBox(storyBox);
            var fader = LoadOrCreatePrefab(FaderPath, BuildFader);

            var miraUsual = LevelDataBuilder.EnsureMiraUsual();
            var steps = MinigamePrefabBuilder.BuildAll(miraUsual);
            var levels = LevelDataBuilder.BuildLevels(steps);
            var ending = LevelDataBuilder.BuildEnding();

            LoadOrCreateScene(BuildScript.Scenes[1], () => BuildKitchenScene(levels[0], storyBox, fader));
            LoadOrCreateScene(BuildScript.Scenes[2], () => BuildEndingScene(ending, storyBox, fader));
            LoadOrCreateScene(BuildScript.Scenes[0], BuildBootScene);
            SyncLevelOrder(levels);

            EditorBuildSettings.scenes = BuildScript.Scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentBuilder] Done. Open Assets/_Game/Scenes/Boot.unity and press Play.");
        }

        /// <summary>GameManager plays every LevelDefinition in day order.</summary>
        private static void SyncLevelOrder(IEnumerable<LevelDefinition> levels)
        {
            var ordered = levels.OrderBy(l => l.DayNumber).Cast<Object>().ToArray();
            EditScene(BuildScript.Scenes[0], scene =>
            {
                var manager = scene.GetRootGameObjects().Select(g => g.GetComponent<GameManager>()).First(m => m != null);
                WireArray(manager, "levels", ordered);
            });
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

        /// <summary>M2 added a background reference to StoryPlayer (note/phone colours); wire it on the existing prefab.</summary>
        private static void UpgradeStoryBox(GameObject prefab)
        {
            var player = prefab.GetComponent<StoryPlayer>();
            if (new SerializedObject(player).FindProperty("background").objectReferenceValue != null) return;

            var contents = PrefabUtility.LoadPrefabContents(StoryBoxPath);
            Wire(contents.GetComponent<StoryPlayer>(), "background", contents.GetComponent<Image>());
            PrefabUtility.SaveAsPrefabAsset(contents, StoryBoxPath);
            PrefabUtility.UnloadPrefabContents(contents);
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

        // ---------------------------------------------------------------- Kitchen

        private static void BuildKitchenScene(LevelDefinition fallbackLevel, GameObject storyBoxPrefab, GameObject faderPrefab)
        {
            var camera = CreateCamera(new Color(0.2f, 0.22f, 0.25f));
            camera.gameObject.AddComponent<Physics2DRaycaster>();

            var director = BuildKitchenWorld();
            var stepRoot = new GameObject("StepRoot").transform;

            var canvas = CreateCanvas();
            var meterHim = BuildMeterView(canvas, "WarmthMeter_Him", "You", new Vector2(40f, -40f));
            var meterMira = BuildMeterView(canvas, "WarmthMeter_Mira", "Mira", new Vector2(40f, -100f));
            var hint = BuildHint(canvas);
            var story = ((GameObject)PrefabUtility.InstantiatePrefab(storyBoxPrefab, canvas)).GetComponent<StoryPlayer>();
            var fader = ((GameObject)PrefabUtility.InstantiatePrefab(faderPrefab, canvas)).GetComponent<ScreenFader>();
            CreateEventSystem();

            var runner = new GameObject("LevelRunner").AddComponent<LevelRunner>();
            Wire(runner, "fallbackLevel", fallbackLevel);
            Wire(runner, "worldCamera", camera);
            Wire(runner, "kitchen", director);
            Wire(runner, "stepRoot", stepRoot);
            Wire(runner, "story", story);
            Wire(runner, "hint", hint);
            WireArray(runner, "meterViews", new Object[] { meterHim, meterMira });
            Wire(runner, "fader", fader);
        }

        /// <summary>Placeholder kitchen (grey boxes until real art exists) plus its KitchenDirector.</summary>
        private static KitchenDirector BuildKitchenWorld()
        {
            var root = new GameObject("Kitchen").transform;
            var wood = new Color(0.5f, 0.38f, 0.28f);
            var paper = new Color(0.96f, 0.94f, 0.88f);

            // Lights
            var globalLight = Empty(root, "Global Light 2D", Vector2.zero).gameObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            var lampLight = PointLight(root, "LampLight", new Vector2(0f, 4.6f), 10f);
            var miraLight = PointLight(root, "MiraWindowLight", new Vector2(1.6f, 2.6f), 2.5f);
            foreach (var light in new[] { globalLight, lampLight, miraLight })
                light.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();

            // Wall, window and courtyard
            Box(root, "Wall", Vector2.zero, new Vector2(19.2f, 10.8f), new Color(0.72f, 0.74f, 0.76f), 0, false, "Wall");
            Box(root, "CeilingLamp", new Vector2(0f, 5.1f), new Vector2(1.8f, 0.45f), Gray(0.9f), 1, false, "Wall");
            Box(root, "WindowFrame", new Vector2(0f, 1.7f), new Vector2(7.4f, 4.9f), wood, 1, false, "Wall");
            var courtyard = Box(root, "Courtyard", new Vector2(0f, 1.7f), new Vector2(7f, 4.5f), new Color(0.62f, 0.66f, 0.72f), 2, false, "Wall");
            Box(root, "OppositeWall", new Vector2(0f, 2.3f), new Vector2(7f, 3.3f), new Color(0.85f, 0.75f, 0.45f, 0.35f), 2, false, "Wall");
            Box(root, "GalleryRailing", new Vector2(0f, 1.2f), new Vector2(7f, 0.08f), Gray(0.2f), 3, false, "Wall");
            Box(root, "TreeTrunk", new Vector2(-2f, 0.5f), new Vector2(0.35f, 2.2f), new Color(0.35f, 0.25f, 0.2f), 4, false, "Wall");
            var treeCrown = Box(root, "TreeCrown", new Vector2(-2f, 2.3f), new Vector2(2.2f, 1.8f), Color.white, 5, false, "Wall");

            var miraWindow = Box(root, "MiraWindow", new Vector2(1.6f, 2.6f), new Vector2(1.1f, 1.3f), Color.white, 3, false, "Wall");
            var silhouette = Box(miraWindow.transform, "Silhouette", new Vector2(0f, -0.2f), new Vector2(0.45f, 0.8f), new Color(0.3f, 0.2f, 0.2f), 4, false, "Wall");
            var arm = Empty(silhouette.transform, "ArmPivot", new Vector2(0.2f, 0.25f));
            Box(arm, "Arm", new Vector2(0.22f, 0f), new Vector2(0.45f, 0.1f), new Color(0.3f, 0.2f, 0.2f), 4, false, "Wall");
            var note = Box(miraWindow.transform, "Note", new Vector2(0f, 0.05f), new Vector2(0.75f, 0.5f), paper, 6, false, "Wall");
            Box(root, "WindowSill", new Vector2(0f, -0.85f), new Vector2(7.8f, 0.25f), wood, 6, false, "Wall");

            // Left wall: shelf, clock, calendar
            Box(root, "Shelf", new Vector2(-7.2f, 1.2f), new Vector2(3.6f, 0.15f), wood, 1, false, "Wall");
            Box(root, "InstantCoffeeJar", new Vector2(-6.2f, 1.65f), new Vector2(0.6f, 0.75f), new Color(0.3f, 0.2f, 0.15f), 2, false, "Wall");
            var beans = Box(root, "BeansJar", new Vector2(-7.2f, 1.7f), new Vector2(0.6f, 0.85f), new Color(0.45f, 0.3f, 0.2f), 2, false, "Wall");
            var cinnamon = Box(root, "CinnamonJar", new Vector2(-8.2f, 1.6f), new Vector2(0.5f, 0.65f), new Color(0.65f, 0.35f, 0.2f), 2, false, "Wall");
            Box(root, "Clock", new Vector2(-8.1f, 3.7f), new Vector2(1.3f, 1.1f), paper, 1, false, "Wall");
            var clock = WorldText(root, "ClockTime", new Vector2(-8.1f, 3.7f), "00:00", 4f, Gray(0.15f), "Wall", 2);
            Box(root, "Calendar", new Vector2(-6.2f, 3.4f), new Vector2(1.5f, 1.8f), paper, 1, false, "Wall");
            Box(root, "CalendarHeader", new Vector2(-6.2f, 4.1f), new Vector2(1.5f, 0.4f), new Color(0.8f, 0.3f, 0.25f), 2, false, "Wall");
            var calendar = WorldText(root, "CalendarDay", new Vector2(-6.2f, 3.2f), "1", 9f, Gray(0.15f), "Wall", 2);

            // Right wall: fridge with notes, mug rack
            Box(root, "Fridge", new Vector2(8.9f, 0.6f), new Vector2(1.6f, 6.8f), Gray(0.93f), 1, false, "Wall");
            var notes = new[] { new Vector2(8.7f, 2.6f), new Vector2(9.1f, 1.8f), new Vector2(8.7f, 1f) }
                .Select((p, i) => (Object)Box(root, $"FridgeNote{i}", p, new Vector2(0.55f, 0.55f), new Color(1f, 0.9f, 0.5f), 2, false, "Wall").gameObject)
                .ToArray();
            Box(root, "MugRack", new Vector2(6.6f, 0.9f), new Vector2(1.8f, 0.1f), wood, 1, false, "Wall");
            Box(root, "ChippedMug", new Vector2(6.1f, 1.3f), new Vector2(0.55f, 0.65f), new Color(0.55f, 0.65f, 0.75f), 2, false, "Wall");
            var herMug = Box(root, "HerMug", new Vector2(7f, 1.3f), new Vector2(0.55f, 0.65f), new Color(0.93f, 0.9f, 0.82f), 2, false, "Wall");
            Box(herMug.transform, "LeafPrint", new Vector2(0.08f, 0.05f), new Vector2(0.18f, 0.18f), new Color(0.95f, 0.55f, 0.2f), 3, false, "Wall")
                .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Window sill plant
            Box(root, "PlantPot", new Vector2(2.9f, -0.5f), new Vector2(0.5f, 0.4f), new Color(0.7f, 0.4f, 0.3f), 7, false, "Wall");
            var plant = Box(root, "Plant", new Vector2(2.9f, -0.05f), new Vector2(0.65f, 0.6f), Color.white, 7, false, "Wall");

            // Counter and counter props (kept to the edges, clear of the minigame area)
            Box(root, "Counter", new Vector2(0f, -4.1f), new Vector2(19.2f, 2.6f), new Color(0.55f, 0.48f, 0.42f), 0, false, "Counter");
            Box(root, "CounterEdge", new Vector2(0f, -2.85f), new Vector2(19.2f, 0.15f), new Color(0.4f, 0.34f, 0.3f), 1, false, "Counter");
            var grinder = Box(root, "GrinderProp", new Vector2(-8.3f, -2.2f), new Vector2(0.8f, 1.2f), new Color(0.5f, 0.36f, 0.25f), 0);
            var milk = Box(root, "MilkProp", new Vector2(6.4f, -2.25f), new Vector2(0.7f, 1.1f), Gray(0.97f), 0);
            var scarf = Box(root, "Scarf", new Vector2(7.8f, -2.5f), new Vector2(1.4f, 0.4f), new Color(0.75f, 0.2f, 0.2f), 1);

            // Foreground
            var blanket = Box(root, "Blanket", new Vector2(-7.6f, -4.6f), new Vector2(3.4f, 1.3f), new Color(0.65f, 0.45f, 0.35f), 0, false, "Foreground");
            var miraAtDoor = Box(root, "MiraAtDoor", new Vector2(8.7f, -0.4f), new Vector2(1.6f, 4.2f), new Color(0.35f, 0.3f, 0.4f), 1, false, "Foreground");
            Box(miraAtDoor.transform, "Scarf", new Vector2(-0.1f, 1.2f), new Vector2(1.7f, 0.45f), new Color(0.75f, 0.2f, 0.2f), 2, false, "Foreground");
            Box(miraAtDoor.transform, "Hand", new Vector2(-0.9f, 0f), new Vector2(0.4f, 0.3f), new Color(0.9f, 0.75f, 0.65f), 2, false, "Foreground");

            var director = root.gameObject.AddComponent<KitchenDirector>();
            Wire(director, "globalLight", globalLight);
            Wire(director, "lampLight", lampLight);
            Wire(director, "miraLight", miraLight);
            Wire(director, "courtyard", courtyard);
            Wire(director, "treeCrown", treeCrown);
            Wire(director, "miraWindow", miraWindow);
            Wire(director, "miraSilhouette", silhouette.gameObject);
            Wire(director, "miraArm", arm);
            Wire(director, "miraNote", note.gameObject);
            Wire(director, "miraAtDoor", miraAtDoor.gameObject);
            Wire(director, "calendarDay", calendar);
            Wire(director, "clockTime", clock);
            Wire(director, "beans", beans.gameObject);
            Wire(director, "grinder", grinder.gameObject);
            Wire(director, "milk", milk.gameObject);
            Wire(director, "cinnamon", cinnamon.gameObject);
            Wire(director, "herMug", herMug.gameObject);
            Wire(director, "scarf", scarf.gameObject);
            Wire(director, "blanket", blanket.gameObject);
            Wire(director, "plant", plant);
            WireArray(director, "fridgeNotes", notes);
            return director;
        }

        private static Light2D PointLight(Transform parent, string name, Vector2 position, float radius)
        {
            var light = Empty(parent, name, position).gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightInnerRadius = radius * 0.2f;
            light.pointLightOuterRadius = radius;
            return light;
        }

        private static WarmthMeterView BuildMeterView(RectTransform canvas, string name, string caption, Vector2 position)
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

            var label = Label(frame, "Caption", caption, 26f, TextAlignmentOptions.MidlineLeft, Color.white);
            label.rectTransform.offsetMin = new Vector2(14f, 0f);

            var view = frame.gameObject.AddComponent<WarmthMeterView>();
            Wire(view, "fill", fill);
            Wire(view, "fillImage", fillImage);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.45f, 0.65f, 0.95f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.25f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            Edit(view, so => so.FindProperty("coldToWarm").gradientValue = gradient);
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

        // ---------------------------------------------------------------- Boot / Ending

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

        private static void BuildBootScene()
        {
            CreateCamera(new Color(0.12f, 0.13f, 0.18f));
            var canvas = CreateCanvas();

            var title = Label(canvas, "Title", "Until the First Snow", 96f, TextAlignmentOptions.Center, new Color(0.98f, 0.9f, 0.75f));
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1600f, 160f));

            var prompt = Label(canvas, "Prompt", "click to start", 40f, TextAlignmentOptions.Center, Color.white);
            Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(800f, 80f));
            var promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();

            new GameObject("GameManager").AddComponent<GameManager>();
            var boot = new GameObject("BootScreen").AddComponent<BootScreen>();
            Wire(boot, "prompt", promptGroup);
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
    }
}
