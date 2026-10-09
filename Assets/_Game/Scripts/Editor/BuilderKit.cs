using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Courtyard.Editor
{
    /// <summary>Shared helpers for the content builder: placeholder boxes, UI pieces, serialized wiring, create-if-missing assets.</summary>
    internal static class BuilderKit
    {
        public const string SquarePath = "Assets/_Game/Art/Sprites/Placeholder_Square.png";
        public const string VignettePath = "Assets/_Game/Art/UI/Vignette.png";
        private const string SpriteUnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        public static Sprite Square { get; private set; }
        public static Sprite Vignette { get; private set; }

        // ---------------------------------------------------------------- project setup

        public static void EnsureSortingLayers(params string[] names)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");

            foreach (var layerName in names)
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

        public static void EnsureSquareSprite()
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

            Square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        }

        /// <summary>Code-generated radial gradient (transparent centre, opaque edges) for screen vignettes.</summary>
        public static void EnsureVignetteSprite()
        {
            if (!File.Exists(VignettePath))
            {
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.95f, distance)));
                }

                texture.SetPixels(pixels);
                File.WriteAllBytes(VignettePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(VignettePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(VignettePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            Vignette = AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath);
        }

        public static Material SpriteUnlitMaterial() => AssetDatabase.LoadAssetAtPath<Material>(SpriteUnlitMaterialPath);

        /// <summary>Simple 2D particle emitter: particles spawn along a horizontal line and move with a constant velocity.</summary>
        public static ParticleSystem Particles(Transform parent, string name, Vector2 position, float width, Vector2 velocity,
            Color color, float size, float lifetime, float rate, string layer, int order)
        {
            var go = Empty(parent, name, position).gameObject;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startColor = color;
            main.startSize = size;
            main.startSpeed = 0f;
            main.startLifetime = lifetime;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, 0.05f, 0.05f);

            var emission = system.emission;
            emission.rateOverTime = rate;

            var motion = system.velocityOverLifetime;
            motion.enabled = true;
            motion.space = ParticleSystemSimulationSpace.World;
            motion.x = velocity.x;
            motion.y = velocity.y;
            motion.z = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SpriteUnlitMaterial();
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
            return system;
        }

        // ---------------------------------------------------------------- create-if-missing

        /// <summary>Creates the prefab if missing. With <paramref name="overwrite"/>, rebuilds it in place (GUID kept).</summary>
        public static GameObject LoadOrCreatePrefab(string path, Func<GameObject> build, bool overwrite = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite) return existing;

            var root = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Creates the asset if missing. With <paramref name="overwrite"/>, refills the existing asset (GUID kept).</summary>
        public static T LoadOrCreateAsset<T>(string path, Action<SerializedObject> fill, bool overwrite = false) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null && !overwrite) return asset;

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>
        /// Builds a scene in an additive scratch scene so scenes the user has open are never touched.
        /// With <paramref name="overwrite"/>, replaces an existing scene file (GUID kept) unless it is open.
        /// </summary>
        public static void LoadOrCreateScene(string path, Action build, bool overwrite = false)
        {
            if (File.Exists(path) && !overwrite) return;
            if (SceneManager.GetSceneByPath(path).isLoaded)
                throw new InvalidOperationException($"Close {path} before rebuilding it (it may have unsaved changes).");

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            build();
            EditorSceneManager.SaveScene(scene, path);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>Opens an existing scene additively, edits it, saves and closes it.</summary>
        public static void EditScene(string path, Action<Scene> edit)
        {
            var alreadyOpen = SceneManager.GetSceneByPath(path);
            var scene = alreadyOpen.isLoaded ? alreadyOpen : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            edit(scene);
            EditorSceneManager.SaveScene(scene);
            if (!alreadyOpen.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        // ---------------------------------------------------------------- world placeholders

        public static Transform Empty(Transform parent, string name, Vector2 position)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            transform.localPosition = position;
            return transform;
        }

        public static SpriteRenderer Box(Transform parent, string name, Vector2 position, Vector2 size, Color color,
            int order, bool withCollider = false, string layer = "Props")
        {
            var renderer = Empty(parent, name, position).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;

            if (withCollider) renderer.gameObject.AddComponent<BoxCollider2D>().size = size;
            return renderer;
        }

        /// <summary>A vertical <see cref="Kitchen.SpriteBar"/> growing up from <paramref name="bottom"/>, with a dark backing.</summary>
        public static Kitchen.SpriteBar Bar(Transform parent, string name, Vector2 bottom, float width, float height,
            Color fillColor, Color backColor, int order)
        {
            Box(parent, name + "Back", bottom + new Vector2(0f, height * 0.5f), new Vector2(width, height), backColor, order);
            var bar = Empty(parent, name, bottom);
            var fill = Box(bar, "Fill", Vector2.zero, new Vector2(width, 0f), fillColor, order + 1);
            var spriteBar = bar.gameObject.AddComponent<Kitchen.SpriteBar>();
            Wire(spriteBar, "fill", fill);
            Wire(spriteBar, "width", width);
            Wire(spriteBar, "maxHeight", height);
            return spriteBar;
        }

        public static TextMeshPro WorldText(Transform parent, string name, Vector2 position, string text, float size,
            Color color, string layer, int order)
        {
            var label = Empty(parent, name, position).gameObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(4f, 1f);
            label.sortingLayerID = SortingLayer.NameToID(layer);
            label.sortingOrder = order;
            return label;
        }

        public static Color Gray(float value) => new(value, value, value);

        /// <summary>Finds a descendant by name (including inactive ones).</summary>
        public static Transform Find(Transform root, string objectName, bool required = true)
        {
            var found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
            if (found == null && required) throw new InvalidOperationException($"{root.name} has no '{objectName}' object.");
            return found;
        }

        // ---------------------------------------------------------------- UI

        public static RectTransform UiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static Image Img(RectTransform rect, Color color, bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static TextMeshProUGUI Label(RectTransform parent, string name, string text, float size,
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

        // ---------------------------------------------------------------- serialized wiring

        public static void Wire(Object target, string field, Object value) =>
            Edit(target, so => Property(so, field).objectReferenceValue = value);

        public static void Wire(Object target, string field, float value) =>
            Edit(target, so => Property(so, field).floatValue = value);

        public static void Wire(Object target, string field, int value) =>
            Edit(target, so => Property(so, field).intValue = value);

        public static void Wire(Object target, string field, bool value) =>
            Edit(target, so => Property(so, field).boolValue = value);

        public static void Wire(Object target, string field, string value) =>
            Edit(target, so => Property(so, field).stringValue = value);

        public static void WireEnum(Object target, string field, Enum value) =>
            Edit(target, so => Property(so, field).enumValueIndex = Convert.ToInt32(value));

        public static void WireArray(Object target, string field, Object[] values) =>
            Edit(target, so =>
            {
                var array = Property(so, field);
                array.arraySize = values.Length;
                for (var i = 0; i < values.Length; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            });

        public static void Edit(Object target, Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static SerializedProperty Property(SerializedObject so, string field) =>
            so.FindProperty(field) ?? throw new ArgumentException($"{so.targetObject.GetType().Name} has no serialized field '{field}'.");
    }
}
