using Courtyard.Core;
using Courtyard.UI;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>Click the framed picture on the wall to see it up close (opens the <see cref="PictureCard"/>).</summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class ViewablePicture : MonoBehaviour
    {
        [SerializeField] private PictureCard card;
        [Tooltip("Not while a note is being read.")]
        [SerializeField] private NoteCard notes;

        private PointerTarget _target;

        private void Awake() => _target = GetComponent<PointerTarget>();
        private void OnEnable() => _target.Clicked += Open;
        private void OnDisable() => _target.Clicked -= Open;

        private void Open()
        {
            if (card == null || (notes != null && notes.IsOpen)) return;
            card.Open();
        }
    }
}
