using UnityEngine;

namespace SoulsMaze
{
    /// <summary>中央大廳的封印之門：門上三道符文對應三座祭壇，全部點燃後沉入地面。</summary>
    public class SealedGate : Interactable
    {
        Transform door;
        Renderer[] runes;
        Material runeOff, runeOn;
        bool open;
        float openT, height;

        public override string Prompt => "E　調查封印之門";
        public override bool CanInteract => !open;

        public static SealedGate Create(Vector3 pos, Quaternion rot, float width, float height, Transform parent)
        {
            var go = new GameObject("SealedGate");
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, rot);
            var g = go.AddComponent<SealedGate>();
            g.Build(width, height);
            return g;
        }

        void Build(float width, float h)
        {
            height = h;
            var stone = Util.Mat(new Color(0.14f, 0.13f, 0.15f), 0.3f);
            runeOff = Util.Mat(new Color(0.05f, 0.05f, 0.06f), 0.2f, new Color(0.08f, 0.1f, 0.2f));
            runeOn = Util.Mat(new Color(1f, 0.5f, 0.2f), 0.2f, new Color(5f, 2f, 0.4f));

            door = new GameObject("DoorRoot").transform;
            door.SetParent(transform, false);
            Util.Prim(PrimitiveType.Cube, "Slab", door, new Vector3(0f, h / 2f, 0f), new Vector3(0.8f, h, width), stone);
            runes = new Renderer[3];
            for (int i = 0; i < 3; i++)
            {
                runes[i] = Util.Prim(PrimitiveType.Cube, "Rune" + i, door, new Vector3(-0.42f, h * 0.55f, (i - 1) * 1.5f),
                    new Vector3(0.05f, 0.9f, 0.35f), runeOff, false).GetComponent<Renderer>();
            }
            Util.PointLight(transform, new Vector3(-1.4f, 2.5f, 0f), new Color(0.4f, 0.5f, 1f), 6f, 1f);
        }

        public void SetProgress(int n)
        {
            for (int i = 0; i < runes.Length; i++)
                runes[i].sharedMaterial = i < n ? runeOn : runeOff;
        }

        public void Open()
        {
            if (open) return;
            open = true;
            SetProgress(3);
        }

        public override void Interact(PlayerController p) => GameManager.I.InspectGate();

        void Update()
        {
            if (!open || openT >= 1f) return;
            openT += Time.deltaTime / 3f;
            door.localPosition = Vector3.down * (height + 0.1f) * Mathf.SmoothStep(0f, 1f, openT);
            if (openT >= 1f) door.gameObject.SetActive(false);
        }
    }
}
