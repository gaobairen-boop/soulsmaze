using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulsMaze
{
    /// <summary>
    /// 遊戲總管：生成迷宮（中央是火龍大廳）、擺放篝火／祭壇／敵人／地上訊息，
    /// 處理死亡與重生、祭壇順序謎題、Boss 戰與勝利，並畫出 HUD。
    /// 流程：從角落出發 → 找到三座餘燼祭壇並依「靈光數量」1→2→3 的順序點燃
    ///      → 中央封印之門開啟 → 擊敗 Q 版火龍 → 觸碰大廳中央的光芒通關。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        [Header("迷宮設定")]
        public int mazeSize = 11;          // 奇數比較好，中央 3x3 會變成 Boss 大廳
        public float cellSize = 6f;
        public float wallHeight = 4.5f;
        public int seed = 1337;
        public int extraEnemies = 7;

        [HideInInspector] public Maze maze;
        [HideInInspector] public PlayerController player;
        [HideInInspector] public Enemy boss;
        [HideInInspector] public ThirdPersonCamera cam;
        public readonly List<Interactable> interactables = new List<Interactable>();
        public readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<EmberAltar> altars = new List<EmberAltar>();

        Transform world;
        SealedGate gate;
        ExitLight exitLight;
        Bonfire lastBonfire;
        Bloodstain bloodstain;
        MobileControls touch;
        Rect hallRect;
        Vector2Int gateFront;
        Material torchWood, torchFlame;
        int nextAltar = 1;
        [HideInInspector] public int souls;

        float deathTimer;
        bool victory;
        float helpTimer = 14f;
        bool helpToggle;
        bool hallAnnounced;
        float victoryTime;

        string message; float messageTime;
        string banner; Color bannerColor; float bannerTime, bannerDur;

        public bool InputBlocked => deathTimer > 0f || victory;
        public bool IsVictory => victory;
        public int AltarsLit => nextAltar - 1;
        bool TouchMode => touch && touch.Active;

        void Awake()
        {
            I = this;
            touch = gameObject.AddComponent<MobileControls>();
        }

        void Start()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) Application.targetFrameRate = 60;
            SetupAtmosphere();
            int half = mazeSize / 2;
            maze = new Maze(mazeSize, mazeSize, cellSize, seed, new RectInt(half - 1, half - 1, 3, 3));
            world = new GameObject("SoulsMaze_World").transform;
            gateFront = new Vector2Int(half, half - 2);
            BuildMaze();
            BuildHall();
            Populate();
        }

        // ================================================================ 建造世界

        void SetupAtmosphere()
        {
            Color fog = new Color(0.015f, 0.015f, 0.022f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = fog;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.15f);
            RenderSettings.skybox = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    l.intensity = 0.15f;
                    l.color = new Color(0.55f, 0.62f, 0.85f);
                }
            }
            var c = Camera.main;
            if (c == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                c = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = fog;
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 90f;
            cam = c.gameObject.AddComponent<ThirdPersonCamera>();
        }

        void BuildMaze()
        {
            float cs = cellSize, wh = wallHeight, t = 0.6f;
            var rng = new System.Random(seed + 1);
            var floorMat = Util.Mat(new Color(0.13f, 0.12f, 0.11f), 0.15f);
            var floorMat2 = Util.Mat(new Color(0.10f, 0.11f, 0.09f), 0.1f);
            var wallMats = new[]
            {
                Util.Mat(new Color(0.24f, 0.23f, 0.21f), 0.05f),
                Util.Mat(new Color(0.20f, 0.20f, 0.19f), 0.05f),
                Util.Mat(new Color(0.18f, 0.21f, 0.17f), 0.05f)
            };
            torchWood = Util.Mat(new Color(0.2f, 0.12f, 0.06f), 0.1f);
            torchFlame = Util.Mat(new Color(1f, 0.6f, 0.2f), 0f, new Color(4f, 1.8f, 0.4f));
            Material WallMat() => wallMats[rng.Next(wallMats.Length)];

            var parent = new GameObject("Maze").transform;
            parent.SetParent(world);

            for (int x = 0; x < maze.W; x++)
            for (int z = 0; z < maze.H; z++)
            {
                var cell = new Vector2Int(x, z);
                Vector3 c = maze.CellCenter(cell);
                bool isHall = maze.blocked[x, z];
                Util.Prim(PrimitiveType.Cube, "Floor", parent, c + Vector3.down * 0.25f, new Vector3(cs, 0.5f, cs),
                    isHall ? floorMat2 : (rng.NextDouble() < 0.8 ? floorMat : floorMat2));

                Vector3 northPos = new Vector3((x + 0.5f) * cs, wh / 2f, (z + 1) * cs);
                Vector3 eastPos = new Vector3((x + 1) * cs, wh / 2f, (z + 0.5f) * cs);
                Vector3 hScale = new Vector3(cs + t, wh, t), vScale = new Vector3(t, wh, cs + t);

                if (isHall)
                {
                    // 大廳本身沒有內牆，只在與迷宮相鄰的北／東邊補牆（南／西邊由迷宮那側的格子負責）
                    var n = new Vector2Int(x, z + 1);
                    var e = new Vector2Int(x + 1, z);
                    if (!maze.In(n) || !maze.blocked[n.x, n.y]) Util.Prim(PrimitiveType.Cube, "HallWall", parent, northPos, hScale, WallMat());
                    if (!maze.In(e) || !maze.blocked[e.x, e.y]) Util.Prim(PrimitiveType.Cube, "HallWall", parent, eastPos, vScale, WallMat());
                    continue;
                }

                bool gateGap = cell == gateFront;
                if (!maze.openN[x, z] && !gateGap) Util.Prim(PrimitiveType.Cube, "Wall", parent, northPos, hScale, WallMat());
                if (!maze.openE[x, z]) Util.Prim(PrimitiveType.Cube, "Wall", parent, eastPos, vScale, WallMat());
                if (z == 0) Util.Prim(PrimitiveType.Cube, "Wall", parent, new Vector3((x + 0.5f) * cs, wh / 2f, 0f), hScale, WallMat());
                if (x == 0) Util.Prim(PrimitiveType.Cube, "Wall", parent, new Vector3(0f, wh / 2f, (z + 0.5f) * cs), vScale, WallMat());

                bool forceTorch = (x == 0 && z == 0) || gateGap;
                if (forceTorch || rng.NextDouble() < 0.25) AddTorch(cell, parent, rng);
            }
        }

        void AddTorch(Vector2Int c, Transform parent, System.Random rng)
        {
            var sides = new List<Vector3>();
            foreach (var d in Maze.Dirs)
            {
                var n = c + d;
                bool closed = !maze.IsOpen(c, n);
                if (c == gateFront && d.y == 1) closed = false; // 門那一側不放火把
                if (closed) sides.Add(new Vector3(d.x, 0f, d.y));
            }
            if (sides.Count == 0) return;
            var dir = sides[rng.Next(sides.Count)];
            var torch = new GameObject("Torch").transform;
            torch.SetParent(parent);
            torch.position = maze.CellCenter(c) + dir * (cellSize / 2f - 0.45f) + Vector3.up * 2.6f;
            Util.Prim(PrimitiveType.Cube, "Bracket", torch, Vector3.zero, new Vector3(0.12f, 0.5f, 0.12f), torchWood, false);
            Util.Prim(PrimitiveType.Sphere, "Flame", torch, new Vector3(0f, 0.35f, 0f), Vector3.one * 0.22f, torchFlame, false);
            var l = Util.PointLight(torch, new Vector3(0f, 0.5f, 0f) - dir * 0.3f, new Color(1f, 0.55f, 0.2f), 10f, 2.2f);
            l.gameObject.AddComponent<Flicker>();
        }

        void BuildHall()
        {
            float cs = cellSize, A = cs * 3f;
            float X0 = maze.hall.xMin * cs, Z0 = maze.hall.yMin * cs;
            Vector3 center = new Vector3(X0 + A / 2f, 0f, Z0 + A / 2f);
            hallRect = new Rect(X0, Z0, A, A);
            var parent = new GameObject("DragonHall").transform;
            parent.SetParent(world);

            var stone = Util.Mat(new Color(0.2f, 0.19f, 0.19f), 0.1f);
            var ember = Util.Mat(new Color(0.3f, 0.1f, 0.05f), 0f, new Color(3f, 0.9f, 0.2f));

            // 四角高塔：從迷宮裡抬頭就能看到，當作「往中心走」的地標
            foreach (var fx in new[] { 0f, 1f })
            foreach (var fz in new[] { 0f, 1f })
            {
                Vector3 corner = new Vector3(X0 + A * fx, 0f, Z0 + A * fz);
                Util.Prim(PrimitiveType.Cube, "Tower", parent, corner + Vector3.up * 5f, new Vector3(1.4f, 10f, 1.4f), stone);
                Util.Prim(PrimitiveType.Cube, "TowerFire", parent, corner + Vector3.up * 10.4f, new Vector3(0.9f, 0.8f, 0.9f), ember, false);
                Util.PointLight(parent, corner + Vector3.up * 11f, new Color(1f, 0.4f, 0.15f), 9f, 3f).gameObject.AddComponent<Flicker>();
            }

            // 大廳內的柱子與火盆
            foreach (var fx in new[] { 0.22f, 0.78f })
            foreach (var fz in new[] { 0.22f, 0.78f })
            {
                Vector3 pp = new Vector3(X0 + A * fx, 0f, Z0 + A * fz);
                Util.Prim(PrimitiveType.Cylinder, "Pillar", parent, pp + Vector3.up * wallHeight / 2f, new Vector3(1.2f, wallHeight / 2f, 1.2f), stone);
                Vector3 toC = (center - pp).normalized;
                Util.Prim(PrimitiveType.Sphere, "Brazier", parent, pp + toC * 0.9f + Vector3.up * 3f, Vector3.one * 0.35f, torchFlame, false);
                Util.PointLight(parent, pp + toC * 1.3f + Vector3.up * 3.2f, new Color(1f, 0.5f, 0.2f), 12f, 2.5f).gameObject.AddComponent<Flicker>();
            }

            // 封印之門（大廳南側中央）
            Vector3 gatePos = new Vector3((gateFront.x + 0.5f) * cs, 0f, Z0);
            gate = SealedGate.Create(gatePos, Quaternion.Euler(0f, -90f, 0f), cs, wallHeight, parent);

            // Q 版火龍與通關之光
            boss = Enemy.Create(center + new Vector3(0f, 0.05f, 3f), Quaternion.LookRotation(Vector3.back), true, parent);
            exitLight = ExitLight.Create(center, parent);
        }

        void Populate()
        {
            var rng = new System.Random(seed + 2);
            var start = new Vector2Int(0, 0);
            var dist = maze.DistanceField(start);
            var used = new HashSet<Vector2Int> { start, gateFront };
            var parent = new GameObject("Things").transform;
            parent.SetParent(world);

            var b1 = Bonfire.Create(maze.CellCenter(start), "迷宮入口", parent);
            Bonfire.Create(maze.CellCenter(gateFront), "封印之門前", parent);
            lastBonfire = b1;

            // ---- 三座餘燼祭壇：放在離起點最遠、彼此分散的死路 ----
            var dead = new List<Vector2Int>();
            var all = new List<Vector2Int>();
            for (int x = 0; x < maze.W; x++)
            for (int z = 0; z < maze.H; z++)
            {
                var c = new Vector2Int(x, z);
                if (used.Contains(c) || dist[x, z] < 0) continue;
                all.Add(c);
                if (maze.OpenCount(c) == 1) dead.Add(c);
            }
            dead.Sort((a, b) => dist[b.x, b.y].CompareTo(dist[a.x, a.y]));
            all.Sort((a, b) => dist[b.x, b.y].CompareTo(dist[a.x, a.y]));
            var picks = new List<Vector2Int>();
            int minSep = Mathf.Max(3, mazeSize / 2);
            foreach (var c in dead)
            {
                if (picks.Count >= 3) break;
                bool ok = true;
                foreach (var p in picks)
                    if (Mathf.Abs(p.x - c.x) + Mathf.Abs(p.y - c.y) < minSep) ok = false;
                if (ok) picks.Add(c);
            }
            foreach (var c in dead) { if (picks.Count >= 3) break; if (!picks.Contains(c)) picks.Add(c); }
            foreach (var c in all) { if (picks.Count >= 3) break; if (!picks.Contains(c)) picks.Add(c); }

            var orders = new List<int> { 1, 2, 3 };
            for (int i = orders.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (orders[i], orders[j]) = (orders[j], orders[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                var c = picks[i];
                used.Add(c);
                altars.Add(EmberAltar.Create(maze.CellCenter(c), orders[i], parent));
                SpawnEnemy(maze.CellCenter(c) + new Vector3(1.6f, 0f, 1.6f), parent, rng);
            }

            // ---- 其他活屍 ----
            var candidates = new List<Vector2Int>();
            foreach (var c in all)
                if (!used.Contains(c) && dist[c.x, c.y] >= 3) candidates.Add(c);
            for (int i = 0; i < extraEnemies && candidates.Count > 0; i++)
            {
                int k = rng.Next(candidates.Count);
                var c = candidates[k];
                candidates.RemoveAt(k);
                used.Add(c);
                SpawnEnemy(maze.CellCenter(c), parent, rng);
            }

            // ---- 地上的訊息 ----
            Vector3 msgOffset = new Vector3(-1.7f, 0f, -1.7f);
            GroundMessage.Create(maze.CellCenter(start) + msgOffset,
                "歡迎，不死人。\n迷宮的中心沉睡著一頭火龍……\n抬頭看，那四座燃燒的高塔就是中心。", parent);
            GroundMessage.Create(maze.CellCenter(gateFront) + msgOffset,
                "門後傳來低沉的龍吼。\n牠仰頭吸氣時……快翻滾！", parent);
            var hints = new[]
            {
                "三座祭壇……\n靈光之數，即是順序。",
                "前方有活屍。\n翻滾的瞬間，刀刃無法傷你。",
                "點錯了順序？\n餘燼將盡數熄滅……",
                "迷失了嗎？\n火把未必指引出口。"
            };
            var spots = new List<Vector2Int>();
            foreach (var c in all)
                if (!used.Contains(c) && dist[c.x, c.y] >= 2) spots.Add(c);
            foreach (var h in hints)
            {
                if (spots.Count == 0) break;
                int k = rng.Next(spots.Count);
                GroundMessage.Create(maze.CellCenter(spots[k]) + msgOffset, h, parent);
                spots.RemoveAt(k);
            }

            // ---- 玩家 ----
            Quaternion face = StartFacing(start);
            player = PlayerController.Create(b1.RespawnPoint);
            player.transform.rotation = face;
            player.cam = cam;
            cam.target = player.transform;
            cam.player = player;
            cam.yaw = face.eulerAngles.y;
        }

        Quaternion StartFacing(Vector2Int cell)
        {
            foreach (var n in maze.Neighbors(cell))
            {
                Vector3 d = maze.CellCenter(n) - maze.CellCenter(cell);
                return Quaternion.LookRotation(d);
            }
            return Quaternion.identity;
        }

        void SpawnEnemy(Vector3 pos, Transform parent, System.Random rng)
        {
            enemies.Add(Enemy.Create(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, rng.Next(360), 0f), false, parent));
        }

        // ================================================================ 查詢

        public bool PlayerInHall => player && IsInHall(player.transform.position);
        public bool IsInHall(Vector3 p) => hallRect.Contains(new Vector2(p.x, p.z)) && p.z > hallRect.yMin + 0.5f;
        public bool InMaze(Vector3 p) => maze != null && maze.TryWorldToCell(p, out _);

        public IEnumerable<Enemy> AllEnemies()
        {
            foreach (var e in enemies) yield return e;
            if (boss) yield return boss;
        }

        // ================================================================ 遊戲事件

        void Update()
        {
            float dt = Time.deltaTime;
            if (messageTime > 0f) messageTime -= dt;
            if (bannerTime > 0f) bannerTime -= dt;
            if (helpTimer > 0f) helpTimer -= dt;

            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) { helpToggle = !helpToggle; helpTimer = 0f; }

            if (deathTimer > 0f)
            {
                deathTimer -= dt;
                if (deathTimer <= 0f) RespawnPlayer();
            }

            if (!hallAnnounced && boss && boss.hp > 0f && PlayerInHall && !player.IsDead)
            {
                hallAnnounced = true;
                ShowBanner(boss.displayName, new Color(1f, 0.55f, 0.25f), 3f);
            }

            if (victory) victoryTime += dt;
            if (victory && ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                            || (TouchMode && touch.AnyTap && victoryTime > 1.5f)))
                Restart();
        }

        public void ShowMessage(string text, float duration)
        {
            message = text;
            messageTime = duration;
        }

        public void ShowBanner(string text, Color color, float duration = 3.5f)
        {
            banner = text;
            bannerColor = color;
            bannerTime = bannerDur = duration;
        }

        public void Rest(Bonfire b)
        {
            lastBonfire = b;
            player.RestoreFull();
            ResetEnemies();
            ShowMessage("你在篝火旁歇息。HP 與原素瓶已恢復……\n但死去的活屍也重新站了起來。", 4f);
        }

        void ResetEnemies()
        {
            foreach (var e in enemies) e.ResetToSpawn();
            if (boss) boss.ResetToSpawn();
            hallAnnounced = false;
        }

        public void OnPlayerDied(Vector3 pos)
        {
            if (bloodstain) { Destroy(bloodstain.gameObject); bloodstain = null; }
            if (souls > 0)
            {
                pos.y = Mathf.Max(pos.y, 0f);
                bloodstain = Bloodstain.Create(pos, souls, world);
                souls = 0;
            }
            deathTimer = 4.5f;
        }

        void RespawnPlayer()
        {
            player.Respawn(lastBonfire.RespawnPoint, player.transform.rotation);
            ResetEnemies();
        }

        public void RecoverSouls(Bloodstain b)
        {
            souls += b.amount;
            ShowMessage($"取回了 {b.amount} 個靈魂。", 3f);
            if (bloodstain == b) bloodstain = null;
            Destroy(b.gameObject);
        }

        public void OnEnemyKilled(Enemy e)
        {
            souls += e.soulReward;
            if (player && player.lockTarget == e) player.lockTarget = null;
            if (e == boss)
            {
                ShowBanner("強敵已被擊敗", new Color(1f, 0.82f, 0.4f), 4.5f);
                ShowMessage("大廳中央浮現出一道光芒……", 5f);
                exitLight.Activate();
            }
        }

        public void TryLightAltar(EmberAltar a)
        {
            if (a.order == nextAltar)
            {
                a.SetLit(true);
                nextAltar++;
                gate.SetProgress(AltarsLit);
                if (AltarsLit >= 3)
                {
                    gate.Open();
                    ShowBanner("封印解除了", new Color(1f, 0.7f, 0.35f));
                    ShowMessage("迷宮中心傳來石門沉落的聲音……\n還有一聲龍吼。", 5f);
                }
                else
                {
                    ShowMessage($"餘燼甦醒了……（{AltarsLit} / 3）", 3.5f);
                }
            }
            else
            {
                foreach (var al in altars) al.SetLit(false);
                nextAltar = 1;
                gate.SetProgress(0);
                player.TakeDamage(20f, a.transform.position);
                ShowMessage("順序錯誤……餘燼盡數熄滅，詛咒灼燒著你。", 4f);
            }
        }

        public void InspectGate()
        {
            ShowMessage(AltarsLit == 0
                ? "古老的封印之門，刻著三道黯淡的符文。\n迷宮深處的某些東西似乎與它相呼應……"
                : $"封印之門上，{AltarsLit} 道符文正在燃燒。", 5f);
        }

        public void Victory()
        {
            if (victory) return;
            victory = true;
            ShowBanner("迷宮已被征服", new Color(1f, 0.85f, 0.45f), 1f);
            ThirdPersonCamera.LockCursor(false);
        }

        void Restart()
        {
            if (world) Destroy(world.gameObject);
            if (player) Destroy(player.gameObject);
            if (cam) Destroy(cam);
            var go = new GameObject("SoulsMaze_Game");
            var gm = go.AddComponent<GameManager>();
            gm.mazeSize = mazeSize;
            gm.cellSize = cellSize;
            gm.wallHeight = wallHeight;
            gm.seed = seed;
            gm.extraEnemies = extraEnemies;
            Destroy(gameObject);
        }

        // ================================================================ HUD

        GUIStyle sLabel, sRight, sSmall, sCenter, sPrompt, sBanner, sBig;
        float styleScale = -1f;

        void Styles(float s)
        {
            if (sLabel != null && Mathf.Approximately(s, styleScale)) return;
            styleScale = s;
            sLabel = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s), alignment = TextAnchor.MiddleLeft };
            sLabel.normal.textColor = Color.white;
            sRight = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleRight };
            sSmall = new GUIStyle(sLabel) { fontSize = Mathf.RoundToInt(22 * s), alignment = TextAnchor.UpperLeft, wordWrap = true };
            sCenter = new GUIStyle(sLabel) { fontSize = Mathf.RoundToInt(28 * s), alignment = TextAnchor.MiddleCenter, wordWrap = true };
            sPrompt = new GUIStyle(sCenter) { fontSize = Mathf.RoundToInt(26 * s) };
            sBanner = new GUIStyle(sCenter) { fontSize = Mathf.RoundToInt(64 * s), fontStyle = FontStyle.Bold };
            sBig = new GUIStyle(sCenter) { fontSize = Mathf.RoundToInt(100 * s), fontStyle = FontStyle.Bold };
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Bar(Rect r, float v, Color c)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * Mathf.Clamp01(v), r.height - 4f), c);
        }

        static void Shadow(Rect r, string text, GUIStyle st, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, c.a * 0.8f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
            GUI.color = old;
        }

        string HelpText => TouchMode
            ? "操作說明\n左半邊：拖曳移動（推到底衝刺）\n右半邊：拖曳轉視角\n右下按鈕：攻擊／翻滾／原素瓶／互動／鎖定\n\n目標：點燃三座祭壇，打開中央的門"
            : "操作說明（H 開關）\nWASD　移動\n滑鼠　轉動視角\n左鍵 / J　攻擊（可連段）\nSpace　翻滾（無敵）\nShift　衝刺\nR　喝原素瓶\nE　互動\nTab / 中鍵　鎖定敵人\nEsc　釋放滑鼠\n\n目標：點燃三座祭壇，打開中央的門";

        void OnGUI()
        {
            if (!player) return;
            if (Util.UIFont) GUI.skin.font = Util.UIFont;
            float s = Screen.height / 1080f;
            Styles(s);
            float sw = Screen.width, sh = Screen.height;

            // HP / 耐力
            Bar(new Rect(40f * s, 40f * s, 460f * s, 22f * s), player.hp / player.maxHP, new Color(0.62f, 0.07f, 0.07f));
            Bar(new Rect(40f * s, 68f * s, 360f * s, 14f * s), player.stamina / player.maxStamina, new Color(0.22f, 0.58f, 0.25f));

            float bottomY = TouchMode ? 96f * s : sh - 90f * s;
            Shadow(new Rect(40f * s, bottomY, 400f * s, 40f * s), $"原素瓶  ×{player.estus}", sLabel, new Color(1f, 0.75f, 0.35f));
            Shadow(new Rect(sw - 440f * s, TouchMode ? 80f * s : sh - 90f * s, 400f * s, 40f * s), $"靈魂  {souls}", sRight, Color.white);
            Shadow(new Rect(sw - 440f * s, 36f * s, 400f * s, 34f * s), $"餘燼祭壇  {AltarsLit} / 3", sRight, new Color(1f, 0.7f, 0.4f));

            // 鎖定標記
            if (player.lockTarget && cam)
            {
                var sp = cam.Cam.WorldToScreenPoint(player.lockTarget.LockPoint);
                if (sp.z > 0f)
                {
                    float d = 14f * s;
                    Fill(new Rect(sp.x - d / 2f, sh - sp.y - d / 2f, d, d), new Color(1f, 1f, 1f, 0.9f));
                }
            }

            // 互動提示
            if (!InputBlocked && player.focus && !string.IsNullOrEmpty(player.focus.Prompt))
            {
                string p = player.focus.Prompt;
                if (TouchMode) p = p.Replace("E　", "點【互動】：");
                var r = new Rect(sw / 2f - 320f * s, sh * 0.70f, 640f * s, 52f * s);
                Fill(r, new Color(0f, 0f, 0f, 0.55f));
                Shadow(r, p, sPrompt, Color.white);
            }

            // Boss 血條
            if (boss && boss.hp > 0f && PlayerInHall && !player.IsDead)
            {
                float bw = Mathf.Min(900f * s, sw * 0.6f);
                float by = sh * 0.80f;
                Shadow(new Rect(sw / 2f - bw / 2f, by - 36f * s, bw, 32f * s), boss.displayName, sLabel, Color.white);
                Bar(new Rect(sw / 2f - bw / 2f, by, bw, 18f * s), boss.hp / boss.maxHP, new Color(0.75f, 0.2f, 0.05f));
            }

            // 訊息
            if (messageTime > 0f && !string.IsNullOrEmpty(message))
            {
                float a = Mathf.Clamp01(messageTime / 0.6f);
                var r = new Rect(sw / 2f - 520f * s, sh * 0.52f, 1040f * s, 130f * s);
                Fill(r, new Color(0f, 0f, 0f, 0.5f * a));
                Shadow(r, message, sCenter, new Color(1f, 1f, 1f, a));
            }

            // 大字橫幅
            if (bannerTime > 0f || victory)
            {
                float a = victory ? 1f : Mathf.Clamp01(bannerTime / 0.8f) * Mathf.Clamp01((bannerDur - bannerTime) / 0.5f);
                var r = new Rect(0f, sh * 0.32f, sw, 140f * s);
                Fill(r, new Color(0f, 0f, 0f, 0.6f * a));
                Shadow(r, banner, sBanner, new Color(bannerColor.r, bannerColor.g, bannerColor.b, a));
            }
            if (victory)
                Shadow(new Rect(0f, sh * 0.32f + 150f * s, sw, 44f * s), TouchMode ? "點擊螢幕重新開始" : "按 Enter 重新開始", sCenter, Color.white);

            // 你死了
            if (deathTimer > 0f)
            {
                float t = 4.5f - deathTimer;
                float a = Mathf.Clamp01(t / 1.2f);
                Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, Mathf.Clamp01((t - 2.8f) / 1.5f)));
                var r = new Rect(0f, sh * 0.4f, sw, 170f * s);
                Fill(r, new Color(0f, 0f, 0f, 0.65f * a));
                Shadow(r, "你死了", sBig, new Color(0.72f, 0.06f, 0.05f, a));
            }

            // 操作說明
            if (helpToggle || helpTimer > 0f)
            {
                var r = new Rect(40f * s, 150f * s, 470f * s, (TouchMode ? 250f : 420f) * s);
                Fill(r, new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(new Rect(r.x + 16f * s, r.y + 12f * s, r.width - 32f * s, r.height - 24f * s), HelpText, sSmall);
            }
        }
    }
}
