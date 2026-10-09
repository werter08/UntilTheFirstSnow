using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>Neighbours across the courtyard: their windows light up and go dark now and then.</summary>
    public sealed class NeighbourWindows : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] windows;
        [SerializeField] private Color lit = new(1f, 0.86f, 0.55f);
        [SerializeField] private Color dark = new(0.22f, 0.24f, 0.32f);
        [SerializeField, Range(0f, 1f)] private float litChance = 0.4f;
        [SerializeField] private Vector2 secondsBetweenChanges = new(4f, 11f);

        private float _next;

        private void Start()
        {
            foreach (var w in windows) w.color = Random.value < litChance ? lit : dark;
            _next = Time.time + Random.Range(secondsBetweenChanges.x, secondsBetweenChanges.y);
        }

        private void Update()
        {
            if (windows.Length == 0 || Time.time < _next) return;
            var w = windows[Random.Range(0, windows.Length)];
            w.color = w.color == lit ? dark : lit;
            _next = Time.time + Random.Range(secondsBetweenChanges.x, secondsBetweenChanges.y);
        }
    }
}
