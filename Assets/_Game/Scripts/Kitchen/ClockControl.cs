using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>Click the wall clock to turn it on a little; the kitchen light slowly follows day and night.</summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class ClockControl : MonoBehaviour
    {
        [SerializeField] private KitchenDirector director;

        private PointerTarget _target;

        private void Awake() => _target = GetComponent<PointerTarget>();
        private void OnEnable() => _target.Clicked += Turn;
        private void OnDisable() => _target.Clicked -= Turn;

        private void Turn()
        {
            if (director == null) return;
            director.AdvanceClock(); // the tick is the clock's Fidget sound
        }
    }
}
