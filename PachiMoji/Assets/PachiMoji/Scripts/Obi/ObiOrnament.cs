using System.Collections.Generic;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 激アツの帯の飾りの立体（金属の材質で光らせる）。
    /// どれも「表の面が -z を向き、外向きが +y、進む向きが +x」で作る（文字と同じ向き）。
    /// 表の面は小さな面に割って向きを少しずつ変え、PachiMetal の映り込みでギラッと光るようにする
    /// </summary>
    public static class ObiOrnament
    {
        /// <summary>
        /// 縁からはみ出す炎のような飾り。根元の幅 w、高さ h、厚み d。
        /// 真ん中の尾根で左右の面を立て（表は 2 面）、まわりに厚みの側面。サブメッシュ 0＝表、1＝側面
        /// </summary>
        public static Mesh Flame(float w, float h, float d)
        {
            // 輪郭（左→先→右）。先は細く尖らせ、肩で少しふくらませる
            var outline = new[]
            {
                new Vector2(-w * 0.5f, 0), new Vector2(-w * 0.42f, h * 0.32f), new Vector2(-w * 0.22f, h * 0.62f),
                new Vector2(0, h), new Vector2(w * 0.22f, h * 0.62f), new Vector2(w * 0.42f, h * 0.32f), new Vector2(w * 0.5f, 0),
            };
            return Extrude(outline, new Vector2(0, h * 0.18f), d, d * 0.6f, "ObiFlame");
        }

        /// <summary>言葉の区切りの菱形（幅 w・高さ h・厚み d）。真ん中を盛り上げて4面に割る</summary>
        public static Mesh Diamond(float w, float h, float d)
        {
            var outline = new[] { new Vector2(-w / 2, 0), new Vector2(0, h / 2), new Vector2(w / 2, 0), new Vector2(0, -h / 2) };
            return Extrude(outline, Vector2.zero, d, d * 0.9f, "ObiDiamond");
        }

        /// <summary>
        /// 輪郭（閉じた多角形、反時計回りでも時計回りでもよい）を、中心 apex を頂点に盛り上げた表の面と、厚み d の側面にする。
        /// 表の面は輪郭の辺ごとに三角（apex は rise だけ手前＝-z へ）
        /// </summary>
        static Mesh Extrude(Vector2[] outline, Vector2 apex, float d, float rise, string name)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var front = new List<int>();
            var side = new List<int>();
            int m = outline.Length;
            var top = new Vector3(apex.x, apex.y, -rise);
            for (int i = 0; i < m; i++)
            {
                var a = new Vector3(outline[i].x, outline[i].y, 0);
                var b = new Vector3(outline[(i + 1) % m].x, outline[(i + 1) % m].y, 0);
                // 表の三角（apex・a・b）。向きは手前（-z）を向くようにそろえる
                var nn = Vector3.Cross(a - top, b - top).normalized;
                if (nn.z > 0) { (a, b) = (b, a); nn = -nn; }
                int k = v.Count;
                v.Add(top); v.Add(a); v.Add(b);
                n.Add(nn); n.Add(nn); n.Add(nn);
                front.AddRange(new[] { k, k + 1, k + 2 });
            }
            for (int i = 0; i < m; i++)
            {
                // 側面（輪郭の辺を奥へ d だけ伸ばした四角）
                var a = new Vector3(outline[i].x, outline[i].y, 0);
                var b = new Vector3(outline[(i + 1) % m].x, outline[(i + 1) % m].y, 0);
                var a2 = a + new Vector3(0, 0, d);
                var b2 = b + new Vector3(0, 0, d);
                var edge = (b - a).normalized;
                var outN = new Vector3(edge.y, -edge.x, 0);
                var mid = (a + b) / 2 - new Vector3(apex.x, apex.y, 0);
                if (Vector3.Dot(outN, mid) < 0) outN = -outN;
                int k = v.Count;
                v.Add(a); v.Add(b); v.Add(b2); v.Add(a2);
                for (int j = 0; j < 4; j++) n.Add(outN);
                side.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3, k, k + 2, k + 1, k, k + 3, k + 2 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(side, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material Metal(string ramp, float boost, bool side = false)
        {
            var mat = new Material(Shader.Find("PachiMoji/Metal"));
            mat.SetTexture("_Ramp", PachiRamps.Get(ramp));
            mat.SetFloat("_Boost", boost);
            if (side)
            {
                mat.SetFloat("_Horizon", -0.7f);   // 横向きの面にも空を映して、真っ黒にしない（文字の側面と同じ）
                mat.SetFloat("_Diffuse", 0.5f);
            }
            return mat;
        }
    }
}
