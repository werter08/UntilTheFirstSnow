using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>A small burst of pieces falling from something that broke (placeholder squares in its colour).</summary>
    public sealed class Shards : MonoBehaviour
    {
        private const int Count = 9;
        private const float Seconds = 0.9f;
        private static Sprite _square;

        public static void Burst(Vector3 position, Color colour, string sortingLayer, int order)
        {
            if (_square == null)
                _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 40f);
            var host = new GameObject("Shards").AddComponent<Shards>();
            host.transform.position = position;
            host.StartCoroutine(host.Fly(colour, sortingLayer, order));
        }

        private System.Collections.IEnumerator Fly(Color colour, string sortingLayer, int order)
        {
            var pieces = new Transform[Count];
            var velocities = new Vector2[Count];
            var spins = new float[Count];
            var renderers = new SpriteRenderer[Count];
            for (var i = 0; i < Count; i++)
            {
                var piece = new GameObject("Shard", typeof(SpriteRenderer));
                piece.transform.SetParent(transform, false);
                piece.transform.localScale = Vector3.one * Random.Range(0.6f, 1.4f);
                renderers[i] = piece.GetComponent<SpriteRenderer>();
                renderers[i].sprite = _square;
                renderers[i].color = Color.Lerp(colour, Color.white, Random.Range(0f, 0.3f));
                renderers[i].sortingLayerName = sortingLayer;
                renderers[i].sortingOrder = order;
                pieces[i] = piece.transform;
                velocities[i] = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.4f, 2.2f));
                spins[i] = Random.Range(-540f, 540f);
            }

            var elapsed = 0f;
            while (elapsed < Seconds)
            {
                var dt = Time.deltaTime;
                elapsed += dt;
                for (var i = 0; i < Count; i++)
                {
                    velocities[i] += Vector2.down * (9f * dt);
                    pieces[i].localPosition += (Vector3)(velocities[i] * dt);
                    pieces[i].Rotate(0f, 0f, spins[i] * dt);
                    var c = renderers[i].color;
                    renderers[i].color = new Color(c.r, c.g, c.b, 1f - elapsed / Seconds);
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
