using UnityEngine;

namespace SoulsMaze
{
    public static class Util
    {
        // 告訴 Unity 打包時保留這些內建元件（CreatePrimitive 在執行時才會用到它們）
        public static readonly System.Type[] KeepTypes =
        {
            typeof(SphereCollider), typeof(BoxCollider), typeof(CapsuleCollider), typeof(MeshCollider),
            typeof(MeshFilter), typeof(MeshRenderer)
        };

        static Shader _lit;
        static Shader Lit
        {
            get
            {
                if (_lit == null)
                {
                    _lit = Shader.Find("Universal Render Pipeline/Lit");
                    if (_lit == null) _lit = Shader.Find("Standard");
                }
                return _lit;
            }
        }

        // 打包時 Unity 會刪掉「沒被用到」的 Shader，所以用 Resources 裡的兩個材質當範本（由編輯器腳本自動建立）
        static Material _baseTpl, _emitTpl;
        static Font _font;
        static bool _fontTried;

        /// <summary>Resources/NotoSansTC 字型（網頁版顯示中文需要）。沒有就用預設字型。</summary>
        public static Font UIFont
        {
            get
            {
                if (!_fontTried) { _fontTried = true; _font = Resources.Load<Font>("NotoSansTC"); }
                return _font;
            }
        }

        public static Material Mat(Color color, float smoothness = 0.1f, Color? emission = null)
        {
            Material tpl;
            if (emission.HasValue)
            {
                if (!_emitTpl) _emitTpl = Resources.Load<Material>("SoulsMazeLitEmissive");
                tpl = _emitTpl;
            }
            else
            {
                if (!_baseTpl) _baseTpl = Resources.Load<Material>("SoulsMazeLit");
                tpl = _baseTpl;
            }
            var m = tpl ? new Material(tpl) : new Material(Lit);
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = true)
        {
            if (KeepTypes.Length == 0) return null;
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col) Object.DestroyImmediate(col);
            }
            return go;
        }

        public static Light PointLight(Transform parent, Vector3 localPos, Color c, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
