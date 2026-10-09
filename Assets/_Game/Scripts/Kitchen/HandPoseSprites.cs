using System;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    [Serializable]
    public sealed class HandPoseSprites
    {
        public HandPose pose;
        public Sprite grey;
        public Sprite beige;

        public Sprite For(Sweater sweater) => sweater == Sweater.Beige && beige != null ? beige : grey;
    }
}
