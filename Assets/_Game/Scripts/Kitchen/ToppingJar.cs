using Courtyard.Core;
using Courtyard.Minigames;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>A clickable topping jar on the counter.</summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class ToppingJar : MonoBehaviour
    {
        [SerializeField] private Topping topping;
        [SerializeField] private PointerTarget target;
        [Tooltip("Shown while this topping is picked.")]
        [SerializeField] private GameObject pickedMarker;

        public Topping Topping => topping;
        public PointerTarget Target => target;
        public bool IsPicked => pickedMarker.activeSelf;

        public void SetPicked(bool picked) => pickedMarker.SetActive(picked);
    }
}
