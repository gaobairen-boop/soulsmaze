using UnityEngine;

namespace SoulsMaze
{
    /// <summary>死亡時掉落的靈魂。再死一次就永遠消失。</summary>
    public class Bloodstain : Interactable
    {
        public int amount;
        Transform orb;

        public override string Prompt => $"E　取回靈魂（{amount}）";

        public static Bloodstain Create(Vector3 pos, int amount, Transform parent)
        {
            var go = new GameObject("Bloodstain");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var b = go.AddComponent<Bloodstain>();
            b.amount = amount;
            var mat = Util.Mat(new Color(0.3f, 1f, 0.5f), 0f, new Color(0.6f, 3f, 1.2f));
            b.orb = Util.Prim(PrimitiveType.Sphere, "SoulOrb", go.transform, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.35f, mat, false).transform;
            Util.PointLight(go.transform, new Vector3(0f, 0.8f, 0f), new Color(0.4f, 1f, 0.6f), 5f, 1.5f);
            return b;
        }

        public override void Interact(PlayerController p) => GameManager.I.RecoverSouls(this);

        void Update()
        {
            if (orb) orb.localPosition = new Vector3(0f, 0.6f + 0.12f * Mathf.Sin(Time.time * 2.5f), 0f);
        }
    }
}
