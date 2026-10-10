using System;
using UnityEngine;

namespace Courtyard.UI
{
    /// <summary>One paper drawing for the <see cref="NoteCard"/>: its size on screen and where the writing goes.</summary>
    [Serializable]
    public sealed class PaperLook
    {
        public NotePaper paper;
        public Sprite sprite;
        [Tooltip("Size on the 1920×1080 canvas.")]
        public Vector2 size = new(760f, 470f);
        [Tooltip("Writing area inset from the paper's edges: left, bottom, right, top (px).")]
        public Vector4 padding = new(60f, 90f, 60f, 50f);
        [Tooltip("Body text colour on this paper.")]
        public Color ink = new(0.22f, 0.17f, 0.14f);
    }
}
