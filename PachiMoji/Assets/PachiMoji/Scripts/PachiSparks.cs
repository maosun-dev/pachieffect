using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// ドンの瞬間にはじける火花の粒。Evaluate(tau, cam) に「はじけてからの秒数」を渡すと、
    /// その瞬間の姿にそろえる（状態を持たない。同じ tau なら同じ絵）。
    /// 粒は速さの向きに伸びた光の筋で、はじめは長く、勢いが落ちると短い点になって消える。
    /// </summary>
    public class PachiSparks
    {
        struct Spark
        {
            public Transform tr; public Material mat;
            public Vector3 p0, v;
            public float life, bright, flick;
        }

        Spark[] sparks;
        const float Drag = 4f;       // 空気の抵抗：はじめは速く、すぐに勢いが落ちる
        float gravity, width;

        /// <param name="center">文字全体の中心</param>
        /// <param name="size">文字全体の大きさ</param>
        public static PachiSparks Build(Vector3 center, Vector3 size, int count, int seed)
        {
            var s = new PachiSparks();
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            float w = Mathf.Max(size.x, size.y);
            s.gravity = w * 1.6f;
            s.width = w * 0.024f;
            s.sparks = new Spark[count];
            var root = new GameObject("Sparks").transform;
            var shader = Shader.Find("PachiMoji/Spark");
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "Spark";
                go.transform.SetParent(root, false);
                var m = new Material(shader);
                // 色：濃い金が多め、ときどき白っぽい金（青い背景から浮くように、青は使わない）
                float c = R();
                m.SetColor("_Color", c < 0.7f ? new Color(1f, 0.6f, 0.18f) : new Color(1f, 0.85f, 0.55f));
                go.GetComponent<MeshRenderer>().sharedMaterial = m;

                // 文字の表の面のあたりから生まれ、中心から外へはじける（カメラは -z 側）
                var p0 = center + new Vector3((R() - 0.5f) * size.x * 0.8f, (R() - 0.5f) * size.y * 0.7f, -size.z * 0.6f);
                var outv = new Vector3(p0.x - center.x, p0.y - center.y, 0);
                if (outv.sqrMagnitude < 1e-6f) outv = Vector3.up;
                outv = Quaternion.Euler(0, 0, (R() - 0.5f) * 60) * outv.normalized;
                float sp = w * (2.2f + 4.5f * R() * R());
                var v = outv * sp + new Vector3(0, w * 0.8f * R(), -w * 1.2f * R());
                s.sparks[i] = new Spark
                {
                    tr = go.transform, mat = m, p0 = p0, v = v,
                    life = 0.45f + 0.6f * R(), bright = 7 + 8 * R(), flick = 20 + 30 * R(),
                };
                go.SetActive(false);
            }
            return s;
        }

        /// <summary>tau＝はじけてからの秒数（負なら隠す）。cam＝粒をこちらへ向けるためのカメラ</summary>
        public void Evaluate(float tau, Transform cam)
        {
            foreach (var s in sparks)
            {
                bool on = tau >= 0 && tau < s.life;
                s.tr.gameObject.SetActive(on);
                if (!on) continue;
                float e = Mathf.Exp(-Drag * tau);
                float d = (1 - e) / Drag;                              // 勢いが落ちながら進んだ距離
                var pos = s.p0 + s.v * d + Vector3.down * (0.5f * gravity * tau * tau);
                var vel = s.v * e + Vector3.down * (gravity * tau);    // 今の速さ
                // 粒はいつもカメラに向け、画面の上で速さの向きに伸ばす
                var view = (pos - cam.position).normalized;
                var up = Vector3.ProjectOnPlane(vel, view);
                if (up.sqrMagnitude < 1e-8f) up = Vector3.up;
                float len = Mathf.Max(width * 2.5f, vel.magnitude * 0.045f);   // 速いほど長い筋
                s.tr.SetPositionAndRotation(pos - up.normalized * len * 0.4f, Quaternion.LookRotation(view, up));
                float fade = Mathf.Clamp01((s.life - tau) / (s.life * 0.4f));
                s.tr.localScale = new Vector3(width * (0.6f + 0.4f * fade), len, 1);
                // チカチカまたたきながら消える
                float tw = 0.65f + 0.35f * Mathf.Sin(tau * s.flick + s.bright);
                s.mat.SetFloat("_Intensity", s.bright * fade * tw);
            }
        }
    }
}
