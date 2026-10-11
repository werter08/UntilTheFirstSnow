using System.Linq;
using Courtyard.Audio;
using Courtyard.Kitchen;
using Courtyard.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// The title screen in Boot: a close look at the kitchen window on an autumn evening — the courtyard, an orange
    /// chestnut swaying in the wind, falling leaves, Mira's lit window, two steaming mugs on the sill — with the title
    /// and sticky-note buttons on the wall beside it. The window is copied from the Kitchen scene (the team's art, as
    /// placed there), stripped of gameplay. Rebuilt from scratch on every run (idempotent).
    /// </summary>
    internal static class MenuBuilder
    {
        private const string BackdropName = "MenuBackdrop";
        private const string MenuName = "MainMenu";

        /// <summary>Kitchen objects that make the window view (copied with their children, shadows and masks).</summary>
        private static readonly string[] WindowParts =
        {
            "Global Light 2D", "MiraWindowLight", "Wall", "Courtyard", "OutsideWall", "TreeCrown", "MiraWindow",
            "WindowMask", "Leaves", "WindowFrame", "WindowFrameShadow", "WindowSill", "WindowSillShadow",
            "CurtainLeft", "CurtainRight", "CurtainRod", "CurtainRodShadow", "Plant", "PlantShadow", "SillMugs",
        };

        // Camera: the window sits right of centre, the wall on the left holds the title.
        private static readonly Vector3 CameraPosition = new(-2.1f, 1.75f, -10f);
        private const float CameraSize = 3.6f;

        private static readonly Color Ink = new(0.24f, 0.16f, 0.12f);
        private static readonly Color EveningLight = new(1f, 0.88f, 0.74f);
        private static readonly Color EveningSky = new(0.98f, 0.72f, 0.5f);

        [MenuItem("Courtyard/Setup/Build Main Menu")]
        public static void Apply()
        {
            var kitchen = SceneManager.GetSceneByPath(BuildScript.Scenes[1]);
            var openedKitchen = !kitchen.isLoaded;
            if (openedKitchen) kitchen = EditorSceneManager.OpenScene(BuildScript.Scenes[1], OpenSceneMode.Additive);
            try
            {
                var kitchenRoot = kitchen.GetRootGameObjects().FirstOrDefault(g => g.name == "Kitchen");
                if (kitchenRoot == null)
                {
                    Debug.LogWarning("MenuBuilder: no Kitchen root in the Kitchen scene — menu not built.");
                    return;
                }

                EditScene(BuildScript.Scenes[0], boot =>
                {
                    BuildBackdrop(boot, kitchenRoot.transform);
                    BuildUi(boot);
                    BuildAudio(boot);
                });
            }
            finally
            {
                if (openedKitchen) EditorSceneManager.CloseScene(kitchen, true);
            }

            FontsBuilder.Apply();
            Debug.Log("MenuBuilder: main menu built in Boot.");
        }

        // ---------------------------------------------------------------- the window view

        private static void BuildBackdrop(Scene boot, Transform kitchen)
        {
            foreach (var old in boot.GetRootGameObjects().Where(g => g.name is BackdropName or "TitleDecor")) Object.DestroyImmediate(old);
            var backdrop = CopyWindow(boot, kitchen, BackdropName, WindowParts);

            var steam = kitchen.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == "Steam");
            Dress(backdrop, steam);

            var camera = boot.GetRootGameObjects().Select(g => g.GetComponentInChildren<Camera>(true)).First(c => c != null);
            camera.transform.position = CameraPosition;
            camera.orthographicSize = CameraSize;
            camera.backgroundColor = EveningSky;
        }

        /// <summary>
        /// Copies kitchen objects (the window view, the sill…) into <paramref name="target"/> under a new root, stripped
        /// of gameplay — for the title screen and the ending.
        /// </summary>
        internal static Transform CopyWindow(Scene target, Transform kitchen, string rootName, string[] parts)
        {
            var root = new GameObject(rootName).transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, target);
            foreach (var name in parts)
            {
                var source = kitchen.Find(name);
                if (source == null)
                {
                    Debug.LogWarning($"MenuBuilder: '{name}' not found in the kitchen — left out of {rootName}.");
                    continue;
                }

                var copy = Object.Instantiate(source.gameObject, root, true);
                copy.name = name;
                copy.SetActive(true);
                StripGameplay(copy);
            }

            return root;
        }

        /// <summary>Only looks and idle motion stay: no clicks, colliders or kitchen logic.</summary>
        private static void StripGameplay(GameObject copy)
        {
            // Several passes: components that others require can only go once their dependants are gone.
            for (var pass = 0; pass < 4; pass++)
            {
                foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour is AmbientMotion || behaviour is DropShadow || behaviour is ContactShadow) continue;
                    if (behaviour is Light2D) continue;
                    if (behaviour.GetType().Namespace?.StartsWith("Courtyard") != true) continue;
                    Object.DestroyImmediate(behaviour);
                }

                foreach (var collider in copy.GetComponentsInChildren<Collider2D>(true))
                    if (collider.gameObject.GetComponents<Component>().All(c => c is not MonoBehaviour behaviour || behaviour == null ||
                            behaviour.GetType().Namespace?.StartsWith("Courtyard") != true || behaviour is AmbientMotion))
                        Object.DestroyImmediate(collider);
            }
        }

        /// <summary>Autumn evening: orange chestnut in the wind, leaves falling, her window lit, both mugs steaming.</summary>
        private static void Dress(Transform backdrop, ParticleSystem kitchenSteam)
        {
            Sprite Outside(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/Outside/{file}.png");

            var light = backdrop.Find("Global Light 2D")?.GetComponent<Light2D>();
            if (light != null)
            {
                light.color = EveningLight;
                light.intensity = 1f;
            }

            var sky = backdrop.Find("Courtyard")?.GetComponent<SpriteRenderer>();
            if (sky != null) sky.color = EveningSky;

            var crown = backdrop.Find("TreeCrown");
            var orange = Outside("tree_2_orange");
            if (crown != null && orange != null)
            {
                var renderer = crown.GetComponent<SpriteRenderer>();
                renderer.sprite = orange;
                renderer.color = Color.white;
                Motion(crown, sway: 1.6f, speed: 0.55f);
            }

            var window = backdrop.Find("MiraWindow");
            var silhouette = Outside("mira_window_silhouette");
            if (window != null && silhouette != null)
            {
                window.GetComponent<SpriteRenderer>().sprite = silhouette;
                foreach (Transform child in window) child.gameObject.SetActive(false); // no waving arm here
                Motion(window, bob: 0.01f, speed: 0.3f);
            }

            var leaves = backdrop.Find("Leaves")?.GetComponent<ParticleSystem>();
            if (leaves != null)
            {
                var main = leaves.main;
                main.playOnAwake = true;
                main.loop = true;
                main.prewarm = true;
                var emission = leaves.emission;
                emission.enabled = true;
                emission.rateOverTime = 7f;
                var velocity = leaves.velocityOverLifetime;
                velocity.enabled = true;
                // Same curve mode on every axis (a mismatch silently stops the system).
                velocity.x = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
                velocity.y = new ParticleSystem.MinMaxCurve(-0.2f, 0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            foreach (var curtain in new[] { "CurtainLeft", "CurtainRight" })
                Motion(backdrop.Find(curtain), sway: 0.5f, stretch: 0.015f, speed: 0.5f);
            Motion(backdrop.Find("Plant"), sway: 1.4f, speed: 0.6f);

            var mugs = backdrop.Find("SillMugs");
            if (mugs == null) return;
            foreach (Transform mug in mugs) mug.gameObject.SetActive(true);
            foreach (var name in new[] { "SillMugHis", "SillMugHers" })
            {
                var mug = mugs.Find(name)?.GetComponent<SpriteRenderer>();
                if (mug != null && kitchenSteam != null) Steam(mug, kitchenSteam);
            }
        }

        internal static void Motion(Transform target, float sway = 0f, float bob = 0f, float stretch = 0f, float speed = 0.6f)
        {
            if (target == null) return;
            var motion = target.GetComponent<AmbientMotion>() ?? target.gameObject.AddComponent<AmbientMotion>();
            Edit(motion, so =>
            {
                so.FindProperty("swayDegrees").floatValue = sway;
                so.FindProperty("bob").floatValue = bob;
                so.FindProperty("stretch").floatValue = stretch;
                so.FindProperty("speed").floatValue = speed;
                so.FindProperty("windResponse").floatValue = 0f;
            });
        }

        /// <summary>The kitchen mug's own steam, rising from a mug on the sill (always on, already rising at start).</summary>
        internal static void Steam(SpriteRenderer mug, ParticleSystem source)
        {
            var steam = Object.Instantiate(source.gameObject, mug.transform).GetComponent<ParticleSystem>();
            steam.name = "Steam";
            steam.gameObject.SetActive(true);
            steam.transform.position = new Vector3(mug.bounds.center.x, mug.bounds.max.y + 0.05f, mug.transform.position.z);
            steam.transform.localScale = Vector3.one;
            var main = steam.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            var emission = steam.emission;
            emission.enabled = true;
            var renderer = steam.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = mug.sortingLayerName;
            renderer.sortingOrder = mug.sortingOrder + 2;
        }

        // ---------------------------------------------------------------- title, buttons, fade

        private static void BuildUi(Scene boot)
        {
            var canvas = boot.GetRootGameObjects().Select(g => g.GetComponentInChildren<Canvas>(true)).First(c => c != null);
            for (var i = canvas.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(canvas.transform.GetChild(i).gameObject);
            foreach (var old in boot.GetRootGameObjects().Where(g => g.name is MenuName or "BootScreen")) Object.DestroyImmediate(old);
            EnsureEventSystem(boot);

            var root = (RectTransform)canvas.transform;

            // Title on the wall, left of the window.
            var title = UiObject("Title", root);
            Place(title, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 170f), new Vector2(620f, 420f));
            var titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            var name = Label(title, "Name", "Until the\nFirst Snow", 104f, TextAlignmentOptions.BottomLeft, Ink);
            name.fontStyle = FontStyles.Bold;
            name.lineSpacing = -18f;
            name.rectTransform.offsetMin = new Vector2(0f, 90f);
            var tagline = Label(title, "Tagline", "a cozy lofi story about coffee and love", 34f, TextAlignmentOptions.TopLeft,
                new Color(Ink.r, Ink.g, Ink.b, 0.75f));
            tagline.fontStyle = FontStyles.Italic;
            tagline.rectTransform.offsetMax = new Vector2(0f, -345f);

            // Sticky-note buttons under it.
            var buttons = UiObject("Buttons", root);
            Place(buttons, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(110f, -70f), new Vector2(620f, 420f));
            var buttonsGroup = buttons.gameObject.AddComponent<CanvasGroup>();
            var start = NoteButton(buttons, "StartButton", "Make coffee", "note_open_yellow", new Vector2(0f, 0f), -2f);
            var quit = NoteButton(buttons, "QuitButton", "Quit", "note_open_pink", new Vector2(40f, -190f), 3f, 0.7f);

            // The first click starts the sound (browsers block it until then); it sits where the buttons will appear.
            var wake = Label(root, "WakePrompt", "click to start", 56f, TextAlignmentOptions.TopLeft, Ink);
            wake.fontStyle = FontStyles.Italic | FontStyles.Bold;
            Place(wake.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(130f, -120f), new Vector2(620f, 90f));
            var wakeGroup = wake.gameObject.AddComponent<CanvasGroup>();

            var fade = UiObject("Fade", root);
            Stretch(fade);
            Img(fade, Color.black);
            var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();

            var menu = new GameObject(MenuName).AddComponent<MainMenu>();
            SceneManager.MoveGameObjectToScene(menu.gameObject, boot);
            Wire(menu, "title", titleGroup);
            Wire(menu, "buttons", buttonsGroup);
            Wire(menu, "wakePrompt", wakeGroup);
            Wire(menu, "fade", fadeGroup);
            Wire(menu, "startButton", start);
            Wire(menu, "quitButton", quit);
        }

        private static Button NoteButton(RectTransform parent, string name, string text, string paper, Vector2 position, float tilt,
            float scale = 1f)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/Props/{paper}.png");
            var rect = UiObject(name, parent);
            var size = (sprite != null ? sprite.rect.size : new Vector2(371f, 229f)) * scale;
            Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), position + new Vector2(size.x * 0.5f, -size.y * 0.5f), size);
            rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            var image = Img(rect, sprite != null ? Color.white : new Color(0.97f, 0.82f, 0.42f), true);
            image.sprite = sprite;
            image.preserveAspect = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; // MenuButton animates it
            rect.gameObject.AddComponent<MenuButton>();
            var label = Label(rect, "Label", text, 54f * Mathf.Sqrt(scale), TextAlignmentOptions.Center, Ink);
            label.rectTransform.offsetMin = new Vector2(20f, 30f * scale);
            label.rectTransform.offsetMax = new Vector2(-40f * scale, -10f);
            return button;
        }

        private static void EnsureEventSystem(Scene boot)
        {
            if (boot.GetRootGameObjects().Any(g => g.GetComponentInChildren<EventSystem>(true) != null)) return;
            var go = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(go, boot);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ---------------------------------------------------------------- sound

        /// <summary>The lofi track (it carries on into Day 1) and a soft autumn wind.</summary>
        private static void BuildAudio(Scene boot)
        {
            var cue = boot.GetRootGameObjects().Select(g => g.GetComponentInChildren<MusicCue>(true)).FirstOrDefault(c => c != null);
            if (cue == null)
            {
                Debug.LogWarning("MenuBuilder: no MusicCue in Boot — run Courtyard/Setup/Apply Audio first.");
                return;
            }

            var wind = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/Ambience/amb_wind.ogg");
            if (wind == null) Debug.LogWarning("MenuBuilder: amb_wind.ogg missing — the title has music only.");
            Wire(cue, "ambience", wind);
            Wire(cue, "ambienceVolume", 0.35f);
        }
    }
}
