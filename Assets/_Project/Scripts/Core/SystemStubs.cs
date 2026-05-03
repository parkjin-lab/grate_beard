using UnityEngine;

namespace LostBreadcrumbs.Runtime.Systems
{
    public abstract class RuntimeSystemBase : MonoBehaviour
    {
        [SerializeField] private bool isEnabledAtStart = true;

        protected virtual void Awake()
        {
            enabled = isEnabledAtStart;
        }
    }
}
