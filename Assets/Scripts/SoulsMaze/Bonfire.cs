using UnityEngine;

namespace SoulsMaze
{
    /// <summary>篝火：點燃後可休息（回滿 HP 與原素瓶、設為重生點），但敵人會復活。</summary>
    public class Bonfire : Interactable
    {
        public string bonfireName;
        public bool lit;
        Light fireLight;
        Flicker flicker;
        Transform flame;

        public Vector3 RespawnPoint => transform.position + new Vector3(1.6f, 0.05f, 0f);
        public override string Prompt => lit ? "E　在篝火旁休息" : "E　點燃篝火";

        public static Bonfire Create(Vector3 pos, string name, Transform parent)
        {
            var go = new GameObject("Bonfire_" + name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var b = go.AddComponent<Bonfire>();
            b.bonfireName = name;
            b.Build();
            return b;
        }

        void Build()
        {
            var stone = Util.Mat(new Color(0.2f, 0.19f, 0.18f), 0.1f);
            var ash = Util.Mat(new Color(0.08f, 0.07f, 0.07f), 0f, new Color(0.25f, 0.06f, 0.01f));
            var sword = Util.Mat(new Color(0.3f, 0.28f, 0.26f), 0.6f, new Color(0.15f, 0.05f, 0f));
            var fire = Util.Mat(new Color(1f, 0.5f, 0.1f), 0f, new Color(6f, 2.4f, 0.5f));

            Util.Prim(PrimitiveType.Cylinder, "Ring", transform, new Vector3(0f, 0.08f, 0f), new Vector3(1.4f, 0.08f, 1.4f), stone);
            Util.Prim(PrimitiveType.Sphere, "Ash", transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.3f, 0.9f), ash, false);
            var sw = Util.Prim(PrimitiveType.Cube, "CoiledSword", transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.08f, 1.3f, 0.16f), sword, false);
            sw.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            flame = Util.Prim(PrimitiveType.Sphere, "Flame", transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.6f), fire, false).transform;
            fireLight = Util.PointLight(transform, new Vector3(0f, 1.2f, 0f), new Color(1f, 0.55f, 0.2f), 12f, 1f);
            flicker = fireLight.gameObject.AddComponent<Flicker>();
            SetLit(false);
        }

        void SetLit(bool v)
        {
            lit = v;
            flame.gameObject.SetActive(v);
            flicker.baseIntensity = v ? 3.2f : 0.6f;
        }

        public override void Interact(PlayerController p)
        {
            var gm = GameManager.I;
            if (!lit)
            {
                SetLit(true);
                gm.ShowBanner("篝火點燃了", new Color(1f, 0.75f, 0.35f));
            }
            gm.Rest(this);
        }

        void Update()
        {
            if (!lit || !flame) return;
            float f = 1f + 0.12f * Mathf.Sin(Time.time * 13f) + 0.08f * Mathf.Sin(Time.time * 7.3f);
            flame.localScale = new Vector3(0.6f, 0.9f * f, 0.6f);
        }
    }
}
