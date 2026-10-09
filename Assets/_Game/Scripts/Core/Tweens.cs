using System;
using System.Collections;
using UnityEngine;

namespace Courtyard.Core
{
    /// <summary>Tiny coroutine tweens (no DOTween in this project).</summary>
    public static class Tweens
    {
        /// <summary>Calls <paramref name="apply"/> with t going 0..1 over <paramref name="duration"/> seconds.</summary>
        public static IEnumerator Run(float duration, Action<float> apply)
        {
            if (duration <= 0f)
            {
                apply(1f);
                yield break;
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                apply(Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }

            apply(1f);
        }

        /// <summary>Like <see cref="Run"/> but with linear t, for tweens that shape their own curve.</summary>
        public static IEnumerator RunLinear(float duration, Action<float> apply)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                apply(elapsed / duration);
                yield return null;
            }

            apply(1f);
        }
    }
}
