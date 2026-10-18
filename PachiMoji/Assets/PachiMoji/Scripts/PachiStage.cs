using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PachiMoji
{
    /// <summary>
    /// 脳汁演出の舞台（カメラ・背景・文字・閃光）と、その流れ（タイムライン）。
    /// Evaluate(t) に時刻を渡すと、その瞬間の姿にそろえる。前の状態を持たないので、
    /// 動画の書き出しでも、アプリで再生しても、同じ時刻なら同じ絵になる。
    ///
    /// 流れ（参考動画の調査より）：
    ///   うなり（赤くにじむ）→ 暗転・無音 → チラ見せ（1文字ずつ超アップ、あいだに暗転、だんだん速く）
    ///   → 文字の裏から光が吹き出す → 青・白の閃光 → 銀の文字が飛んできてドン
    ///   → 昇格（前ぶれ→白く飛ぶ→一瞬で金に切り替え＋跳ねる）→ 同じく虹へ → 余韻（背景の色が切り替わり続ける）
    /// </summary>
    public class PachiStage
    {
        // ───────── 時刻（秒） ─────────
        public const float Rumble = 1.0f;       // うなりの終わり＝暗転の始まり
        public const float Tame = 1.0f;         // 暗転の長さ
        public const float RayLen = 0.6f;       // 最後のチラ見せのあと、文字の裏から光が吹き出す長さ
        public const float Pre = 0.35f;         // 昇格の前ぶれ（暗くなって震える）
        public const float WhiteGold = 0.12f;   // 金に変わる前に白く飛ぶ長さ
        public const float WhiteRain = 0.3f;    // 虹に変わる前に白く飛ぶ長さ（白い数字→虹の参考に合わせて長め）

        public float TeaseStart => Rumble + Tame;
        public float RayStart { get; private set; }   // チラ見せの長さは文字数で変わるので Build で決める
        public float FlashStart => RayStart + RayLen;
        public float SlamStart => FlashStart + 0.14f;
        public float Impact => SlamStart + 0.2f;
        public float GoldAt => Impact + 1.3f;          // 白飛びの始まり。色が切り替わるのは +WhiteGold
        public float RainbowAt => GoldAt + WhiteGold + 1.9f;
        public float End => RainbowAt + WhiteRain + 3.0f;

        // ───────── 舞台の部品 ─────────
        public Camera cam;
        public Transform text;
        public PachiText.Rig rig;
        Material bgMat, flashMat, raysMat;
        Transform bg; float aspect;
        const float BgDist = 60;
        int teaseCount;
        float[] teaseAt, teaseLen;   // チラ見せ1文字ずつの始まりと長さ（だんだん短く＝速くなる）
        Vector3 camBasePos; Quaternion camBaseRot;
        Quaternion textBaseRot;
        const float BaseFov = 32;

        public static PachiStage Build(string[] lines, float aspect, string font = "ReggaeOne")
        {
            var st = new PachiStage();

            // 文字（基本の向き：斜め下から見上げて側面を見せる）
            var go = PachiText.Create(lines, PachiText.Finish.Silver, font);
            st.text = go.transform;
            st.rig = go.GetComponent<PachiText.Rig>();
            st.textBaseRot = Quaternion.Euler(12, -24, 0);
            st.text.rotation = st.textBaseRot;
            st.teaseCount = Mathf.Min(4, st.rig.charCenters.Count);
            st.teaseAt = new float[st.teaseCount]; st.teaseLen = new float[st.teaseCount];
            float cur = st.TeaseStart;
            for (int k = 0; k < st.teaseCount; k++)
            {
                if (k > 0) cur += Mathf.Max(0.3f, 0.6f - 0.1f * k);   // 文字のあいだの暗転：0.5→0.4→0.3
                st.teaseAt[k] = cur;
                st.teaseLen[k] = Mathf.Max(0.55f, 0.9f - 0.15f * k);  // 1文字の長さ：0.9→0.75→0.6
                cur += st.teaseLen[k];
            }
            st.RayStart = cur;
            var b = go.GetComponent<MeshRenderer>().bounds;

            // カメラ
            var camGo = new GameObject("Camera");
            st.cam = camGo.AddComponent<Camera>();
            st.cam.fieldOfView = BaseFov;
            st.cam.clearFlags = CameraClearFlags.SolidColor;
            st.cam.backgroundColor = Color.black;
            st.cam.nearClipPlane = 0.05f; st.cam.farClipPlane = 100;
            float fitH = Mathf.Max(b.size.y, b.size.x / aspect) * (aspect < 1 ? 1.12f : 1.38f);
            float dist = fitH * 0.5f / Mathf.Tan(BaseFov * 0.5f * Mathf.Deg2Rad);
            camGo.transform.position = b.center + new Vector3(0, -fitH * 0.22f, -dist);
            camGo.transform.LookAt(b.center + new Vector3(0, fitH * 0.02f, 0));
            st.camBasePos = camGo.transform.position; st.camBaseRot = camGo.transform.rotation;
            st.cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // 背景：カメラの子にして、いつも画面いっぱい
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(bg.GetComponent<Collider>());
            bg.name = "Background";
            bg.transform.SetParent(camGo.transform, false);
            bg.transform.localPosition = new Vector3(0, 0, BgDist);
            st.bg = bg.transform; st.aspect = aspect;   // 大きさは Evaluate で画角に合わせる
            st.bgMat = new Material(Shader.Find("PachiMoji/Burst"));
            bg.GetComponent<MeshRenderer>().sharedMaterial = st.bgMat;

            // 閃光・暗転の板：カメラの目の前
            var fl = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(fl.GetComponent<Collider>());
            fl.name = "Flash";
            fl.transform.SetParent(camGo.transform, false);
            fl.transform.localPosition = new Vector3(0, 0, 0.2f);
            fl.transform.localScale = new Vector3(2, 2, 1);
            st.flashMat = new Material(Shader.Find("PachiMoji/Flash"));
            fl.GetComponent<MeshRenderer>().sharedMaterial = st.flashMat;

            // 光の放射の板：閃光の板のすぐ奥（足し算で重ねる）
            var rq = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(rq.GetComponent<Collider>());
            rq.name = "Rays";
            rq.transform.SetParent(camGo.transform, false);
            rq.transform.localPosition = new Vector3(0, 0, 0.21f);
            rq.transform.localScale = new Vector3(2, 2, 1);
            st.raysMat = new Material(Shader.Find("PachiMoji/Rays"));
            rq.GetComponent<MeshRenderer>().sharedMaterial = st.raysMat;

            // 光のにじみ（ブルーム）
            var vol = new GameObject("Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.65f);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);
            vol.sharedProfile = profile;
            return st;
        }

        static float Clamp01(float x) => Mathf.Clamp01(x);
        static float Decay(float t, float at, float k) => t < at ? 0 : Mathf.Exp(-(t - at) * k);
        static float Hash(float x) => Mathf.Repeat(Mathf.Sin(x * 127.1f) * 43758.5453f, 1f);

        // 昇格1回分の動き。at＝白飛びの始まり、wl＝白飛びの長さ。色が切り替わるのは at+wl
        struct Promo { public float pre, white, x; public bool snapped; }
        static Promo PromoAt(float t, float at, float wl)
        {
            var p = new Promo { x = t - (at + wl) };
            p.snapped = p.x >= 0;
            if (t >= at - Pre && t < at) p.pre = (t - (at - Pre)) / Pre;
            if (t >= at && t < at + wl) p.white = Clamp01((t - at) / 0.05f);
            return p;
        }

        /// <summary>時刻 t（秒）の姿にそろえる</summary>
        public void Evaluate(float t)
        {
            var tr = cam.transform;
            float fov = BaseFov;
            bool textOn = true;
            float scale = 1;
            Quaternion rot = textBaseRot;
            Vector3 camPos = camBasePos; Quaternion camRot = camBaseRot;
            float shake = 0;
            Color flash = new Color(0, 0, 0, 0);
            var look = (PachiText.Finish.Silver, PachiText.Finish.Silver, 0f);
            float lookGlow = 1.5f;
            float white = 0, hue = 0;
            float bgMode = 0, bgGain = 1, bgCore = 1, bgBolts = 1, bgSparks = 1, bgRays = 0, bgReach = 2, fgRays = 0, fgReach = 2;
            float glint = 0;

            if (t < Rumble)
            {
                // うなり：赤い背景がじわじわ明るくなり、揺れが強くなる。文字はまだ出さない
                float u = t / Rumble;
                textOn = false;
                bgGain = Mathf.Lerp(0.02f, 0.3f, u * u); bgCore = 0.15f; bgBolts = 0; bgSparks = 0.3f;
                shake = 0.03f * u;
            }
            else if (t < FlashStart)
            {
                // 暗転 → チラ見せ（1文字ずつ超アップ。カメラがゆっくり流れながら寄る）→ 文字の裏から光が吹き出す
                int k = -1; float lt = 0, len = 1;
                for (int i = 0; i < teaseCount; i++)
                    if (t >= teaseAt[i] && (t < teaseAt[i] + teaseLen[i] || (i == teaseCount - 1 && t >= RayStart)))
                    { k = i; lt = t - teaseAt[i]; len = teaseLen[i]; }
                if (k < 0) { textOn = false; bgGain = 0; }   // 暗転・無音
                else
                {
                    float u = lt / len;                       // 光の放射中は 1 を超えて、そのままゆっくり流れ続ける
                    float dir = k % 2 == 0 ? 1 : -1;
                    rot = Quaternion.Euler(4, dir * -10, 0);
                    text.rotation = rot; text.localScale = Vector3.one;
                    var c = text.TransformPoint(rig.charCenters[k]);
                    var off = rot * new Vector3(dir * Mathf.LerpUnclamped(-0.14f, 0.14f, u), Mathf.LerpUnclamped(-0.04f, 0.04f, u), Mathf.LerpUnclamped(-0.74f, -0.62f, u));
                    camPos = c + off;
                    camRot = Quaternion.LookRotation(c - camPos + rot * new Vector3(0, 0.02f, 0)) * Quaternion.Euler(0, 0, dir * 4);
                    fov = 46;
                    bgMode = 2; bgGain = 0.55f; bgCore = 0.4f; bgBolts = 0.25f;
                    flash = new Color(0, 0, 0, 1 - Clamp01(lt / 0.1f));   // 暗転からすっと出る
                    shake = 0.012f * Decay(t, teaseAt[k], 10);
                    if (t >= RayStart)
                    {
                        // 光の放射：文字の裏から光の筋が伸び、最後は白く飛ぶ
                        float rp = (t - RayStart) / RayLen;
                        fgRays = 0.25f + rp * 1.0f; fgReach = Mathf.Lerp(0.15f, 2.2f, Mathf.Pow(rp, 0.7f));
                        bgGain = Mathf.Lerp(0.55f, 1.3f, rp); bgCore = Mathf.Lerp(0.4f, 1.5f, rp);
                        float wa = Mathf.SmoothStep(0, 1, (rp - 0.55f) / 0.45f);
                        flash = new Color(1, 1, 1, wa);
                        shake = 0.006f + 0.02f * rp;
                    }
                }
            }
            else if (t < SlamStart)
            {
                // 閃光：青の反転 → 白
                textOn = false;
                flash = t - FlashStart < 0.06f ? new Color(0.1f, 0.25f, 1f, 1) : new Color(1, 1, 1, 1);
            }
            else
            {
                var g = PromoAt(t, GoldAt, WhiteGold);
                var r = PromoAt(t, RainbowAt, WhiteRain);
                if (t < Impact)
                {
                    // 銀の文字が手前から飛んできて、ドン
                    float k = (t - SlamStart) / (Impact - SlamStart);
                    scale = Mathf.Lerp(3.5f, 1f, k * k);
                    flash = new Color(1, 1, 1, 1 - k * 0.75f);
                }
                else
                {
                    // 余韻：ゆっくり揺れて、少しずつ寄る
                    float h = t - Impact;
                    rot = textBaseRot * Quaternion.Euler(0, Mathf.Sin(h * 0.7f) * 4, 0);
                    camPos = Vector3.Lerp(camBasePos, text.position, Mathf.Min(0.06f, h * 0.012f));
                    glint = 1;
                }

                // 今の色：銀 → 金 → 虹（塗っていかず、白飛びのあと一瞬で切り替える）
                var fin = r.snapped ? PachiText.Finish.Rainbow : g.snapped ? PachiText.Finish.Gold : PachiText.Finish.Silver;
                look = (fin, fin, 0f);
                var last = r.snapped || r.pre > 0 || r.white > 0 ? r : g;   // いま効いている昇格

                // 前ぶれ：背景がふっと暗くなり、文字が少し縮んで小刻みに震える
                float pre = last.pre;
                // 切り替え直後の勢い（1→0）
                float pop = last.snapped ? Mathf.Exp(-last.x * 5) : 0;
                white = Mathf.Max(last.white, 0.3f * pre * pre);

                scale *= 1 - 0.03f * pre;
                if (last.snapped) scale *= 1 + 0.17f * Mathf.Exp(-last.x * 6) * Mathf.Cos(last.x * 16);   // 跳ねて戻る

                // 切り替えのあと、表面を光の筋が1本サッと横切る
                if (last.snapped && last.x > 0.2f && last.x < 0.65f) { look = (fin, fin, (last.x - 0.2f) / 0.45f); lookGlow = 2.8f; }

                // 背景：銀は青、金は炎、虹は虹（ときどき青・炎に切り替わり続ける）
                if (fin == PachiText.Finish.Silver) bgMode = 2;
                else if (fin == PachiText.Finish.Gold) bgMode = 0;
                else
                {
                    int[] cyc = { 1, 1, 1, 2, 1, 1, 0 };
                    bgMode = cyc[(int)(t / 0.1f) % cyc.Length];
                    hue = (t - RainbowAt) * 0.25f;
                }
                bgRays = pop * 1.4f; bgReach = 2.4f;
                fgRays = pop * 0.35f; fgReach = 2.4f;

                // ドン・切り替えの瞬間：白く光って、揺れる
                // 白飛び中は背景を沈めて、白い文字だけを浮かせる（にごらせない）
                float f1 = Decay(t, Impact, 5);
                float fa = Mathf.Max(f1, 0.7f * (last.snapped ? Mathf.Exp(-last.x * 12) : 0));
                if (t >= Impact) flash = fin == PachiText.Finish.Silver ? new Color(1, 1, 1, fa) : new Color(1, 0.95f, 0.8f, fa);
                // 前ぶれ：画面全体をふっと暗くする
                if (pre > 0 && fa < 0.05f) flash = new Color(0, 0, 0, 0.45f * pre);
                shake = Decay(t, Impact, 6) * 0.07f + pop * 0.08f + pre * 0.012f;
                // 虹の白飛び中は、画面がガタガタとずれる（ノイズ）
                if (r.white > 0) shake += 0.05f;
                bgGain = t < Impact ? 0.7f : Mathf.Lerp(1f, 0.15f, Mathf.Max(pre, last.white > 0 ? 1 : 0)) + 0.4f * pop;
            }

            // 反映
            text.gameObject.SetActive(textOn);
            text.rotation = rot;
            text.localScale = Vector3.one * scale;
            PachiText.SetLook(rig, look.Item1, look.Item2, look.Item3, lookGlow);
            PachiText.SetHeat(rig, white, hue);
            for (int i = 0; i < rig.glints.Count; i++)
            {
                var g = rig.glints[i];
                float tw = 0.55f + 0.45f * Mathf.Sin(t * 7 + i * 2.1f);
                g.enabled = glint > 0;
                g.sharedMaterial.SetFloat("_Intensity", (i == 0 ? 7f : 4f) * tw);
            }

            // 揺れ（時刻から決まる乱数で）
            var sh = new Vector3(Hash(t * 31.7f) - 0.5f, Hash(t * 17.3f + 4) - 0.5f, 0) * 2 * shake;
            tr.position = camPos + camRot * sh;
            tr.rotation = camRot;
            cam.fieldOfView = fov;
            // 背景の板は画面ぴったりに（大きすぎると、白く光る中心だけが拡大されて映る）
            float bh = 2 * BgDist * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.04f;
            bg.localScale = new Vector3(bh * aspect, bh, 1);

            bgMat.SetFloat("_T", t);
            bgMat.SetFloat("_Mode", bgMode);
            bgMat.SetFloat("_Gain", bgGain);
            bgMat.SetFloat("_Core", bgCore);
            bgMat.SetFloat("_Bolts", bgBolts);
            bgMat.SetFloat("_Sparks", bgSparks);
            bgMat.SetFloat("_Rays", bgRays);
            bgMat.SetFloat("_RayReach", bgReach);
            raysMat.SetFloat("_Rays", fgRays);
            raysMat.SetFloat("_RayReach", fgReach);
            raysMat.SetFloat("_T", t);
            flashMat.SetColor("_Color", flash);
        }
    }
}
