using System;
using UnityEngine;

namespace GorillaInfo
{
    public class ButtonClick : MonoBehaviour
    {
        public GameObject fingerSphere;
        public Action OnClick;
        private const float SphereScale = 0.01f;
        private const float PositionOffsetY = -0.1f;

        public void ball()
        {
            if (fingerSphere != null) return;

            fingerSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fingerSphere.name = "HandSphere";
            fingerSphere.transform.localScale = Vector3.one * SphereScale;

            SphereCollider col = fingerSphere.GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = true;

            Renderer r = fingerSphere.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = Color.white;
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            fingerSphere.layer = LayerMask.NameToLayer("GorillaInteractable");
        }

        public void ballvisibility()
        {
            if (fingerSphere == null || GorillaInfoMain.Instance == null) return;
            fingerSphere.SetActive(GorillaInfoMain.Instance.menuState == GorillaInfoMain.MenuState.Open);
        }

        public void uptadeball()
        {
            if (fingerSphere == null || GorillaInfoMain.Instance == null || GorillaTagger.Instance == null) return;
            if (GorillaInfoMain.Instance.menuState != GorillaInfoMain.MenuState.Open) return;

            Transform hand = GorillaTagger.Instance.rightHandTransform;
            if (hand == null) return;

            fingerSphere.transform.SetParent(hand, false);
            fingerSphere.transform.localPosition = new Vector3(0f, PositionOffsetY, 0f);
            fingerSphere.transform.localRotation = Quaternion.identity;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && other.CompareTag("GorillaInteractable"))
                OnClick?.Invoke();
        }
    }
}
