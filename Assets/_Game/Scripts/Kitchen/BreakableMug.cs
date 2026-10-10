using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>A mug on the rack: click it too often and it falls apart (gone for the rest of the day).</summary>
    [RequireComponent(typeof(Breakable))]
    public sealed class BreakableMug : MonoBehaviour
    {
        private Breakable _breakable;

        private void Awake() => _breakable = GetComponent<Breakable>();
        private void OnEnable() => _breakable.Broke += Shatter;
        private void OnDisable() => _breakable.Broke -= Shatter;

        private void Shatter()
        {
            AudioManager.Play(SfxId.Break);
            var renderer = GetComponent<SpriteRenderer>();
            Shards.Burst(transform.position, renderer != null ? renderer.color * _breakable.ShardColour : _breakable.ShardColour,
                renderer != null ? renderer.sortingLayerName : "Props", renderer != null ? renderer.sortingOrder + 1 : 0);
            gameObject.SetActive(false);
        }
    }
}
