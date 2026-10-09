using System;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Minigames
{
    [Serializable]
    public sealed class ToppingOption
    {
        public Topping topping;
        public PointerTarget target;
        [Tooltip("Shown while this topping is picked.")]
        public GameObject pickedMarker;
    }
}
