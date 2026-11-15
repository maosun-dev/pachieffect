using System.Collections.Generic;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 激アツの帯の本体：帯に沿って燃える炎（PachiFireBand を足し算で重ねる細長い板）。
    /// 板の面はカメラの方を向き、進む向きに直角（文字と同じ向き）。炎は文字と同じ速さで流れ、10 秒で元に戻る
    /// </summary>
    public static class ObiBand
    {
        /// <summary>炎の幅（帯の幅に対する倍率。炎の舌が文字の上下へはみ出す）</summary>
        public const float FireWidth = 2.1f;

        /// <summary>帯の面の向き：fwd は文字の奥向き、up は帯の幅の向き</summary>
        public static void Frame(Vector3 p, Vector3 tan, Vector3 camPos, out Vector3 fwd, out Vector3 up)
        {
            var toCam = (camPos - p).normalized;
            var n = (toCam - Vector3.Dot(toCam, tan) * tan).normalized;
            fwd = -n;
            up = Vector3.Cross(fwd, tan).normalized;
        }

        /// <summary>炎の板を作る。返す材質の _T に時刻を入れると炎が流れる</summary>
        public static Material Build(Vector3[] arc, float[] len, Vector3 camPos, float bandW, float loop, float speed, int dir, Transform parent)
        {
            var mat = new Material(Shader.Find("PachiMoji/FireBand"));
            mat.SetFloat("_Loop", loop);
            mat.SetFloat("_Speed", speed);
            mat.SetFloat("_Dir", dir);
            mat.SetFloat("_Len", len[len.Length - 1]);
            float hw = bandW * 0.5f * FireWidth;
            const float back = 0.15f;   // 文字の表の面より少し奥（文字が炎の手前に立つ）

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int n = arc.Length;
            for (int i = 0; i < n; i++)
            {
                var tan = (arc[Mathf.Min(i + 1, n - 1)] - arc[Mathf.Max(i - 1, 0)]).normalized;
                Frame(arc[i], tan, camPos, out var fwd, out var up);
                verts.Add(arc[i] - up * hw + fwd * back);
                verts.Add(arc[i] + up * hw + fwd * back);
                uvs.Add(new Vector2(len[i], -1));
                uvs.Add(new Vector2(len[i], 1));
                if (i > 0)
                {
                    int q = (i - 1) * 2;
                    // 表と裏の両方（材質は片方だけ描く）
                    tris.AddRange(new[] { q, q + 2, q + 1, q + 1, q + 2, q + 3, q, q + 1, q + 2, q + 1, q + 3, q + 2 });
                }
            }
            var mesh = new Mesh { name = "ObiFire" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("ObiFire");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return mat;
        }
    }
}
