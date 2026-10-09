using System.Collections.Generic;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>Sleeve continuation data for every arm sprite (generated in the editor from the drawings).</summary>
    [CreateAssetMenu(menuName = "Courtyard/Sleeve Library", fileName = "SleeveLibrary")]
    public sealed class SleeveLibrary : ScriptableObject
    {
        [SerializeField] private List<SleeveEntry> entries = new();

        private Dictionary<Sprite, SleeveEntry> _lookup;

        public bool TryGet(Sprite sprite, out SleeveEntry entry)
        {
            entry = null;
            if (sprite == null) return false;
            if (_lookup == null)
            {
                _lookup = new Dictionary<Sprite, SleeveEntry>();
                foreach (var e in entries)
                    if (e?.sprite != null) _lookup[e.sprite] = e;
            }

            return _lookup.TryGetValue(sprite, out entry) && entry.strip != null;
        }

        private void OnValidate() => _lookup = null;
    }
}
