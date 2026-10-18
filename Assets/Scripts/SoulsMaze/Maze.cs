using System.Collections.Generic;
using UnityEngine;

namespace SoulsMaze
{
    /// <summary>迷宮資料：用遞迴回溯法生成，再打通幾條捷徑。也負責敵人尋路（BFS）。</summary>
    public class Maze
    {
        public readonly int W, H;
        public readonly float Cell;
        public readonly bool[,] openE; // (x,z) 與 (x+1,z) 之間是否相通
        public readonly bool[,] openN; // (x,z) 與 (x,z+1) 之間是否相通
        public readonly bool[,] blocked; // 中央 Boss 大廳佔用的格子（不屬於迷宮走道）
        public readonly RectInt hall;

        public static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        public Maze(int w, int h, float cell, int seed, RectInt hallCells)
        {
            W = w; H = h; Cell = cell; hall = hallCells;
            openE = new bool[w, h];
            openN = new bool[w, h];
            blocked = new bool[w, h];
            for (int x = hall.xMin; x < hall.xMax; x++)
                for (int z = hall.yMin; z < hall.yMax; z++)
                    if (In(new Vector2Int(x, z))) blocked[x, z] = true;
            var rng = new System.Random(seed);
            var visited = new bool[w, h];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(0, 0));
            visited[0, 0] = true;
            var options = new List<Vector2Int>(4);

            while (stack.Count > 0)
            {
                var c = stack.Peek();
                options.Clear();
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    if (Walkable(n) && !visited[n.x, n.y]) options.Add(d);
                }
                if (options.Count == 0) { stack.Pop(); continue; }
                var next = c + options[rng.Next(options.Count)];
                SetOpen(c, next, true);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }

            // 打通少量額外通道，讓迷宮有迴圈、不會只有唯一解
            int loops = (w * h) / 12;
            for (int i = 0; i < loops; i++)
            {
                var c = new Vector2Int(rng.Next(w), rng.Next(h));
                var n = c + Dirs[rng.Next(4)];
                if (Walkable(c) && Walkable(n)) SetOpen(c, n, true);
            }
        }

        public bool In(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < W && c.y < H;
        public bool Walkable(Vector2Int c) => In(c) && !blocked[c.x, c.y];

        public void SetOpen(Vector2Int a, Vector2Int b, bool v)
        {
            if (b.x == a.x + 1) openE[a.x, a.y] = v;
            else if (b.x == a.x - 1) openE[b.x, b.y] = v;
            else if (b.y == a.y + 1) openN[a.x, a.y] = v;
            else if (b.y == a.y - 1) openN[b.x, b.y] = v;
        }

        public bool IsOpen(Vector2Int a, Vector2Int b)
        {
            if (!In(a) || !In(b)) return false;
            if (b.x == a.x + 1 && b.y == a.y) return openE[a.x, a.y];
            if (b.x == a.x - 1 && b.y == a.y) return openE[b.x, b.y];
            if (b.y == a.y + 1 && b.x == a.x) return openN[a.x, a.y];
            if (b.y == a.y - 1 && b.x == a.x) return openN[b.x, b.y];
            return false;
        }

        public IEnumerable<Vector2Int> Neighbors(Vector2Int c)
        {
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (IsOpen(c, n)) yield return n;
            }
        }

        public int OpenCount(Vector2Int c)
        {
            int k = 0;
            foreach (var _ in Neighbors(c)) k++;
            return k;
        }

        public Vector3 CellCenter(Vector2Int c) => new Vector3((c.x + 0.5f) * Cell, 0f, (c.y + 0.5f) * Cell);

        public bool TryWorldToCell(Vector3 p, out Vector2Int c)
        {
            c = new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
            return Walkable(c);
        }

        public int[,] DistanceField(Vector2Int from)
        {
            var dist = new int[W, H];
            for (int x = 0; x < W; x++)
                for (int z = 0; z < H; z++)
                    dist[x, z] = -1;
            var q = new Queue<Vector2Int>();
            q.Enqueue(from);
            dist[from.x, from.y] = 0;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var n in Neighbors(c))
                {
                    if (dist[n.x, n.y] >= 0) continue;
                    dist[n.x, n.y] = dist[c.x, c.y] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>從 from 走向 to 時，下一步該進入哪一格。</summary>
        public Vector2Int NextStep(Vector2Int from, Vector2Int to)
        {
            if (from == to) return to;
            var dist = DistanceField(to);
            var best = from;
            int bestD = int.MaxValue;
            foreach (var n in Neighbors(from))
            {
                int d = dist[n.x, n.y];
                if (d >= 0 && d < bestD) { bestD = d; best = n; }
            }
            return best;
        }
    }
}
