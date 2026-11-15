using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PachiMoji
{
    /// <summary>
    /// 2つ目の演出「激アツの帯」の舞台。黒（または写真）の上を、言葉をくり返した金の帯が傾いた輪のように流れ続ける。
    /// 上の帯は左へ、下の帯は右へ。Evaluate(t) は時刻だけから姿を決める（前の状態を持たない）
    /// </summary>
    public class ObiStage
    {
        public Camera cam;
        GameObject root, volumeGo;
        VolumeProfile profile;
        Transform photoQuad;
        float photoAspect = 1, frameW;   // 写真の横÷縦、写真の奥行きでの書き出しの枠の幅
        const float PhotoZ = 14;

        class Lane
        {
            public Vector3[] pts;
            public float[] len;
            public float Total => len[len.Length - 1];
            public float loop, speed;
            public int dir;

            public void At(float s, out Vector3 p, out Vector3 tan)
            {
                int i = System.Array.BinarySearch(len, s);
                if (i < 0) i = ~i;
                i = Mathf.Clamp(i, 1, pts.Length - 1);
                float u = Mathf.InverseLerp(len[i - 1], len[i], s);
                p = Vector3.Lerp(pts[i - 1], pts[i], u);
                tan = (pts[i] - pts[i - 1]).normalized;
            }
        }

        // 帯の上を流れるもの（文字・区切りの菱形・縁の飾り）。lift は帯の幅の向きのずらし、flip は外向きを下にするか
        readonly List<(Transform t, Lane lane, float s0, float lift, bool flip)> letters = new List<(Transform, Lane, float, float, bool)>();
        Vector3 camPos;

        public static ObiStage Build(string[] lines, float aspect, EffectSettings settings, Texture2D photo)
        {
            var st = new ObiStage();
            settings ??= new EffectSettings();
            st.root = new GameObject("ObiStage");
            float dist = ObiPlan.ViewH * 0.5f / Mathf.Tan(ObiPlan.Fov * 0.5f * Mathf.Deg2Rad);
            st.camPos = new Vector3(0, 0, -dist);
            // 横幅を基準の画面と同じに保つ：縦が短い画面は縦の画角を狭める
            float halfW = ObiPlan.ViewH * ObiPlan.BaseAspect * 0.5f;
            float halfH = halfW / aspect;

            // カメラ
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(st.root.transform, false);
            st.cam = camGo.AddComponent<Camera>();
            st.cam.fieldOfView = 2 * Mathf.Atan(halfH / dist) * Mathf.Rad2Deg;
            st.cam.clearFlags = CameraClearFlags.SolidColor;
            st.cam.backgroundColor = Color.black;
            st.cam.nearClipPlane = 0.1f; st.cam.farClipPlane = 200;
            camGo.transform.position = st.camPos;
            st.cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // 文字の型（1文字ずつ。同じ字は1つだけ作って複製する）
            string word = string.Concat(lines ?? new string[0]).Replace(" ", "").Replace("　", "");
            if (word.Length == 0) word = "激アツ";
            var chars = new List<string>();
            for (int i = 0; i < word.Length; i++)
            {
                // サロゲートペアの文字も1文字として扱う
                if (char.IsHighSurrogate(word[i]) && i + 1 < word.Length) { chars.Add(word.Substring(i, 2)); i++; }
                else chars.Add(word[i].ToString());
            }
            var templates = new Dictionary<string, (GameObject go, Vector3 c)>();
            float maxH = 0.001f, refCy = 0;
            // 帯の文字は極太ゴシック（参考の帯の字）。その書体に無い字だけ、設定の書体で作る
            var band = PachiText.LoadFont(BandFont);
            foreach (var ch in chars)
            {
                if (templates.ContainsKey(ch)) continue;
                bool has = band.GlyphIndex(char.ConvertToUtf32(ch, 0)) != 0;
                var go = PachiText.Create(new[] { ch }, Tier.Gold, has ? BandFont : settings.FontName);
                go.transform.SetParent(st.root.transform, false);
                var b = go.GetComponent<MeshRenderer>().bounds;
                go.SetActive(false);
                templates[ch] = (go, new Vector3(b.center.x, b.center.y, b.min.z));
                if (b.size.y > maxH) { maxH = b.size.y; refCy = b.center.y; }
            }
            float scale = ObiPlan.LetterH / maxH;
            // 塗りを「焼けた金・赤い縁・赤い厚み」に替える（王道昇格の黒い縁どりの金とは別の塗り）
            st.hot = HotMaterials(refCy - maxH / 2, refCy + maxH / 2);
            foreach (var tpl in templates.Values)
            {
                var mr = tpl.go.GetComponent<MeshRenderer>();
                foreach (var old in mr.sharedMaterials) Kill(old);
                mr.sharedMaterials = st.hot;
                tpl.go.GetComponent<PachiText.Rig>().mats = st.hot;
            }

            // 帯：画面に見える範囲（＋少し外）だけに切る（短いほど目安の速さで 10 秒につながる）
            var lanes = new[]
            {
                st.MakeLane(ObiPlan.ArcPoints(ObiPlan.TopFor(aspect), true, 600), -1, halfW, halfH, dist, chars.Count),
                st.MakeLane(ObiPlan.ArcPoints(ObiPlan.BottomFor(aspect), false, 600), +1, halfW, halfH, dist, chars.Count),
            };
            int unit = chars.Count + 1;
            var rnd = new System.Random(20261115);   // キラキラの散らし方（いつも同じ）
            var starMat = new Material(Shader.Find("PachiMoji/Glint"));
            starMat.SetColor("_Color", new Color(1f, 0.96f, 0.8f));
            starMat.SetFloat("_Angle", 0.2f);
            foreach (var lane in lanes)
            {
                st.fires.Add(ObiBand.Build(lane.pts, lane.len, st.camPos, ObiPlan.BandW, lane.loop, lane.speed, lane.dir, st.root.transform));
                int slots = Mathf.RoundToInt(lane.loop / ObiPlan.Spacing);
                for (int k = 0; k < slots; k++)
                {
                    int ci = k % unit;
                    if (ci == chars.Count) continue;   // 言葉と言葉のあいだ（炎だけ）
                    var tp = templates[chars[ci]];
                    var holder = new GameObject("Letter");
                    holder.transform.SetParent(st.root.transform, false);
                    var copy = Object.Instantiate(tp.go, holder.transform);
                    copy.SetActive(true);
                    copy.transform.localScale = new Vector3(scale * ObiPlan.Squeeze, scale, scale);
                    copy.transform.localPosition = -new Vector3(tp.c.x * scale * ObiPlan.Squeeze, refCy * scale, tp.c.z * scale);
                    // 角のキラッは4文字に1つだけ
                    var rig = copy.GetComponent<PachiText.Rig>();
                    for (int g = 0; g < rig.glints.Count; g++) rig.glints[g].gameObject.SetActive(k % 4 == 0 && g == 0);
                    st.letters.Add((holder.transform, lane, k * ObiPlan.Spacing, 0, false));
                }

                // 星のキラキラ：帯の上に散らして、文字といっしょに流しながら、ばらばらの間でまたたかせる
                int starCount = Mathf.Max(3, Mathf.RoundToInt(lane.loop / 1.0f));
                for (int j = 0; j < starCount; j++)
                {
                    var holder = new GameObject("Star");
                    holder.transform.SetParent(st.root.transform, false);
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(holder.transform, false);
                    q.transform.localPosition = new Vector3(0, 0, -0.3f);   // 文字より手前
                    q.transform.localScale = Vector3.one * (0.55f + 0.75f * (float)rnd.NextDouble());
                    var r = q.GetComponent<MeshRenderer>();
                    r.sharedMaterial = starMat;
                    st.stars.Add((r, (float)rnd.NextDouble(), 2 + rnd.Next(4), 3.5f + 4f * (float)rnd.NextDouble()));
                    float lift = ((float)rnd.NextDouble() * 2 - 1) * ObiPlan.BandW * 0.8f;
                    st.letters.Add((holder.transform, lane, (j + (float)rnd.NextDouble()) * lane.loop / starCount, lift, false));
                }
            }

            // 写真：帯の奥に、書き出しの枠（9:16）を覆う板
            st.frameW = 2 * halfW * (PhotoZ + dist) / dist;
            if (photo != null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "Photo";
                q.transform.SetParent(st.root.transform, false);
                var m = new Material(Shader.Find("PachiMoji/Photo"));
                m.SetTexture("_MainTex", photo);
                q.GetComponent<MeshRenderer>().sharedMaterial = m;
                st.photoQuad = q.transform;
                st.photoAspect = (float)photo.width / photo.height;
                st.SetPhotoFit(settings.PhotoOffset, settings.PhotoZoom);
            }
            else
            {
                // 写真が無いとき：黒に、暗い赤の熱がゆっくりうねる背景
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "Back";
                q.transform.SetParent(st.root.transform, false);
                float bh = 2 * halfH * (PhotoZ + dist) / dist * 1.04f;
                q.transform.localPosition = new Vector3(0, 0, PhotoZ);
                q.transform.localScale = new Vector3(bh * aspect, bh, 1);
                st.back = new Material(Shader.Find("PachiMoji/ObiBack"));
                st.back.SetFloat("_Aspect", aspect);
                q.GetComponent<MeshRenderer>().sharedMaterial = st.back;
            }

            // 光のにじみ（王道昇格と同じ）
            st.volumeGo = new GameObject("Volume");
            var vol = st.volumeGo.AddComponent<Volume>();
            vol.isGlobal = true;
            st.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = st.profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.65f);
            st.profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);
            vol.sharedProfile = st.profile;

            st.Evaluate(0);
            return st;
        }

        /// <summary>写真のずらし・倍率を変える（組み立て直さずに写真の板だけ動かす）</summary>
        public void SetPhotoFit(Vector2 offset, float zoom)
        {
            if (photoQuad == null) return;
            var size = PhotoFit.Size(photoAspect, zoom) * frameW;
            var off = PhotoFit.ClampOffset(offset, photoAspect, zoom) * frameW;
            photoQuad.localPosition = new Vector3(off.x, off.y, PhotoZ);
            photoQuad.localScale = new Vector3(size.x, size.y, 1);
        }

        public bool HasPhoto => photoQuad != null;
        public float PhotoAspect => photoAspect;

        Lane MakeLane(Vector3[] pts, int dir, float halfW, float halfH, float dist, int wordChars)
        {
            // 画面の外へ文字1.5個ぶんまでを残す
            float margin = ObiPlan.LetterH * 1.5f;
            int first = -1, last = -1;
            for (int i = 0; i < pts.Length; i++)
            {
                float k = dist / (pts[i].z + dist);
                bool vis = Mathf.Abs(pts[i].x * k) <= halfW + margin && Mathf.Abs(pts[i].y * k) <= halfH + margin;
                if (!vis) continue;
                if (first < 0) first = i;
                last = i;
            }
            if (first < 0) { first = 0; last = pts.Length - 1; }
            first = Mathf.Max(0, first - 1); last = Mathf.Min(pts.Length - 1, last + 1);
            var lane = new Lane { dir = dir, pts = new Vector3[last - first + 1] };
            System.Array.Copy(pts, first, lane.pts, 0, lane.pts.Length);
            lane.len = new float[lane.pts.Length];
            for (int i = 1; i < lane.pts.Length; i++) lane.len[i] = lane.len[i - 1] + Vector3.Distance(lane.pts[i], lane.pts[i - 1]);
            lane.loop = ObiPlan.LetterSlots(lane.Total, wordChars) * ObiPlan.Spacing;
            lane.speed = ObiPlan.LoopSpeed(lane.loop);
            return lane;
        }

        const string BandFont = "MPLUS1pBlack";

        // 文字の材質（サブメッシュ順：表の面・細い線・字のすき間の床・外フチ・側面）。y0〜y1 は字の高さの範囲
        static Material[] HotMaterials(float y0, float y1)
        {
            Material M(string top, string bot, float band, float lit, float glow, float sheen)
            {
                var m = new Material(Shader.Find("PachiMoji/Hot"));
                ColorUtility.TryParseHtmlString(top, out var ct);
                ColorUtility.TryParseHtmlString(bot, out var cb);
                m.SetColor("_Top", ct.linear);
                m.SetColor("_Bot", cb.linear);
                m.SetVector("_Band", new Vector4(0.56f, 0.085f, band, 0));
                m.SetVector("_Y", new Vector4(y0, y1, 0, 0));
                m.SetFloat("_Lit", lit);
                m.SetFloat("_Glow", glow);
                m.SetFloat("_Sheen", sheen);
                return m;
            }
            return new[]
            {
                M("#FFE02A", "#FFAE00", 0.95f, 0, 1.2f, 0.9f),     // 表の面：黄〜濃い金に、白く焼けた帯
                M("#FFF7B8", "#FFE060", 0.6f, 0, 1.7f, 0.9f),      // 細い線：白っぽい金
                M("#C01000", "#7E0000", 0f, 0, 1.0f, 0.15f),      // 字のすき間の床：濃い赤（字の線を立たせる）
                M("#FF5A00", "#C80E00", 0.2f, 0.35f, 1.15f, 0.4f), // 外フチ：赤〜橙
                M("#D01200", "#5E0000", 0f, 0.8f, 1.0f, 0f),       // 側面（厚み）：濃い赤
            };
        }

        Material[] hot;
        Material back;
        // キラキラ（描くもの・またたきのずれ・10 秒でまたたく回数・明るさ）
        readonly List<(Renderer r, float phase, int times, float bright)> stars = new List<(Renderer, float, int, float)>();
        MaterialPropertyBlock block;

        readonly List<Material> fires = new List<Material>();   // 帯の炎（時刻を入れて流す）

        public void Evaluate(float t)
        {
            foreach (var f in fires) f.SetFloat("_T", t);
            if (hot != null) foreach (var m in hot) m.SetFloat("_T", t);
            if (back != null) back.SetFloat("_T", t);
            block ??= new MaterialPropertyBlock();
            foreach (var sp in stars)
            {
                // またたき：10 秒で times 回、するどく光って消える
                float v = Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * (t * sp.times / ObiPlan.Duration + sp.phase)));
                block.SetFloat("_Intensity", sp.bright * Mathf.Pow(v, 10));
                sp.r.SetPropertyBlock(block);
            }
            foreach (var L in letters)
            {
                float s = ObiPlan.SlotPos(L.s0, L.lane.dir, L.lane.speed, t, L.lane.loop, L.lane.Total);
                bool on = s >= 0 && s <= L.lane.Total;
                if (L.t.gameObject.activeSelf != on) L.t.gameObject.SetActive(on);
                if (!on) continue;
                L.lane.At(s, out var p, out var tan);
                ObiBand.Frame(p, tan, camPos, out var fwd, out var up);
                L.t.localPosition = p + up * L.lift;
                L.t.localRotation = Quaternion.LookRotation(fwd, L.flip ? -up : up);
            }
        }

        /// <summary>作ったものを全部消す（PachiRamps の色表と Quad の共有メッシュは消さない）</summary>
        public void Dispose()
        {
            if (root != null)
            {
                var mats = new HashSet<Material>();
                var meshes = new HashSet<Mesh>();
                foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in rend.sharedMaterials) if (m != null) mats.Add(m);
                foreach (var rg in root.GetComponentsInChildren<PachiText.Rig>(true))
                    if (rg.mesh != null) meshes.Add(rg.mesh);
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("Obi")) meshes.Add(mf.sharedMesh);
                Kill(root);
                foreach (var m in mats) Kill(m);
                foreach (var m in meshes) Kill(m);
            }
            Kill(volumeGo);
            if (profile != null)
            {
                foreach (var c in profile.components) Kill(c);
                Kill(profile);
            }
            root = null; volumeGo = null; profile = null; cam = null; photoQuad = null; hot = null; back = null;
            letters.Clear(); fires.Clear(); stars.Clear();
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
