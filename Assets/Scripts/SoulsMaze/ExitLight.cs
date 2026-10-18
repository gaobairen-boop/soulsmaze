using UnityEngine;

namespace SoulsMaze
{
    /// <summary>擊敗火龍後在大廳中央出現的光柱，觸碰即通關。</summary>
    public class ExitLight : Interactable
    {
        bool active;
        GameObject beam;
        Light l;

        public override string Prompt => "E　觸碰光芒";
        public override bool CanInteract => active;
        public override float Range => 3f;

        public static ExitLight Create(Vector3 pos, Transform parent)
        {
            var go = new GameObject("ExitLight");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var e = go.AddComponent<ExitLight>();
            e.Build();
            return e;
        }

        void Build()
        {
            var glow = Util.Mat(new Color(1f, 0.95f, 0.8f), 0f, new Color(4f, 3.6f, 2.6f));
            beam = Util.Prim(PrimitiveType.Cylinder, "Beam", transform, new Vector3(0f, 6f, 0f), new Vector3(1.1f, 6f, 1.1f), glow, false);
            l = Util.PointLight(transform, new Vector3(0f, 2f, 0f), new Color(1f, 0.92f, 0.75f), 14f, 4f);
            beam.SetActive(false);
            l.enabled = false;
        }

        public void Activate()
        {
            active = true;
            beam.SetActive(true);
            l.enabled = true;
        }

        public override void Interact(PlayerController p) => GameManager.I.Victory();

        void Update()
        {
            if (!active) return;
            float f = 1f + 0.08f * Mathf.Sin(Time.time * 3f);
            beam.transform.localScale = new Vector3(1.1f * f, 6f, 1.1f * f);
        }
    }
}
