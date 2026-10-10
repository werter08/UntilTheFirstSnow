using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>Click the paper calendar: today's page with a hopeful line opens on the note card.</summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class CalendarPage : MonoBehaviour
    {
        [SerializeField] private KitchenDirector director;

        private PointerTarget _target;

        private void Awake() => _target = GetComponent<PointerTarget>();
        private void OnEnable() => _target.Clicked += Open;
        private void OnDisable() => _target.Clicked -= Open;

        private void Open()
        {
            if (director != null) director.OpenCalendar();
        }
    }
}
