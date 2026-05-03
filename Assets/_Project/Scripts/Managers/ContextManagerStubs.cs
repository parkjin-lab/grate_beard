using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public abstract class ManagerBase : MonoBehaviour
    {
        [SerializeField] private bool logLifecycle;

        protected virtual void Awake()
        {
            if (logLifecycle)
            {
                Debug.Log($"{name} initialized.", this);
            }
        }
    }
    public sealed partial class AudioManager : ManagerBase { }
}
