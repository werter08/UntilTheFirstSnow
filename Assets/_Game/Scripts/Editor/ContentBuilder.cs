using System.Collections.Generic;
using System.Linq;
using Courtyard.Audio;
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
        private const string SfxLibraryPath = "Assets/_Game/Data/Audio/SfxLibrary.asset";

        private static readonly string[] SortingLayerNames = { "Sky", "Courtyard", "Wall", "Counter", "Props", "Foreground" };
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private const float CameraSize = 5.4f; // 1080 px at 100 px per unit

        [MenuItem("Courtyard/Setup/Build Content (keep existing)")]
        public static void Build() => Build(false);

        /// <summary>
        /// Rebuilds generated minigame prefabs, level data and the Kitchen scene in place (GUIDs kept).
        /// Hand edits to those assets are lost; Boot, Ending and UI prefabs are kept.
        /// </summary>
        [MenuItem("Courtyard/Setup/Rebuild Generated Content (overwrite)")]
        public static void Rebuild() => Build(true);

        public static void Build(bool overwrite)
        {
            EnsureSortingLayers(SortingLayerNames);
            EnsureSquareSprite();
            EnsureVignetteSprite();
            var sfx = EnsureSfxLibrary();
            SyncSfxLibrary();

            var storyBox = LoadOrCreatePrefab(StoryBoxPath, BuildStoryBox);
            UpgradeStoryBox(storyBox);
            var fader = LoadOrCreatePrefab(FaderPath, BuildFader);

            var miraUsual = LevelDataBuilder.EnsureMiraUsual();
            var steps = MinigamePrefabBuilder.BuildAll(miraUsual, overwrite);
            var levels = LevelDataBuilder.BuildLevels(steps, overwrite);
            var ending = LevelDataBuilder.BuildEnding();

            LoadOrCreateScene(BuildScript.Scenes[1], () => BuildKitchenScene(levels[0], storyBox, fader), overwrite);
            LoadOrCreateScene(BuildScript.Scenes[2], () => BuildEndingScene(ending, storyBox, fader));
            LoadOrCreateScene(BuildScript.Scenes[0], BuildBootScene);
            SyncLevelOrder(levels);
            EnsureAudioManager(sfx);
            DecorBuilder.Apply();

            EditorBuildSettings.scenes = BuildScript.Scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentBuilder] Done. Open Assets/_Game/Scenes/Boot.unity and press Play.");
        }

        private static SfxLibrary EnsureSfxLibrary()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Audio")) AssetDatabase.CreateFolder("Assets/_Game/Data", "Audio");
            return LoadOrCreateAsset<SfxLibrary>(SfxLibraryPath, so =>
            {
                var ids = (SfxId[])System.Enum.GetValues(typeof(SfxId));
                var entries = so.FindProperty("entries");
                entries.arraySize = ids.Length;
                for (var i = 0; i < ids.Length; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("id").enumValueIndex = (int)ids[i];
                    entry.FindPropertyRelative("volume").floatValue = 1f;
                    entry.FindPropertyRelative("pitch").vector2Value = new Vector2(0.95f, 1.05f);
                }
            });
        }

        /// <summary>Adds a slot for every SfxId the library doesn't have yet (existing clips are kept).</summary>
        [MenuItem("Courtyard/Setup/Sync Sfx Library")]
        public static void SyncSfxLibrary()
        {
            var library = EnsureSfxLibrary();
            Edit(library, so =>
            {
                var entries = so.FindProperty("entries");
                var have = new HashSet<int>();
                for (var i = 0; i < entries.arraySize; i++)
                    have.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").enumValueIndex);

                foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
                {
                    if (have.Contains((int)id)) continue;
                    entries.InsertArrayElementAtIndex(entries.arraySize);
                    var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    entry.FindPropertyRelative("id").enumValueIndex = (int)id;
                    entry.FindPropertyRelative("clips").arraySize = 0;
                    entry.FindPropertyRelative("volume").floatValue = 1f;
                    entry.FindPropertyRelative("pitch").vector2Value = new Vector2(0.95f, 1.05f);
                }
            });
            AssetDatabase.SaveAssets();
        }

        /// <summary>The Boot scene owns the persistent AudioManager (audio starts after the title-screen click).</summary>
        private static void EnsureAudioManager(SfxLibrary sfx)
        {
            EditScene(BuildScript.Scenes[0], scene =>
            {
                if (scene.GetRootGameObjects().Any(g => g.GetComponent<AudioManager>() != null)) return;

                var go = new GameObject("AudioManager");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                Wire(go.AddComponent<AudioManager>(), "library", sfx);
            });
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
            var counter = CounterLayout.Build(director.transform);
            BuildMugSteam(counter);
            var fx = BuildFeedbackFx();
            var hands = BuildHands(camera);
            var stepRoot = new GameObject("StepRoot").transform;

            var canvas = CreateCanvas();
            var vignette = BuildVignette(canvas);
            var meterHim = BuildMeterView(canvas, "WarmthMeter_Him", "You", new Vector2(40f, -40f));
            var meterMira = BuildMeterView(canvas, "WarmthMeter_Mira", "Mira", new Vector2(40f, -108f));
            var hint = BuildHint(canvas);
            var story = ((GameObject)PrefabUtility.InstantiatePrefab(storyBoxPrefab, canvas)).GetComponent<StoryPlayer>();
            var fader = ((GameObject)PrefabUtility.InstantiatePrefab(faderPrefab, canvas)).GetComponent<ScreenFader>();
            var dayCard = BuildDayCard(canvas); // after the fader, so it shows over black
            CreateEventSystem();

            var runner = new GameObject("LevelRunner").AddComponent<LevelRunner>();
            Wire(runner, "fallbackLevel", fallbackLevel);
            Wire(runner, "worldCamera", camera);
            Wire(runner, "kitchen", director);
            Wire(runner, "counter", counter);
            Wire(runner, "stepRoot", stepRoot);
            Wire(runner, "story", story);
            Wire(runner, "hint", hint);
            WireArray(runner, "meterViews", new Object[] { meterHim, meterMira });
            Wire(runner, "fader", fader);
            Wire(runner, "fx", fx);
            Wire(runner, "hands", hands);
            Wire(runner, "dayCard", dayCard);
            Wire(runner, "vignette", vignette);
        }

        private static void BuildMugSteam(CounterStation counter)
        {
            var mug = new SerializedObject(counter).FindProperty("mug").objectReferenceValue as Component;
            var steam = Particles(mug.transform, "Steam", new Vector2(0f, 0.75f), 0.6f, new Vector2(0f, 0.7f),
                new Color(1f, 1f, 1f, 0.35f), 0.18f, 1.6f, 7f, "Props", 6);
            var main = steam.main;
            main.playOnAwake = true;
            var size = steam.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var colour = steam.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            Wire(counter, "mugSteam", steam);
        }

        private static FeedbackFx BuildFeedbackFx()
        {
            var root = new GameObject("FeedbackFx");
            var popup = WorldText(root.transform, "PopupTemplate", Vector2.zero, "Perfect!", 12f, new Color(1f, 0.85f, 0.4f), "Foreground", 60);
            popup.fontStyle = FontStyles.Bold;
            popup.gameObject.SetActive(false); // template; FeedbackFx clones it
            var fx = root.AddComponent<FeedbackFx>();
            Wire(fx, "popupTemplate", popup);
            return fx;
        }

        /// <summary>The clerk's hand: hand-drawn pose sprites (HandArt), or a placeholder box if they are missing.</summary>
        private static HandsView BuildHands(Camera camera)
        {
            var root = new GameObject("Hands");
            var poses = ((HandPose[])System.Enum.GetValues(typeof(HandPose)))
                .Select(p => (pose: p, grey: HandArt.Load(p, Sweater.Grey), beige: HandArt.Load(p, Sweater.Beige)))
                .ToArray();
            SpriteRenderer hand;
            var first = poses.FirstOrDefault(p => p.grey != null).grey;
            if (first != null)
            {
                hand = Empty(root.transform, "Hand", Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
                hand.sprite = first;
                hand.sortingLayerName = "Foreground";
                hand.sortingOrder = 40;
            }
            else
            {
                hand = Box(root.transform, "Hand", Vector2.zero, new Vector2(0.7f, 0.8f), new Color(0.93f, 0.78f, 0.66f), 40, false, "Foreground");
            }

            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
                renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, 0f); // HandsView fades it in
            var view = root.AddComponent<HandsView>();
            Wire(view, "worldCamera", camera);
            Wire(view, "hand", hand);
            Edit(view, so =>
            {
                var array = Property(so, "poses");
                array.arraySize = poses.Length;
                for (var i = 0; i < poses.Length; i++)
                {
                    var element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("pose").enumValueIndex = (int)poses[i].pose;
                    element.FindPropertyRelative("grey").objectReferenceValue = poses[i].grey;
                    element.FindPropertyRelative("beige").objectReferenceValue = poses[i].beige;
                }
            });
            return view;
        }

        private static WarmthVignette BuildVignette(RectTransform canvas)
        {
            var root = UiObject("WarmthVignette", canvas);
            Stretch(root);
            var frost = UiObject("Frost", root);
            Stretch(frost);
            var frostImage = Img(frost, new Color(0.78f, 0.9f, 1f, 0f));
            frostImage.sprite = BuilderKit.Vignette;
            var warm = UiObject("Warm", root);
            Stretch(warm);
            var warmImage = Img(warm, new Color(1f, 0.62f, 0.3f, 0f));
            warmImage.sprite = BuilderKit.Vignette;

            var vignette = root.gameObject.AddComponent<WarmthVignette>();
            Wire(vignette, "frost", frostImage);
            Wire(vignette, "warm", warmImage);
            return vignette;
        }

        private static DayCard BuildDayCard(RectTransform canvas)
        {
            var root = UiObject("DayCard", canvas);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var page = UiObject("Page", root);
            Place(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 240f), new Vector2(420f, 480f));
            Img(page, new Color(0.96f, 0.94f, 0.88f));
            var header = UiObject("Header", page);
            Place(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 110f));
            Img(header, new Color(0.8f, 0.3f, 0.25f));
            Label(header, "Label", "DAY", 56f, TextAlignmentOptions.Center, Color.white);

            var number = Label(page, "Number", "1", 200f, TextAlignmentOptions.Center, new Color(0.2f, 0.17f, 0.15f));
            number.rectTransform.offsetMax = new Vector2(0f, -110f);
            number.rectTransform.offsetMin = new Vector2(0f, 90f);
            var clock = Label(page, "Clock", "06:40", 48f, TextAlignmentOptions.Bottom, new Color(0.35f, 0.3f, 0.28f));
            clock.rectTransform.offsetMin = new Vector2(0f, 30f);

            var card = root.gameObject.AddComponent<DayCard>();
            Wire(card, "group", group);
            Wire(card, "page", page);
            Wire(card, "dayNumber", number);
            Wire(card, "clockTime", clock);
            return card;
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
            // Wall objects (not UI): clock above the left shelf, calendar right of the window. The top-left corner stays free for the warmth meters.
            Box(root, "Clock", new Vector2(-7.3f, 3.15f), new Vector2(1.2f, 1f), paper, 1, false, "Wall");
            var clock = WorldText(root, "ClockTime", new Vector2(-7.3f, 3.15f), "00:00", 3.6f, Gray(0.15f), "Wall", 2);
            Box(root, "Calendar", new Vector2(5.3f, 2.85f), new Vector2(1.4f, 1.7f), paper, 1, false, "Wall");
            Box(root, "CalendarHeader", new Vector2(5.3f, 3.5f), new Vector2(1.4f, 0.4f), new Color(0.8f, 0.3f, 0.25f), 2, false, "Wall");
            var calendar = WorldText(root, "CalendarDay", new Vector2(5.3f, 2.65f), "1", 8.5f, Gray(0.15f), "Wall", 2);

            // Right wall: fridge with notes, mug rack
            // Fridge stands on the floor (to the bottom of the screen); the tabletop ends at its left edge (x = 6.8).
            Box(root, "Fridge", new Vector2(8.2f, -0.6f), new Vector2(2.8f, 9.6f), Gray(0.93f), 1, false, "Wall");
            var notes = new[] { new Vector2(7.6f, 2.6f), new Vector2(8.5f, 1.8f), new Vector2(7.8f, 1f) }
                .Select((p, i) => (Object)Box(root, $"FridgeNote{i}", p, new Vector2(0.55f, 0.55f), new Color(1f, 0.9f, 0.5f), 2, false, "Wall").gameObject)
                .ToArray();
            Box(root, "MugRack", new Vector2(5.3f, 0.75f), new Vector2(1.6f, 0.1f), wood, 1, false, "Wall");
            Box(root, "ChippedMug", new Vector2(4.9f, 1.15f), new Vector2(0.55f, 0.65f), new Color(0.55f, 0.65f, 0.75f), 2, false, "Wall");
            var herMug = Box(root, "HerMug", new Vector2(5.7f, 1.15f), new Vector2(0.55f, 0.65f), new Color(0.93f, 0.9f, 0.82f), 2, false, "Wall");
            Box(herMug.transform, "LeafPrint", new Vector2(0.08f, 0.05f), new Vector2(0.18f, 0.18f), new Color(0.95f, 0.55f, 0.2f), 3, false, "Wall")
                .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Window sill plant
            Box(root, "PlantPot", new Vector2(2.9f, -0.5f), new Vector2(0.5f, 0.4f), new Color(0.7f, 0.4f, 0.3f), 7, false, "Wall");
            var plant = Box(root, "Plant", new Vector2(2.9f, -0.05f), new Vector2(0.65f, 0.6f), Color.white, 7, false, "Wall");

            // Counter (tools are built by CounterLayout)
            Box(root, "Counter", new Vector2(0f, -4.1f), new Vector2(19.2f, 2.6f), new Color(0.55f, 0.48f, 0.42f), 0, false, "Counter");
            Box(root, "CounterEdge", new Vector2(0f, -2.85f), new Vector2(19.2f, 0.15f), new Color(0.4f, 0.34f, 0.3f), 1, false, "Counter");
            var scarf = Box(root, "Scarf", new Vector2(-9f, -2.55f), new Vector2(1f, 0.35f), new Color(0.75f, 0.2f, 0.2f), 1);

            // Foreground
            var blanket = Box(root, "Blanket", new Vector2(-7.6f, -4.6f), new Vector2(3.4f, 1.3f), new Color(0.65f, 0.45f, 0.35f), 0, false, "Foreground");
            var miraAtDoor = Box(root, "MiraAtDoor", new Vector2(8.7f, -0.4f), new Vector2(1.6f, 4.2f), new Color(0.35f, 0.3f, 0.4f), 1, false, "Foreground");
            Box(miraAtDoor.transform, "Scarf", new Vector2(-0.1f, 1.2f), new Vector2(1.7f, 0.45f), new Color(0.75f, 0.2f, 0.2f), 2, false, "Foreground");
            Box(miraAtDoor.transform, "Hand", new Vector2(-0.9f, 0f), new Vector2(0.4f, 0.3f), new Color(0.9f, 0.75f, 0.65f), 2, false, "Foreground");

            // Weather, visible only inside the window.
            var mask = Empty(root, "WindowMask", new Vector2(0f, 1.7f)).gameObject.AddComponent<SpriteMask>();
            mask.sprite = Square;
            mask.transform.localScale = new Vector3(7f, 4.5f, 1f);
            var weatherTop = new Vector2(0f, 4.1f);
            var rain = Particles(root, "Rain", weatherTop, 7.5f, new Vector2(-0.6f, -9f), new Color(0.75f, 0.85f, 1f, 0.6f), 0.06f, 0.6f, 0f, "Wall", 6);
            var rainShape = rain.main;
            rainShape.startSize3D = true;
            rainShape.startSizeX = 0.04f;
            rainShape.startSizeY = 0.35f;
            rainShape.startRotation = 0f;
            var leaves = Particles(root, "Leaves", weatherTop, 7.5f, new Vector2(0.2f, -0.9f), new Color(0.9f, 0.5f, 0.2f), 0.16f, 5.5f, 0f, "Wall", 6);
            var spin = leaves.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var snow = Particles(root, "Snow", weatherTop, 7.5f, new Vector2(0.1f, -0.7f), new Color(1f, 1f, 1f, 0.9f), 0.09f, 7f, 0f, "Wall", 6);
            foreach (var system in new[] { rain, leaves, snow })
                system.GetComponent<ParticleSystemRenderer>().maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

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
            Wire(director, "cinnamon", cinnamon.gameObject);
            Wire(director, "herMug", herMug.gameObject);
            Wire(director, "scarf", scarf.gameObject);
            Wire(director, "blanket", blanket.gameObject);
            Wire(director, "plant", plant);
            WireArray(director, "fridgeNotes", notes);
            Edit(director, so =>
            {
                var presets = new (Weather weather, ParticleSystem system, float rate, float wind)[]
                {
                    (Weather.LightRain, rain, 45f, -0.6f),
                    (Weather.HeavyRain, rain, 160f, -1.5f),
                    (Weather.Leaves, leaves, 4f, 0.3f),
                    (Weather.Wind, leaves, 10f, 3.5f),
                    (Weather.Snow, snow, 30f, 0.15f),
                };
                var array = Property(so, "weatherPresets");
                array.arraySize = presets.Length;
                for (var i = 0; i < presets.Length; i++)
                {
                    var element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("weather").enumValueIndex = (int)presets[i].weather;
                    element.FindPropertyRelative("system").objectReferenceValue = presets[i].system;
                    element.FindPropertyRelative("emissionRate").floatValue = presets[i].rate;
                    element.FindPropertyRelative("wind").floatValue = presets[i].wind;
                }
            });
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

            var glow = UiObject("Glow", frame);
            Stretch(glow);
            glow.offsetMin = new Vector2(-8f, -8f);
            glow.offsetMax = new Vector2(8f, 8f);
            var glowImage = Img(glow, new Color(1f, 0.8f, 0.45f, 0f));

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
            Wire(view, "glow", glowImage);
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
            // Bottom centre over the drawer fronts; never on screen together with the story box.
            Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1300f, 70f));
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
