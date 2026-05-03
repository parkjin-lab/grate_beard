using System;
using LostBreadcrumbs.Runtime.Core.Input;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public sealed class DebugManager : ManagerBase
    {
        public static event Action<bool> OverlayToggled;

        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [SerializeField] private bool overlayEnabled = true;

        public bool OverlayEnabled => overlayEnabled;

        private void Update()
        {
            if (RuntimeInputAdapter.GetKeyDown(toggleKey))
            {
                SetOverlayEnabled(!overlayEnabled);
            }
        }

        public void SetOverlayEnabled(bool enabled)
        {
            if (overlayEnabled == enabled)
            {
                return;
            }

            overlayEnabled = enabled;
            OverlayToggled?.Invoke(overlayEnabled);
        }
    }
}
