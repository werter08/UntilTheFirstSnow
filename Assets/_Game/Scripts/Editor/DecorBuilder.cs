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
        public static void Apply()
        {
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
            ArtManifestBuilder.Apply(scene, kitchen, _layout); // manifest zones, layers and file slots win
            SleeveBuilder.WireScene(scene, _sleeves);

            // Ceiling: hanging bulb; the lamp light sits on the bulb.
            if (Fit(kitchen, "CeilingLamp", "Oddblot_light", new Vector2(0f, 5.05f), 1.6f, "Wall", 13))
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
            if (Fit(kitchen, "Plant", "Oddblot_tbldeco2", new Vector2(1.4f, -0.12f), 1f, "Wall", 12))
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

            // Fill gauge beside the mug (shown only while pouring).
            var halfWidth = chipped.bounds.extents.x;
            var gauge = Child(mug, "Gauge");
            gauge.localPosition = new Vector3(halfWidth + 0.25f, -0.55f, 0f);
            var back = SpriteChild(gauge, "Back");
            var backRenderer = back.GetComponent<SpriteRenderer>();
            backRenderer.sprite = Square;
            backRenderer.drawMode = SpriteDrawMode.Sliced;
            backRenderer.size = new Vector2(0.22f, 1.1f);
            backRenderer.color = new Color(0.15f, 0.13f, 0.12f, 0.75f);
            back.localPosition = new Vector3(0f, 0.55f, 0f);
            SetLayer(back, "Props", 1);
            var fill = Find(mug, "Fill");
            fill.SetParent(gauge, false);
            fill.localPosition = Vector3.zero;
            var bar = fill.GetComponent<SpriteBar>();
            Wire(bar, "width", 0.16f);
            SetLayer(Find(fill, "Liquid"), "Props", 2);
            var line = Find(fill, "FillLine").GetComponent<SpriteRenderer>();
            line.size = new Vector2(0.32f, 0.05f);
            Wire(counter, "mugGauge", gauge.gameObject);
            for (var i = 0; i < 3; i++)
                Find(mug, $"SpoonMarker{i}").localPosition = new Vector3(halfWidth + 0.6f, -0.45f + 0.28f * i, 0f);

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
            if (collider != null) Object.DestroyImmediate(collider);

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
