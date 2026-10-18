using UnityEngine;

namespace SoulsMaze
{
    /// <summary>可以按 E 互動的物件（篝火、祭壇、訊息、血跡……）。</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public virtual Vector3 Position => transform.position;
        public virtual float Range => 2.6f;
        public abstract string Prompt { get; }
        public virtual bool CanInteract => true;
        public abstract void Interact(PlayerController p);

        protected virtual void OnEnable()
        {
            if (GameManager.I) GameManager.I.interactables.Add(this);
        }

        protected virtual void OnDisable()
        {
            if (GameManager.I) GameManager.I.interactables.Remove(this);
        }
    }
}
