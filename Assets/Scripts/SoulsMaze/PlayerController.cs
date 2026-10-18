using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulsMaze
{
    /// <summary>魂類操作：移動、衝刺、翻滾（無敵幀）、攻擊（可連段）、原素瓶、鎖定、互動。</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float maxHP = 200f, maxStamina = 100f;
        public float hp, stamina;
        public int maxEstus = 3, estus;
        public float walkSpeed = 4.2f, sprintSpeed = 6.8f, rollSpeed = 9.5f;
        public float attackDamage = 40f;
        public float healAmount = 90f;

        [HideInInspector] public ThirdPersonCamera cam;
        [HideInInspector] public Enemy lockTarget;
        [HideInInspector] public Interactable focus;
        public bool IsDead { get; private set; }

        enum Act { None, Roll, Attack, Heal, Stagger }
        Act act = Act.None;
        float actTime, actDur;
        Vector3 rollDir, knock;
        bool hitDone, healDone, attackBuffered, sprinting;
        float vy, staminaDelay, invulnTimer, hurtFlash;

        CharacterController cc;
        Transform visual, swordPivot;
        Material bodyMat;
        Color bodyColor;
        readonly HashSet<Enemy> hitSet = new HashSet<Enemy>();

        public bool Invulnerable => invulnTimer > 0f || (act == Act.Roll && actTime > 0.04f && actTime < 0.42f);

        public static PlayerController Create(Vector3 pos)
        {
            var go = new GameObject("Player");
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            return go.AddComponent<PlayerController>();
        }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            hp = maxHP; stamina = maxStamina; estus = maxEstus;
            BuildVisual();
        }

        void BuildVisual()
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            bodyColor = new Color(0.38f, 0.36f, 0.33f);
            bodyMat = Util.Mat(bodyColor, 0.45f);
            var dark = Util.Mat(new Color(0.12f, 0.12f, 0.13f), 0.5f);
            var cloth = Util.Mat(new Color(0.32f, 0.08f, 0.07f), 0.05f);
            var steel = Util.Mat(new Color(0.7f, 0.72f, 0.75f), 0.8f);
            var visor = Util.Mat(Color.black, 0f, new Color(0.6f, 0.5f, 0.3f));

            Util.Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "Helmet", visual, new Vector3(0f, 1.62f, 0.02f), new Vector3(0.48f, 0.5f, 0.5f), dark, false);
            Util.Prim(PrimitiveType.Cube, "Visor", visual, new Vector3(0f, 1.64f, 0.24f), new Vector3(0.3f, 0.05f, 0.05f), visor, false);
            Util.Prim(PrimitiveType.Cube, "Cape", visual, new Vector3(0f, 1.0f, -0.36f), new Vector3(0.7f, 1.2f, 0.06f), cloth, false);
            Util.Prim(PrimitiveType.Cube, "Shield", visual, new Vector3(-0.48f, 1.0f, 0.12f), new Vector3(0.08f, 0.65f, 0.5f), dark, false);

            swordPivot = new GameObject("SwordPivot").transform;
            swordPivot.SetParent(visual, false);
            swordPivot.localPosition = new Vector3(0.45f, 1.05f, 0.1f);
            Util.Prim(PrimitiveType.Cube, "Blade", swordPivot, new Vector3(0f, 0f, 0.7f), new Vector3(0.07f, 0.12f, 1.2f), steel, false);
            Util.Prim(PrimitiveType.Cube, "Guard", swordPivot, new Vector3(0f, 0f, 0.08f), new Vector3(0.35f, 0.07f, 0.07f), dark, false);
            SwordRest();
        }

        void SwordRest() => swordPivot.localRotation = Quaternion.Euler(35f, 10f, 0f);

        void Update()
        {
            float dt = Time.deltaTime;
            var gm = GameManager.I;
            if (IsDead || gm == null || gm.InputBlocked)
            {
                Gravity(dt, Vector3.zero);
                return;
            }

            // ---------- 讀取輸入 ----------
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

            Vector2 mv = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) mv.y += 1f;
                if (kb.sKey.isPressed) mv.y -= 1f;
                if (kb.dKey.isPressed) mv.x += 1f;
                if (kb.aKey.isPressed) mv.x -= 1f;
            }
            if (pad != null) mv += pad.leftStick.ReadValue();
            var touch = MobileControls.I;
            bool touchOn = touch && touch.Active;
            if (touchOn) mv += touch.Move;
            mv = Vector2.ClampMagnitude(mv, 1f);

            bool sprintHeld = (kb != null && kb.leftShiftKey.isPressed) || (pad != null && pad.leftStickButton.isPressed);
            bool rollPressed = (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            bool attackPressed = (mouse != null && cursorLocked && mouse.leftButton.wasPressedThisFrame)
                                 || (kb != null && kb.jKey.wasPressedThisFrame)
                                 || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            bool healPressed = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            bool interactPressed = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool lockPressed = (kb != null && kb.tabKey.wasPressedThisFrame)
                               || (mouse != null && mouse.middleButton.wasPressedThisFrame)
                               || (pad != null && pad.rightStickButton.wasPressedThisFrame);
            if (touchOn)
            {
                sprintHeld |= touch.SprintHeld;
                rollPressed |= touch.RollPressed;
                attackPressed |= touch.AttackPressed;
                healPressed |= touch.HealPressed;
                interactPressed |= touch.InteractPressed;
                lockPressed |= touch.LockPressed;
            }

            Vector3 camF = cam ? cam.transform.forward : Vector3.forward;
            camF.y = 0f;
            camF = camF.sqrMagnitude > 0.0001f ? camF.normalized : Vector3.forward;
            Vector3 camR = new Vector3(camF.z, 0f, -camF.x);
            Vector3 wish = camF * mv.y + camR * mv.x;

            // ---------- 鎖定 ----------
            if (lockTarget && (!lockTarget.Alive || Vector3.Distance(lockTarget.transform.position, transform.position) > 22f))
                lockTarget = null;
            if (lockPressed) lockTarget = lockTarget ? null : FindLockTarget();

            // ---------- 可互動物件 ----------
            focus = null;
            float best = float.MaxValue;
            foreach (var it in gm.interactables)
            {
                if (!it || !it.CanInteract) continue;
                Vector3 d = it.Position - transform.position;
                d.y = 0f;
                float m = d.magnitude;
                if (m <= it.Range && m < best) { best = m; focus = it; }
            }

            // ---------- 動作 ----------
            if (act == Act.None)
            {
                if (rollPressed && stamina > 1f) StartRoll(wish);
                else if (attackPressed && stamina > 1f) StartAttack();
                else if (healPressed && estus > 0) StartHeal();
                else if (interactPressed && focus) focus.Interact(this);
            }
            else if (act == Act.Attack && attackPressed && actTime > 0.25f) attackBuffered = true;
            else if (act == Act.Attack && rollPressed && actTime > 0.4f && stamina > 1f) StartRoll(wish);

            Vector3 vel = Vector3.zero;
            sprinting = false;
            actTime += dt;
            switch (act)
            {
                case Act.None:
                {
                    sprinting = sprintHeld && wish.sqrMagnitude > 0.01f && stamina > 0.5f;
                    if (sprinting) UseStamina(22f * dt);
                    vel = wish * (sprinting ? sprintSpeed : walkSpeed);
                    if (lockTarget && !sprinting) FaceDir(lockTarget.transform.position - transform.position, 14f, dt);
                    else FaceDir(wish, 12f, dt);
                    break;
                }
                case Act.Roll:
                {
                    float t = Mathf.Clamp01(actTime / actDur);
                    vel = rollDir * rollSpeed * Mathf.Lerp(1.1f, 0.3f, t);
                    float squash = Mathf.Sin(t * Mathf.PI);
                    visual.localScale = new Vector3(1f + 0.15f * squash, 1f - 0.45f * squash, 1f + 0.15f * squash);
                    break;
                }
                case Act.Attack:
                {
                    if (lockTarget) FaceDir(lockTarget.transform.position - transform.position, 20f, dt);
                    if (actTime < 0.08f)
                    {
                        swordPivot.localRotation = Quaternion.Slerp(swordPivot.localRotation, Quaternion.Euler(-50f, -80f, 0f), 1f - Mathf.Exp(-25f * dt));
                    }
                    else
                    {
                        float swing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.3f, actTime));
                        swordPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-50f, 60f, swing), Mathf.Lerp(-80f, 70f, swing), 0f);
                    }
                    if (actTime > 0.1f && actTime < 0.24f) vel = transform.forward * 2.5f;
                    if (!hitDone && actTime >= 0.2f) { hitDone = true; DoHit(); }
                    break;
                }
                case Act.Heal:
                {
                    vel = wish * 1.2f;
                    if (!healDone && actTime >= 0.7f) { healDone = true; hp = Mathf.Min(maxHP, hp + healAmount); }
                    break;
                }
                case Act.Stagger:
                    vel = knock * Mathf.Clamp01(1f - actTime / actDur);
                    break;
            }
            if (act != Act.None && actTime >= actDur) EndAct();

            // ---------- 耐力 ----------
            staminaDelay -= dt;
            if (staminaDelay <= 0f && act != Act.Roll && act != Act.Attack && !sprinting)
                stamina = Mathf.Min(maxStamina, stamina + (act == Act.Heal ? 15f : 38f) * dt);
            if (invulnTimer > 0f) invulnTimer -= dt;

            // ---------- 受傷 / 喝藥的顏色 ----------
            Color tint = bodyColor;
            if (act == Act.Heal) tint = Color.Lerp(bodyColor, new Color(1f, 0.8f, 0.4f), Mathf.Sin(Mathf.Clamp01(actTime / actDur) * Mathf.PI));
            if (hurtFlash > 0f)
            {
                hurtFlash -= dt;
                tint = Color.Lerp(tint, new Color(1f, 0.25f, 0.2f), Mathf.Clamp01(hurtFlash / 0.18f));
            }
            Util.SetColor(bodyMat, tint);

            Gravity(dt, vel);
            if (transform.position.y < -15f) Die();
        }

        void Gravity(float dt, Vector3 vel)
        {
            if (!cc.enabled) return;
            if (cc.isGrounded && vy < 0f) vy = -2f;
            vy -= 20f * dt;
            vel.y = vy;
            cc.Move(vel * dt);
        }

        void FaceDir(Vector3 d, float speed, float dt)
        {
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-speed * dt));
        }

        void UseStamina(float amount)
        {
            stamina = Mathf.Max(0f, stamina - amount);
            staminaDelay = 0.7f;
        }

        void StartRoll(Vector3 wish)
        {
            SwordRest();
            act = Act.Roll; actTime = 0f; actDur = 0.55f;
            rollDir = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(rollDir);
            UseStamina(18f);
            attackBuffered = false;
        }

        void StartAttack()
        {
            act = Act.Attack; actTime = 0f; actDur = 0.6f;
            hitDone = false; attackBuffered = false;
            hitSet.Clear();
            UseStamina(16f);
        }

        void StartHeal()
        {
            act = Act.Heal; actTime = 0f; actDur = 1.0f;
            healDone = false;
            estus--;
        }

        void EndAct()
        {
            var prev = act;
            act = Act.None; actTime = 0f;
            visual.localScale = Vector3.one;
            SwordRest();
            bool chain = prev == Act.Attack && attackBuffered && stamina > 1f;
            attackBuffered = false;
            if (chain) StartAttack();
        }

        void DoHit()
        {
            Vector3 c = transform.position + Vector3.up * 1.0f + transform.forward * 1.1f;
            bool any = false;
            foreach (var col in Physics.OverlapSphere(c, 1.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                var e = col.GetComponentInParent<Enemy>();
                if (e && e.Alive && hitSet.Add(e))
                {
                    e.TakeDamage(attackDamage, transform.position);
                    any = true;
                }
            }
            if (any && cam) cam.Shake(0.12f, 0.08f);
        }

        Enemy FindLockTarget()
        {
            Enemy bestE = null;
            float bestScore = float.MaxValue;
            Vector3 camF = cam ? cam.transform.forward : transform.forward;
            camF.y = 0f;
            foreach (var e in GameManager.I.AllEnemies())
            {
                if (!e || !e.Alive) continue;
                Vector3 to = e.transform.position - transform.position;
                float d = to.magnitude;
                if (d > 16f) continue;
                float ang = Vector3.Angle(camF, new Vector3(to.x, 0f, to.z));
                if (ang > 75f) continue;
                if (!LineClear(e)) continue;
                float score = d + ang * 0.1f;
                if (score < bestScore) { bestScore = score; bestE = e; }
            }
            return bestE;
        }

        bool LineClear(Enemy e)
        {
            Vector3 a = transform.position + Vector3.up * 1.4f;
            Vector3 b = e.LockPoint;
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return true;
            if (Physics.Raycast(a, dir / len, out var hit, len, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<Enemy>() == e;
            return true;
        }

        public void TakeDamage(float dmg, Vector3 from)
        {
            if (IsDead || Invulnerable) return;
            hp -= dmg;
            hurtFlash = 0.18f;
            invulnTimer = 0.35f;
            if (cam) cam.Shake(0.25f, 0.18f);
            if (hp <= 0f) { hp = 0f; Die(); return; }
            Vector3 k = transform.position - from;
            k.y = 0f;
            knock = (k.sqrMagnitude > 0.001f ? k.normalized : -transform.forward) * 6f;
            act = Act.Stagger; actTime = 0f; actDur = 0.35f;
            attackBuffered = false;
            visual.localScale = Vector3.one;
            SwordRest();
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            lockTarget = null;
            act = Act.None;
            visual.localScale = Vector3.one;
            visual.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            visual.localPosition = new Vector3(0f, 0.35f, 0f);
            Util.SetColor(bodyMat, bodyColor);
            GameManager.I.OnPlayerDied(transform.position);
        }

        public void Respawn(Vector3 pos, Quaternion rot)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
            vy = 0f;
            IsDead = false;
            act = Act.None; actTime = 0f;
            invulnTimer = 1f;
            visual.localRotation = Quaternion.identity;
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            SwordRest();
            RestoreFull();
            if (cam) cam.yaw = rot.eulerAngles.y;
        }

        public void RestoreFull()
        {
            hp = maxHP;
            stamina = maxStamina;
            estus = maxEstus;
        }
    }
}
