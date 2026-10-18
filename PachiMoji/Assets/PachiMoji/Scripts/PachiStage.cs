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
    ///   プチュン（白い閃光 → レンズフレアが点滅してしぼむ＋青い電気 → 消える）→ 暗転・無音 → チラ見せ（1文字ずつ、立体を回り込んで映す。あいだに暗転、だんだん速く）
    ///   → 文字の裏から光が吹き出す → 青・白の閃光 → 銀の文字が飛んできてドン
    ///   → 昇格（前ぶれ→白く飛ぶ→一瞬で金に切り替え＋跳ねる）→ 紫の光線で切り裂かれて灰色に固まる（タメ）
    ///   → 一気に虹（1回転→止まる→1回転）→ 余韻（背景の色が切り替わり続ける）
    /// </summary>
    public class PachiStage
    {
        // ───────── 時刻（秒） ─────────
        public const float Rumble = 1.25f;      // プチュンの終わり＝暗転の始まり
        public const float PuchunAt = 0.3f;     // 真っ暗から、青白い閃光が走る時刻
        public const float Tame = 1.3f;         // 暗転（無音）の長さ
        public const float RayLen = 0.6f;       // 最後のチラ見せのあと、文字の裏から光が吹き出す長さ
        public const float Pre = 0.35f;         // 昇格の前ぶれ（暗くなって震える）
        public const float WhiteGold = 0.12f;   // 金に変わる前に白く飛ぶ長さ
        public const float SlashDraw = 0.15f;   // 切り裂きの光線が画面を横切る長さ
        public const float GrayHold = 1.2f;     // 切り裂かれて灰色に固まったままタメる長さ
        public const float Spin = 0.7f;         // 虹になったあとの1回転の長さ
        public const float SpinRest = 0.45f;    // 1回転と1回転のあいだに止まる長さ

        public float TeaseStart => Rumble + Tame;
        public float RayStart { get; private set; }   // チラ見せの長さは文字数で変わるので Build で決める
        public float FlashStart => RayStart + RayLen;
        public float SlamStart => FlashStart + 0.14f;
        public float Impact => SlamStart + 0.2f;
        public float GoldAt => Impact + 1.3f;          // 白飛びの始まり。色が切り替わるのは +WhiteGold
        public float SlashAt => GoldAt + WhiteGold + 1.7f;              // 金のあと、切り裂かれる
        public float RainbowAt => SlashAt + SlashDraw + GrayHold;       // 灰色のタメのあと、一気に虹
        public float End => RainbowAt + 2 * Spin + SpinRest + 1.8f;

        // ───────── 舞台の部品 ─────────
        public Camera cam;
        public Transform text;
        public PachiText.Rig rig;
        Material bgMat, flashMat, raysMat;
        Transform bg; float aspect;
        const float BgDist = 60;
        int teaseCount;
        float[] teaseAt, teaseLen;   // チラ見せ1文字ずつの始まりと長さ（だんだん短く＝速くなる）
        // チラ見せ用に1文字ずつ別に作った立体（となりの文字が映り込んで先に全体が見えないように）
        GameObject[] teaseObj; Vector3[] teaseC; float[] teaseSize;
        int[] teaseShot;             // 1文字ずつのカメラの動き方（TeaseShot の番号）。言葉ごとに並びが変わる
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

            // チラ見せ用の1文字ずつの立体。ふだんは隠しておく
            var chars = string.Concat(lines).Replace(" ", "").Replace("　", "");
            st.teaseObj = new GameObject[st.teaseCount]; st.teaseC = new Vector3[st.teaseCount]; st.teaseSize = new float[st.teaseCount];
            // カメラの動き方を、言葉から決まる乱数で並べ替える（同じ言葉なら毎回同じ。となり同士は必ず違う型）
            var shots = new List<int> { 0, 1, 2, 3, 4 };
            var rnd = new System.Random(StableHash(string.Concat(lines)));
            for (int i = shots.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (shots[i], shots[j]) = (shots[j], shots[i]); }
            st.teaseShot = new int[st.teaseCount];
            for (int k = 0; k < st.teaseCount; k++) st.teaseShot[k] = shots[k % shots.Count];
            for (int k = 0; k < st.teaseCount && k < chars.Length; k++)
            {
                var one = PachiText.Create(new[] { chars[k].ToString() }, PachiText.Finish.Silver, font);
                var ob = one.GetComponent<MeshRenderer>().bounds;
                st.teaseC[k] = ob.center;
                // 縦長の画面なので、高さと「横幅÷画面の縦横比」の大きい方に合わせる
                st.teaseSize[k] = Mathf.Max(ob.size.y, ob.size.x / aspect);
                one.SetActive(false);
                st.teaseObj[k] = one;
            }
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

        // 文字列から決まる数（実行環境で変わらないように自前で計算する）
        static int StableHash(string str)
        {
            unchecked { int h = 17; foreach (char ch in str) h = h * 31 + ch; return h & 0x7fffffff; }
        }

        // チラ見せのカメラの動き方。c＝文字の中心、d＝文字がちょうど収まる距離、u＝0→1（1を超えても続く）
        static void TeaseShot(int shot, Vector3 c, float d, float size, float u, float dir, out Vector3 pos, out Quaternion rot)
        {
            float L(float a, float b) => Mathf.LerpUnclamped(a, b, u);
            float roll = 0;
            Vector3 look = c;
            switch (shot)
            {
                case 0:   // 下からあおって、横へ大きく回り込む
                    pos = c + Quaternion.Euler(-L(20, 8), dir * L(-38, 32), 0) * new Vector3(0, 0, -d * L(1.1f, 0.9f));
                    roll = dir * 3;
                    break;
                case 1:   // 上から見下ろし、降りてきながら正面へ
                    pos = c + Quaternion.Euler(L(48, 16), dir * L(18, -8), 0) * new Vector3(0, 0, -d * L(1.05f, 0.92f));
                    roll = -dir * 4;
                    break;
                case 2:   // 斜め45°から、横へすべるように移動しながら見る（文字の手前を横切る）
                    {
                        var q = Quaternion.Euler(-6, dir * 42, 0);
                        var right = q * Vector3.right;
                        pos = c + q * new Vector3(0, 0, -d * 0.82f) + right * (dir * L(-0.45f, 0.45f) * size);
                        look = c + right * (dir * L(-0.12f, 0.12f) * size);
                        roll = dir * L(6, -2);
                    }
                    break;
                case 3:   // ほぼ正面から、カメラを傾けたまま一気に寄る
                    pos = c + Quaternion.Euler(-8, dir * 16, 0) * new Vector3(0, 0, -d * L(1.45f, 0.82f));
                    roll = dir * L(-22, -4);
                    break;
                default:  // 真下近くから、せり上がりながら見上げる
                    pos = c + Quaternion.Euler(-L(42, 10), dir * L(10, 26), 0) * new Vector3(0, 0, -d * L(0.95f, 0.9f));
                    roll = dir * 2;
                    break;
            }
            rot = Quaternion.LookRotation(look - pos) * Quaternion.Euler(0, 0, roll);
        }

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
            int teaseOn = -1;
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
            float slash = 0, slashProg = 0, gray = 0;
            float pFlash = 0, pDot = 0, pArc = 0, pScale = 1;   // プチュンの閃光・フレア・電気・フレアの大きさ

            if (t < Rumble)
            {
                // プチュン：真っ暗 → 一瞬だけ青白い閃光 → 暗闇の中心に光の点が残り、まわりで青い電気がバチバチ → 点が消える
                textOn = false; bgGain = 0;
                if (t >= PuchunAt)
                {
                    float q = t - PuchunAt;
                    // 頭の2コマだけ白い閃光（インパクト）
                    pFlash = q < 0.067f ? 1f : Mathf.Exp(-(q - 0.067f) * 75);
                    // フレア：5コマほど点滅 → 15コマほどで一気にしぼんで消える（急に上がって長く尾を引く）
                    float blink = q < 0.17f ? (((int)(q * 30) % 2 == 0) ? 1f : 0.55f) : 1f;
                    float fall = Mathf.Exp(-Mathf.Max(0, q - 0.17f) * 10);
                    pDot = Clamp01(q / 0.03f) * blink * fall * 1.3f;
                    pScale = Mathf.Lerp(0.35f, 1.15f, fall) * (1 + 0.15f * Mathf.Exp(-q * 20));
                    if (pDot < 0.01f) pDot = 0;
                    // 電気：フレアの芯のまわりで少しだけバチバチ
                    pArc = q > 0.06f && q < 0.5f ? (0.45f + 0.25f * Mathf.Sin(t * 90)) * fall : 0;
                    shake = 0.02f * Mathf.Exp(-q * 8);
                }
            }
            else if (t < FlashStart)
            {
                // 暗転 → チラ見せ（1文字ずつ、立体のまわりを回り込む）→ 文字の裏から光が吹き出す
                int k = -1; float lt = 0, len = 1;
                for (int i = 0; i < teaseCount; i++)
                    if (t >= teaseAt[i] && (t < teaseAt[i] + teaseLen[i] || (i == teaseCount - 1 && t >= RayStart)))
                    { k = i; lt = t - teaseAt[i]; len = teaseLen[i]; }
                textOn = false;
                if (k < 0) { bgGain = 0; }   // 暗転・無音
                else
                {
                    // 1文字まるごと見える距離から、文字ごとに違う動きで立体をなめるように映す
                    teaseOn = k;
                    float u = lt / len;                       // 光の放射中は 1 を超えて、そのまま動き続ける
                    float dir = k % 2 == 0 ? 1 : -1;          // 左右の向きを1文字ごとに入れ替える
                    fov = 38;
                    float d = teaseSize[k] * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / 0.9f;   // 画面の約9割に収める
                    TeaseShot(teaseShot[k], teaseC[k], d, teaseSize[k], u, dir, out camPos, out camRot);
                    bgMode = 2; bgGain = 0.2f; bgCore = 0.8f; bgBolts = 0; bgSparks = 0.5f;   // 暗い青。文字の後ろだけほんのり光る
                    flash = new Color(0, 0, 0, 1 - Clamp01(lt / 0.1f));   // 暗転からすっと出る
                    shake = 0.012f * Decay(t, teaseAt[k], 10);
                    if (t >= RayStart)
                    {
                        // 光の放射：文字の裏から光の筋が伸び、最後は白く飛ぶ
                        float rp = (t - RayStart) / RayLen;
                        fgRays = 0.25f + rp * 1.0f; fgReach = Mathf.Lerp(0.15f, 2.2f, Mathf.Pow(rp, 0.7f));
                        bgGain = Mathf.Lerp(0.2f, 1.3f, rp); bgCore = Mathf.Lerp(0.8f, 1.5f, rp);
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
                bool cut = t >= SlashAt && t < RainbowAt;          // 切り裂かれて灰色でタメている間
                bool rain = t >= RainbowAt;
                float rx = t - RainbowAt;
                if (t < Impact)
                {
                    // 銀の文字が手前から飛んできて、ドン
                    float k = (t - SlamStart) / (Impact - SlamStart);
                    scale = Mathf.Lerp(3.5f, 1f, k * k);
                    flash = new Color(1, 1, 1, 1 - k * 0.75f);
                }
                else
                {
                    // 余韻：ゆっくり揺れて、少しずつ寄る。切り裂かれたら止まる。虹のあとは回転してから揺れ直す
                    float h = rain ? Mathf.Max(0, rx - 2 * Spin - SpinRest) : Mathf.Min(t, SlashAt) - Impact;
                    float spin = 0;
                    if (rain)
                    {
                        float e1 = Clamp01(rx / Spin), e2 = Clamp01((rx - Spin - SpinRest) / Spin);
                        spin = 360 * (1 - Mathf.Pow(1 - e1, 3)) + 360 * (1 - Mathf.Pow(1 - e2, 3));   // 1回転 → 止まる → 1回転
                    }
                    rot = textBaseRot * Quaternion.Euler(0, Mathf.Sin(h * 0.7f) * 4 + spin, 0);
                    float hc = Mathf.Min(t, SlashAt) - Impact;
                    camPos = Vector3.Lerp(camBasePos, text.position, Mathf.Min(0.06f, hc * 0.012f));
                    glint = cut ? 0 : 1;
                }

                // 今の色：銀 → 金（白飛びのあと一瞬で切り替える）→ 切り裂かれて灰色 → 一気に虹
                var fin = rain ? PachiText.Finish.Rainbow : g.snapped ? PachiText.Finish.Gold : PachiText.Finish.Silver;
                look = (fin, fin, 0f);

                float pre = rain ? 0 : g.pre;
                float pop = rain ? Mathf.Exp(-rx * 4) : g.snapped ? Mathf.Exp(-g.x * 5) : 0;
                float since = rain ? rx : g.x;
                white = rain ? 0 : Mathf.Max(g.white, 0.3f * pre * pre);

                scale *= 1 - 0.03f * pre;
                if (rain || g.snapped) scale *= 1 + (rain ? 0.22f : 0.17f) * Mathf.Exp(-since * 6) * Mathf.Cos(since * 16);   // 跳ねて戻る

                // 金に変わったあと、表面を光の筋が1本サッと横切る
                if (!rain && !cut && g.snapped && g.x > 0.2f && g.x < 0.65f) { look = (fin, fin, (g.x - 0.2f) / 0.45f); lookGlow = 2.8f; }

                // 背景：銀は青、金は炎、虹は虹（ときどき青・炎に切り替わり続ける）
                if (fin == PachiText.Finish.Silver) bgMode = 2;
                else if (fin == PachiText.Finish.Gold) bgMode = 0;
                else
                {
                    int[] cyc = { 1, 1, 1, 2, 1, 1, 0 };
                    bgMode = cyc[(int)(t / 0.1f) % cyc.Length];
                    hue = rx * 0.25f;
                }
                bgRays = pop * (rain ? 2.0f : 1.4f); bgReach = 2.4f;
                fgRays = pop * (rain ? 0.6f : 0.35f); fgReach = 2.4f;

                // ドン・切り替えの瞬間：白く光って、揺れる
                float f1 = Decay(t, Impact, 5);
                float fa = Mathf.Max(f1, (rain ? 0.9f : 0.7f) * ((rain || g.snapped) ? Mathf.Exp(-since * 12) : 0));
                if (t >= Impact) flash = fin == PachiText.Finish.Silver ? new Color(1, 1, 1, fa) : new Color(1, 0.95f, 0.8f, fa);
                // 前ぶれ：画面全体をふっと暗くする
                if (pre > 0 && fa < 0.05f) flash = new Color(0, 0, 0, 0.45f * pre);
                shake = Decay(t, Impact, 6) * 0.07f + pop * (rain ? 0.1f : 0.08f) + pre * 0.012f;
                bgGain = t < Impact ? 0.7f : Mathf.Lerp(1f, 0.15f, Mathf.Max(pre, g.white > 0 ? 1 : 0)) + 0.4f * pop;

                if (cut)
                {
                    // 切り裂き：紫の光線が画面を横切り、文字は色が抜けて灰色に固まる。背景は沈み、震えがだんだん強くなる
                    float sl = t - SlashAt;
                    slashProg = Clamp01(sl / SlashDraw);
                    slash = 0.85f + 0.15f * Mathf.Sin(t * 53) * Mathf.Sin(t * 31);
                    gray = Clamp01((sl - SlashDraw * 0.5f) / 0.08f);
                    float hold = Clamp01((sl - SlashDraw) / GrayHold);
                    bgMode = 2; bgGain = Mathf.Lerp(0.6f, 0.1f, Clamp01(sl / 0.25f)); bgBolts = 0; bgSparks = 0.2f; bgCore = 0.3f;
                    bgRays = 0; fgRays = 0;
                    float hit = Mathf.Exp(-Mathf.Max(0, sl - SlashDraw) * 10) * (sl >= SlashDraw * 0.5f ? 1 : 0);
                    flash = new Color(0.9f, 0.4f, 1f, 0.45f * hit);
                    shake = 0.07f * hit + 0.003f + 0.018f * hold * hold;
                    scale *= 1 + 0.035f * hold;   // じわじわ迫ってくる
                    // 虹の直前：一瞬暗くして息を止める
                    float last = Clamp01((t - (RainbowAt - 0.2f)) / 0.2f);
                    if (last > 0) flash = new Color(0, 0, 0, 0.55f * last);
                }
            }

            // 反映
            text.gameObject.SetActive(textOn);
            for (int i = 0; i < teaseCount; i++) if (teaseObj[i] != null) teaseObj[i].SetActive(i == teaseOn);
            if (teaseOn >= 0)
            {
                // チラ見せの角のキラッは、1つずつ順番に光らせる（全部光りっぱなしだとうるさい）
                var tg = teaseObj[teaseOn].GetComponent<PachiText.Rig>().glints;
                for (int i = 0; i < tg.Count; i++)
                {
                    float ph = Mathf.Repeat(t * 1.6f - i * 0.37f, 1f);
                    tg[i].sharedMaterial.SetFloat("_Intensity", 6f * Mathf.Exp(-ph * 10f));
                }
            }
            text.rotation = rot;
            text.localScale = Vector3.one * scale;
            PachiText.SetLook(rig, look.Item1, look.Item2, look.Item3, lookGlow);
            PachiText.SetHeat(rig, white, hue, gray);
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
            raysMat.SetFloat("_Slash", slash);
            raysMat.SetFloat("_SlashProg", slashProg);
            raysMat.SetFloat("_PFlash", pFlash);
            raysMat.SetFloat("_PDot", pDot);
            raysMat.SetFloat("_PArc", pArc);
            raysMat.SetFloat("_PScale", pScale);
            flashMat.SetColor("_Color", flash);
        }
    }
}
