using System.Collections;
using UnityEngine;

namespace SoulsMaze
{
    /// <summary>
    /// 敵人 AI：巡邏 → 發現玩家 → 沿迷宮尋路追擊 → 舉起武器（蓄力，眼睛變亮）→ 揮擊 → 硬直。
    /// isBoss = true 時是中央大廳的頭目「Q 版火龍」：撲咬會二連擊，中距離會仰頭噴火。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Enemy : MonoBehaviour
    {
        public bool isBoss;
        public string displayName = "活屍";
        public float maxHP = 70f, hp;
        public float damage = 20f;
        public float walkSpeed = 1.4f, chaseSpeed = 3.6f;
        public float attackRange = 1.9f, windup = 0.7f, recover = 0.9f;
        public float sightRange = 12f;
        public int soulReward = 80;
        public float poise = 2f; // 被打幾下會硬直

        enum State { Idle, Chase, Windup, Strike, Recover, Return, Stagger, BreathWindup, Breath, Dead }
        State state;
        float st, vy, flash, repath, poiseDmg, scale = 1f;
        bool comboUsed, comboPlanned;
        Vector3 spawnPos, wanderTarget, pathPoint, knock;
        Vector2Int lastFrom = new Vector2Int(-99, -99), lastTo = new Vector2Int(-99, -99);
        Quaternion spawnRot;
        CharacterController cc;
        Transform visual, arm;
        Material bodyMat, eyeMat;
        Transform mouth;
        Light breathLight;
        float breathCd = 4f, breathTick, emitTimer;
        Color bodyColor, eyeColor;

        public bool Alive => hp > 0f && state != State.Dead && gameObject.activeInHierarchy;
        public bool IsDeadForever => isBoss && hp <= 0f;
        public Vector3 LockPoint => transform.position + Vector3.up * 1.25f * scale;

        public static Enemy Create(Vector3 pos, Quaternion rot, bool boss, Transform parent)
        {
            var go = new GameObject(boss ? "Boss" : "Hollow");
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, rot);
            float s = boss ? 1.9f : 1f;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f * s;
            cc.radius = 0.4f * s;
            cc.center = new Vector3(0f, 0.9f * s, 0f);
            cc.stepOffset = 0.3f;
            var e = go.AddComponent<Enemy>();
            e.Setup(boss, s);
            return e;
        }

        void Setup(bool boss, float s)
        {
            isBoss = boss;
            scale = s;
            cc = GetComponent<CharacterController>();
            if (boss)
            {
                displayName = "炎之幼龍・迷宮之心";
                maxHP = 500f; damage = 30f; walkSpeed = 2f; chaseSpeed = 3.4f;
                attackRange = 3.0f; windup = 0.85f; recover = 1.0f; sightRange = 40f;
                soulReward = 3000; poise = 5f;
            }
            hp = maxHP;
            spawnPos = transform.position;
            spawnRot = transform.rotation;
            wanderTarget = spawnPos;
            BuildVisual();
        }

        void BuildVisual()
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.localScale = Vector3.one * scale;
            if (isBoss) { BuildDragon(); return; }
            bodyColor = isBoss ? new Color(0.16f, 0.15f, 0.15f) : new Color(0.33f, 0.29f, 0.24f);
            eyeColor = isBoss ? new Color(4f, 1.6f, 0.3f) : new Color(3f, 0.25f, 0.15f);
            bodyMat = Util.Mat(bodyColor, isBoss ? 0.55f : 0.1f);
            eyeMat = Util.Mat(Color.black, 0f, eyeColor);
            var rag = Util.Mat(isBoss ? new Color(0.25f, 0.05f, 0.04f) : new Color(0.2f, 0.18f, 0.14f), 0.05f);
            var metal = Util.Mat(isBoss ? new Color(0.25f, 0.24f, 0.26f) : new Color(0.35f, 0.27f, 0.2f), isBoss ? 0.7f : 0.3f);

