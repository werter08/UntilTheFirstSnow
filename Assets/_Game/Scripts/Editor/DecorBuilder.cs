using System.IO;
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
            if (AssetDatabase.IsValidFolder(ScenesFolder.TrimEnd('/'))) ConfigureImports(ScenesFolder); // Day 10 door scene
        }

        public static void Apply()
        {
            AssetDatabase.Refresh(); // art dropped in from outside the Editor
            OutsideBuilder.ConfigureImports();
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
            OutsideBuilder.PatchLevels();
            EditScene(BuildScript.Scenes[1], ApplyToKitchen);
            EditScene(BuildScript.Scenes[0], ApplyToBoot);
            EditScene(BuildScript.Scenes[2], ApplyToEnding);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Applies only the outside art, the coffee bag / spoon and the shadows to the Kitchen scene that is already
        /// open, without saving it — leaves everything else (and any unsaved edits) as it is.
        /// </summary>
        public static void ApplyOutsideAndPropsInPlace()
        {
            AssetDatabase.Refresh(); // art dropped in from outside the Editor
            ConfigureArtImports();
            OutsideBuilder.ConfigureImports();
            OutsideBuilder.PatchLevels();
            var scene = SceneManager.GetSceneByPath(BuildScript.Scenes[1]);
            if (!scene.isLoaded) throw new System.InvalidOperationException("Open the Kitchen scene first.");
            var kitchen = scene.GetRootGameObjects().First(g => g.name == "Kitchen").transform;
            ApplyCoffeeBagAndSpoon(kitchen);
            OutsideBuilder.ApplyToKitchen(kitchen);
            ApplyShadows(kitchen);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        private static KitchenArtLayout _layout;
        private static SleeveLibrary _sleeves;

        private static void ApplyToKitchen(Scene scene)
        {
            var kitchen = scene.GetRootGameObjects().First(g => g.name == "Kitchen").transform;
            var handPlaced = SnapshotHandPlaced(kitchen); // whatever the passes below do, the team's placements win
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
            if (Load(PropsFolder, "coffee_bag") == null) // the team's bag (ApplyCoffeeBagAndSpoon) wins; pack bag only as fallback
                Fit(kitchen, "BeansJar", "Oddblot_deco", new Vector2(-8.85f, -2.2f + CounterLift), 1.15f, "Props", 1);
            Find(kitchen, "CinnamonJar").localPosition = new Vector3(-8.1f, -2.4f + CounterLift, 0f);
            SetLayer(Find(kitchen, "CinnamonJar"), "Props", 1);

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

            // Last: everything above has placed its props; these passes build on the final positions and orders.
            ApplyCoffeeBagAndSpoon(kitchen);
            OutsideBuilder.ApplyToKitchen(kitchen); // after the manifest: the outside art owns its layers
            ApplyPourStream(kitchen);
            ApplyDoorScene(kitchen);
            NotesBuilder.ApplyToKitchen(scene, kitchen);
            ApplyHoverHighlight(kitchen);
            ApplyScarf(kitchen);
            RestoreHandPlaced(handPlaced);
            ApplyRackShelf(kitchen);
            ApplyClickables(kitchen);
            ApplyInteractionFeedback(scene);
            ApplyRecipeCard(scene, kitchen);
            ApplyHintsToggle(scene);
            var counterStation = kitchen.GetComponentInChildren<CounterStation>(true);
            if (counterStation != null) Wire(counterStation, "hisMugSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "mug_his.png"));
            ApplyShadows(kitchen); // last: props are in their final places
            ApplySillDrinks(kitchen);
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

            // Placeholder spots only: hand-placed outside pieces keep the team's position (OutsideBuilder places the art).
            if (!IsHandPlaced(Find(kitchen, "MiraWindow")))
            {
                Find(kitchen, "MiraWindow").localPosition = new Vector3(0.85f, 2.75f, 0f);
                Find(kitchen, "MiraWindowLight").localPosition = new Vector3(0.85f, 2.75f, 0f);
            }

            if (!IsHandPlaced(Find(kitchen, "TreeTrunk"))) Find(kitchen, "TreeTrunk").localPosition = new Vector3(-1.5f, 0.5f, 0f);
            if (!IsHandPlaced(Find(kitchen, "TreeCrown"))) Find(kitchen, "TreeCrown").localPosition = new Vector3(-1.5f, 2.3f, 0f);

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
                if (!IsHandPlaced(note)) note.localPosition = notePositions[i];
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
            mug.localScale = new Vector3(1f, CupStretch, 1f); // a bit taller than drawn
            var collider = mug.GetComponent<BoxCollider2D>();
            collider.size = chipped.bounds.size;
            collider.offset = chipped.bounds.center;
            Find(mug, "LeafPrint").gameObject.SetActive(false);
            Find(mug, "Foam").GetComponent<SpriteRenderer>().enabled = false; // foam stays as state, not drawn
            Edit(counter, so =>
            {
                var array = Property(so, "mugSprites");
                var byStyle = MugSpritesByStyle(Load);
                array.arraySize = byStyle.Length;
                for (var i = 0; i < byStyle.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = byStyle[i];
            });

            // V60 art (placed on the mug by the manifest pass) and its little shadow on the rim.
            var v60Empty = Load("v60_empty");
            if (v60Empty != null)
            {
                var v60 = Find(tools, "V60");
                PlaceAtScreenScale(v60, v60Empty, v60.localPosition, "Props", 7);
                Wire(counter, "dripperRenderer", v60.GetComponent<SpriteRenderer>());
                Wire(counter, "dripperEmpty", v60Empty);
                Wire(counter, "dripperGrounds", Load("v60_grounds"));
                var shadowSprite = Load("v60_shadow");
                if (shadowSprite != null)
                    PlaceAtScreenScale(SpriteChild(v60, "Shadow"), shadowSprite, new Vector2(-0.07f, -v60Empty.bounds.extents.y + 0.02f), "Props", 6);
            }

            // The inside of the cup, as in the team's mockup: a rounded drink tile (with its own outline) BEHIND the mug.
            // Its flat bottom tucks 8 px behind the mug's straight rim, so the rim (and the chip) sit in front and the
            // tile's rounded top shows above it. Grey "empty" tile at the back; the drink tile over it is revealed from
            // the rim up as the cup fills and at the end covers it completely. Sorting: empty -3 < drink -2 < spice -1
            // < mug body 1. The cup is stretched (CupStretch); the tiles are not, so Contents undoes the stretch.
            var rimLocal = chipped.bounds.max.y - 0.02f;                  // mug top edge (2 px padding), mug-local units
            const float tuck = 0.18f;                                    // cap bottom hidden behind the rim (the inside sits low)
            const float capHalf = 0.33f;                                  // 66 px of tile, 2 px padding top and bottom
            var region = capHalf * 2f - tuck;                             // visible inside: rim → top of the cap
            var bodyCentreX = chipped.bounds.min.x + 0.02f + 0.55f;       // body is 110 px wide from its left edge
            var regionCentre = new Vector2(bodyCentreX, rimLocal + region * 0.5f / CupStretch);
            var capOffset = -region * 0.5f - tuck + capHalf;              // cap centre in the (unstretched) Contents frame
            Sprite Drink(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(DrinksFolder + n + ".png");

            SetLayer(mug, "Props", 1);
            var oldOutline = mug.Find("Outline");
            if (oldOutline != null) Object.DestroyImmediate(oldOutline.gameObject);
            Wire(counter, "mugOutline", (Object)null);
            WireArray(counter, "mugOutlineSprites", System.Array.Empty<Object>());

            var oldGauge = mug.Find("Gauge");
            var fill = Find(mug, "Fill");
            fill.SetParent(mug, false);
            if (oldGauge != null) Object.DestroyImmediate(oldGauge.gameObject);
            Wire(counter, "mugGauge", (Object)null);

            // The placeholder bar keeps the fill value for the minigames but no longer draws.
            fill.localPosition = new Vector2(bodyCentreX, rimLocal);
            var bar = fill.GetComponent<SpriteBar>();
            Wire(bar, "width", 1.1f);
            Wire(bar, "maxHeight", region / CupStretch); // Fill lives in the stretched mug
            Find(fill, "Liquid").GetComponent<SpriteRenderer>().sprite = null;
            var line = Find(fill, "FillLine").GetComponent<SpriteRenderer>();
            line.size = new Vector2(1.12f, 0.045f);
            line.color = new Color(1f, 1f, 1f, 0.9f);
            SetLayer(line.transform, "Props", 8);

            var contents = Child(mug, "Contents");
            contents.localPosition = regionCentre;
            contents.localScale = new Vector3(1f, 1f / CupStretch, 1f); // tiles at their drawn size
            // Empty mug: the plain dark cap (the inside of an empty mug), always under the drink.
            var empty = SpriteChild(contents, "EmptyCap");
            PlaceAtScreenScale(empty, Drink("drink_instant"), new Vector2(0f, capOffset), "Props", -3);
            var liquid = SpriteChild(contents, "Liquid");
            var liquidRenderer = liquid.GetComponent<SpriteRenderer>();
            liquidRenderer.sprite = Drink("drink_black");
            liquidRenderer.drawMode = SpriteDrawMode.Simple;
            liquidRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            liquid.localPosition = new Vector3(0f, capOffset, 0f);
            liquid.localScale = Vector3.one;
            SetLayer(liquid, "Props", -2);
            var spice = SpriteChild(contents, "Spice");
            var spiceRenderer = spice.GetComponent<SpriteRenderer>();
            spiceRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            spiceRenderer.drawMode = SpriteDrawMode.Sliced;
            spiceRenderer.size = new Vector2(0.96f, 0.06f);
            spiceRenderer.enabled = false;
            SetLayer(spice, "Props", -1);
            var maskTransform = Child(contents, "Mask");
            var mask = maskTransform.GetComponent<SpriteMask>();
            if (mask == null) mask = maskTransform.gameObject.AddComponent<SpriteMask>();
            mask.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath); // decor pass runs without the content build
            mask.isCustomRangeActive = true; // only the drink, never the weather / courtyard masks
            mask.frontSortingLayerID = SortingLayer.NameToID("Props");
            mask.frontSortingOrder = -2;
            mask.backSortingLayerID = SortingLayer.NameToID("Props");
            mask.backSortingOrder = -3;
            var mugContents = GetOrAdd<MugContents>(contents);
            Wire(mugContents, "liquid", liquidRenderer);
            Wire(mugContents, "emptyCap", empty.GetComponent<SpriteRenderer>());
            Wire(mugContents, "spice", spiceRenderer);
            Wire(mugContents, "mask", mask);
            Wire(mugContents, "regionHeight", region);
            Wire(mugContents, "regionWidth", 1.2f);
            Wire(counter, "mugContents", mugContents);
            WireArray(mugContents, "roundTiles", System.Array.Empty<Object>()); // one tile set: the rim does the masking
            WireArray(mugContents, "chippedTiles", System.Array.Empty<Object>());
            Edit(counter, so =>
            {
                var drinks = Property(so, "mugDrinkSprites");
                drinks.arraySize = 3;
                // The plain dark tile is the inside of an EMPTY mug; instant coffee is the speckled dark one.
                drinks.GetArrayElementAtIndex((int)MugDrink.Instant).objectReferenceValue = Drink("drink_black");
                drinks.GetArrayElementAtIndex((int)MugDrink.Black).objectReferenceValue = Drink("drink_pourover");
                drinks.GetArrayElementAtIndex((int)MugDrink.Latte).objectReferenceValue = Drink("drink_latte");
            });

            // Spoonfuls of instant coffee now show as grounds inside the mug (CounterStation.ShowSpoons).
            for (var i = 0; i < 3; i++)
                Find(mug, $"SpoonMarker{i}").GetComponent<SpriteRenderer>().enabled = false;

            // Wall rack: one slot per earlier day; KitchenDirector fills it from the level data (each day has its own
            // mug, earlier ones wait on the rack — never two copies of the same mug).
            Find(kitchen, "ChippedMug").gameObject.SetActive(false);
            Find(kitchen, "HerMug").gameObject.SetActive(false);
            var plank = Find(kitchen, "MugRack").GetComponent<SpriteRenderer>();
            plank.size = new Vector2(3.1f, 0.1f);
            plank.transform.localPosition = new Vector3(RackCentreX, RackY, 0f);
            var director = kitchen.GetComponent<KitchenDirector>();
            var slots = new Object[4];
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = Find(kitchen, $"RackMug{i}", false) ?? Empty(kitchen, $"RackMug{i}", Vector2.zero);
                if (slot.GetComponent<SpriteRenderer>() == null) slot.gameObject.AddComponent<SpriteRenderer>();
                FitMug(slot, chipped, new Vector2(RackCentreX - 1.08f + 0.72f * i, RackY + 0.05f + chipped.bounds.extents.y * RackScale), RackScale, "WallProps", 2);
                slots[i] = slot.GetComponent<SpriteRenderer>();
            }

            Wire(director, "herMug", (Object)null);
            WireArray(director, "rackSlots", slots);
            WireArray(director, "rackMugSprites", MugSpritesByStyle(Load).Cast<Object>().ToArray());

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

            // Foam rising on the milk: the team's small foam puff (milk_foam_puff), else the older big splash scaled down.
            var puff = Load("milk_foam_puff");
            var foam = puff != null ? puff : Load("milk_foam");
            if (foam != null)
            {
                var blob = SpriteChild(station, "MilkFoamBlob");
                var rim = top + jugSprite.bounds.size.y - 0.05f;
                PlaceAtScreenScale(blob, foam, new Vector2(x + JugBodyOffset, rim - 0.08f), "Props", 4); // behind the frother
                Wire(counter, "milkFoamBlob", blob);
                Wire(counter, "foamMinScale", puff != null ? 0.3f : 0.08f);  // a modest cap at the rim, not a splat
                Wire(counter, "foamMaxScale", puff != null ? 1.25f : 0.38f);
            }
        }

        private const float ToppingJarScale = 0.75f; // drawn jars are taller than the jar slots on the counter

        /// <summary>Drawn jars: cinnamon shaker and cocoa among the topping jars, cinnamon also as a counter prop.</summary>
        private static void ApplyJars(Transform kitchen)
        {
            Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + n + ".png");
            // Honey and salt have no drawing yet: until jar_honey / jar_salt exist they borrow the team's cocoa and
            // instant-coffee jars, tinted (their labels say which is which).
            var honey = Load("jar_honey");
            var salt = Load("jar_salt");
            var art = new System.Collections.Generic.Dictionary<string, (Sprite sprite, Color tint)>
            {
                ["Cinnamon"] = (Load("jar_cinnamon"), Color.white),
                ["Cocoa"] = (Load("jar_cocoa"), Color.white),
                ["Honey"] = honey != null ? (honey, Color.white) : (Load("jar_cocoa"), new Color(1f, 0.8f, 0.4f)),
                ["Salt"] = salt != null ? (salt, Color.white) : (Load("jar_instant"), new Color(0.93f, 0.94f, 0.97f)),
            };

            var toppings = Find(Find(kitchen, "Counter Tools"), "Toppings");
            foreach (var (jarName, (sprite, tint)) in art)
            {
                if (sprite == null) continue;
                var jar = Find(toppings, jarName);
                var x = jar.localPosition.x;
                var height = sprite.bounds.size.y * ToppingJarScale;
                PlaceAtScreenScale(jar, sprite, new Vector2(x, CounterLayout.Top + height * 0.5f), "Props", 1);
                jar.GetComponent<SpriteRenderer>().color = tint;
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
            Poke(lamp, lampPivot, swing: 9f, punch: 0f, sound: Audio.SfxId.LampSwitch);

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
            Poke(calendar, calendarGroup, swing: 5f, punch: 0f, sound: Audio.SfxId.UiClick); // the page opening rustles

            // Window: curtains breathe (more on windy days), plant sways, Mira's silhouette moves a little.
            foreach (var curtain in new[] { "CurtainLeft", "CurtainRight" })
            {
                var t = Find(kitchen, curtain, false);
                if (t == null) continue;
                Motion(t, sway: 0.35f, stretch: 0.012f, speed: 0.5f, wind: 1f);
                Poke(t, t, swing: 2.5f, punch: 0f, sound: Audio.SfxId.Curtain);
            }

            var plant = Find(kitchen, "Plant");
            Motion(plant, sway: 1.8f, speed: 0.7f, wind: 0.7f);
            Poke(plant, plant, swing: 12f, punch: 0.2f, sound: Audio.SfxId.UiClick, particles: 6, colour: new Color(0.45f, 0.65f, 0.3f));
            Motion(Find(kitchen, "Silhouette"), bob: 0.02f, speed: 0.35f, wind: 0f);

            // Wall and fridge details.
            Poke(Find(kitchen, "Clock"), Find(kitchen, "Clock"), swing: 4f, punch: 0.15f, sound: Audio.SfxId.ClockTick);
            Poke(Find(kitchen, "WallPicture"), Find(kitchen, "WallPicture"), swing: 7f, punch: 0f, sound: Audio.SfxId.UiClick);
            Poke(Find(kitchen, "Shelf"), Find(kitchen, "Shelf"), swing: 0f, punch: 0.12f, sound: Audio.SfxId.MugClink);
            for (var i = 0; i < 4; i++)
            {
                var rackMug = Find(kitchen, $"RackMug{i}", false);
                if (rackMug != null) Poke(rackMug, rackMug, swing: 0f, punch: 0.3f, sound: Audio.SfxId.MugClink);
            }
            Poke(Find(kitchen, "BeansJar"), Find(kitchen, "BeansJar"), swing: 0f, punch: 0.35f, sound: Audio.SfxId.Spoon,
                particles: 4, colour: new Color(0.35f, 0.22f, 0.15f));
            for (var i = 0; i < 3; i++)
            {
                var note = Find(kitchen, $"FridgeNote{i}");
                Poke(note, note, swing: 8f, punch: 0f, sound: Audio.SfxId.PaperNote);
            }

            ApplySoftParticles(kitchen, scene);
            ApplyMoreParticles(kitchen, scene);
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
                .Where(p => p != null && !IsHandPlaced(p.transform)); // hand-tuned particles keep their settings
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

        /// <summary>
        /// Extra particles: a soft puff emitter for FeedbackFx (warm glow, cold mist), dust motes drifting in the lamp
        /// light, and his breath showing when the kitchen gets cold. Idempotent.
        /// </summary>
        private static void ApplyMoreParticles(Transform kitchen, Scene scene)
        {
            var soft = AssetDatabase.LoadAssetAtPath<Sprite>(FxFolder + "glow_radial.png");
            if (soft == null) return;
            var roots = scene.GetRootGameObjects();
            var fx = roots.Select(g => g.GetComponentInChildren<FeedbackFx>(true)).First(f => f != null);
            var vignette = roots.Select(g => g.GetComponentInChildren<UI.WarmthVignette>(true)).FirstOrDefault(v => v != null);

            // Soft puffs: emitted on demand by FeedbackFx.Puff.
            var puffs = SoftParticles(fx.transform, "SoftBurst", Vector2.zero, 0.01f, Vector2.zero, Color.white, 0.4f, 1.2f, 0f, "FX");
            var puffMain = puffs.main;
            puffMain.maxParticles = 300;
            var puffMotion = puffs.velocityOverLifetime;
            puffMotion.enabled = false;
            FadeOut(puffs);
            puffs.GetComponent<ParticleSystemRenderer>().sortingOrder = 19;
            Wire(fx, "softBurst", puffs);

            // Dust motes slowly drifting in the lamp light (behind the counter, in front of the wall).
            var lamp = Find(kitchen, "CeilingLamp");
            var dust = SoftParticles(kitchen, "LampDust", new Vector2(lamp.position.x, 2.6f), 3.6f, new Vector2(0.02f, -0.04f),
                new Color(1f, 0.92f, 0.75f, 0.5f), 0.05f, 9f, 2.2f, "WallProps");
            var dustShape = dust.shape;
            dustShape.scale = new Vector3(3.6f, 2.6f, 0.05f);
            var dustMain = dust.main;
            dustMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            dustMain.prewarm = true;
            var noise = dust.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;
            var dustGrowth = dust.sizeOverLifetime;
            dustGrowth.enabled = false;
            FadeInOut(dust);
            dust.GetComponent<ParticleSystemRenderer>().sortingOrder = 40;

            // Cold breath: puffs rise from the bottom of the view when warmth runs low.
            var breath = Find(kitchen, "ColdBreath", false) ?? Empty(kitchen, "ColdBreath", new Vector2(-0.4f, -5.7f));
            breath.position = new Vector3(-0.4f, -4.9f, 0f);
            var cold = GetOrAdd<ColdBreath>(breath);
            Wire(cold, "fx", fx);
            if (vignette != null) Wire(cold, "vignette", vignette);
        }

        private static void FadeOut(ParticleSystem system) => SetFade(system, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        private static void FadeInOut(ParticleSystem system) => SetFade(system, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });

        private static void SetFade(ParticleSystem system, GradientAlphaKey[] alpha)
        {
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            colour.color = fade;
        }

        /// <summary>
        /// Day 30: her scarf hangs over the fridge's main handle (scarf_hanging.png, 1:1): the top of the loop at
        /// (1680, 520), in front of the fridge and behind the notes, swaying gently from the top of the loop (±1.5°, 3 s).
        /// The KitchenDirector shows it on scarf days only. The old blanket slot is removed.
        /// </summary>
        /// <summary>
        /// The team's rack shelf (rack_shelf.png, 1:1) under the rack mugs: as wide as the row of mugs plus a margin,
        /// its board top where the mugs stand. Replaces the placeholder plank (and its drop shadow — the art has one).
        /// </summary>
        private static void ApplyRackShelf(Transform kitchen)
        {
            var art = AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "rack_shelf.png");
            var plank = Find(kitchen, "MugRack", false);
            if (art == null || plank == null)
            {
                Debug.LogWarning("DecorBuilder: rack_shelf.png (or MugRack) missing — the rack keeps its placeholder plank.");
                return;
            }

            var mugs = Enumerable.Range(0, 4).Select(i => Find(kitchen, $"RackMug{i}", false))
                .Where(m => m != null).Select(m => m.GetComponent<SpriteRenderer>()).Where(m => m != null && m.sprite != null).ToArray();
            if (mugs.Length == 0) return;
            var left = mugs.Min(m => m.bounds.min.x);
            var right = mugs.Max(m => m.bounds.max.x);
            var bottom = mugs.Min(m => m.bounds.min.y);

            var shadow = plank.Find("DropShadow");
            if (shadow != null) Object.DestroyImmediate(shadow.gameObject);
            var renderer = plank.GetComponent<SpriteRenderer>();
            renderer.sprite = art;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            var scale = (right - left + 0.5f) / art.bounds.size.x;
            plank.localScale = new Vector3(scale, scale, 1f);
            plank.position = new Vector3((left + right) * 0.5f, bottom + 0.03f - art.bounds.max.y * scale, plank.position.z);
        }

        /// <summary>
        /// Things you can play with: the clock turns time (light follows), the lamp switches on/off, the calendar opens
        /// today's page, curtains open and close; clicked too much, lamp, curtains and rack mugs break.
        /// </summary>
        private static void ApplyClickables(Transform kitchen)
        {
            var director = kitchen.GetComponent<KitchenDirector>();

            var clock = Find(kitchen, "Clock", false);
            if (clock != null) Wire(GetOrAdd<ClockControl>(clock), "director", director);

            var calendar = Find(kitchen, "Calendar", false);
            if (calendar != null) Wire(GetOrAdd<CalendarPage>(calendar), "director", director);

            var lamp = Find(kitchen, "CeilingLamp", false);
            if (lamp != null)
            {
                Edit(GetOrAdd<Breakable>(lamp), so => so.FindProperty("shardColour").colorValue = new Color(1f, 0.95f, 0.8f));
                Wire(GetOrAdd<LampSwitch>(lamp), "director", director);
            }

            for (var i = 0; i < 4; i++)
            {
                var mug = Find(kitchen, $"RackMug{i}", false);
                if (mug == null || mug.GetComponent<PointerTarget>() == null) continue;
                Edit(GetOrAdd<Breakable>(mug), so => so.FindProperty("shardColour").colorValue = new Color(0.95f, 0.93f, 0.88f));
                GetOrAdd<BreakableMug>(mug);
            }

            // Curtains: tied back (as placed) ↔ hanging loose over the window (Window_CurtainDrawn), top on the rod.
            var drawn = Load(WindowFolder, "Window_CurtainDrawn");
            foreach (var name in new[] { "CurtainLeft", "CurtainRight" })
            {
                var curtain = Find(kitchen, name, false);
                var renderer = curtain != null ? curtain.GetComponent<SpriteRenderer>() : null;
                if (renderer == null || renderer.sprite == null || curtain.GetComponent<PointerTarget>() == null) continue;
                GetOrAdd<Breakable>(curtain);
                var toggle = GetOrAdd<CurtainToggle>(curtain);
                if (drawn == null)
                {
                    Debug.LogWarning("DecorBuilder: Window_CurtainDrawn.png missing — curtains can't be drawn.");
                    continue;
                }

                // Same height as the tied curtain; wide enough to reach the window's middle (the two meet there).
                var tied = renderer.sprite;
                var height = renderer.bounds.size.y;
                var top = renderer.bounds.max.y;
                var outer = name == "CurtainLeft" ? renderer.bounds.min.x : renderer.bounds.max.x;
                var sy = height / drawn.bounds.size.y;
                var sx = Mathf.Clamp((Mathf.Abs(outer) + 0.05f) / drawn.bounds.size.x, sy * 0.8f, sy * 1.8f);
                var centreX = outer - Mathf.Sign(outer) * drawn.bounds.size.x * sx * 0.5f;
                var parent = curtain.parent;
                var world = new Vector3(centreX, top - drawn.bounds.max.y * sy, curtain.position.z);
                Edit(toggle, so =>
                {
                    so.FindProperty("tiedBack").objectReferenceValue = tied;
                    so.FindProperty("drawn").objectReferenceValue = drawn;
                    so.FindProperty("tiedPosition").vector3Value = curtain.localPosition;
                    so.FindProperty("tiedScale").vector3Value = curtain.localScale;
                    so.FindProperty("drawnPosition").vector3Value = parent != null ? parent.InverseTransformPoint(world) : world;
                    so.FindProperty("drawnScale").vector3Value = new Vector3(sx, sy, 1f);
                });
            }
        }

        /// <summary>The fallback feedback for every action (hover tick, click sound, lift on pick-up), next to FeedbackFx.</summary>
        private static void ApplyInteractionFeedback(Scene scene)
        {
            var fx = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<FeedbackFx>(true)).FirstOrDefault(f => f != null);
            if (fx == null)
            {
                Debug.LogWarning("DecorBuilder: no FeedbackFx in the scene — no interaction fallback.");
                return;
            }

            Wire(GetOrAdd<InteractionFeedback>(fx.transform), "fx", fx);
        }

        /// <summary>Day 30's finale: the two mugs on the sill are full — his americano, her cappuccino.</summary>
        private static void ApplySillDrinks(Transform kitchen)
        {
            var sill = Find(kitchen, "SillMugs", false);
            if (sill == null) return;
            DrinkInside(sill.Find("SillMugHis")?.GetComponent<SpriteRenderer>(), "drink_black");
            DrinkInside(sill.Find("SillMugHers")?.GetComponent<SpriteRenderer>(), "drink_latte");
        }

        /// <summary>
        /// The drink inside a still mug, drawn like the counter mug's: the team's drink tile behind the mug, its
        /// rounded top showing above the rim (bottom tucked behind it).
        /// </summary>
        private static void DrinkInside(SpriteRenderer mug, string drinkName)
        {
            var drink = AssetDatabase.LoadAssetAtPath<Sprite>(DrinksFolder + drinkName + ".png");
            if (mug == null || mug.sprite == null || drink == null) return;
            var t = mug.transform.Find("Drink") ?? new GameObject("Drink").transform;
            t.SetParent(mug.transform, false);
            var renderer = GetOrAdd<SpriteRenderer>(t);
            renderer.sprite = drink;
            renderer.color = Color.white;
            renderer.sortingLayerID = mug.sortingLayerID;
            renderer.sortingOrder = mug.sortingOrder - 1;
            var bounds = mug.sprite.bounds;
            const float tuck = 0.18f; // as on the counter mug
            var rim = bounds.max.y - 0.02f;
            var bodyCentreX = bounds.min.x + 0.02f + 0.55f; // body is 110 px wide from its left edge
            t.localPosition = new Vector3(bodyCentreX, rim - tuck + drink.bounds.extents.y, 0f);
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private static void ApplyScarf(Transform kitchen)
        {
            var blanket = kitchen.Find("Blanket");
            if (blanket != null) Object.DestroyImmediate(blanket.gameObject);

            var scarf = kitchen.Find("Scarf");
            if (scarf == null) return;
            var renderer = scarf.GetComponent<SpriteRenderer>();
            const string path = PropsFolder + "scarf_hanging.png";
            if (!File.Exists(path))
            {
                renderer.enabled = false;
                Debug.LogWarning($"DecorBuilder: {path} missing — the Day 30 scarf is hidden.");
                return;
            }

            SetPivot(path, new Vector2(0.5f, 1f)); // top of the loop
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            renderer.enabled = true;
            SetLayer(scarf, "Fridge", 24); // fridge 20 < note shadows 21 < notes 22 < magnets 23 < scarf 24 < handle 25
            scarf.localScale = Vector3.one;
            scarf.localRotation = Quaternion.identity;
            if (!IsHandPlaced(scarf))
            {
                var top = Px(1680f, 520f);
                scarf.position = new Vector3(top.x, top.y, scarf.position.z);
            }

            // The fridge's main handle drawn over the scarf (the team's handle drawing, exactly where Fridge.png has it:
            // pixel 77, 308), so the scarf hangs behind the handle.
            var fridge = Find(kitchen, "Fridge");
            var fridgeSprite = fridge.GetComponent<SpriteRenderer>().sprite;
            var handleArt = LoadKitchen("Fridge_HandleLong");
            if (handleArt != null && fridgeSprite != null && fridgeSprite.name == "Fridge")
            {
                var handle = SpriteChild(fridge, "HandleOverFront");
                PlaceAtScreenScale(handle, handleArt, Vector2.zero, "Fridge", 25);
                var w = fridgeSprite.rect.width;
                var h = fridgeSprite.rect.height;
                handle.localPosition = new Vector3((77f + handleArt.rect.width * 0.5f - w * 0.5f) / 100f,
                    (h * 0.5f - 308f - handleArt.rect.height * 0.5f) / 100f, 0f);
                handle.gameObject.SetActive(true);
            }

            var collider = scarf.GetComponent<BoxCollider2D>();
            if (collider != null) Object.DestroyImmediate(collider);
            var motion = GetOrAdd<AmbientMotion>(scarf);
            Edit(motion, so =>
            {
                so.FindProperty("swayDegrees").floatValue = 1.5f;
                so.FindProperty("bob").floatValue = 0f;
                so.FindProperty("stretch").floatValue = 0f;
                so.FindProperty("speed").floatValue = 2f * Mathf.PI / 3f; // one slow sway every 3 s
                so.FindProperty("windResponse").floatValue = 0f;          // indoors: no wind
            });
        }

        /// <summary>Position, rotation and scale of every hand-placed object (HandPlaced) in the kitchen.</summary>
        private static System.Collections.Generic.List<(Transform t, Vector3 p, Quaternion r, Vector3 s)> SnapshotHandPlaced(Transform kitchen) =>
            kitchen.GetComponentsInChildren<HandPlaced>(true)
                .Select(h => (h.transform, h.transform.localPosition, h.transform.localRotation, h.transform.localScale)).ToList();

        private static void RestoreHandPlaced(System.Collections.Generic.List<(Transform t, Vector3 p, Quaternion r, Vector3 s)> snapshot)
        {
            foreach (var (t, p, r, s) in snapshot)
            {
                if (t == null) continue;
                t.localPosition = p;
                t.localRotation = r;
                t.localScale = s;
            }
        }

        /// <summary>One soft warm light that lights up whatever clickable / draggable thing the cursor is over.</summary>
        private static void ApplyHoverHighlight(Transform kitchen)
        {
            var go = Find(kitchen, "HoverHighlight", false) ?? Empty(kitchen, "HoverHighlight", Vector2.zero);
            var light = GetOrAdd<UnityEngine.Rendering.Universal.Light2D>(go);
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            light.color = new Color(1f, 0.9f, 0.7f);
            light.intensity = 0f;
            light.falloffIntensity = 0.7f;
            light.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();
            GetOrAdd<HoverHighlight>(go);
        }

        /// <summary>The kettle pours drops of water (the team's raindrop) instead of a solid rectangle.</summary>
        private static void ApplyPourStream(Transform kitchen)
        {
            var stream = Find(kitchen, "Stream", false);
            if (stream == null || stream.GetComponentInParent<KettleView>(true) == null)
            {
                Debug.LogWarning("DecorBuilder: no kettle 'Stream' for the pour drops.");
                return;
            }

            var existing = stream.Find("Drops");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var water = new Color(0.78f, 0.9f, 1f, 0.95f);
            // The kettle's origin is the spout tip: the drops start there and fall into the cup.
            var drops = Particles(stream, "Drops", -(Vector2)stream.localPosition, 0.06f, new Vector2(0f, -3.2f), water, 0.12f, 0.32f, 0f, "Props", 0);
            var main = drops.main;
            main.playOnAwake = true;
            main.gravityModifier = 0.8f;
            main.startRotation = 0f;
            var drop = AssetDatabase.LoadAssetAtPath<Sprite>(FxFolder + "weather_rain.png");
            if (drop != null)
            {
                var sheet = drops.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Sprites;
                sheet.AddSprite(drop);
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(0.05f, 0.07f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(0.16f, 0.22f);
                main.startSizeZ = 1f;
            }

            var pour = GetOrAdd<PourStream>(stream);
            Wire(pour, "drops", drops);
        }

        private const string ScenesFolder = "Assets/_Game/Art/Scenes/";
        private static readonly Vector2 DoorViewCentre = new(40f, 0f); // far from the kitchen; the camera visits it

        /// <summary>Canvas pixel centre (1920×1080, y down) → local offset inside the door view.</summary>
        private static Vector3 DoorPx(float x, float y) => new((x - 960f) / 100f, (540f - y) / 100f, 0f);

        /// <summary>
        /// Day 10's front door, from the team's art in Art/Scenes (1:1, as in their mockup): door_bg behind, Mira
        /// (mira_door_figure) in the doorway, her cup (mira_door_cup — the click target) between her hands, and her
        /// fingers (mira_door_fingers, cut from the figure) over the cup. Placeholders while a file is missing.
        /// Hand-placed Mira / cup keep their spots.
        /// </summary>
        private static void ApplyDoorScene(Transform kitchen)
        {
            var view = Find(kitchen, "DoorView", false) ?? Empty(kitchen, "DoorView", DoorViewCentre);
            view.position = new Vector3(DoorViewCentre.x, DoorViewCentre.y, 0f);
            Sprite Art(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(ScenesFolder + n + ".png");
            var background = Art("door_bg");
            var figure = Art("mira_door_figure");
            var cupArt = Art("mira_door_cup");
            var fingers = Art("mira_door_fingers");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            var oldDoor = view.Find("Door");
            if (oldDoor != null) oldDoor.gameObject.SetActive(false); // the placeholder door is in door_bg now

            var backdrop = SpriteChild(view, "Backdrop");
            if (background != null) PlaceAtScreenScale(backdrop, background, Vector2.zero, "Wall", 0);
            else
            {
                Debug.LogWarning($"DecorBuilder: {ScenesFolder}door_bg.png missing — the door scene uses a placeholder.");
                Box(backdrop, square, new Vector2(19.2f, 10.8f), new Color(0.86f, 0.74f, 0.58f), "Wall", 0);
            }

            // Figure placed from the mockup: top-left at (525, -55), so the scarf and jeans run off the screen.
            var mira = SpriteChild(view, "Mira");
            var miraKeep = IsHandPlaced(mira) ? mira.localPosition : (Vector3?)null;
            if (figure != null) PlaceAtScreenScale(mira, figure, Vector2.zero, "Props", 1);
            else Box(mira, square, new Vector2(2.2f, 6.2f), new Color(0.93f, 0.6f, 0.42f), "Props", 1);
            mira.localPosition = miraKeep ?? (figure != null ? DoorPx(525f + figure.rect.width * 0.5f, -55f + figure.rect.height * 0.5f) : new Vector3(0.1f, -1.9f, 0f));

            var cup = SpriteChild(view, "HerCup");
            var cupKeep = IsHandPlaced(cup) ? cup.localPosition : (Vector3?)null;
            PlaceAtScreenScale(cup, cupArt != null ? cupArt : Load(PropsFolder, "mug_hers"), Vector2.zero, "Props", 2);
            cup.localPosition = cupKeep ?? (cupArt != null ? DoorPx(872f + cupArt.rect.width * 0.5f, 375f + cupArt.rect.height * 0.5f) : new Vector3(1.0f, -1.2f, 0f));
            var cupSprite = cup.GetComponent<SpriteRenderer>().sprite;

            // Her fingers over the cup (cut from the figure), aligned with the figure.
            var hands = SpriteChild(view, "Fingers");
            hands.gameObject.SetActive(fingers != null && figure != null);
            if (fingers != null && figure != null)
            {
                PlaceAtScreenScale(hands, fingers, Vector2.zero, "Props", 3);
                hands.localPosition = mira.localPosition;
            }

            var collider = GetOrAdd<BoxCollider2D>(cup);
            if (cupSprite != null)
            {
                collider.size = cupSprite.bounds.size + new Vector3(0.3f, 0.3f, 0f);
                collider.offset = cupSprite.bounds.center;
            }

            var target = GetOrAdd<PointerTarget>(cup);
            Edit(target, so => so.FindProperty("interactable").boolValue = false);
            var director = kitchen.GetComponent<KitchenDirector>();
            Wire(director, "doorView", view);
            Wire(director, "herCupAtDoor", target);
            view.gameObject.SetActive(false);
        }

        private static void Box(Transform target, Sprite square, Vector2 size, Color colour, string layer, int order)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = colour;
            SetLayer(target, layer, order);
            target.localScale = Vector3.one;
        }

        /// <summary>Figma pixel (1920×1080, y down) → world units.</summary>
        private static Vector2 Px(float x, float y) => new((x - 960f) / 100f, (540f - y) / 100f);

        /// <summary>
        /// The drawn coffee bag (closed; open while grinding) at x 620 with its bottom on y 840, from Day 5 (it replaces
        /// the pack bag), and Day 1's spoon lying at (700, 820) turned ~30°, pivot where the handle meets the bowl.
        /// Each part waits (with a warning) until its PNG is in Art/Props.
        /// </summary>
        private static void ApplyCoffeeBagAndSpoon(Transform kitchen)
        {
            var tools = Find(kitchen, "Counter Tools");
            var counter = tools.GetComponent<CounterStation>();

            // Closed and open bags differ in size: both stand on their bottom centre so swapping keeps the bag on the counter.
            if (File.Exists(PropsFolder + "coffee_bag.png")) SetPivot(PropsFolder + "coffee_bag.png", new Vector2(0.5f, 0f));
            if (File.Exists(PropsFolder + "coffee_bag_open.png")) SetPivot(PropsFolder + "coffee_bag_open.png", new Vector2(0.5f, 0f));
            var bag = Load(PropsFolder, "coffee_bag");
            var bagOpen = Load(PropsFolder, "coffee_bag_open");
            if (bag == null) Debug.LogWarning($"DecorBuilder: {PropsFolder}coffee_bag.png missing — the pack bag stays.");
            else
            {
                var beans = Find(kitchen, "BeansJar");
                var renderer = beans.GetComponent<SpriteRenderer>();
                renderer.sprite = bag;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.color = Color.white;
                beans.localScale = Vector3.one;
                beans.localRotation = Quaternion.identity;
                var bottom = Px(620f, 840f);
                beans.position = new Vector3(bottom.x, bottom.y, beans.position.z); // pivot = bottom centre
                var collider = beans.GetComponent<BoxCollider2D>();
                if (collider != null)
                {
                    collider.size = bag.bounds.size;
                    collider.offset = bag.bounds.center;
                }

                Wire(counter, "coffeeBag", renderer);
                Wire(counter, "coffeeBagClosed", bag);
                if (bagOpen != null) Wire(counter, "coffeeBagOpen", bagOpen);
                else Debug.LogWarning($"DecorBuilder: {PropsFolder}coffee_bag_open.png missing — the bag stays closed while grinding.");
            }

            if (!File.Exists(PropsFolder + "spoon.png"))
            {
                Debug.LogWarning($"DecorBuilder: {PropsFolder}spoon.png missing — Day 1 keeps the placeholder spoon.");
                return;
            }

            var pivot = SpoonPivot(PropsFolder + "spoon.png");
            SetPivot(PropsFolder + "spoon.png", pivot);
            if (File.Exists(PropsFolder + "spoon_full.png")) SetPivot(PropsFolder + "spoon_full.png", pivot);
            var spoonSprite = Load(PropsFolder, "spoon");
            var resting = SpriteChild(tools, "RestingSpoon");
            PlaceAtScreenScale(resting, spoonSprite, Vector2.zero, "Props", 2);
            var at = Px(700f, 820f);
            resting.position = new Vector3(at.x, at.y, tools.position.z);
            resting.localRotation = Quaternion.Euler(0f, 0f, 30f);
            Wire(counter, "restingSpoon", resting);
        }

        /// <summary>
        /// Pivot (0..1) where the spoon's handle meets its bowl: the bowl is the run of wide rows; the pivot is its edge
        /// on the handle's side, centred on that row. Measured from the artist's alpha, so it follows the drawing.
        /// </summary>
        private static Vector2 SpoonPivot(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            var pixels = texture.GetPixels32();
            int w = texture.width, h = texture.height;
            Object.DestroyImmediate(texture);

            var widths = new int[h];
            var centres = new float[h];
            for (var y = 0; y < h; y++)
            {
                int min = w, max = -1;
                for (var x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a < 128) continue;
                    min = Mathf.Min(min, x);
                    max = Mathf.Max(max, x);
                }

                widths[y] = max >= min ? max - min + 1 : 0;
                centres[y] = max >= min ? (min + max) * 0.5f : w * 0.5f;
            }

            var widest = widths.Max();
            var bowl = Enumerable.Range(0, h).Where(y => widths[y] >= widest * 0.6f).ToArray();
            var opaque = Enumerable.Range(0, h).Where(y => widths[y] > 0).ToArray();
            if (bowl.Length == 0 || opaque.Length == 0) return new Vector2(0.5f, 0.5f);

            // Texture rows go bottom-up. The handle is on the side with more opaque rows beyond the bowl.
            var below = bowl.Min() - opaque.Min();
            var above = opaque.Max() - bowl.Max();
            var neck = above > below ? bowl.Max() : bowl.Min();
            return new Vector2(centres[neck] / w, (neck + 0.5f) / h);
        }

        /// <summary>
        /// Shadows for every prop, in the team's style: the drawn contact shadows (mug_shadow for round things,
        /// grinder_shadow for wide ones) under everything standing on the counter, sill or rack, and a 45% black
        /// silhouette drop shadow (like the window frame's) behind everything hanging on the wall or fridge. Idempotent.
        /// </summary>
        private static void ApplyShadows(Transform kitchen)
        {
            var round = Load(PropsFolder, "mug_shadow");
            var wide = Load(PropsFolder, "grinder_shadow");
            if (round == null || wide == null) return;

            // Things standing on a surface: shadow one order below, on the same layer.
            Contact(kitchen, "Mug", round, 1.35f, 0.05f);
            Contact(kitchen, "KettleBase", wide, 2.6f);
            Contact(kitchen, "Body", wide, 1.3f, parentName: "Grinder");
            Contact(kitchen, "Jug", round, 1.3f, parentName: "MilkStation");
            foreach (var jar in new[] { "Cinnamon", "Honey", "Cocoa", "Salt" }) Contact(kitchen, jar, round, 0.9f, parentName: "Toppings");
            Contact(kitchen, "BeansJar", round, 1.3f);
            Contact(kitchen, "CinnamonJar", round, 0.65f);

            // Window sill (plant, Day 30 mugs) and the rack plank: casters move up one order so the shadow fits
            // between them and the surface.
            SetOrder(kitchen, "Plant", 13);
            SetOrder(kitchen, "SillMugHis", 13);
            SetOrder(kitchen, "SillMugHers", 13);
            Contact(kitchen, "Plant", round, 1f);
            Contact(kitchen, "SillMugHis", round, 0.9f, 0.05f);
            Contact(kitchen, "SillMugHers", round, 0.9f, 0.05f);
            for (var i = 0; i < 4; i++)
            {
                SetOrder(kitchen, $"RackMug{i}", 3);
                Contact(kitchen, $"RackMug{i}", round, 0.45f, 0.02f);
            }

            // Hanging on the wall / fridge: silhouette drop shadows (wall items move up one order so the shadow
            // sits above the wall itself).
            SetOrder(kitchen, "Shelf", 2);
            SetOrder(kitchen, "WallPicture", 2);
            foreach (var name in new[] { "Clock", "Calendar", "CeilingLamp", "Shelf", "WallPicture" }) Drop(kitchen, name);
            for (var i = 0; i < 5; i++)
            {
                if (Find(kitchen, $"FridgeNote{i}", false) == null) continue;
                SetOrder(kitchen, $"FridgeNote{i}", 22); // shadows 21, magnets 23; the scarf (24) hangs over them
                var magnet = Find(kitchen, $"FridgeNote{i}", false)?.Find("Magnet");
                if (magnet != null) SetOrder(magnet, 23);
                Drop(kitchen, $"FridgeNote{i}");
            }
        }

        private static Sprite Load(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>(folder + name + ".png");

        private static Transform FindUnder(Transform kitchen, string name, string parentName)
        {
            if (parentName == null) return Find(kitchen, name, false);
            return kitchen.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name && t.parent != null && t.parent.name == parentName);
        }

        private static void SetOrder(Transform kitchen, string name, int order)
        {
            var target = Find(kitchen, name, false);
            if (target != null) SetOrder(target, order);
        }

        private static void SetOrder(Transform target, int order)
        {
            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.sortingOrder = order;
        }

        /// <summary>
        /// The drawn contact shadow under <paramref name="casterName"/>, sized to <paramref name="width"/> (world units),
        /// centred under it (+ <paramref name="xNudge"/>, e.g. to sit under a mug's body rather than its handle).
        /// A sibling of the caster, so it can stay on the surface while the caster is lifted.
        /// </summary>
        private static void Contact(Transform kitchen, string casterName, Sprite shadow, float width, float xNudge = 0f, string parentName = null)
        {
            var caster = FindUnder(kitchen, casterName, parentName);
            var renderer = caster != null ? caster.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                Debug.LogWarning($"DecorBuilder: no prop '{casterName}' for a contact shadow.");
                return;
            }

            var shadowName = casterName + "Shadow";
            var transform = caster.parent.Find(shadowName) ?? new GameObject(shadowName).transform;
            transform.SetParent(caster.parent, false);
            var shadowRenderer = GetOrAdd<SpriteRenderer>(transform);
            shadowRenderer.sprite = shadow;
            // The drawn shadow (≈ 128,112,112 at 50%) barely shows on the warm counter: tint it darker.
            shadowRenderer.color = new Color(0.42f, 0.34f, 0.34f, 1f);
            shadowRenderer.sortingLayerID = renderer.sortingLayerID;
            shadowRenderer.sortingOrder = renderer.sortingOrder - 1;

            var bounds = renderer.bounds;
            var parentScale = caster.parent.lossyScale;
            var scaleX = width / shadow.bounds.size.x;
            var scaleY = Mathf.Clamp(scaleX, 0.6f, 1.1f); // small props get a thinner shadow
            transform.localScale = new Vector3(scaleX / parentScale.x, scaleY / parentScale.y, 1f);
            // Centred on where the drawing really ends (its PNG has transparent padding below), so the prop stands in it.
            transform.position = new Vector3(bounds.center.x + xNudge, VisibleBottom(renderer) + 0.01f, caster.position.z);
            transform.localRotation = Quaternion.identity;
            Edit(GetOrAdd<ContactShadow>(transform), so =>
            {
                so.FindProperty("caster").objectReferenceValue = caster;
                so.FindProperty("casterRenderer").objectReferenceValue = renderer;
            });
        }

        private static readonly System.Collections.Generic.Dictionary<Sprite, float> BottomPadding = new();

        /// <summary>World y of the lowest visible pixel of a sprite (not its transparent padding).</summary>
        private static float VisibleBottom(SpriteRenderer renderer)
        {
            var sprite = renderer.sprite;
            if (!BottomPadding.TryGetValue(sprite, out var padPx))
            {
                padPx = 0f;
                var path = AssetDatabase.GetAssetPath(sprite.texture);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var texture = new Texture2D(2, 2);
                    if (texture.LoadImage(File.ReadAllBytes(path)))
                    {
                        var rect = sprite.rect;
                        var scale = texture.width / (float)sprite.texture.width; // imported size may be capped
                        var x0 = Mathf.FloorToInt(rect.x * scale);
                        var y0 = Mathf.FloorToInt(rect.y * scale);
                        var w = Mathf.Max(1, Mathf.FloorToInt(rect.width * scale));
                        var h = Mathf.Max(1, Mathf.FloorToInt(rect.height * scale));
                        var pixels = texture.GetPixels(x0, y0, w, h);
                        var row = 0;
                        for (; row < h; row++)
                        {
                            var any = false;
                            for (var col = 0; col < w && !any; col++) any = pixels[row * w + col].a > 0.15f;
                            if (any) break;
                        }

                        padPx = row < h ? row / scale : 0f;
                    }

                    Object.DestroyImmediate(texture);
                }

                BottomPadding[sprite] = padPx;
            }

            return renderer.bounds.min.y + padPx / sprite.pixelsPerUnit * Mathf.Abs(renderer.transform.lossyScale.y);
        }

        /// <summary>
        /// The recipe pinned on the wall near the shelf (the team's recipe_card.png; until it exists, a cream paper
        /// stand-in). Click to read; it grows over the days (LevelDefinition.recipeCardText).
        /// </summary>
        private static void ApplyRecipeCard(Scene scene, Transform kitchen)
        {
            var card = Find(kitchen, "RecipeCard", false) ?? Empty(kitchen, "RecipeCard", Vector2.zero);
            var renderer = GetOrAdd<SpriteRenderer>(card);
            var art = AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "recipe_card.png");
            if (art != null)
            {
                renderer.sprite = art;
                renderer.color = Color.white;
            }
            else
            {
                Debug.LogWarning("DecorBuilder: recipe_card.png missing in Art/Props — the recipe card is a paper stand-in.");
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + "note_yellow_3.png");
                renderer.color = new Color(1f, 0.97f, 0.88f);
            }

            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingLayerName = "WallProps";
            renderer.sortingOrder = 4;
            if (!IsHandPlaced(card))
            {
                card.localPosition = Px(560f, 300f);
                card.localRotation = Quaternion.Euler(0f, 0f, 3f);
                card.localScale = art != null ? Vector3.one : Vector3.one * 0.9f;
            }

            if (renderer.sprite != null)
            {
                var collider = GetOrAdd<BoxCollider2D>(card);
                collider.size = renderer.sprite.bounds.size;
                collider.offset = renderer.sprite.bounds.center;
            }

            WireEnum(GetOrAdd<PointerTarget>(card), "handPose", HandPose.Point);
            GetOrAdd<RecipeCard>(card);
            var runner = scene.GetRootGameObjects().Select(g => g.GetComponent<LevelRunner>()).FirstOrDefault(r => r != null);
            if (runner != null) Wire(runner, "recipeCard", card.GetComponent<RecipeCard>());
        }

        /// <summary>"Hints: on/off" in the top-right corner (remembered between sessions).</summary>
        private static void ApplyHintsToggle(Scene scene)
        {
            var canvas = scene.GetRootGameObjects().First(g => g.name == "Canvas").transform;
            var old = canvas.Find("HintsToggle");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var font = canvas.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null);

            var rect = UiObject("HintsToggle", canvas);
            Place(rect, Vector2.one, Vector2.one, Vector2.one, new Vector2(-28f, -24f), new Vector2(190f, 52f));
            var image = Img(rect, new Color(0.15f, 0.11f, 0.09f, 0.45f), true);
            var label = Label(rect, "Label", "Hints: on", 28f, TMPro.TextAlignmentOptions.Center, new Color(1f, 0.96f, 0.88f));
            if (font != null) label.font = font;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var toggle = rect.gameObject.AddComponent<UI.HintsToggle>();
            Wire(toggle, "label", label);
            // Below the note card and the screen fade.
            var card = canvas.GetComponentInChildren<UI.NoteCard>(true);
            if (card != null) rect.SetSiblingIndex(card.transform.GetSiblingIndex());
        }

        /// <summary>A silhouette drop shadow behind <paramref name="casterName"/> (child: it swings and swaps with it).</summary>
        private static void Drop(Transform kitchen, string casterName)
        {
            var caster = Find(kitchen, casterName, false);
            var renderer = caster != null ? caster.GetComponent<SpriteRenderer>() : null;
            if (renderer == null)
            {
                Debug.LogWarning($"DecorBuilder: no prop '{casterName}' for a drop shadow.");
                return;
            }

            var transform = caster.Find("DropShadow") ?? new GameObject("DropShadow").transform;
            transform.SetParent(caster, false);
            var shadowRenderer = GetOrAdd<SpriteRenderer>(transform);
            shadowRenderer.sprite = renderer.sprite;
            shadowRenderer.drawMode = renderer.drawMode;
            if (renderer.drawMode != SpriteDrawMode.Simple) shadowRenderer.size = renderer.size;
            shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);
            shadowRenderer.sortingLayerID = renderer.sortingLayerID;
            shadowRenderer.sortingOrder = renderer.sortingOrder - 1;
            var scale = caster.lossyScale;
            transform.localPosition = new Vector3(ShadowOffset.x / scale.x, ShadowOffset.y / scale.y, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            Wire(GetOrAdd<DropShadow>(transform), "caster", renderer);
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

        /// <summary>The title screen is MenuBuilder's (the autumn window); the old title decor is removed.</summary>
        private static void ApplyToBoot(Scene scene)
        {
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "TitleDecor")) Object.DestroyImmediate(old);
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
            PlaceAtScreenScale(glow, AssetDatabase.LoadAssetAtPath<Sprite>(FxFolder + "glow_radial.png"), new Vector2(0f, 0.4f), "Props", -1);
            DrinkInside(root.Find("HisMug").GetComponent<SpriteRenderer>(), "drink_black");  // his americano
            DrinkInside(root.Find("HerMug").GetComponent<SpriteRenderer>(), "drink_latte");  // her cappuccino
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

        /// <summary>The counter mug is drawn this much taller than the art (vertical stretch).</summary>
        private const float CupStretch = 1.2f;

        private const float RackCentreX = 5.05f;
        private const float RackY = 0.75f;
        private const float RackScale = 0.42f;

        /// <summary>Mug art indexed by MugStyle: Chipped, His, Hers, Yellow, Green.</summary>
        private static Sprite[] MugSpritesByStyle(System.Func<string, Sprite> load) => new[]
        {
            load("mug_chipped"), load("mug_his"), load("mug_hers"), load("mug_decoy_2"), load("mug_decoy_1"),
        };

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
                var changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                              || settings.spriteMeshType != SpriteMeshType.Tight
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
