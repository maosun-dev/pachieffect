using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// ドンの瞬間に飛び散る金属の破片。Evaluate(tau) に「飛び散ってからの秒数」を渡すと、
    /// その瞬間の位置・向き・大きさにそろえる（状態を持たない。同じ tau なら同じ絵）。
    /// 破片は三角の薄い板（面ごとに向きが違う平らな面）なので、回るたびに面がキラッと光る。
    /// </summary>
    public class PachiShards
    {
        struct Shard
        {
            public Transform tr;
            public Vector3 p0, v;      // 生まれる位置・はじめの速さ
            public Vector3 axis; public float spin;
            public float size, life;
        }

        Shard[] shards;
        const float Drag = 3.2f;     // 空気の抵抗：はじめは速く、すぐに勢いが落ちる
        float gravity;

        /// <param name="center">文字全体の中心</param>
        /// <param name="size">文字全体の大きさ</param>
        /// <param name="mats">破片の材質（順番に割り当てる）</param>
        public static PachiShards Build(Vector3 center, Vector3 size, Material[] mats, int count, int seed)
        {
            var s = new PachiShards();
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            float w = Mathf.Max(size.x, size.y);
            s.gravity = w * 1.1f;
            s.shards = new Shard[count];
            var root = new GameObject("Shards").transform;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Shard");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = ShardMesh(rnd);
                go.AddComponent<MeshRenderer>().sharedMaterial = mats[i % mats.Length];

                // 文字の表の面のあたりから生まれる（カメラは -z 側）
                var p0 = center + new Vector3((R() - 0.5f) * size.x * 0.9f, (R() - 0.5f) * size.y * 0.8f, -size.z * 0.5f);
                // 中心から外へ。少しカメラ側にも飛ばす。ごく一部はカメラの目の前をかすめる
                var outv = new Vector3(p0.x - center.x, p0.y - center.y, 0);
                if (outv.sqrMagnitude < 1e-6f) outv = Vector3.up;
                outv = outv.normalized;
                float ang = (R() - 0.5f) * 0.9f;
                outv = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg) * outv;
                bool near = i % 9 == 0;
                float sp = w * (near ? 1.2f : 1.6f + 2.4f * R());
                var v = outv * sp + new Vector3(0, w * 0.6f * R(), -w * (near ? 2.6f : 0.3f + 0.9f * R()));

                // 小さいかけらが多く、大きいものは少し
                float sz = w * (R() < 0.2f ? 0.09f + 0.07f * R() : 0.03f + 0.04f * R());
                s.shards[i] = new Shard
                {
                    tr = go.transform, p0 = p0, v = v,
                    axis = Random3(rnd).normalized, spin = 8 + 22 * R(),
                    size = sz, life = 0.9f + 0.8f * R(),
                };
                go.SetActive(false);
            }
            return s;
        }

        static Vector3 Random3(System.Random rnd) =>
            new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) + new Vector3(0, 0, 0.01f);

        // とがった三角の薄い板。表裏と3つの側面を別々の平らな面にする（面ごとに光り方が変わる）
        static Mesh ShardMesh(System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            var a = new Vector3(-0.5f + 0.2f * R(), -0.4f, 0);
            var b = new Vector3(0.5f - 0.2f * R(), -0.3f - 0.2f * R(), 0);
            var c = new Vector3((R() - 0.5f) * 0.6f, 0.6f + 0.6f * R(), 0);   // とがった先
            float th = 0.08f + 0.06f * R();
            var top = new[] { a + Vector3.back * th, b + Vector3.back * th, c + Vector3.back * th };
            var bot = new[] { a + Vector3.forward * th, b + Vector3.forward * th, c + Vector3.forward * th };
            // 表の面を少しねじって、平らすぎないようにする
            top[2] += new Vector3(0, 0, -th * 0.8f);

            var vs = new System.Collections.Generic.List<Vector3>();
            var ts = new System.Collections.Generic.List<int>();
            void Tri(Vector3 p, Vector3 q, Vector3 r) { int n = vs.Count; vs.Add(p); vs.Add(q); vs.Add(r); ts.Add(n); ts.Add(n + 1); ts.Add(n + 2); }
            Tri(top[0], top[2], top[1]);
            Tri(bot[0], bot[1], bot[2]);
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                Tri(top[i], top[j], bot[j]);
                Tri(top[i], bot[j], bot[i]);
            }
            var m = new Mesh { name = "Shard" };
            m.SetVertices(vs); m.SetTriangles(ts, 0);
            m.RecalculateNormals();   // 頂点を面ごとに分けてあるので、カクカクの平らな面になる
            m.RecalculateBounds();
            return m;
        }

        /// <summary>tau＝飛び散ってからの秒数（負なら隠す）</summary>
        public void Evaluate(float tau)
        {
            foreach (var s in shards)
            {
                bool on = tau >= 0 && tau < s.life;
                s.tr.gameObject.SetActive(on);
                if (!on) continue;
                float d = (1 - Mathf.Exp(-Drag * tau)) / Drag;   // 勢いが落ちながら進んだ距離
                s.tr.position = s.p0 + s.v * d + Vector3.down * (0.5f * gravity * tau * tau);
                s.tr.rotation = Quaternion.AngleAxis(s.spin * Mathf.Rad2Deg * (d + 0.15f * tau), s.axis);
                // 終わりぎわに小さくなって消える
                float fade = Mathf.Clamp01((s.life - tau) / (s.life * 0.35f));
                s.tr.localScale = Vector3.one * s.size * fade;
            }
        }
    }
}
