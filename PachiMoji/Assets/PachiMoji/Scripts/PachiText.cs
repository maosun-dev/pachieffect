using System.Collections.Generic;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>パチンコ文字1つ分（立体＋材質＋角のキラッ）をシーンに置く</summary>
    public static class PachiText
    {
        public enum Finish { Gold, Silver, Rainbow }

        static readonly Dictionary<string, TtfFont> fonts = new Dictionary<string, TtfFont>();

        public static TtfFont LoadFont(string name)
        {
            if (fonts.TryGetValue(name, out var f)) return f;
            var ta = Resources.Load<TextAsset>("Fonts/" + name);
            f = new TtfFont(ta.bytes);
            fonts[name] = f;
            return f;
        }

        public static GameObject Create(string[] lines, Finish finish, string fontName = "ReggaeOne", PachiTextBuilder.Style style = null)
        {
            style ??= PachiTextBuilder.DefaultStyle();
            var font = LoadFont(fontName);
            var outline = PachiTextBuilder.LayoutOutline(font, lines, style);
            var mesh = PachiTextBuilder.Build(outline, style);
            mesh.name = string.Join("/", lines);

            var go = new GameObject("PachiText " + mesh.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Materials(finish);

            // 角のキラッ
            var glintShader = Shader.Find("PachiMoji/Glint");
            int gi = 0;
            foreach (var p in PachiTextBuilder.GlintPoints(outline, style, 5))
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "Glint";
                q.transform.SetParent(go.transform, false);
                q.transform.localPosition = p;
                float size = gi == 0 ? 0.9f : 0.55f;
                q.transform.localScale = Vector3.one * size;
                var m = new Material(glintShader);
                m.SetColor("_Color", finish == Finish.Silver ? new Color(0.85f, 0.92f, 1f) : new Color(1f, 0.93f, 0.75f));
                m.SetFloat("_Intensity", gi == 0 ? 7f : 4f);
                m.SetFloat("_Angle", gi * 0.35f);
                q.GetComponent<MeshRenderer>().sharedMaterial = m;
                gi++;
            }
            return go;
        }

        // 側面用：そのままだと横向き・下向きの壁に暗い地面が映って真っ黒になるので、水平線を大きく下げる
        static Material SideMetal(string ramp)
        {
            var m = Metal(ramp, false, 1.5f);
            m.SetFloat("_Horizon", -0.7f);   // 横向き・下向きの面にも空が映る
            m.SetFloat("_Diffuse", 0.5f);    // 面ごとの明暗は光の当たり方で付ける
            return m;
        }

        static Material Metal(string ramp, bool rainbow = false, float boost = 3f)
        {
            var m = new Material(Shader.Find("PachiMoji/Metal"));
            if (ramp != null) m.SetTexture("_Ramp", PachiRamps.Get(ramp));
            m.SetFloat("_Rainbow", rainbow ? 1 : 0);
            m.SetFloat("_Boost", boost);
            return m;
        }

        /// <summary>サブメッシュ順：表の面・細い線・黒・外フチ・側面</summary>
        public static Material[] Materials(Finish f)
        {
            switch (f)
            {
                case Finish.Silver:
                    return new[] { Metal("silver"), Metal("silverLine"), Metal("black", false, 0), Metal("silver"), SideMetal("silverSide") };
                case Finish.Rainbow:
                    return new[] { Metal(null, true, 2), Metal("silverLine"), Metal("black", false, 0), Metal("goldRim"), SideMetal("goldSide") };
                default:
                    return new[] { Metal("gold"), Metal("goldLine"), Metal("black", false, 0), Metal("goldRim"), SideMetal("goldSide") };
            }
        }
    }
}
