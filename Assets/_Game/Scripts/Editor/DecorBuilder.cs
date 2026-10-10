using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// Swaps kitchen placeholders for decor from asset packs (see CREDITS.md). Idempotent: it finds objects by name
    /// and re-applies sprites and placement, so it also runs after the content builder regenerates the Kitchen scene.
    /// A missing sprite file leaves that placeholder as it is.
    /// </summary>
    public static class DecorBuilder
    {
        private const string OddblotFolder = "Assets/_Game/Art/Sprites/Decor/ODDBLOT/";
        private const string WindowFolder = "Assets/_Game/Art/Sprites/Window/";
        private const string KitchenFolder = "Assets/_Game/Art/Sprites/Kitchen/";
        private const string KettleFolder = "Assets/_Game/Art/Sprites/Kettle/";
        private const string PropsFolder = "Assets/_Game/Art/Props/";
        private const string DrinksFolder = "Assets/_Game/Art/Drinks/";
        private const string FxFolder = "Assets/_Game/Art/FX/";

        // Kettle parts, offsets from the standing kettle's centre (px at 100 px/unit), measured by matching the parts
        // against the assembled drawing in Kettle_Sheet_Source.png.
        private static readonly (string sprite, string name, Vector2 offsetPx, int order)[] KettleParts =
        {
            ("Kettle_Handle", "Handle", new Vector2(73.5f, 7.5f), 2),
            ("Kettle_Spout", "Spout", new Vector2(-79.5f, 7f), 2),
            ("Kettle_Body", "Body", new Vector2(-6f, -8f), 3),
            ("Kettle_Badge", "Badge", new Vector2(-6f, -67.5f), 4),
        };
        private static readonly Vector2 LidOffsetPx = new(-6f, 68f);
        private static readonly Vector2 KnobOffsetPx = new(31f, 85f);
        private static readonly Vector2 IdleSpoutTipPx = new(-90f, 61f);
        private static readonly Vector2 PourSpoutPivot = new(0.985f, 0.818f);
        private const float KettleX = -7f;
        private const float KettleBaseY = -2.54f; // base ellipse centre, before CounterLift
        private const float KettleCentreY = -1.56f; // standing kettle centre on the base, before CounterLift

        // Counter (top + drawer fronts) as in the layout mockup: back edge at y ≈ -1.47, drawers to the screen bottom,
        // right end hidden behind the fridge. Uniform scale (the art is ~4.16:1).
        private static readonly Rect TableRect = Rect.MinMaxRect(-9.7f, -5.5f, 6.9f, -1.47f);

        // Counter tools and minigame gauges were laid out for a counter top at y = -2.75; lift them onto the new surface.
        private const float CounterLift = 0.85f;

        // Fridge stands in front of the counter, on the floor; the drawing's body outline starts 48 px in from its left edge.
        private const float FridgeBodyLeft = 6.67f;
        private const float FridgeOutlineInset = 0.48f;

        // Window layout (world units). The frame art sets the size; everything else hangs off it.
        private static readonly Vector2 WindowCenter = new(0f, 1.75f);
        private const float FrameHeight = 4.8f;
        private static readonly Vector2 GlassSize = new(5.8f, 4.4f);
        private static readonly Vector2 ShadowOffset = new(0.07f, -0.09f);
        private static readonly Color WallColour = new(0.86f, 0.83f, 0.8f);

        [MenuItem("Courtyard/Setup/Apply Pack Decor")]
        /// <summary>
        /// Final import settings for every art folder. Run before anything stores sprite references: re-importing a
        /// texture with different settings can give its sprite a new ID and break earlier references.
        /// </summary>
        public static void ConfigureArtImports()
        {
            ConfigureImports(OddblotFolder);
            ConfigureImports(WindowFolder);
            ConfigureImports(KitchenFolder);
            ConfigureImports(KettleFolder);
            ConfigureImports(PropsFolder);
            ConfigureImports(DrinksFolder);
            ConfigureImports(FxFolder);
        }

        public static void Apply()
        {
            ConfigureArtImports();
            ArtManifestBuilder.EnsureSortingLayers();
            _layout = ArtManifestBuilder.LoadOrCreateLayout();
            _sleeves = SleeveBuilder.BuildLibrary();
            ConfigureImports(OddblotFolder);
            ConfigureImports(WindowFolder);
            ConfigureImports(KitchenFolder);
            ConfigureImports(KettleFolder);
            ConfigureImports(PropsFolder);
            ConfigureImports(DrinksFolder);
            SetPivot(KettleFolder + "Kettle_HandGrey.png", PourSpoutPivot);
            SetPivot(KettleFolder + "Kettle_HandBeige.png", PourSpoutPivot);
            EditScene(BuildScript.Scenes[1], ApplyToKitchen);
            EditScene(BuildScript.Scenes[0], ApplyToBoot);
            EditScene(BuildScript.Scenes[2], ApplyToEnding);
            AssetDatabase.SaveAssets();
        }

        private static KitchenArtLayout _layout;
        private static SleeveLibrary _sleeves;

        private static void ApplyToKitchen(Scene scene)
        {
            var kitchen = scene.GetRootGameObjects().First(g => g.name == "Kitchen").transform;
            ApplyWindow(kitchen);
            ApplyWallBackground(kitchen);
            ApplyTable(kitchen);
            ApplyFridge(kitchen);
            ApplyKettle(kitchen);
            ApplyMugs(kitchen);
            ApplyGrinder(kitchen);
            ApplyMilk(kitchen);
            ApplyJars(kitchen);
            ApplyClock(kitchen);
            ApplyCalendar(kitchen);
            ApplyLamp(kitchen);
            ApplyPlant(kitchen);
            ApplyLife(scene, kitchen);
            ArtManifestBuilder.Apply(scene, kitchen, _layout); // manifest zones, layers and file slots win
            SleeveBuilder.WireScene(scene, _sleeves);

            // Ceiling: hanging bulb; the lamp light sits on the bulb.
            // Fallback bulb from the pack only when the team's lamp drawing isn't there (ApplyLamp handles it).
            if (AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "lamp.png") == null
                && Fit(kitchen, "CeilingLamp", "Oddblot_light", new Vector2(0f, 5.05f), 1.6f, "Wall", 13))
                Find(kitchen, "LampLight").localPosition = new Vector3(0f, 4.8f, 0f);

            // Left wall: shelf with dishes (replaces the plank and the instant coffee jar placeholder).
            if (Fit(kitchen, "Shelf", "Oddblot_shelf", new Vector2(-7.3f, 1.6f), 1.9f, "Wall", 1))
                Find(kitchen, "InstantCoffeeJar").gameObject.SetActive(false);

            // Bag of real beans from Day 5 ("Bought real beans today") stands at the left end of the counter.
            Fit(kitchen, "BeansJar", "Oddblot_deco", new Vector2(-8.85f, -2.2f + CounterLift), 1.15f, "Props", 1);
            Find(kitchen, "CinnamonJar").localPosition = new Vector3(-8.1f, -2.4f + CounterLift, 0f);
            SetLayer(Find(kitchen, "CinnamonJar"), "Props", 1);
            // Day 30: her scarf lies on the front of the counter, between the kettle and the grinder.
            Find(kitchen, "Scarf").localPosition = new Vector3(-5.7f, -2.75f, 0f);

            // Window sill: flower in a glass (tinted per PlantStage by the KitchenDirector).
            if (AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "plant_wilted.png") == null
                && Fit(kitchen, "Plant", "Oddblot_tbldeco2", new Vector2(1.4f, -0.12f), 1f, "Wall", 12))
            {
                Find(kitchen, "PlantPot").gameObject.SetActive(false);
                Edit(kitchen.GetComponent<KitchenDirector>(), so =>
                {
                    var tints = new[]
                    {
                        new Color(0.72f, 0.62f, 0.5f), new Color(0.88f, 0.84f, 0.74f), Color.white, new Color(1f, 0.93f, 0.95f),
                    };
                    var array = Property(so, "plantStageColors");
                    array.arraySize = tints.Length;
                    for (var i = 0; i < tints.Length; i++) array.GetArrayElementAtIndex(i).colorValue = tints[i];
                });
            }

            // Right wall: a small framed picture above the mug rack.
            var picture = Find(kitchen, "WallPicture", false);
            if (picture == null)
            {
                picture = Empty(kitchen, "WallPicture", Vector2.zero);
                picture.gameObject.AddComponent<SpriteRenderer>();
            }

            Fit(kitchen, "WallPicture", "Oddblot_walldeco", new Vector2(-5f, 2.55f), 1.25f, "Wall", 1);
        }

        /// <summary>
        /// Hand-drawn window: frame with transparent glass over the courtyard, curtain rod, two curtains (one drawing,
        /// mirrored), window sill, and soft drop shadows. Mira's window and the tree move so the curtains never hide her.
        /// </summary>
        private static void ApplyWindow(Transform kitchen)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(WindowFolder + "Window_Frame.png") == null) return;

            Find(kitchen, "Wall").GetComponent<SpriteRenderer>().color = WallColour;

            // Glass area: courtyard, weather mask and everything seen outside.
            var glass = Find(kitchen, "Courtyard").GetComponent<SpriteRenderer>();
            glass.transform.localPosition = WindowCenter;
            glass.size = GlassSize;
            Find(kitchen, "WindowMask").localScale = new Vector3(GlassSize.x, GlassSize.y, 1f);
            Find(kitchen, "WindowMask").localPosition = WindowCenter;
            foreach (var name in new[] { "OppositeWall", "GalleryRailing" })
            {
                var piece = Find(kitchen, name).GetComponent<SpriteRenderer>();
                piece.size = new Vector2(GlassSize.x, piece.size.y);
            }

            Find(kitchen, "MiraWindow").localPosition = new Vector3(0.85f, 2.75f, 0f);
            Find(kitchen, "MiraWindowLight").localPosition = new Vector3(0.85f, 2.75f, 0f);
            Find(kitchen, "TreeTrunk").localPosition = new Vector3(-1.5f, 0.5f, 0f);
            Find(kitchen, "TreeCrown").localPosition = new Vector3(-1.5f, 2.3f, 0f);

            // Frame in front of the courtyard, its shadow just behind it.
            Fit(kitchen, "WindowFrame", "Window_Frame", WindowCenter, FrameHeight, "Wall", 9, WindowFolder);
            var frame = Find(kitchen, "WindowFrame");
            ShadowOf(kitchen, frame, "WindowFrameShadow", "Window_FrameShadow", 8);

            var top = WindowCenter.y + FrameHeight * 0.5f;
            var bottom = WindowCenter.y - FrameHeight * 0.5f;

            // Sill under the frame.
            Fit(kitchen, "WindowSill", "Window_Sill", new Vector2(0f, bottom - 0.14f), 0.5f, "Wall", 11, WindowFolder);
            ShadowOf(kitchen, Find(kitchen, "WindowSill"), "WindowSillShadow", "Window_SillShadow", 7);

            // Curtains hang from the rod down to the sill; the drawing is the right curtain, the left one is mirrored.
            var curtainHeight = top + 0.2f - (bottom + 0.05f);
            var halfWidth = FrameHeight * 1.3f * 0.5f;
            EnsureSprite(kitchen, "CurtainRight");
            EnsureSprite(kitchen, "CurtainLeft");
            Fit(kitchen, "CurtainRight", "Window_Curtain", new Vector2(halfWidth - 0.7f, (top + 0.2f + bottom + 0.05f) * 0.5f), curtainHeight, "Wall", 10, WindowFolder);
            Fit(kitchen, "CurtainLeft", "Window_Curtain", new Vector2(-(halfWidth - 0.7f), (top + 0.2f + bottom + 0.05f) * 0.5f), curtainHeight, "Wall", 10, WindowFolder);
            Find(kitchen, "CurtainLeft").GetComponent<SpriteRenderer>().flipX = true;

            // Rod across the top, over the curtain tops.
            EnsureSprite(kitchen, "CurtainRod");
            Fit(kitchen, "CurtainRod", "Window_Rod", new Vector2(0f, top + 0.22f), 0.25f, "Wall", 12, WindowFolder);
            ShadowOf(kitchen, Find(kitchen, "CurtainRod"), "CurtainRodShadow", "Window_RodShadow", 7);
        }

        /// <summary>Full-screen wall with the tiled backsplash, drawn at 1920×1080 (one pixel = 0.01 unit).</summary>
        private static void ApplyWallBackground(Transform kitchen)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitchenFolder + "Wall_Background.png");
            if (sprite == null) return;

            var wall = Find(kitchen, "Wall");
            var renderer = wall.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            SetLayer(wall, "Wall", 0);

            var size = sprite.bounds.size;
            wall.localScale = new Vector3(19.2f / size.x, 10.8f / size.y, 1f);
            wall.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Fridge drawn at screen scale (1 px = 0.01 unit), standing on the floor with its left edge where the counter ends.
        /// A shopping list hangs on the freezer door; Mira's notes (Day 10+) are held by magnets.
        /// </summary>
        private static void ApplyFridge(Transform kitchen)
        {
            var fridgeSprite = LoadKitchen("Fridge");
            if (fridgeSprite == null) return;

            // In front of the counter: Counter layer, above the tabletop.
            var fridge = Find(kitchen, "Fridge");
            PlaceAtScreenScale(fridge, fridgeSprite, Vector2.zero, "Counter", 20);
            var size = fridgeSprite.bounds.size;
            fridge.localPosition = new Vector3(FridgeBodyLeft - FridgeOutlineInset + size.x * 0.5f, -5.4f + size.y * 0.5f, 0f);

            // The new drawing already has the handles, magnets and shopping list.
            var list = Find(kitchen, "ShoppingList", false);
            if (list != null) list.gameObject.SetActive(false);

            // Mira's notes (Day 10+) on the empty lower door, each held by a magnet.
            var notePositions = new[] { new Vector2(7.7f, -1.3f), new Vector2(8.75f, -1.9f), new Vector2(7.95f, -2.7f) };
            var magnets = new[] { "Magnet_Yellow", "Magnet_Blue", "Magnet_Orange" };
            for (var i = 0; i < notePositions.Length; i++)
            {
                var note = Find(kitchen, $"FridgeNote{i}");
                note.localPosition = notePositions[i];
                SetLayer(note, "Counter", 21);
                AddMagnet(note, magnets[i], 0.25f);
                SetLayer(note.Find("Magnet"), "Counter", 22);
            }
        }

        /// <summary>
        /// Electric kettle from the team's sprite sheet: base, switch (green = ready, red = heating), the standing kettle
        /// built from parts so the lid can rattle, spout steam, and the "hand holding the kettle" sprites for pouring.
        /// The kettle root is the pouring spout tip; the standing kettle and its click area sit around it.
        /// </summary>
        private static void ApplyKettle(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(KettleFolder + n + ".png");
            if (Load("Kettle_Body") == null) return;

            var tools = Find(kitchen, "Counter Tools");
            var counter = tools.GetComponent<CounterStation>();
            var kettle = Find(tools, "Kettle");
            kettle.localPosition = new Vector3(KettleX, KettleCentreY, 0f);
            kettle.localRotation = Quaternion.identity;

            // Base and switch.
            var baseObj = Find(tools, "KettleBase");
            PlaceAtScreenScale(baseObj, Load("Kettle_Base"), new Vector2(KettleX, KettleBaseY), "Props", 1);
            var switchObj = Find(tools, "KettleSwitch");
            PlaceAtScreenScale(switchObj, Load("Kettle_SwitchReady"), new Vector2(KettleX + 0.85f, KettleBaseY - 0.02f), "Props", 5);
            switchObj.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.5f);

            // Standing kettle, built from parts.
            var idle = Child(kettle, "Idle");
            idle.localPosition = Vector3.zero;
            foreach (var (spriteName, partName, offset, order) in KettleParts)
            {
                var part = MoveUnder(kettle, idle, partName);
                PlaceAtScreenScale(part, Load(spriteName), offset / 100f, "Props", order);
                var oldTarget = part.GetComponent<PointerTarget>(); // before its required collider
                if (oldTarget != null) Object.DestroyImmediate(oldTarget);
                var oldCollider = part.GetComponent<BoxCollider2D>();
                if (oldCollider != null) Object.DestroyImmediate(oldCollider);
            }

            var lid = Child(idle, "Lid");
            lid.localPosition = LidOffsetPx / 100f;
            PlaceAtScreenScale(SpriteChild(lid, "LidBar"), Load("Kettle_Lid"), Vector2.zero, "Props", 5);
            PlaceAtScreenScale(SpriteChild(lid, "Knob"), Load("Kettle_Knob"), (KnobOffsetPx - LidOffsetPx) / 100f, "Props", 6);

            var steam = Find(idle, "SpoutSteam", false);
            if (steam == null)
            {
                var system = Particles(idle, "SpoutSteam", IdleSpoutTipPx / 100f, 0.15f, new Vector2(-0.25f, 0.9f),
                    new Color(1f, 1f, 1f, 0.45f), 0.16f, 1.3f, 0f, "Props", 7);
                var main = system.main;
                main.playOnAwake = true;
                var size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
                steam = system.transform;
            }

            // Click area outside the visuals, so hiding the standing kettle while pouring never cancels the hold.
            var hit = Child(kettle, "Hit");
            hit.localPosition = Vector3.zero;
            var hitCollider = GetOrAdd<BoxCollider2D>(hit);
            hitCollider.size = new Vector2(1.9f, 1.85f);
            var hitTarget = GetOrAdd<PointerTarget>(hit);
            WireEnum(hitTarget, "handPose", HandPose.Hidden);

            // Hand holding the kettle (pivot = spout tip = kettle root while pouring).
            var pourGrey = SpriteChild(kettle, "PourGrey");
            PlaceAtScreenScale(pourGrey, Load("Kettle_HandGrey"), Vector2.zero, "Props", 8);
            pourGrey.GetComponent<SpriteRenderer>().enabled = false;
            var pourBeige = SpriteChild(kettle, "PourBeige");
            PlaceAtScreenScale(pourBeige, Load("Kettle_HandBeige"), Vector2.zero, "Props", 8);
            pourBeige.GetComponent<SpriteRenderer>().enabled = false;

            var view = GetOrAdd<KettleView>(kettle);
            Wire(view, "idle", idle.gameObject);
            Wire(view, "lid", lid);
            Wire(view, "badge", Find(idle, "Badge").GetComponent<SpriteRenderer>());
            Wire(view, "spoutSteam", steam.GetComponent<ParticleSystem>());
            Wire(view, "switchRenderer", switchObj.GetComponent<SpriteRenderer>());
            Wire(view, "switchReady", Load("Kettle_SwitchReady"));
            Wire(view, "switchOn", Load("Kettle_SwitchOn"));
            Wire(view, "pourGrey", pourGrey.GetComponent<SpriteRenderer>());
            Wire(view, "pourBeige", pourBeige.GetComponent<SpriteRenderer>());
            Wire(view, "stream", Find(kettle, "Stream"));

            Wire(counter, "kettleView", view);
            Wire(counter, "kettleTarget", hitTarget);
            Wire(counter, "kettleBody", Find(idle, "Body").GetComponent<SpriteRenderer>());
        }

        // Unity's fake-null means `??` can't be used with GetComponent in the Editor.
        private static T GetOrAdd<T>(Transform target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Mug art (side view, 1 px = 0.01 unit): the counter mug swaps per day (CounterStation.mugSprites); its fill
        /// becomes a thin gauge beside it, since a side-view mug can't show its contents. Spare mugs on the wall rack;
        /// Day 30's two mugs on the window sill.
        /// </summary>
        private static void ApplyMugs(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + n + ".png");
            var chipped = Load("mug_chipped");
            if (chipped == null) return;

            var tools = Find(kitchen, "Counter Tools");
            var counter = tools.GetComponent<CounterStation>();
            var mug = Find(tools, "Mug");
            var mugRenderer = mug.GetComponent<SpriteRenderer>();
            mugRenderer.sprite = chipped;
            mugRenderer.drawMode = SpriteDrawMode.Simple;
            mugRenderer.color = Color.white;
            mug.localScale = Vector3.one;
            var collider = mug.GetComponent<BoxCollider2D>();
            collider.size = chipped.bounds.size;
            collider.offset = chipped.bounds.center;
            Find(mug, "LeafPrint").gameObject.SetActive(false);
            Find(mug, "Foam").GetComponent<SpriteRenderer>().enabled = false; // foam stays as state, not drawn
            Edit(counter, so =>
            {
                var array = Property(so, "mugSprites");
                array.arraySize = 3;
                array.GetArrayElementAtIndex((int)MugStyle.Chipped).objectReferenceValue = chipped;
                array.GetArrayElementAtIndex((int)MugStyle.His).objectReferenceValue = Load("mug_his");
                array.GetArrayElementAtIndex((int)MugStyle.Hers).objectReferenceValue = Load("mug_hers");
            });

            // V60 art (placed on the mug by the manifest pass) and its little shadow on the rim.
            var v60Empty = Load("v60_empty");
            if (v60Empty != null)
            {
                var v60 = Find(tools, "V60");
                PlaceAtScreenScale(v60, v60Empty, v60.localPosition, "Props", 4);
                Wire(counter, "dripperRenderer", v60.GetComponent<SpriteRenderer>());
                Wire(counter, "dripperEmpty", v60Empty);
                Wire(counter, "dripperGrounds", Load("v60_grounds"));
                var shadowSprite = Load("v60_shadow");
                if (shadowSprite != null)
                    PlaceAtScreenScale(SpriteChild(v60, "Shadow"), shadowSprite, new Vector2(-0.07f, -v60Empty.bounds.extents.y + 0.02f), "Props", 3);
            }

            // The drink is shown inside the mug: a drink tile over the top of the mug body (the tiles are exactly the
            // body's width), revealed from the bottom up by a mask as the mug fills. Measured on the mug art:
            // body top 3 px below the sprite top, body centre 23.5 px left of the sprite centre (handle on the right).
            // As in the team's mockup: the cap's top edge and outline sit exactly on the mug's rim and outline.
            // Both PNGs carry 2 px of padding; caps are 110×66 px, the mug body is 110 px wide from its left edge.
            const float region = 0.66f;
            var regionCentre = new Vector2(chipped.bounds.min.x + 0.02f + 0.55f, chipped.bounds.max.y - 0.02f - region * 0.5f);

            var oldGauge = mug.Find("Gauge");
            var fill = Find(mug, "Fill");
            fill.SetParent(mug, false);
            if (oldGauge != null) Object.DestroyImmediate(oldGauge.gameObject);
            Wire(counter, "mugGauge", (Object)null);

            // The placeholder bar keeps the fill value for the minigames but no longer draws.
            fill.localPosition = regionCentre - new Vector2(0f, region * 0.5f);
            var bar = fill.GetComponent<SpriteBar>();
            Wire(bar, "width", 1.1f);
            Wire(bar, "maxHeight", region);
            Find(fill, "Liquid").GetComponent<SpriteRenderer>().sprite = null;
            var line = Find(fill, "FillLine").GetComponent<SpriteRenderer>();
            line.size = new Vector2(1.12f, 0.045f);
            line.color = new Color(1f, 1f, 1f, 0.9f);
            SetLayer(line.transform, "Props", 6);

            var contents = Child(mug, "Contents");
            contents.localPosition = regionCentre;
            // Empty mug: the plain dark cap (the inside of an empty mug), always under the drink.
            var empty = SpriteChild(contents, "EmptyCap");
            PlaceAtScreenScale(empty, AssetDatabase.LoadAssetAtPath<Sprite>(DrinksFolder + "drink_instant.png"), Vector2.zero, "Props", 2);
            var liquid = SpriteChild(contents, "Liquid");
            var liquidRenderer = liquid.GetComponent<SpriteRenderer>();
            liquidRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DrinksFolder + "drink_black.png");
            liquidRenderer.drawMode = SpriteDrawMode.Simple;
            liquidRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            liquid.localPosition = Vector3.zero;
            liquid.localScale = Vector3.one;
            SetLayer(liquid, "Props", 3);
            var spice = SpriteChild(contents, "Spice");
            var spiceRenderer = spice.GetComponent<SpriteRenderer>();
            spiceRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            spiceRenderer.drawMode = SpriteDrawMode.Sliced;
            spiceRenderer.size = new Vector2(0.96f, 0.06f);
            spiceRenderer.enabled = false;
            SetLayer(spice, "Props", 4);
            var maskTransform = Child(contents, "Mask");
            var mask = maskTransform.GetComponent<SpriteMask>();
            if (mask == null) mask = maskTransform.gameObject.AddComponent<SpriteMask>();
            mask.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath); // decor pass runs without the content build
            mask.isCustomRangeActive = true; // only the drink, never the weather / courtyard masks
            mask.frontSortingLayerID = SortingLayer.NameToID("Props");
            mask.frontSortingOrder = 3;
            mask.backSortingLayerID = SortingLayer.NameToID("Props");
            mask.backSortingOrder = 2;
            var mugContents = GetOrAdd<MugContents>(contents);
            Wire(mugContents, "liquid", liquidRenderer);
            Wire(mugContents, "emptyCap", empty.GetComponent<SpriteRenderer>());
            Wire(mugContents, "spice", spiceRenderer);
            Wire(mugContents, "mask", mask);
            Wire(mugContents, "regionHeight", region);
            Wire(mugContents, "regionWidth", 1.2f);
            Wire(counter, "mugContents", mugContents);
            Edit(counter, so =>
            {
                var drinks = Property(so, "mugDrinkSprites");
                drinks.arraySize = 3;
                // The plain dark tile is the inside of an EMPTY mug; instant coffee is the speckled dark one.
                drinks.GetArrayElementAtIndex((int)MugDrink.Instant).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(DrinksFolder + "drink_black.png");
                drinks.GetArrayElementAtIndex((int)MugDrink.Black).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(DrinksFolder + "drink_pourover.png");
                drinks.GetArrayElementAtIndex((int)MugDrink.Latte).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(DrinksFolder + "drink_latte.png");
            });

            // Spoonfuls of instant coffee now show as grounds inside the mug (CounterStation.ShowSpoons).
            for (var i = 0; i < 3; i++)
                Find(mug, $"SpoonMarker{i}").GetComponent<SpriteRenderer>().enabled = false;

            // Spare mugs on the wall rack.
            FitMug(Find(kitchen, "ChippedMug"), Load("mug_decoy_2"), new Vector2(4.9f, 0.99f), 0.4f);
            FitMug(Find(kitchen, "HerMug"), Load("mug_hers"), new Vector2(5.75f, 0.99f), 0.4f);
            var rackLeaf = Find(kitchen, "HerMug").Find("LeafPrint");
            if (rackLeaf != null) rackLeaf.gameObject.SetActive(false);

            // Day 30: his and her mugs side by side on the sill (Figma x 900 / 1020, standing on y 600).
            var sill = Child(kitchen, "SillMugs");
            sill.localPosition = Vector3.zero;
            FitMug(SpriteChild(sill, "SillMugHis"), Load("mug_his"), new Vector2(-0.6f, -0.54f + chipped.bounds.extents.y), 1f, "Wall", 12);
            FitMug(SpriteChild(sill, "SillMugHers"), Load("mug_hers"), new Vector2(0.6f, -0.54f + chipped.bounds.extents.y), 1f, "Wall", 12);
            Wire(kitchen.GetComponent<KitchenDirector>(), "sillMugs", sill.gameObject);
            Find(kitchen, "Plant").localPosition += new Vector3(2.15f - Find(kitchen, "Plant").GetComponent<SpriteRenderer>().bounds.center.x, 0f, 0f);
        }

        // Grinder sheet measurements (px at 100 px/unit): the crank's axle (knob) relative to the assembled drawing's
        // centre, the handle's pivot (round end) and its length to the grip.
        private static readonly Vector2 GrinderKnobPx = new(-1f, 98f);
        private static readonly Vector2 GrinderHandlePivot = new(0.101f, 0.511f);
        private const float GrinderHandleReach = 1.285f;

        /// <summary>
        /// Hand grinder: the assembled drawing stands on the counter; the handle turns around the knob on top, and the
        /// drag target sits on the handle's grip. Authored at the grinder zone like the placeholder (the manifest pass
        /// moves the whole group to the zone).
        /// </summary>
        private static void ApplyGrinder(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + n + ".png");
            var assembled = Load("grinder");
            var handleSprite = Load("grinder_handle");
            if (assembled == null || handleSprite == null) return;
            SetPivot(PropsFolder + "grinder_handle.png", GrinderHandlePivot);
            handleSprite = Load("grinder_handle");

            var grinder = Find(Find(kitchen, "Counter Tools"), "Grinder");
            var centre = new Vector2(CounterLayout.GrinderX, CounterLayout.Top + assembled.bounds.extents.y);
            var body = Find(grinder, "Body");
            PlaceAtScreenScale(body, assembled, centre, "Props", 1);
            Find(grinder, "Hopper").gameObject.SetActive(false);

            var arm = Find(grinder, "Arm");
            arm.localPosition = centre + GrinderKnobPx / 100f;
            var bar = Find(arm, "ArmBar");
            PlaceAtScreenScale(bar, handleSprite, Vector2.zero, "Props", 3);

            // The grip is drawn on the handle; the knob object stays as the invisible drag target at the grip.
            var knob = Find(arm, "Knob");
            knob.localPosition = new Vector3(GrinderHandleReach, 0f, 0f);
            knob.GetComponent<SpriteRenderer>().enabled = false;
            knob.GetComponent<BoxCollider2D>().size = new Vector2(0.7f, 0.7f);
        }

        private const float JugBodyOffset = -0.115f; // jug body centre vs sprite centre (handle on the right)

        /// <summary>
        /// Milk jug with a hand frother standing in it (whisk drawn behind the jug so only the part above the rim
        /// shows) and the foam blob on the rim. Authored at the milk zone; the manifest pass moves the group.
        /// </summary>
        private static void ApplyMilk(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + n + ".png");
            var jugSprite = Load("milk_jug");
            if (jugSprite == null) return;

            var tools = Find(kitchen, "Counter Tools");
            var counter = tools.GetComponent<CounterStation>();
            var station = Find(tools, "MilkStation");
            var top = CounterLayout.Top;
            var x = CounterLayout.MilkX;

            var jug = Find(station, "Jug");
            PlaceAtScreenScale(jug, jugSprite, new Vector2(x, top + jugSprite.bounds.extents.y), "Props", 3);
            var collider = jug.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(1.4f, 3.4f); // jug + frother
            collider.offset = new Vector2(-0.1f, 0.95f);
            Find(station, "Wand").gameObject.SetActive(false);

            var whisk = Load("frother_whisk");
            var body = Load("frother_body");
            var button = Load("frother_button");
            if (whisk != null && body != null && button != null)
            {
                var whiskBottom = top + 0.3f;
                var whiskCentre = whiskBottom + whisk.bounds.extents.y;
                var bodyCentre = whiskBottom + whisk.bounds.size.y + body.bounds.extents.y - 0.05f;
                var frother = Child(station, "Frother");
                frother.localPosition = new Vector3(x + JugBodyOffset, bodyCentre, 0f);
                PlaceAtScreenScale(SpriteChild(frother, "Whisk"), whisk, new Vector2(0f, whiskCentre - bodyCentre), "Props", 2);
                PlaceAtScreenScale(SpriteChild(frother, "Body"), body, Vector2.zero, "Props", 5);
                var buttonY = body.bounds.extents.y + button.bounds.extents.y + 0.02f;
                PlaceAtScreenScale(SpriteChild(frother, "Button"), button, new Vector2(0f, buttonY), "Props", 6);
                Wire(counter, "frother", frother);
                Wire(counter, "frotherButton", Find(frother, "Button").GetComponent<SpriteRenderer>());
            }

            var foam = Load("milk_foam");
            if (foam != null)
            {
                var blob = SpriteChild(station, "MilkFoamBlob");
                var rim = top + jugSprite.bounds.size.y - 0.05f;
                PlaceAtScreenScale(blob, foam, new Vector2(x + JugBodyOffset, rim - 0.08f), "Props", 4); // behind the frother
                Wire(counter, "milkFoamBlob", blob);
                Wire(counter, "foamMinScale", 0.08f); // a modest cap at the rim, not a splat
                Wire(counter, "foamMaxScale", 0.38f);
            }
        }

        private const float ToppingJarScale = 0.75f; // drawn jars are taller than the jar slots on the counter

        /// <summary>Drawn jars: cinnamon shaker and cocoa among the topping jars, cinnamon also as a counter prop.</summary>
        private static void ApplyJars(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + n + ".png");
            var art = new System.Collections.Generic.Dictionary<string, Sprite>
            {
                ["Cinnamon"] = Load("jar_cinnamon"),
                ["Cocoa"] = Load("jar_cocoa"),
            };

            var toppings = Find(Find(kitchen, "Counter Tools"), "Toppings");
            foreach (var (jarName, sprite) in art)
            {
                if (sprite == null) continue;
                var jar = Find(toppings, jarName);
                var x = jar.localPosition.x;
                var height = sprite.bounds.size.y * ToppingJarScale;
                PlaceAtScreenScale(jar, sprite, new Vector2(x, CounterLayout.Top + height * 0.5f), "Props", 1);
                jar.localScale = new Vector3(ToppingJarScale, ToppingJarScale, 1f);
                var collider = jar.GetComponent<BoxCollider2D>();
                collider.size = sprite.bounds.size;
                collider.offset = sprite.bounds.center;
                // Children (label, picked marker) are in the jar's local space: undo the scale for them.
                Find(jar, "Label").localPosition = new Vector3(0f, (sprite.bounds.extents.y + 0.25f) , 0f);
                Find(jar, "Label").localScale = Vector3.one / ToppingJarScale;
                Find(jar, "Picked").localPosition = new Vector3(0f, -sprite.bounds.extents.y - 0.12f, 0f);
                Find(jar, "Picked").localScale = Vector3.one / ToppingJarScale;
            }

            var cinnamon = Load("jar_cinnamon");
            if (cinnamon != null)
            {
                var prop = Find(kitchen, "CinnamonJar");
                PlaceAtScreenScale(prop, cinnamon, prop.localPosition, "Props", 1);
                prop.localScale = new Vector3(0.7f, 0.7f, 1f);
            }
        }

        /// <summary>Drawn wall clock above the shelf; the KitchenDirector turns the hands to the day's time.</summary>
        private static void ApplyClock(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + n + ".png");
            var face = Load("clock_face");
            if (face == null) return;
            SetPivot(PropsFolder + "clock_hour.png", new Vector2(0.5f, 0.12f));
            SetPivot(PropsFolder + "clock_minute.png", new Vector2(0.5f, 0.1f));

            var clock = Find(kitchen, "Clock");
            PlaceAtScreenScale(clock, face, clock.localPosition, "WallProps", 1);
            Find(kitchen, "ClockTime").gameObject.SetActive(false);

            var hour = SpriteChild(clock, "HourHand");
            PlaceAtScreenScale(hour, Load("clock_hour"), (Vector2)face.bounds.center, "WallProps", 2);
            var minute = SpriteChild(clock, "MinuteHand");
            PlaceAtScreenScale(minute, Load("clock_minute"), (Vector2)face.bounds.center, "WallProps", 3);

            var director = kitchen.GetComponent<KitchenDirector>();
            Wire(director, "clockHourHand", hour);
            Wire(director, "clockMinuteHand", minute);
        }

        /// <summary>Paper calendar right of the window; Unity writes the day number and a hand-drawn circle rings it.</summary>
        private static void ApplyCalendar(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + n + ".png");
            var sheet = Load("calendar");
            if (sheet == null) return;

            // World positions: these may sit under the CalendarGroup hanger (ApplyLife).
            var centre = KitchenArtLayout.ToWorld(_layout.calendarCentre);
            var calendarSheet = Find(kitchen, "Calendar");
            PlaceAtScreenScale(calendarSheet, sheet, Vector2.zero, "WallProps", 1);
            calendarSheet.position = centre;
            Find(kitchen, "CalendarHeader").gameObject.SetActive(false);

            var numberPos = centre + new Vector2(_layout.calendarNumberOffsetPx.x, -_layout.calendarNumberOffsetPx.y) / KitchenArtLayout.PixelsPerUnit;
            var number = Find(kitchen, "CalendarDay");
            number.position = numberPos;
            var text = number.GetComponent<TMPro.TextMeshPro>();
            text.color = new Color(0.17f, 0.14f, 0.13f);
            text.fontSize = 7.5f;
            text.sortingOrder = 2;

            var circle = Load("calendar_circle");
            if (circle != null)
            {
                var ring = Find(kitchen, "CalendarCircle", false) ?? Empty(kitchen, "CalendarCircle", Vector2.zero);
                if (ring.GetComponent<SpriteRenderer>() == null) ring.gameObject.AddComponent<SpriteRenderer>();
                PlaceAtScreenScale(ring, circle, Vector2.zero, "WallProps", 3);
                ring.position = numberPos;
            }
        }

        /// <summary>
        /// Ceiling lamp from the manifest frame: cord from the frame top (off the top of the screen), shade, bulb.
        /// Light and a soft glow sit on the bulb; Day 1 swaps to the unlit drawing (KitchenDirector).
        /// </summary>
        private static void ApplyLamp(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + n + ".png");
            var lit = Load("lamp");
            if (lit == null) return;

            // The cropped drawing starts at the frame top (the cord), so place its top at the frame top.
            var frameTop = KitchenArtLayout.ToWorldY(_layout.lampFrameCentre.y - _layout.lampFrameSize.y * 0.5f);
            var centre = new Vector2(KitchenArtLayout.ToWorldX(_layout.lampFrameCentre.x), frameTop - lit.bounds.extents.y);
            // World positions: these may sit under the LampPivot hanger (ApplyLife).
            var lamp = Find(kitchen, "CeilingLamp");
            PlaceAtScreenScale(lamp, lit, Vector2.zero, "WallProps", 2);
            lamp.position = centre;

            var bulb = new Vector2(centre.x, centre.y - lit.bounds.extents.y + 0.14f);
            Find(kitchen, "LampLight").position = bulb;

            var glow = Find(kitchen, "LampGlow", false) ?? Empty(kitchen, "LampGlow", Vector2.zero);
            if (glow.GetComponent<SpriteRenderer>() == null) glow.gameObject.AddComponent<SpriteRenderer>();
            PlaceAtScreenScale(glow, AssetDatabase.LoadAssetAtPath<Sprite>(FxFolder + "glow_radial.png"), Vector2.zero, "WallProps", 0);
            glow.position = bulb;
            glow.localScale = new Vector3(1.6f, 1.6f, 1f);

            var director = kitchen.GetComponent<KitchenDirector>();
            Wire(director, "lampRenderer", lamp.GetComponent<SpriteRenderer>());
            Wire(director, "lampOnSprite", lit);
            Wire(director, "lampOffSprite", Load("lamp_off"));
            Wire(director, "lampGlow", glow.GetComponent<SpriteRenderer>());
        }

        private const float SillTopY = -0.56f;
        private const float PlantX = 2.15f;

        /// <summary>Drawn plant on the window sill, one drawing per stage (wilted → green → blooming), pot on the sill.</summary>
        private static void ApplyPlant(Transform kitchen)
        {
            var paths = new[] { "plant_wilted", "plant_green", "plant_bloom" }.Select(n => PropsFolder + n + ".png").ToArray();
            if (AssetDatabase.LoadAssetAtPath<Sprite>(paths[0]) == null) return;
            foreach (var path in paths) SetPivot(path, new Vector2(0.5f, 0.017f)); // pot bottom: stages swap in place

            Sprite Load(int i) => AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            var plant = Find(kitchen, "Plant");
            PlaceAtScreenScale(plant, Load(0), new Vector2(PlantX, SillTopY), "WindowFrame", 12);
            var pot = Find(kitchen, "PlantPot", false);
            if (pot != null) pot.gameObject.SetActive(false);

            Edit(kitchen.GetComponent<KitchenDirector>(), so =>
            {
                var array = Property(so, "plantStageSprites");
                var byStage = new[] { Load(0), Load(1), Load(1), Load(2) }; // Wilted, Recovering, Healthy, Blooming
                array.arraySize = byStage.Length;
                for (var i = 0; i < byStage.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = byStage[i];
            });
        }

        /// <summary>
        /// Life in the kitchen: particle emitter for juice bursts, gentle ambient motion, and clickable details
        /// (fidgets) that swing / punch / puff when poked. Idempotent.
        /// </summary>
        private static void ApplyLife(Scene scene, Transform kitchen)
        {
            // Particle bursts (sparkles, dust, splashes, sprinkles).
            var fx = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<FeedbackFx>(true)).First(f => f != null);
            var burstT = Find(fx.transform, "Burst", false);
            if (burstT == null)
            {
                var system = Particles(fx.transform, "Burst", Vector2.zero, 0.01f, Vector2.zero, Color.white, 0.08f, 0.6f, 0f, "FX", 20);
                var main = system.main;
                main.playOnAwake = true;
                main.maxParticles = 600;
                var velocity = system.velocityOverLifetime;
                velocity.enabled = false;
                var colour = system.colorOverLifetime;
                colour.enabled = true;
                var fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
                colour.color = fade;
                burstT = system.transform;
            }
            Wire(fx, "burst", burstT.GetComponent<ParticleSystem>());

            // Topping sprinkle colours.
            var sprinkles = new System.Collections.Generic.Dictionary<string, Color>
            {
                ["Cinnamon"] = new(0.65f, 0.35f, 0.2f), ["Honey"] = new(0.95f, 0.7f, 0.2f),
                ["Cocoa"] = new(0.35f, 0.22f, 0.15f), ["Salt"] = new(0.95f, 0.95f, 0.97f),
            };
            foreach (var jar in Find(kitchen, "Counter Tools").GetComponentsInChildren<ToppingJar>(true))
            {
                if (sprinkles.TryGetValue(jar.name, out var c)) Edit(jar, so => so.FindProperty("sprinkleColour").colorValue = c);
                // Spices are carried to the cup by hand (PickToppingsStep).
                var drag = GetOrAdd<DragTarget>(jar.transform);
                WireEnum(drag, "handPose", HandPose.Pinch);
                Wire(jar, "drag", drag);
            }

            // Lamp hangs from its cord: everything that should swing goes under a pivot at the cord's top.
            var lamp = Find(kitchen, "CeilingLamp");
            var lampPivot = Find(kitchen, "LampPivot", false);
            if (lampPivot == null)
            {
                var top = lamp.GetComponent<SpriteRenderer>().bounds.max.y;
                lampPivot = Empty(kitchen, "LampPivot", new Vector2(lamp.position.x, top));
                foreach (var name in new[] { "CeilingLamp", "LampLight", "LampGlow" })
                {
                    var t = Find(kitchen, name, false);
                    if (t != null) t.SetParent(lampPivot, true);
                }
            }
            Motion(lampPivot, sway: 1.2f, speed: 0.45f, wind: 0.6f);
            Poke(lamp, lampPivot, swing: 9f, punch: 0f, sound: Audio.SfxId.UiClick);

            // Calendar: sheet, number and ring move together.
            var calendarGroup = Find(kitchen, "CalendarGroup", false);
            var calendar = Find(kitchen, "Calendar");
            if (calendarGroup == null)
            {
                var top = calendar.GetComponent<SpriteRenderer>().bounds.max.y;
                calendarGroup = Empty(kitchen, "CalendarGroup", new Vector2(calendar.position.x, top));
                foreach (var name in new[] { "Calendar", "CalendarDay", "CalendarCircle", "CalendarHeader" })
                {
                    var t = Find(kitchen, name, false);
                    if (t != null) t.SetParent(calendarGroup, true);
                }
            }
            Poke(calendar, calendarGroup, swing: 5f, punch: 0f, sound: Audio.SfxId.PaperNote);

            // Window: curtains breathe (more on windy days), plant sways, Mira's silhouette moves a little.
            foreach (var curtain in new[] { "CurtainLeft", "CurtainRight" })
            {
                var t = Find(kitchen, curtain, false);
                if (t == null) continue;
                Motion(t, sway: 0.35f, stretch: 0.012f, speed: 0.5f, wind: 1f);
                Poke(t, t, swing: 2.5f, punch: 0f, sound: Audio.SfxId.UiClick);
            }

            var plant = Find(kitchen, "Plant");
            Motion(plant, sway: 1.8f, speed: 0.7f, wind: 0.7f);
            Poke(plant, plant, swing: 12f, punch: 0.2f, sound: Audio.SfxId.UiClick, particles: 6, colour: new Color(0.45f, 0.65f, 0.3f));
            Motion(Find(kitchen, "Silhouette"), bob: 0.02f, speed: 0.35f, wind: 0f);

            // Wall and fridge details.
            Poke(Find(kitchen, "Clock"), Find(kitchen, "Clock"), swing: 4f, punch: 0.15f, sound: Audio.SfxId.UiClick);
            Poke(Find(kitchen, "WallPicture"), Find(kitchen, "WallPicture"), swing: 7f, punch: 0f, sound: Audio.SfxId.UiClick);
            Poke(Find(kitchen, "Shelf"), Find(kitchen, "Shelf"), swing: 0f, punch: 0.12f, sound: Audio.SfxId.MugClink);
            Poke(Find(kitchen, "ChippedMug"), Find(kitchen, "ChippedMug"), swing: 0f, punch: 0.3f, sound: Audio.SfxId.MugClink);
            Poke(Find(kitchen, "HerMug"), Find(kitchen, "HerMug"), swing: 0f, punch: 0.3f, sound: Audio.SfxId.MugClink);
            Poke(Find(kitchen, "BeansJar"), Find(kitchen, "BeansJar"), swing: 0f, punch: 0.35f, sound: Audio.SfxId.Spoon,
                particles: 4, colour: new Color(0.35f, 0.22f, 0.15f));
            for (var i = 0; i < 3; i++)
            {
                var note = Find(kitchen, $"FridgeNote{i}");
                Poke(note, note, swing: 8f, punch: 0f, sound: Audio.SfxId.PaperNote);
            }

            ApplySoftParticles(kitchen, scene);
            ApplyNeighbours(kitchen);
            ApplyFinale(scene, kitchen);

            // Mira's window: poke it and (from Day 5) she waves back.
            var miraWindow = Find(kitchen, "MiraWindow");
            var miraFidget = Poke(miraWindow, miraWindow, swing: 0f, punch: 0f, sound: Audio.SfxId.UiClick);
            Wire(kitchen.GetComponent<KitchenDirector>(), "miraWindowFidget", miraFidget);
        }

        /// <summary>Snow and steam as soft round puffs (code-generated radial gradient) instead of squares.</summary>
        private static void ApplySoftParticles(Transform kitchen, Scene scene)
        {
            var soft = AssetDatabase.LoadAssetAtPath<Sprite>(FxFolder + "glow_radial.png");
            if (soft == null) return;
            var systems = new[] { "Snow", "Steam", "SpoutSteam" }
                .Select(n => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ParticleSystem>(true)).FirstOrDefault(p => p.name == n))
                .Where(p => p != null);
            foreach (var system in systems)
            {
                var sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Sprites;
                while (sheet.spriteCount > 0) sheet.RemoveSprite(0);
                sheet.AddSprite(soft);
                var main = system.main;
                if (system.name == "Snow") main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.18f);
                else main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
                EditorUtility.SetDirty(system);
            }
        }

        /// <summary>A few windows across the courtyard whose lights go on and off.</summary>
        private static void ApplyNeighbours(Transform kitchen)
        {
            var root = Find(kitchen, "NeighbourWindows", false) ?? Empty(kitchen, "NeighbourWindows", Vector2.zero);
            var positions = new[] { new Vector2(2.25f, 2.75f), new Vector2(-0.35f, 2.75f), new Vector2(0.85f, 0.85f), new Vector2(2.25f, 0.85f) };
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            var renderers = new Object[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var w = SpriteChild(root, $"Window{i}");
                var r = w.GetComponent<SpriteRenderer>();
                r.sprite = square;
                r.drawMode = SpriteDrawMode.Sliced;
                r.size = new Vector2(0.75f, 0.9f);
                r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                w.localPosition = positions[i];
                SetLayer(w, "Outside", 3);
                renderers[i] = r;
            }

            WireArray(GetOrAdd<NeighbourWindows>(root), "windows", renderers);
        }

        /// <summary>Finale: camera focus on the sill mugs, and a HUD group that fades out.</summary>
        private static void ApplyFinale(Scene scene, Transform kitchen)
        {
            var focus = Find(kitchen, "FinaleFocus", false) ?? Empty(kitchen, "FinaleFocus", Vector2.zero);
            focus.localPosition = new Vector3(0f, -0.1f, 0f);

            var canvas = scene.GetRootGameObjects().First(g => g.name == "Canvas").transform;
            var hud = canvas.Find("HUD");
            if (hud == null)
            {
                hud = UiObject("HUD", canvas);
                Stretch((RectTransform)hud);
                hud.SetSiblingIndex(1); // above the vignette, below story / fader / day card
                hud.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
                foreach (var name in new[] { "WarmthMeter_Him", "WarmthMeter_Mira", "Hint" })
                {
                    var t = canvas.Find(name);
                    if (t != null) t.SetParent(hud, false);
                }
            }

            var runner = scene.GetRootGameObjects().Select(g => g.GetComponent<LevelRunner>()).First(r => r != null);
            Wire(runner, "finaleFocus", focus);
            Wire(runner, "hudGroup", hud.GetComponent<CanvasGroup>());
        }

        /// <summary>Title screen: a steaming mug under the title and autumn leaves drifting down.</summary>
        private static void ApplyToBoot(Scene scene)
        {
            var root = SceneRoot(scene, "TitleDecor");
            var mug = SpriteChild(root, "Mug");
            var his = AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "mug_his.png");
            if (his == null) return;
            FitMug(mug, his, new Vector2(0f, -3.4f), 1.3f, "Props", 1);
            var steam = SoftParticles(root, "Steam", new Vector2(-0.3f, -2.45f), 0.5f, new Vector2(0f, 0.45f), new Color(1f, 1f, 1f, 0.3f), 0.3f, 1.8f, 5f, "FX");
            var oldLeaves = root.Find("Leaves");
            if (oldLeaves != null) Object.DestroyImmediate(oldLeaves.gameObject);
            var leaves = Particles(root, "Leaves", new Vector2(0f, 5.8f), 19f, new Vector2(0.25f, -0.8f), new Color(0.9f, 0.5f, 0.2f), 0.16f, 14f, 3f, "FX", 0);
            var spin = leaves.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var main = leaves.main;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.45f, 0.2f), new Color(0.95f, 0.75f, 0.3f));
            Motion(mug, sway: 0f, bob: 0.03f, speed: 0.5f, wind: 0f);
            EditorUtility.SetDirty(steam);
        }

        /// <summary>Ending: his and her mugs side by side in falling snow while the last lines and credits play.</summary>
        private static void ApplyToEnding(Scene scene)
        {
            var root = SceneRoot(scene, "EndingDecor");
            var his = AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "mug_his.png");
            var hers = AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "mug_hers.png");
            if (his == null || hers == null) return;
            FitMug(SpriteChild(root, "HisMug"), his, new Vector2(-0.95f, 0.4f), 1.3f, "Props", 1);
            FitMug(SpriteChild(root, "HerMug"), hers, new Vector2(0.95f, 0.4f), 1.3f, "Props", 1);
            var glow = SpriteChild(root, "Glow");
            PlaceAtScreenScale(glow, AssetDatabase.LoadAssetAtPath<Sprite>(FxFolder + "glow_radial.png"), new Vector2(0f, 0.4f), "Props", 0);
            glow.localScale = new Vector3(2.4f, 1.6f, 1f);
            glow.GetComponent<SpriteRenderer>().color = new Color(1f, 0.75f, 0.45f, 0.35f);
            SoftParticles(root, "SteamHis", new Vector2(-1.2f, 1.35f), 0.4f, new Vector2(0f, 0.55f), new Color(1f, 1f, 1f, 0.3f), 0.3f, 2f, 4f, "FX");
            SoftParticles(root, "SteamHers", new Vector2(0.7f, 1.35f), 0.4f, new Vector2(0f, 0.55f), new Color(1f, 1f, 1f, 0.3f), 0.3f, 2f, 4f, "FX");
            var snow = SoftParticles(root, "Snow", new Vector2(0f, 5.8f), 19f, new Vector2(0.15f, -0.7f), new Color(1f, 1f, 1f, 0.85f), 0.14f, 16f, 18f, "FX");
            var main = snow.main;
            main.prewarm = true;
        }

        private static Transform SceneRoot(Scene scene, string name)
        {
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (existing != null) return existing.transform;
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.transform;
        }

        /// <summary>Soft round particles (steam, snow) using the radial puff sprite.</summary>
        private static ParticleSystem SoftParticles(Transform parent, string name, Vector2 position, float width, Vector2 velocity,
            Color colour, float size, float lifetime, float rate, string layer)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var system = Particles(parent, name, position, width, velocity, colour, size, lifetime, rate, layer, 5);
            var main = system.main;
            main.playOnAwake = true;
            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            sheet.AddSprite(AssetDatabase.LoadAssetAtPath<Sprite>(FxFolder + "glow_radial.png"));
            var growth = system.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.6f));
            return system;
        }

        private static void Motion(Transform target, float sway = 0f, float bob = 0f, float stretch = 0f, float speed = 0.6f, float wind = 1f)
        {
            if (target == null) return;
            var motion = GetOrAdd<AmbientMotion>(target);
            Edit(motion, so =>
            {
                so.FindProperty("swayDegrees").floatValue = sway;
                so.FindProperty("bob").floatValue = bob;
                so.FindProperty("stretch").floatValue = stretch;
                so.FindProperty("speed").floatValue = speed;
                so.FindProperty("windResponse").floatValue = wind;
            });
        }

        /// <summary>Makes a sprite clickable: collider from its sprite, PointerTarget (hand on hover) and a Fidget.</summary>
        private static Fidget Poke(Transform target, Transform visual, float swing, float punch, Audio.SfxId sound,
            int particles = 0, Color colour = default)
        {
            if (target == null) return null;
            var renderer = target.GetComponent<SpriteRenderer>();
            var collider = target.GetComponent<BoxCollider2D>();
            if (collider == null) collider = target.gameObject.AddComponent<BoxCollider2D>();
            if (renderer != null && renderer.sprite != null)
            {
                var b = renderer.sprite.bounds;
                collider.size = renderer.drawMode == SpriteDrawMode.Simple ? (Vector2)b.size : renderer.size;
                collider.offset = renderer.drawMode == SpriteDrawMode.Simple ? (Vector2)b.center : Vector2.zero;
            }

            var pointer = GetOrAdd<PointerTarget>(target);
            WireEnum(pointer, "handPose", HandPose.Point);
            var fidget = GetOrAdd<Fidget>(target);
            Edit(fidget, so =>
            {
                so.FindProperty("visual").objectReferenceValue = visual;
                so.FindProperty("swingDegrees").floatValue = swing;
                so.FindProperty("punch").floatValue = punch;
                so.FindProperty("sound").enumValueIndex = (int)sound;
                so.FindProperty("particles").intValue = particles;
                so.FindProperty("particleColour").colorValue = colour;
            });
            return fidget;
        }

        private static void FitMug(Transform target, Sprite sprite, Vector2 centre, float scale, string layer = "Wall", int order = 2)
        {
            if (sprite == null) return;
            var renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            SetLayer(target, layer, order);
            target.localScale = new Vector3(scale, scale, 1f);
            target.localPosition = centre - (Vector2)sprite.bounds.center * scale;
        }

        private static Transform Child(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null) return child;
            child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform SpriteChild(Transform parent, string childName)
        {
            var child = Child(parent, childName);
            if (child.GetComponent<SpriteRenderer>() == null) child.gameObject.AddComponent<SpriteRenderer>();
            return child;
        }

        /// <summary>Moves an existing child of <paramref name="from"/> under <paramref name="to"/> (or creates it there).</summary>
        private static Transform MoveUnder(Transform from, Transform to, string childName)
        {
            var existing = from.Find(childName);
            if (existing != null) existing.SetParent(to, false);
            return SpriteChild(to, childName);
        }

        private static void SetPivot(string path, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment == (int)SpriteAlignment.Custom && settings.spritePivot == pivot) return;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Sprite LoadKitchen(string spriteName) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(KitchenFolder + spriteName + ".png");

        /// <summary>Art drawn for a 1920×1080 screen: unscaled at 100 px per unit.</summary>
        private static void PlaceAtScreenScale(Transform target, Sprite sprite, Vector2 position, string layer, int order)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            SetLayer(target, layer, order);
            target.localScale = Vector3.one;
            target.localPosition = position;
        }

        /// <summary>A magnet on the top edge of a note; parented to it so it hides with the note.</summary>
        private static void AddMagnet(Transform note, string spriteName, float offsetY)
        {
            var sprite = LoadKitchen(spriteName);
            if (sprite == null) return;

            var magnet = note.Find("Magnet");
            if (magnet == null)
            {
                magnet = new GameObject("Magnet").transform;
                magnet.SetParent(note, false);
                magnet.gameObject.AddComponent<SpriteRenderer>();
            }

            var noteScale = note.lossyScale;
            PlaceAtScreenScale(magnet, sprite, new Vector2(0f, offsetY / noteScale.y), "Wall", 4);
            magnet.localScale = new Vector3(1f / noteScale.x, 1f / noteScale.y, 1f);
        }

        /// <summary>Hand-drawn tabletop stretched across the counter (replaces the counter box and its edge strip).</summary>
        private static void ApplyTable(Transform kitchen)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitchenFolder + "Counter_Table.png");
            if (sprite == null) return;

            var counter = Find(kitchen, "Counter");
            var renderer = counter.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            SetLayer(counter, "Counter", 0);

            var size = sprite.bounds.size;
            counter.localScale = new Vector3(TableRect.width / size.x, TableRect.height / size.y, 1f);
            counter.localPosition = TableRect.center - (Vector2)sprite.bounds.center * new Vector2(counter.localScale.x, counter.localScale.y);
            Find(kitchen, "CounterEdge").gameObject.SetActive(false);

            Find(kitchen, "Counter Tools").localPosition = new Vector3(0f, CounterLift, 0f);
            var stepRoot = kitchen.gameObject.scene.GetRootGameObjects().FirstOrDefault(g => g.name == "StepRoot");
            if (stepRoot != null) stepRoot.transform.position = new Vector3(0f, CounterLift, 0f);
        }

        /// <summary>Soft drop shadow: same size as <paramref name="caster"/>, nudged down-right, drawn behind it.</summary>
        private static void ShadowOf(Transform root, Transform caster, string objectName, string spriteName, int order)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WindowFolder + spriteName + ".png");
            if (sprite == null) return;

            EnsureSprite(root, objectName);
            var shadow = Find(root, objectName);
            var renderer = shadow.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            SetLayer(shadow, "Wall", order);

            // Shadow PNGs are exported at the caster's pixel size, so matching scale and centre lines them up.
            var casterSprite = caster.GetComponent<SpriteRenderer>().sprite;
            var scale = caster.localScale.x * casterSprite.rect.width / sprite.rect.width * (sprite.pixelsPerUnit / casterSprite.pixelsPerUnit);
            shadow.localScale = new Vector3(scale, scale, 1f);
            var casterCentre = (Vector2)caster.localPosition + (Vector2)(casterSprite.bounds.center * caster.localScale.x);
            shadow.localPosition = casterCentre + ShadowOffset - (Vector2)(sprite.bounds.center * scale);
        }

        private static void EnsureSprite(Transform root, string objectName)
        {
            if (Find(root, objectName, false) != null) return;
            Empty(root, objectName, Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Puts <paramref name="spriteName"/> on the named placeholder so its visible (non-transparent) area is
        /// <paramref name="height"/> units tall and centred on <paramref name="center"/>. Returns false if the sprite is missing.
        /// </summary>
        private static bool Fit(Transform root, string objectName, string spriteName, Vector2 center, float height, string layer, int order,
            string folder = OddblotFolder)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + spriteName + ".png");
            if (sprite == null) return false;

            var target = Find(root, objectName);
            var renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            SetLayer(target, layer, order);

            var collider = target.GetComponent<BoxCollider2D>();
            if (collider != null && target.GetComponent<PointerTarget>() == null) Object.DestroyImmediate(collider); // keep clickable details

            var vertices = sprite.vertices;
            var min = new Vector2(vertices.Min(v => v.x), vertices.Min(v => v.y));
            var max = new Vector2(vertices.Max(v => v.x), vertices.Max(v => v.y));
            var scale = height / (max.y - min.y);
            target.localScale = new Vector3(scale, scale, 1f);
            target.localPosition = center - (min + max) * 0.5f * scale;
            return true;
        }

        private static void SetLayer(Transform target, string layer, int order)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
        }

        private static void ConfigureImports(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                var changed = importer.textureType != TextureImporterType.Sprite || settings.spriteMeshType != SpriteMeshType.Tight
                              || importer.maxTextureSize != 2048 || importer.mipmapEnabled || importer.filterMode != FilterMode.Bilinear
                              || importer.textureCompression != TextureImporterCompression.CompressedHQ;
                if (!changed) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048; // art manifest: PPU 100, bilinear, high-quality compression, max 2048
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.Tight;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }
    }
}
