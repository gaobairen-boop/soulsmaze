using UnityEngine;

namespace SoulsMaze
{
    /// <summary>地上發光的訊息（提示／陷阱話）。</summary>
    public class GroundMessage : Interactable
    {
        [TextArea] public string text;

        public override string Prompt => "E　閱讀地上的訊息";
        public override float Range => 2f;

        public static GroundMessage Create(Vector3 pos, string text, Transform parent)
        {
            var go = new GameObject("GroundMessage");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var m = go.AddComponent<GroundMessage>();
            m.text = text;
            var mat = Util.Mat(new Color(1f, 0.6f, 0.2f), 0f, new Color(2.5f, 1.2f, 0.3f));
            Util.Prim(PrimitiveType.Cube, "Glyph", go.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.9f, 0.02f, 0.45f), mat, false);
            Util.Prim(PrimitiveType.Cube, "Glyph2", go.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.12f, 0.02f, 0.7f), mat, false);
            return m;
        }

        public override void Interact(PlayerController p) => GameManager.I.ShowMessage(text, 6f);
    }
}
