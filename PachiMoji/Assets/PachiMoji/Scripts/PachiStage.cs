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
    ///   うなり（赤くにじむ）→ 暗転・無音 → チラ見せ（1文字ずつ超アップ）→ 青・白の閃光
    ///   → 銀の文字が飛んできてドン → 左から金へ塗り替え → 左から虹へ塗り替え → 余韻
    /// </summary>
    public class PachiStage
    {
        // ───────── 時刻（秒） ─────────
        public const float Rumble = 1.3f;       // うなりの終わり＝暗転の始まり
        public const float Tame = 1.2f;         // 暗転の長さ
        public const float Cut = 0.4f;          // チラ見せ1文字の長さ
        public const float Wipe = 0.5f;         // 塗り替えの長さ

        public float TeaseStart => Rumble + Tame;
        public float FlashStart => TeaseStart + teaseCount * Cut;
        public float SlamStart => FlashStart + 0.12f;
        public float Impact => SlamStart + 0.2f;
        public float GoldWipe => Impact + 1.2f;
        public float RainbowWipe => GoldWipe + Wipe + 1.0f;
        public float End => RainbowWipe + Wipe + 2.4f;

        // ───────── 舞台の部品 ─────────
        public Camera cam;
        public Transform text;
        public PachiText.Rig rig;
        Material bgMat, flashMat;
        Transform bg; float aspect;
        const float BgDist = 60;
        int teaseCount;
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
            float bgMode = 0, bgGain = 1, bgCore = 1, bgBolts = 1, bgSparks = 1;
            float glint = 0;

            if (t < Rumble)
            {
                // うなり：赤い背景がじわじわ明るくなり、揺れが強くなる。文字はまだ出さない
                float u = t / Rumble;
                textOn = false;
                bgGain = Mathf.Lerp(0.02f, 0.22f, u * u); bgCore = 0.15f; bgBolts = 0; bgSparks = 0.3f;
                shake = 0.025f * u;
            }
            else if (t < TeaseStart)
            {
                // 暗転・無音：真っ暗
                textOn = false; bgGain = 0;
            }
            else if (t < FlashStart)
            {
                // チラ見せ：1文字ずつ、銀の文字を超アップで。切り替わりの一瞬は真っ暗
                int k = Mathf.Min(teaseCount - 1, (int)((t - TeaseStart) / Cut));
                float lt = t - TeaseStart - k * Cut;
                if (lt < 0.06f) { textOn = false; bgGain = 0; }
                else
                {
                    float u = (lt - 0.06f) / (Cut - 0.06f);
                    rot = Quaternion.Euler(4, (k % 2 == 0 ? -10 : 10), 0);
                    text.rotation = rot; text.localScale = Vector3.one;
                    var c = text.TransformPoint(rig.charCenters[k]);
                    float dir = k % 2 == 0 ? 1 : -1;
                    var off = rot * new Vector3(dir * Mathf.Lerp(-0.16f, 0.16f, u), Mathf.Lerp(-0.05f, 0.05f, u), -0.66f);
                    camPos = c + off;
                    camRot = Quaternion.LookRotation(c - camPos + rot * new Vector3(0, 0.02f, 0)) * Quaternion.Euler(0, 0, dir * 4);
                    fov = 46;
                    bgMode = 2; bgGain = 0.55f; bgCore = 0.4f; bgBolts = 0.25f;
                    shake = 0.012f * Decay(t, t - lt + 0.06f, 14);
                }
            }
            else if (t < SlamStart)
            {
                // 閃光：青の反転 → 白
                textOn = false;
                flash = t - FlashStart < 0.05f ? new Color(0.1f, 0.25f, 1f, 1) : new Color(1, 1, 1, 1);
            }
            else
            {
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

                // 色：銀 → 金 → 虹（左から塗り替え）
                if (t < GoldWipe) look = (PachiText.Finish.Silver, PachiText.Finish.Silver, 0f);
                else if (t < GoldWipe + Wipe) look = (PachiText.Finish.Silver, PachiText.Finish.Gold, Clamp01((t - GoldWipe) / Wipe));
                else if (t < RainbowWipe) look = (PachiText.Finish.Gold, PachiText.Finish.Gold, 0f);
                else if (t < RainbowWipe + Wipe) look = (PachiText.Finish.Gold, PachiText.Finish.Rainbow, Clamp01((t - RainbowWipe) / Wipe));
                else look = (PachiText.Finish.Rainbow, PachiText.Finish.Rainbow, 0f);

                bgMode = t < RainbowWipe + Wipe ? 0 : 1;

                // ドン・塗り替え完了の瞬間：白く光って、揺れる
                float f1 = Decay(t, Impact, 5) * 1.0f;
                float f2 = Decay(t, GoldWipe + Wipe, 6) * 0.6f;
                float f3 = Decay(t, RainbowWipe + Wipe, 5) * 0.85f;
                float fa = Mathf.Max(f1, Mathf.Max(f2, f3));
                if (t >= Impact) flash = t < GoldWipe + Wipe ? new Color(1, 1, 1, fa) : new Color(1, 0.95f, 0.8f, fa);
                shake = Decay(t, Impact, 6) * 0.07f + Decay(t, GoldWipe + Wipe, 7) * 0.04f + Decay(t, RainbowWipe + Wipe, 6) * 0.05f;
                bgGain = t < Impact ? 0.7f : 1f + 0.6f * fa;
            }

            // 反映
            text.gameObject.SetActive(textOn);
            text.rotation = rot;
            text.localScale = Vector3.one * scale;
            PachiText.SetLook(rig, look.Item1, look.Item2, look.Item3, 1.5f);
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
            flashMat.SetColor("_Color", flash);
        }
    }
}
