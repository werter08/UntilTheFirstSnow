using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>Click the ceiling lamp to switch it on or off; flick it too often and the bulb pops for the day.</summary>
    [RequireComponent(typeof(PointerTarget), typeof(Breakable))]
    public sealed class LampSwitch : MonoBehaviour
    {
        [SerializeField] private KitchenDirector director;

        private PointerTarget _target;
        private Breakable _breakable;

        private void Awake()
        {
            _target = GetComponent<PointerTarget>();
            _breakable = GetComponent<Breakable>();
        }

        private void OnEnable()
        {
            _target.Clicked += Flick;
            _breakable.Broke += Pop;
        }

        private void OnDisable()
        {
            _target.Clicked -= Flick;
            _breakable.Broke -= Pop;
        }

        private void Flick()
        {
            if (director == null || _breakable.IsBroken) return;
            director.ToggleLamp(); // the click sound is the lamp's Fidget (LampSwitch)
        }

        private void Pop()
        {
            if (director != null) director.BreakLamp();
            AudioManager.Play(SfxId.Break);
            var renderer = GetComponent<SpriteRenderer>();
            Shards.Burst(transform.position, _breakable.ShardColour,
                renderer != null ? renderer.sortingLayerName : "FX", renderer != null ? renderer.sortingOrder + 1 : 0);
        }
    }
}
