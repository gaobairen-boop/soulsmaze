using System.Collections.Generic;
using UnityEngine;

namespace SoulsMaze
{
    /// <summary>
    /// 餘燼祭壇（謎題）：上方漂浮的藍色靈光數量（1、2、3）就是點燃順序。
    /// 順序錯了，所有祭壇都會熄滅。
    /// </summary>
    public class EmberAltar : Interactable
    {
        public int order;
        public bool lit;
        GameObject flame;
        Light glow;
        Flicker flicker;
        Transform orbRoot;

        public override string Prompt => "E　點燃餘燼祭壇";
        public override bool CanInteract => !lit;

        public static EmberAltar Create(Vector3 pos, int order, Transform parent)
        {
            var go = new GameObject("EmberAltar_" + order);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var a = go.AddComponent<EmberAltar>();
            a.order = order;
            a.Build();
            return a;
        }

        void Build()
        {
            var stone = Util.Mat(new Color(0.26f, 0.25f, 0.24f), 0.1f);
            var rune = Util.Mat(new Color(0.1f, 0.1f, 0.12f), 0.2f, new Color(0.3f, 0.6f, 1.2f));
            var fire = Util.Mat(new Color(1f, 0.5f, 0.1f), 0f, new Color(6f, 2.4f, 0.5f));
            var orbMat = Util.Mat(new Color(0.6f, 0.8f, 1f), 0f, new Color(1.5f, 2.5f, 4f));

            Util.Prim(PrimitiveType.Cube, "Base", transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1f, 1.2f), stone);
            Util.Prim(PrimitiveType.Cylinder, "Bowl", transform, new Vector3(0f, 1.08f, 0f), new Vector3(0.9f, 0.08f, 0.9f), stone, false);
            for (int i = 0; i < 4; i++)
            {
                var r = new GameObject("RuneSide").transform;
                r.SetParent(transform, false);
                r.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                Util.Prim(PrimitiveType.Cube, "Rune", r, new Vector3(0f, 0.55f, 0.61f), new Vector3(0.5f, 0.08f, 0.02f), rune, false);
            }

            flame = Util.Prim(PrimitiveType.Sphere, "Ember", transform, new Vector3(0f, 1.4f, 0f), new Vector3(0.5f, 0.7f, 0.5f), fire, false);
            flame.SetActive(false);

            orbRoot = new GameObject("Orbs").transform;
            orbRoot.SetParent(transform, false);
            orbRoot.localPosition = new Vector3(0f, 2.3f, 0f);
            float radius = order > 1 ? 0.45f : 0f;
            for (int i = 0; i < order; i++)
            {
                float ang = i * Mathf.PI * 2f / order;
                Util.Prim(PrimitiveType.Sphere, "Orb", orbRoot, new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius), Vector3.one * 0.2f, orbMat, false);
            }

            glow = Util.PointLight(transform, new Vector3(0f, 2.2f, 0f), new Color(0.5f, 0.7f, 1f), 7f, 1.2f);
            flicker = glow.gameObject.AddComponent<Flicker>();
        }

        public void SetLit(bool v)
        {
            lit = v;
            flame.SetActive(v);
            glow.color = v ? new Color(1f, 0.55f, 0.2f) : new Color(0.5f, 0.7f, 1f);
            flicker.baseIntensity = v ? 3f : 1.2f;
        }

        public override void Interact(PlayerController p) => GameManager.I.TryLightAltar(this);

        void Update()
        {
            orbRoot.localRotation = Quaternion.Euler(0f, Time.time * 40f, 0f);
            orbRoot.localPosition = new Vector3(0f, 2.3f + 0.1f * Mathf.Sin(Time.time * 2f), 0f);
        }
    }
}
