using System.Collections.Generic;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 激アツの帯の本体（帯の地と、上下の縁の金の山）。点の並びに沿って細長い板を張る。
    /// 板の面はカメラの方を向き、進む向きに直角（文字と同じ向き）
    /// </summary>
    public static class ObiBand
    {
        /// <summary>帯の面の向き：fwd は文字の奥向き、up は帯の幅の向き</summary>
        public static void Frame(Vector3 p, Vector3 tan, Vector3 camPos, out Vector3 fwd, out Vector3 up)
        {
            var toCam = (camPos - p).normalized;
            var n = (toCam - Vector3.Dot(toCam, tan) * tan).normalized;
            fwd = -n;
            up = Vector3.Cross(fwd, tan).normalized;
        }

        public static GameObject Build(Vector3[] arc, Vector3 camPos, float bandW, Transform parent)
        {
            var root = new GameObject("ObiBand");
            root.transform.SetParent(parent, false);
            float hw = bandW * 0.5f;
            const float back = 0.12f;   // 文字の裏に少し下げる
            Strip(root, arc, camPos, "Base", -hw * 0.86f, hw * 0.86f, back, back, 0, Mat("black", 0.6f));
            var rim = Mat("goldRim", 3f);
            Strip(root, arc, camPos, "RimTopOut", hw * 0.93f, hw, back - 0.06f, back, +0.6f, rim);
            Strip(root, arc, camPos, "RimTopIn", hw * 0.86f, hw * 0.93f, back, back - 0.06f, -0.6f, rim);
            Strip(root, arc, camPos, "RimBotIn", -hw * 0.93f, -hw * 0.86f, back - 0.06f, back, +0.6f, rim);
            Strip(root, arc, camPos, "RimBotOut", -hw, -hw * 0.93f, back, back - 0.06f, -0.6f, rim);
            return root;
        }

        static Material Mat(string ramp, float boost)
        {
            var m = new Material(Shader.Find("PachiMoji/Metal"));
            m.SetTexture("_Ramp", PachiRamps.Get(ramp));
            m.SetFloat("_Boost", boost);
            return m;
        }

        // 帯の幅の向きの a〜b を埋める細長い板。zA・zB は a・b の側の奥行き（fwd の向き）、tiltN は面の傾き
        static void Strip(GameObject root, Vector3[] arc, Vector3 camPos, string name, float a, float b, float zA, float zB, float tiltN, Material mat)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            int n = arc.Length;
            for (int i = 0; i < n; i++)
            {
                var tan = (arc[Mathf.Min(i + 1, n - 1)] - arc[Mathf.Max(i - 1, 0)]).normalized;
                Frame(arc[i], tan, camPos, out var fwd, out var up);
                verts.Add(arc[i] + up * a + fwd * zA);
                verts.Add(arc[i] + up * b + fwd * zB);
                var nn = (-fwd + up * tiltN).normalized;
                norms.Add(nn); norms.Add(nn);
                if (i > 0)
                {
                    int q = (i - 1) * 2;
                    // 表と裏の両方（向きが逆でも見えるように）
                    tris.AddRange(new[] { q, q + 2, q + 1, q + 1, q + 2, q + 3, q, q + 1, q + 2, q + 1, q + 3, q + 2 });
                }
            }
            var mesh = new Mesh { name = "Obi" + name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
