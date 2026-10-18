using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulsMaze
{
    /// <summary>
    /// 手機觸控操作：左半邊虛擬搖桿（推到底＝衝刺）、右半邊拖曳轉視角、右下角動作按鈕。
    /// 在手機上或偵測到觸控螢幕時自動啟用。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MobileControls : MonoBehaviour
    {
        public static MobileControls I;

        public Vector2 Move { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool RollPressed { get; private set; }
        public bool HealPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool LockPressed { get; private set; }
        public bool AnyTap { get; private set; }

        public bool Active => forceActive || Application.isMobilePlatform || touchSeen
                              || (Application.platform == RuntimePlatform.WebGLPlayer && Touchscreen.current != null);
        public bool forceActive;
        public float lookSensitivity = 0.18f;

        bool touchSeen;
        int moveId = -1, lookId = -1;
        Vector2 moveOrigin, moveNow, lookLast;
        Texture2D circle;

        enum Btn { Attack, Roll, Heal, Interact, Lock }
        static readonly string[] Labels = { "攻擊", "翻滾", "原素瓶", "互動", "鎖定" };
        readonly bool[] held = new bool[5];

        void Awake()
        {
            I = this;
            circle = MakeCircle(128);
        }

        float S => Screen.height / 1080f;

        // 按鈕位置（觸控座標：左下為原點）
        Vector2 BtnCenter(Btn b)
        {
            float s = S, w = Screen.width;
            switch (b)
            {
                case Btn.Attack: return new Vector2(w - 210f * s, 230f * s);
                case Btn.Roll: return new Vector2(w - 440f * s, 140f * s);
                case Btn.Heal: return new Vector2(w - 470f * s, 360f * s);
                case Btn.Interact: return new Vector2(w - 250f * s, 470f * s);
                default: return new Vector2(w - 110f * s, 470f * s);
            }
        }

        float BtnRadius(Btn b) => (b == Btn.Attack ? 120f : b == Btn.Lock ? 60f : 80f) * S;

        int HitButton(Vector2 p)
        {
            for (int i = 0; i < 5; i++)
                if ((p - BtnCenter((Btn)i)).magnitude <= BtnRadius((Btn)i) * 1.15f) return i;
            return -1;
        }

        void Update()
        {
            AttackPressed = RollPressed = HealPressed = InteractPressed = LockPressed = AnyTap = false;
            LookDelta = Vector2.zero;
            for (int i = 0; i < held.Length; i++) held[i] = false;

            var ts = Touchscreen.current;
            if (ts == null) { Move = Vector2.zero; SprintHeld = false; return; }

            bool moveAlive = false, lookAlive = false;
            foreach (var t in ts.touches)
            {
                bool pressed = t.press.isPressed;
                if (!pressed) continue;
                touchSeen = true;
                int id = t.touchId.ReadValue();
                Vector2 pos = t.position.ReadValue();
                bool began = t.press.wasPressedThisFrame;

                if (began)
                {
                    AnyTap = true;
                    int b = HitButton(pos);
                    if (b >= 0)
                    {
                        switch ((Btn)b)
                        {
                            case Btn.Attack: AttackPressed = true; break;
                            case Btn.Roll: RollPressed = true; break;
                            case Btn.Heal: HealPressed = true; break;
                            case Btn.Interact: InteractPressed = true; break;
                            case Btn.Lock: LockPressed = true; break;
                        }
                    }
                    else if (pos.x < Screen.width * 0.45f && moveId < 0)
                    {
                        moveId = id; moveOrigin = pos; moveNow = pos;
                    }
                    else if (lookId < 0)
                    {
                        lookId = id; lookLast = pos;
                    }
                }

                int hb = HitButton(pos);
                if (hb >= 0 && id != moveId && id != lookId) held[hb] = true;

                if (id == moveId) { moveAlive = true; moveNow = pos; }
                if (id == lookId)
                {
                    lookAlive = true;
                    LookDelta += (pos - lookLast) * lookSensitivity;
                    lookLast = pos;
                }
            }

            if (!moveAlive) moveId = -1;
            if (!lookAlive) lookId = -1;

            if (moveId >= 0)
            {
                Vector2 v = (moveNow - moveOrigin) / (130f * S);
                SprintHeld = v.magnitude > 1.15f;
                Move = Vector2.ClampMagnitude(v, 1f);
            }
            else
            {
                Move = Vector2.zero;
                SprintHeld = false;
            }
        }

        void OnGUI()
        {
            if (!Active) return;
            if (Util.UIFont) GUI.skin.font = Util.UIFont;
            float h = Screen.height, s = S;
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(26 * s),
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            // 搖桿
            if (moveId >= 0)
            {
                DrawCircle(moveOrigin, 130f * s, new Color(1f, 1f, 1f, 0.15f), h);
                Vector2 knob = moveOrigin + Vector2.ClampMagnitude(moveNow - moveOrigin, 130f * s);
                DrawCircle(knob, 55f * s, new Color(1f, 1f, 1f, SprintHeld ? 0.6f : 0.35f), h);
            }
            else
            {
                Vector2 hint = new Vector2(230f * s, 230f * s);
                DrawCircle(hint, 130f * s, new Color(1f, 1f, 1f, 0.07f), h);
                DrawCircle(hint, 55f * s, new Color(1f, 1f, 1f, 0.15f), h);
            }

            // 按鈕
            for (int i = 0; i < 5; i++)
            {
                var b = (Btn)i;
                Vector2 c = BtnCenter(b);
                float r = BtnRadius(b);
                Color col = b == Btn.Attack ? new Color(0.8f, 0.25f, 0.2f, 0.45f) : new Color(1f, 1f, 1f, 0.22f);
                if (held[i]) col.a += 0.3f;
                DrawCircle(c, r, col, h);
                var rect = new Rect(c.x - r, h - c.y - r, r * 2f, r * 2f);
                GUI.Label(rect, Labels[i], style);
            }
        }

        void DrawCircle(Vector2 touchPos, float r, Color c, float screenH)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(touchPos.x - r, screenH - touchPos.y - r, r * 2f, r * 2f), circle);
            GUI.color = old;
        }

        static Texture2D MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01((1f - d) * 12f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return tex;
        }
    }
}
