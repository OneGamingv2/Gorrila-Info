using System;
using UnityEngine;

namespace GorillaInfo.Classes
{
    public class Button : MonoBehaviour
    {
        public event Action OnClick;
        private static float _buttonDelay;
        private const float DelayThreshold = 0.2f;

        private void Start()
        {
            gameObject.layer = 18;
            Collider col = gameObject.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (collider != null && collider.name == "RightHandTriggerCollider" && Time.time > _buttonDelay)
            {
                _buttonDelay = Time.time + DelayThreshold;
                GorillaTagger.Instance?.StartVibration(false, GorillaTagger.Instance.tagHapticStrength * 0.5f, GorillaTagger.Instance.tagHapticDuration * 0.5f);
                OnClick?.Invoke();
            }
        }
    }
}