            var body = Util.Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.88f, 0f), new Vector3(0.75f, 0.88f, 0.7f), bodyMat, false);
            body.transform.localRotation = Quaternion.Euler(isBoss ? 0f : 12f, 0f, 0f);
            float headZ = isBoss ? 0.02f : 0.15f;
            Util.Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.6f, headZ), Vector3.one * 0.42f, bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "EyeL", visual, new Vector3(-0.09f, 1.63f, headZ + 0.19f), Vector3.one * 0.07f, eyeMat, false);
            Util.Prim(PrimitiveType.Sphere, "EyeR", visual, new Vector3(0.09f, 1.63f, headZ + 0.19f), Vector3.one * 0.07f, eyeMat, false);
            Util.Prim(PrimitiveType.Cube, "Rags", visual, new Vector3(0f, 0.55f, 0f), new Vector3(0.82f, 0.6f, 0.76f), rag, false);

            arm = new GameObject("Arm").transform;
            arm.SetParent(visual, false);
            arm.localPosition = new Vector3(0.42f, 1.25f, 0.05f);
            Util.Prim(PrimitiveType.Cube, "Forearm", arm, new Vector3(0f, -0.3f, 0f), new Vector3(0.14f, 0.6f, 0.14f), bodyMat, false);
            Util.Prim(PrimitiveType.Cube, "Club", arm, new Vector3(0f, -0.6f, 0.45f), new Vector3(0.12f, 0.12f, 0.9f), metal, false);
            arm.localRotation = Quaternion.Euler(20f, 0f, 0f);
        }

        /// <summary>Q 版火龍：大頭、圓肚、短腿、小翅膀、背刺、尾巴尖端著火。</summary>
        void BuildDragon()
        {
            bodyColor = new Color(0.85f, 0.28f, 0.12f);
            eyeColor = new Color(4f, 1.5f, 0.2f);
            bodyMat = Util.Mat(bodyColor, 0.35f);
            eyeMat = Util.Mat(Color.black, 0.8f, eyeColor);
            var belly = Util.Mat(new Color(1f, 0.82f, 0.45f), 0.2f);
            var white = Util.Mat(Color.white, 0.9f);
            var blush = Util.Mat(new Color(1f, 0.55f, 0.6f), 0.1f);
            var spike = Util.Mat(new Color(1f, 0.7f, 0.2f), 0.4f);
            var wing = Util.Mat(new Color(0.55f, 0.12f, 0.1f), 0.2f);
            var fire = Util.Mat(new Color(1f, 0.6f, 0.1f), 0f, new Color(7f, 3f, 0.4f));

            // 身體
            Util.Prim(PrimitiveType.Sphere, "Body", visual, new Vector3(0f, 0.75f, 0f), new Vector3(1.1f, 1.0f, 1.2f), bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "Belly", visual, new Vector3(0f, 0.7f, 0.28f), new Vector3(0.8f, 0.75f, 0.7f), belly, false);
            Util.Prim(PrimitiveType.Cylinder, "LegL", visual, new Vector3(-0.3f, 0.2f, 0.05f), new Vector3(0.32f, 0.2f, 0.32f), bodyMat, false);
            Util.Prim(PrimitiveType.Cylinder, "LegR", visual, new Vector3(0.3f, 0.2f, 0.05f), new Vector3(0.32f, 0.2f, 0.32f), bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "ArmL", visual, new Vector3(-0.45f, 0.85f, 0.35f), Vector3.one * 0.2f, bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "ArmR", visual, new Vector3(0.45f, 0.85f, 0.35f), Vector3.one * 0.2f, bodyMat, false);

            // 背刺與小翅膀
            for (int i = 0; i < 3; i++)
            {
                var sp = Util.Prim(PrimitiveType.Cube, "Spike" + i, visual, new Vector3(0f, 1.2f - i * 0.25f, -0.35f - i * 0.15f), Vector3.one * (0.22f - i * 0.04f), spike, false);
                sp.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            }
            Util.Prim(PrimitiveType.Cube, "WingL", visual, new Vector3(-0.5f, 1.05f, -0.3f), new Vector3(0.5f, 0.35f, 0.04f), wing, false)
                .transform.localRotation = Quaternion.Euler(0f, 35f, 25f);
            Util.Prim(PrimitiveType.Cube, "WingR", visual, new Vector3(0.5f, 1.05f, -0.3f), new Vector3(0.5f, 0.35f, 0.04f), wing, false)
                .transform.localRotation = Quaternion.Euler(0f, -35f, -25f);

            // 尾巴＋尾焰
            Util.Prim(PrimitiveType.Sphere, "Tail1", visual, new Vector3(0f, 0.45f, -0.6f), Vector3.one * 0.45f, bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "Tail2", visual, new Vector3(0f, 0.38f, -0.92f), Vector3.one * 0.33f, bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "Tail3", visual, new Vector3(0f, 0.36f, -1.18f), Vector3.one * 0.24f, bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "TailFlame", visual, new Vector3(0f, 0.48f, -1.36f), new Vector3(0.25f, 0.35f, 0.25f), fire, false);
            Util.PointLight(visual, new Vector3(0f, 0.6f, -1.4f), new Color(1f, 0.5f, 0.15f), 6f, 2f).gameObject.AddComponent<Flicker>();

            // 大頭（arm 這個欄位拿來當「頭」的關節，撲咬/仰頭都靠它）
            arm = new GameObject("HeadPivot").transform;
            arm.SetParent(visual, false);
            arm.localPosition = new Vector3(0f, 1.2f, 0.2f);
            Util.Prim(PrimitiveType.Sphere, "Head", arm, new Vector3(0f, 0.28f, 0.12f), new Vector3(0.95f, 0.85f, 0.9f), bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "Snout", arm, new Vector3(0f, 0.14f, 0.52f), new Vector3(0.55f, 0.4f, 0.45f), bodyMat, false);
            Util.Prim(PrimitiveType.Sphere, "EyeWhiteL", arm, new Vector3(-0.2f, 0.4f, 0.47f), Vector3.one * 0.24f, white, false);
            Util.Prim(PrimitiveType.Sphere, "EyeWhiteR", arm, new Vector3(0.2f, 0.4f, 0.47f), Vector3.one * 0.24f, white, false);
            Util.Prim(PrimitiveType.Sphere, "PupilL", arm, new Vector3(-0.2f, 0.4f, 0.57f), Vector3.one * 0.12f, eyeMat, false);
            Util.Prim(PrimitiveType.Sphere, "PupilR", arm, new Vector3(0.2f, 0.4f, 0.57f), Vector3.one * 0.12f, eyeMat, false);
            Util.Prim(PrimitiveType.Sphere, "BlushL", arm, new Vector3(-0.33f, 0.18f, 0.45f), new Vector3(0.14f, 0.08f, 0.06f), blush, false);
            Util.Prim(PrimitiveType.Sphere, "BlushR", arm, new Vector3(0.33f, 0.18f, 0.45f), new Vector3(0.14f, 0.08f, 0.06f), blush, false);
            Util.Prim(PrimitiveType.Cube, "HornL", arm, new Vector3(-0.22f, 0.7f, -0.05f), new Vector3(0.1f, 0.22f, 0.1f), spike, false)
                .transform.localRotation = Quaternion.Euler(-20f, 0f, 15f);
            Util.Prim(PrimitiveType.Cube, "HornR", arm, new Vector3(0.22f, 0.7f, -0.05f), new Vector3(0.1f, 0.22f, 0.1f), spike, false)
                .transform.localRotation = Quaternion.Euler(-20f, 0f, -15f);

            mouth = new GameObject("Mouth").transform;
            mouth.SetParent(arm, false);
            mouth.localPosition = new Vector3(0f, 0.08f, 0.8f);
            breathLight = Util.PointLight(mouth, Vector3.forward * 1.5f, new Color(1f, 0.5f, 0.1f), 10f, 0f);
        }

        void Update()
        {
            if (state == State.Dead) return;
            var gm = GameManager.I;
            if (gm == null || gm.player == null) return;
            var p = gm.player;
            float dt = Time.deltaTime;
            st += dt;

            Vector3 toP = p.transform.position - transform.position;
            toP.y = 0f;
            float dist = toP.magnitude;
            Vector3 vel = Vector3.zero;
            float armTarget = Pose(20f, 0f), armSpeed = 8f;
            if (isBoss) breathCd -= dt;

            switch (state)
            {
                case State.Idle:
                    if (isBoss)
                    {
                        FaceDir(spawnRot * Vector3.forward, 3f, dt);
                    }
                    else
                    {
                        if (Flat(wanderTarget - transform.position).sqrMagnitude < 0.5f || st > 5f)
                        {
                            st = 0f;
                            Vector2 r = Random.insideUnitCircle * gm.cellSize * 0.3f;
                            wanderTarget = spawnPos + new Vector3(r.x, 0f, r.y);
                        }
                        Vector3 w = Flat(wanderTarget - transform.position);
                        if (w.sqrMagnitude > 0.25f) { vel = w.normalized * walkSpeed; FaceDir(w, 4f, dt); }
                    }
                    if (CanSee(p, dist)) { state = State.Chase; st = 0f; }
                    break;

                case State.Chase:
                    if (ShouldGiveUp(p, dist, gm)) { state = State.Return; st = 0f; break; }
                    if (dist <= attackRange) { state = State.Windup; st = 0f; comboUsed = false; break; }
                    if (isBoss && breathCd <= 0f && dist > 4.5f && dist < 11f) { state = State.BreathWindup; st = 0f; break; }
                    {
                        Vector3 dir = PathDir(p.transform.position, gm, dt);
                        vel = dir * chaseSpeed;
                        FaceDir(dir, 8f, dt);
                    }
                    break;

                case State.Windup:
                    FaceDir(toP, isBoss ? 3.5f : 5f, dt);
                    armTarget = Pose(-150f, -35f); armSpeed = 6f;
                    if (st >= windup) { state = State.Strike; st = 0f; DoStrike(p, dist, toP); }
                    break;

                case State.Strike:
                    armTarget = Pose(70f, 30f); armSpeed = 30f;
                    if (st < 0.15f) vel = transform.forward * (isBoss ? 5f : 3f);
                    if (st >= 0.3f) { state = State.Recover; st = 0f; }
                    break;

                case State.Recover:
                    armTarget = Pose(50f, 15f); armSpeed = 3f;
                    if (comboPlanned && st > 0.3f && dist <= attackRange + 1.5f && !p.IsDead)
                    {
                        comboPlanned = false; comboUsed = true;
                        state = State.Windup; st = windup * 0.45f;
                        break;
                    }
                    if (st >= recover) { state = p.IsDead ? State.Return : State.Chase; st = 0f; }
                    break;

                case State.BreathWindup:
                    // 仰頭吸氣：眼睛變亮，這是翻滾的提示
                    FaceDir(toP, 4f, dt);
                    armTarget = -45f; armSpeed = 5f;
                    if (st >= 0.8f) { state = State.Breath; st = 0f; breathTick = 0f; emitTimer = 0f; }
                    break;

                case State.Breath:
                {
                    FaceDir(toP, 1.6f, dt);
                    armTarget = 12f; armSpeed = 10f;
                    emitTimer -= dt;
                    while (emitTimer <= 0f)
                    {
                        emitTimer += 0.03f;
                        Vector3 dir = Quaternion.Euler(Random.Range(-6f, 10f), Random.Range(-12f, 12f), 0f) * transform.forward;
                        FireParticle.Spawn(mouth.position, dir * 12f);
                    }
                    breathTick -= dt;
                    if (breathTick <= 0f)
                    {
                        breathTick = 0.2f;
                        if (!p.IsDead && dist < 8f && Vector3.Angle(transform.forward, toP) < 25f)
                            p.TakeDamage(10f, transform.position);
                    }
                    if (st >= 1.5f) { state = State.Recover; st = 0f; breathCd = Random.Range(5f, 8f); }
                    break;
                }

                case State.Return:
                {
                    Vector3 home = Flat(spawnPos - transform.position);
                    if (home.magnitude < 0.8f)
                    {
                        state = State.Idle; st = 0f;
                        if (isBoss) hp = maxHP;
                        break;
                    }
                    Vector3 dir = PathDir(spawnPos, gm, dt);
                    vel = dir * chaseSpeed * 0.8f;
                    FaceDir(dir, 6f, dt);
                    if (CanSee(p, dist) && !ShouldGiveUp(p, dist, gm)) { state = State.Chase; st = 0f; }
                    break;
                }

                case State.Stagger:
                    vel = knock * Mathf.Clamp01(1f - st / 0.4f);
                    armTarget = Pose(40f, 10f);
                    if (st >= (isBoss ? 0.9f : 0.45f)) { state = State.Chase; st = 0f; }
                    break;
            }

            if (arm) arm.localRotation = Quaternion.Slerp(arm.localRotation, Quaternion.Euler(armTarget, 0f, 0f), 1f - Mathf.Exp(-armSpeed * dt));

            if (flash > 0f) flash -= dt;
            Util.SetColor(bodyMat, flash > 0f ? Color.Lerp(bodyColor, Color.white, flash / 0.15f) : bodyColor);
            float eyeBoost = state == State.Windup ? 1f + 2.5f * Mathf.Clamp01(st / windup)
                           : state == State.BreathWindup ? 1f + 3f * Mathf.Clamp01(st / 0.8f) : 1f;
            if (breathLight) breathLight.intensity = Mathf.MoveTowards(breathLight.intensity, state == State.Breath ? 6f : 0f, 30f * dt);
            eyeMat.SetColor("_EmissionColor", eyeColor * eyeBoost);

            if (cc.enabled)
            {
                if (cc.isGrounded && vy < 0f) vy = -2f;
                vy -= 20f * dt;
                vel.y = vy;
                cc.Move(vel * dt);
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        float Pose(float hollowAngle, float dragonAngle) => isBoss ? dragonAngle : hollowAngle;

        void FaceDir(Vector3 d, float speed, float dt)
        {
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-speed * dt));
        }

        bool ShouldGiveUp(PlayerController p, float dist, GameManager gm)
        {
            if (p.IsDead) return true;
            if (isBoss) return !gm.PlayerInHall;
            if (!gm.InMaze(p.transform.position)) return true;
            return dist > sightRange * 1.8f;
        }

        bool CanSee(PlayerController p, float dist)
        {
            var gm = GameManager.I;
            if (p.IsDead) return false;
            if (isBoss) return gm.PlayerInHall;
            if (!gm.InMaze(p.transform.position)) return false;
            if (dist > sightRange) return false;
            if (dist > 3.5f)
            {
                Vector3 to = Flat(p.transform.position - transform.position);
                if (Vector3.Angle(transform.forward, to) > 70f) return false;
            }
            Vector3 eye = transform.position + Vector3.up * 1.6f * scale;
            Vector3 tgt = p.transform.position + Vector3.up * 1.2f;
            Vector3 dir = tgt - eye;
            float len = dir.magnitude;
            if (len < 0.01f) return true;
            if (Physics.Raycast(eye, dir / len, out var hit, len, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PlayerController>() != null;
            return true;
        }

        Vector3 PathDir(Vector3 target, GameManager gm, float dt)
        {
            Vector3 direct = Flat(target - transform.position);
            if (!isBoss && gm.maze.TryWorldToCell(transform.position, out var a) && gm.maze.TryWorldToCell(target, out var b) && a != b)
            {
                if (a != lastFrom || b != lastTo) { repath = 0f; lastFrom = a; lastTo = b; }
                repath -= dt;
                if (repath <= 0f)
                {
                    repath = 0.5f;
                    pathPoint = gm.maze.CellCenter(gm.maze.NextStep(a, b));
                }
                Vector3 d = Flat(pathPoint - transform.position);
                if (d.sqrMagnitude > 0.01f) return d.normalized;
            }
            return direct.sqrMagnitude > 0.0001f ? direct.normalized : Vector3.zero;
        }

        void DoStrike(PlayerController p, float dist, Vector3 toP)
        {
            comboPlanned = isBoss && !comboUsed && Random.value < 0.5f;
            if (p.IsDead) return;
            float reach = attackRange + (isBoss ? 0.6f : 0.4f);
            if (dist <= reach && Vector3.Angle(transform.forward, toP) < (isBoss ? 80f : 60f))
                p.TakeDamage(damage, transform.position);
        }

        public void TakeDamage(float dmg, Vector3 from)
        {
            if (!Alive) return;
            hp -= dmg;
            flash = 0.15f;
            if (hp <= 0f) { hp = 0f; Die(); return; }
            poiseDmg += 1f;
            if (state == State.Idle || state == State.Return) { state = State.Chase; st = 0f; }
            if (poiseDmg >= poise && state != State.Strike)
            {
                poiseDmg = 0f;
                Vector3 k = Flat(transform.position - from);
                knock = (k.sqrMagnitude > 0.001f ? k.normalized : -transform.forward) * (isBoss ? 2f : 4f);
                state = State.Stagger; st = 0f;
            }
        }

        void Die()
        {
            state = State.Dead;
            if (breathLight) breathLight.intensity = 0f;
            GameManager.I.OnEnemyKilled(this);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            cc.enabled = false;
            float t = 0f;
            while (t < 1.4f)
            {
                t += Time.deltaTime;
                visual.localPosition = Vector3.down * (t / 1.4f) * 1.9f * scale;
                visual.localRotation = Quaternion.Euler(Mathf.Min(t * 80f, 70f), 0f, 0f);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        public void ResetToSpawn()
        {
            if (IsDeadForever) return;
            gameObject.SetActive(true);
            StopAllCoroutines();
            cc.enabled = false;
            transform.SetPositionAndRotation(spawnPos, spawnRot);
            cc.enabled = true;
            hp = maxHP;
            state = State.Idle;
            st = 0f; vy = 0f; poiseDmg = 0f; flash = 0f; repath = 0f;
            comboPlanned = false; comboUsed = false;
            breathCd = 4f;
            if (breathLight) breathLight.intensity = 0f;
            if (arm) arm.localRotation = Quaternion.identity;
            wanderTarget = spawnPos;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }
    }
}
