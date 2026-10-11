using System.Linq;
using Courtyard.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// The ending scene: the kitchen window copied from the Kitchen (the team's art), at night in the first snow — the
    /// chestnut bare and snowy, her window dark (she's here), his americano and her heart cappuccino steaming on the sill —
    /// plus the title, the rolling credits and the thanks for <see cref="EndingScreen"/>. Rebuilt on every run.
    /// </summary>
    internal static class EndingBuilder
    {
        private const string RootName = "EndingBackdrop";

        private static readonly string[] Parts =
        {
            "Global Light 2D", "Wall", "Courtyard", "OutsideWall", "TreeCrown", "MiraWindow", "WindowMask", "Snow",
            "WindowFrame", "WindowFrameShadow", "WindowSill", "WindowSillShadow", "CurtainLeft", "CurtainRight",
            "CurtainRod", "CurtainRodShadow", "Plant", "PlantShadow", "SillMugs",
        };

        private static readonly Color Night = new(0.1f, 0.12f, 0.22f);
        private static readonly Color Cream = new(0.99f, 0.95f, 0.87f);

        [MenuItem("Courtyard/Setup/Build Ending")]
        public static void Apply()
        {
            var kitchen = SceneManager.GetSceneByPath(BuildScript.Scenes[1]);
            var opened = !kitchen.isLoaded;
            if (opened) kitchen = EditorSceneManager.OpenScene(BuildScript.Scenes[1], OpenSceneMode.Additive);
            try
            {
                var kitchenRoot = kitchen.GetRootGameObjects().First(g => g.name == "Kitchen").transform;
                var steam = kitchenRoot.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == "Steam");
                EditScene(BuildScript.Scenes[2], ending =>
                {
                    foreach (var old in ending.GetRootGameObjects().Where(g => g.name is RootName or "EndingDecor")) Object.DestroyImmediate(old);
                    var backdrop = MenuBuilder.CopyWindow(ending, kitchenRoot, RootName, Parts);
                    Dress(backdrop, steam);
                    var camera = ending.GetRootGameObjects().Select(g => g.GetComponentInChildren<Camera>(true)).First(c => c != null);
                    camera.transform.position = new Vector3(0f, 1.6f, -10f);
                    camera.orthographicSize = 3.9f;
                    camera.backgroundColor = Night;
                    BuildUi(ending, camera);
                });
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(kitchen, true);
            }

            FontsBuilder.Apply();
        }

        /// <summary>Night, first snow, her window dark, both mugs full and steaming.</summary>
        private static void Dress(Transform backdrop, ParticleSystem kitchenSteam)
        {
            Sprite Outside(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/Outside/{file}.png");

            var light = backdrop.Find("Global Light 2D")?.GetComponent<Light2D>();
            if (light != null)
            {
                light.color = new Color(1f, 0.86f, 0.72f); // the kitchen lamp's warmth against the night
                light.intensity = 0.95f;
            }

            var sky = backdrop.Find("Courtyard")?.GetComponent<SpriteRenderer>();
            if (sky != null) sky.color = Night;
            var wall = backdrop.Find("OutsideWall")?.GetComponent<SpriteRenderer>();
            if (wall != null) wall.color = new Color(0.42f, 0.45f, 0.62f);

            var crown = backdrop.Find("TreeCrown")?.GetComponent<SpriteRenderer>();
            var snowTree = Outside("tree_5_snow");
            if (crown != null && snowTree != null)
            {
                crown.sprite = snowTree;
                crown.color = new Color(0.72f, 0.75f, 0.9f);
            }

            var window = backdrop.Find("MiraWindow")?.GetComponent<SpriteRenderer>();
            var dark = Outside("mira_window_dark");
            if (window != null && dark != null)
            {
                window.sprite = dark; // she's here now
                window.color = new Color(0.6f, 0.62f, 0.78f);
                foreach (Transform child in window.transform) child.gameObject.SetActive(false);
            }

            var snow = backdrop.Find("Snow")?.GetComponent<ParticleSystem>();
            if (snow != null)
            {
                var main = snow.main;
                main.playOnAwake = true;
                main.loop = true;
                main.prewarm = true;
                var emission = snow.emission;
                emission.enabled = true;
                emission.rateOverTime = 26f;
                var velocity = snow.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(0.05f, 0.3f); // a gentle drift
                velocity.y = new ParticleSystem.MinMaxCurve(-0.1f, 0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            foreach (var curtain in new[] { "CurtainLeft", "CurtainRight" })
                MenuBuilder.Motion(backdrop.Find(curtain), sway: 0.3f, stretch: 0.01f, speed: 0.4f);

            var mugs = backdrop.Find("SillMugs");
            if (mugs == null || kitchenSteam == null) return;
            foreach (Transform mug in mugs) mug.gameObject.SetActive(true);
            foreach (var name in new[] { "SillMugHis", "SillMugHers" })
            {
                var mug = mugs.Find(name)?.GetComponent<SpriteRenderer>();
                if (mug != null) MenuBuilder.Steam(mug, kitchenSteam);
            }
        }

        private static void BuildUi(Scene ending, Camera camera)
        {
            var canvas = ending.GetRootGameObjects().Select(g => g.GetComponentInChildren<Canvas>(true)).First(c => c != null).transform;
            foreach (var old in new[] { "EndingTitle", "EndingDim", "EndingCredits", "EndingThanks", "EndingPrompt" })
            {
                var t = canvas.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            var root = (RectTransform)canvas;

            // Title over the window.
            var titleRect = UiObject("EndingTitle", root);
            Place(titleRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1400f, 300f));
            var titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
            // A soft dark glow under the words so the cream title reads over the light wall and curtains.
            var shade = UiObject("Shade", titleRect);
            Place(shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1500f, 420f));
            Img(shade, new Color(0.08f, 0.06f, 0.1f, 0.6f)).sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/FX/glow_radial.png");
            var title = Label(titleRect, "Title", "Until the First Snow", 112f, TextAlignmentOptions.Top, Cream);
            title.fontStyle = FontStyles.Bold;
            var tagline = Label(titleRect, "Tagline", "", 40f, TextAlignmentOptions.Bottom, new Color(Cream.r, Cream.g, Cream.b, 0.85f));
            tagline.fontStyle = FontStyles.Italic;
            tagline.rectTransform.offsetMin = new Vector2(0f, 40f);

            // A soft dim for the credits.
            var dimRect = UiObject("EndingDim", root);
            Stretch(dimRect);
            Img(dimRect, new Color(0.03f, 0.03f, 0.08f, 0.55f));
            var dim = dimRect.gameObject.AddComponent<CanvasGroup>();
            dim.blocksRaycasts = false;

            // Credits roll up through a window in the middle.
            var viewport = UiObject("EndingCredits", root);
            Place(viewport, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 760f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var credits = Label(viewport, "Credits", "", 40f, TextAlignmentOptions.Top, Cream);
            var creditsRect = credits.rectTransform;
            creditsRect.anchorMin = new Vector2(0f, 0f);
            creditsRect.anchorMax = new Vector2(1f, 0f);
            creditsRect.pivot = new Vector2(0.5f, 1f);
            creditsRect.sizeDelta = new Vector2(0f, 1400f);
            credits.lineSpacing = 10f;

            var thanksRect = UiObject("EndingThanks", root);
            Stretch(thanksRect);
            var thanksGroup = thanksRect.gameObject.AddComponent<CanvasGroup>();
            var thanks = Label(thanksRect, "Thanks", "Thanks for playing. Stay warm.", 72f, TextAlignmentOptions.Center, Cream);
            thanks.fontStyle = FontStyles.Italic;

            var promptRect = UiObject("EndingPrompt", root);
            Stretch(promptRect);
            var prompt = promptRect.gameObject.AddComponent<CanvasGroup>();
            var promptText = Label(promptRect, "Prompt", "click to return to the title", 34f, TextAlignmentOptions.Bottom, Cream);
            promptText.rectTransform.offsetMin = new Vector2(0f, 70f);
            promptText.fontStyle = FontStyles.Italic;

            // Everything under the screen fade.
            foreach (var fader in canvas.GetComponentsInChildren<ScreenFader>(true)) fader.transform.SetAsLastSibling();

            var screen = ending.GetRootGameObjects().Select(g => g.GetComponentInChildren<EndingScreen>(true)).First(e => e != null);
            Wire(screen, "worldCamera", camera);
            Edit(screen, so =>
            {
                so.FindProperty("cameraTo").vector3Value = new Vector3(0f, 0.15f, 0f); // toward the two mugs on the sill
                so.FindProperty("cameraSizeTo").floatValue = 2.5f;
            });
            Wire(screen, "titleGroup", titleGroup);
            Wire(screen, "title", title);
            Wire(screen, "tagline", tagline);
            Wire(screen, "dim", dim);
            Wire(screen, "creditsViewport", viewport);
            Wire(screen, "credits", credits);
            Wire(screen, "thanksGroup", thanksGroup);
            Wire(screen, "thanks", thanks);
            Wire(screen, "prompt", prompt);
        }
    }
}
