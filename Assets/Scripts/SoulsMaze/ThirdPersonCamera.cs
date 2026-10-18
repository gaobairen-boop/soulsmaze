using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulsMaze
{
    /// <summary>第三人稱跟隨鏡頭：滑鼠轉視角、撞牆自動拉近、鎖定時自動對準敵人。</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public PlayerController player;
        public float distance = 4.6f, height = 1.6f;
        public float mouseSensitivity = 0.12f, padSensitivity = 160f;
        public float yaw, pitch = 15f;

        float shakeTime, shakeAmp;
        Camera cam;
        readonly RaycastHit[] hits = new RaycastHit[16];

        public Camera Cam => cam ? cam : (cam = GetComponent<Camera>());

        void Start()
        {
            if (!Application.isMobilePlatform) LockCursor(true);
        }

        public static void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        public void Shake(float time, float amp)
        {
            shakeTime = Mathf.Max(shakeTime, time);
            shakeAmp = Mathf.Max(shakeAmp, amp);
        }

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.deltaTime;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var gm = GameManager.I;
            bool victory = gm && gm.IsVictory;

            if (kb != null && kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            bool touchMode = MobileControls.I && MobileControls.I.Active;
            if (!victory && !touchMode && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);

            Vector2 look = Vector2.zero;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked) look += mouse.delta.ReadValue() * mouseSensitivity;
            if (pad != null) look += pad.rightStick.ReadValue() * padSensitivity * dt;
            var touch = MobileControls.I;
            if (touch && touch.Active) look += touch.LookDelta;

            Vector3 pivot = target.position + Vector3.up * height;
            if (player && player.lockTarget)
            {
                Vector3 to = player.lockTarget.LockPoint - pivot;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                {
                    float ty = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    yaw = Mathf.LerpAngle(yaw, ty, 1f - Mathf.Exp(-8f * dt));
                }
                pitch = Mathf.Lerp(pitch, 14f, 1f - Mathf.Exp(-4f * dt));
            }
            else
            {
                yaw += look.x;
                pitch -= look.y;
            }
            pitch = Mathf.Clamp(pitch, -35f, 70f);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rot * Vector3.back;
            float d = distance;
            int n = Physics.SphereCastNonAlloc(pivot, 0.25f, back, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f) continue;
                if (h.collider.GetComponentInParent<PlayerController>() || h.collider.GetComponentInParent<Enemy>()) continue;
                d = Mathf.Min(d, Mathf.Max(0.4f, h.distance - 0.05f));
            }

            Vector3 pos = pivot + back * d;
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                pos += Random.insideUnitSphere * shakeAmp;
                if (shakeTime <= 0f) shakeAmp = 0f;
            }
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
