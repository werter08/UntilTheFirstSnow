using System.Collections.Generic;
using UnityEngine;

namespace Courtyard.Audio
{
    /// <summary>Maps <see cref="SfxId"/> to clips. Missing clips are silently skipped, so the game runs before audio is imported.</summary>
    [CreateAssetMenu(menuName = "Courtyard/Sfx Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrary : ScriptableObject
    {
        [SerializeField] private List<SfxEntry> entries = new();

        private Dictionary<SfxId, SfxEntry> _lookup;

        public bool TryGet(SfxId id, out SfxEntry entry)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<SfxId, SfxEntry>();
                foreach (var e in entries)
                    if (e != null) _lookup[e.id] = e;
            }

            return _lookup.TryGetValue(id, out entry) && entry.clips != null && entry.clips.Length > 0;
        }

        private void OnValidate() => _lookup = null;
    }
}
