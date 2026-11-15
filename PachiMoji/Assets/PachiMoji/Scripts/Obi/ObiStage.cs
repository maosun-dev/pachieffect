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
            public Transform parent;   // 列の入れ物（指で合わせたずらし・大きさ）

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
            if (aspect >= 1)
            {
                // 横長（16:9 の書き出し）：高さを基準にする。帯の輪がまるごと横に広がって見える
                halfH = ObiPlan.ViewH * 0.5f * LandscapeHeight;
                halfW = halfH * aspect;
            }
            st.frameAspect = aspect >= 1 ? aspect : PhotoFit.FrameAspect;

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
            // 立体の形：王道昇格と同じ金属の字（フチ・溝あり。ユーザー「金属感あるほうが好き」）。厚みを深く、外のフチは細く（字の細さを保つ）
            var style = PachiTextBuilder.DefaultStyle();
            style.sideProfile = new[] { new Vector2(0, 32), new Vector2(0, settings.ObiDepthValue) };
            const float rimK = 0.6f;
            style.silhouette *= rimK;
            for (int r = 0; r < style.rimSteps.Length; r++)
            {
                var q = style.rimSteps[r];
                style.rimSteps[r] = new PachiTextBuilder.Step(q.inner * rimK, q.zIn, q.outer * rimK, q.zOut, q.part);
            }
            foreach (var ch in chars)
            {
                if (templates.ContainsKey(ch)) continue;
                bool has = band.GlyphIndex(char.ConvertToUtf32(ch, 0)) != 0;
                var go = PachiText.Create(new[] { ch }, Tier.Gold, has ? BandFont : settings.FontName, style);
                go.transform.SetParent(st.root.transform, false);
                // 厚みを赤銅にする（表の面・フチは金のまま）
                var gm = go.GetComponent<MeshRenderer>().sharedMaterials;
                gm[4].SetTexture("_Ramp", PachiRamps.Get("obiSide"));
                gm[4].SetColor("_DeepTint", new Color(1f, 0.1f, 0.02f));
                gm[4].SetFloat("_DeepAmount", settings.ObiRedValue);
                gm[2].SetTexture("_Ramp", PachiRamps.Get("obiFloor"));   // 字のすき間・きわ：黒ではなく深い赤茶
                gm[2].SetFloat("_Boost", 1.2f);
                Shear(go.GetComponent<PachiText.Rig>().mesh, ObiPlan.Slant);
                var b = go.GetComponent<MeshRenderer>().bounds;
                go.SetActive(false);
                templates[ch] = (go, new Vector3(b.center.x, b.center.y, b.min.z));
                if (b.size.y > maxH) { maxH = b.size.y; refCy = b.center.y; }
            }
            float scale = ObiPlan.LetterH / maxH;

            // 帯：画面に見える範囲（＋少し外）だけに切る（短いほど目安の速さで 10 秒につながる）
            // 上の列・下の列は、指で合わせたずらしと大きさで動かす（列ごとの入れ物を、列のまん中を中心に動かす・大きくする）
            bool straight = settings.ObiLayout == ObiLayout.Straight;
            float sy = halfH * ObiPlan.StraightHeight * (dist + ObiPlan.StraightZ) / dist;   // 奥行きの分も入れて、画面の上下の同じ割合の高さに
            var topPts = straight ? ObiPlan.StraightPoints(true, 600, halfW * 1.8f, sy, ObiPlan.StraightSlant) : ObiPlan.ArcPoints(ObiPlan.TopFor(aspect), true, 600);
            var botPts = straight ? ObiPlan.StraightPoints(false, 600, halfW * 1.8f, sy, ObiPlan.StraightSlant) : ObiPlan.ArcPoints(ObiPlan.BottomFor(aspect), false, 600);
            var fits = new[] { (settings.ObiTopOffset, settings.ObiTopScale), (settings.ObiBottomOffset, settings.ObiBottomScale) };
            var pivots = new[] { topPts[300], botPts[300] };
            var lanes = new[]
            {
                st.MakeLane(topPts, -1, halfW, halfH, dist, chars.Count, pivots[0], fits[0].Item1, fits[0].Item2),
                st.MakeLane(botPts, +1, halfW, halfH, dist, chars.Count, pivots[1], fits[1].Item1, fits[1].Item2),
            };
            for (int li = 0; li < 2; li++)
            {
                var outer = new GameObject(li == 0 ? "LaneTop" : "LaneBottom").transform;
                outer.SetParent(st.root.transform, false);
                var inner = new GameObject("Inner").transform;
                inner.SetParent(outer, false);
                inner.localPosition = -pivots[li];
                st.laneOuter[li] = outer;
                st.lanePivot[li] = pivots[li];
                st.SetLaneFit(li, fits[li].Item1, fits[li].Item2);
                lanes[li].parent = inner;
            }
            int unit = chars.Count + 1;
            int spinCount = settings.ObiSpinCount;
            var rnd = new System.Random(20261115);   // キラキラの散らし方（いつも同じ）
            var starMat = new Material(Shader.Find("PachiMoji/Glint"));
            starMat.SetColor("_Color", new Color(1f, 0.96f, 0.8f));
            starMat.SetFloat("_Angle", 0.2f);
            foreach (var lane in lanes)
            {
                if (settings.ObiGlow != ObiGlow.None)
                {
                    var fire = ObiBand.Build(lane.pts, lane.len, st.camPos, ObiPlan.BandW * settings.ObiGlowWidth, lane.loop, lane.speed, lane.dir, lane.parent);
                    fire.SetFloat("_Intensity", fire.GetFloat("_Intensity") * settings.ObiGlowBright);
                    fire.SetFloat("_Gold", settings.ObiGlow == ObiGlow.Gold ? 1 : 0);
                    st.fires.Add(fire);
                }
                int slots = Mathf.RoundToInt(lane.loop / ObiPlan.Spacing);
                for (int k = 0; k < slots; k++)
                {
                    int ci = k % unit;
                    if (ci == chars.Count) continue;   // 言葉と言葉のあいだ（炎だけ）
                    var tp = templates[chars[ci]];
                    var holder = new GameObject("Letter");
                    holder.transform.SetParent(lane.parent, false);
                    // 字の向きと回転をつける入れ物（字のまん中を中心に回るように、字はこの下に置く）
                    var spin = new GameObject("Spin").transform;
                    spin.SetParent(holder.transform, false);
                    var copy = Object.Instantiate(tp.go, spin);
                    copy.SetActive(true);
                    copy.transform.localScale = new Vector3(scale * ObiPlan.Squeeze, scale, scale);
                    copy.transform.localPosition = -new Vector3(tp.c.x * scale * ObiPlan.Squeeze, refCy * scale, tp.c.z * scale);
                    // 角のキラッは4文字に1つだけ
                    var rig = copy.GetComponent<PachiText.Rig>();
                    for (int g = 0; g < rig.glints.Count; g++) rig.glints[g].gameObject.SetActive(k % 4 == 0 && g == 0);
                    // まっすぐの帯は、上の列も下の列も同じ向き（傾きをそろえる）
                    var turn = straight || lane.dir < 0 ? TopTurn : BottomTurn;
                    spin.localRotation = turn;
                    if (spinCount > 0) st.spins.Add((spin, turn, k, spinCount));
                    st.letters.Add((holder.transform, lane, k * ObiPlan.Spacing, 0, false));
                }

                // 星のキラキラ：帯の上に散らして、文字といっしょに流しながら、ばらばらの間でまたたかせる
                int starCount = Mathf.Max(2, Mathf.RoundToInt(lane.loop / 2.4f));
                for (int j = 0; j < starCount; j++)
                {
                    var holder = new GameObject("Star");
                    holder.transform.SetParent(lane.parent, false);
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(holder.transform, false);
                    q.transform.localPosition = new Vector3(0, 0, -0.3f);   // 文字より手前
                    q.transform.localScale = Vector3.one * (0.35f + 0.5f * (float)rnd.NextDouble());
                    var r = q.GetComponent<MeshRenderer>();
                    r.sharedMaterial = starMat;
                    st.stars.Add((r, (float)rnd.NextDouble(), 2 + rnd.Next(4), 2.2f + 2.6f * (float)rnd.NextDouble()));
                    float lift = ((float)rnd.NextDouble() * 2 - 1) * ObiPlan.BandW * 0.8f;
                    st.letters.Add((holder.transform, lane, (j + (float)rnd.NextDouble()) * lane.loop / starCount, lift, false));
                }
            }

            // 写真：帯の奥に、書き出しの枠（9:16）を覆う板
            st.frameW = 2 * halfW * (PhotoZ + dist) / dist;
            if (settings.ObiBlueBack)
            {
                // ブルーバック：背景は青一色（写真・模様は出さない。動画編集で青を抜いて重ねる）
                st.cam.backgroundColor = BlueBack;
            }
            else if (photo != null)
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
            var size = PhotoFit.Size(photoAspect, zoom, frameAspect) * frameW;
            var off = PhotoFit.ClampOffset(offset, photoAspect, zoom, frameAspect) * frameW;
            photoQuad.localPosition = new Vector3(off.x, off.y, PhotoZ);
            photoQuad.localScale = new Vector3(size.x, size.y, 1);
        }

        /// <summary>ブルーバックの青（動画編集の「クロマキー」で抜く。金の字の反対の色なので、字のまわりの光のにじみが字に近い色にならない）</summary>
        public static readonly Color BlueBack = new Color(0f, 0.3f, 1f, 1);

        readonly Transform[] laneOuter = new Transform[2];
        readonly Vector3[] lanePivot = new Vector3[2];

        /// <summary>列（0＝上・1＝下）のずらし（画面の高さ 10 の単位）と大きさを変える。組み立て直さずに動かす</summary>
        public void SetLaneFit(int lane, Vector2 offset, float scale)
        {
            if (laneOuter[lane] == null) return;
            laneOuter[lane].localPosition = lanePivot[lane] + (Vector3)EffectSettings.ClampObiOffset(offset);
            laneOuter[lane].localScale = Vector3.one * EffectSettings.ClampObiScale(scale);
        }

        /// <summary>画面の縦の長さ（0〜1）を、帯の動く単位に直す倍率</summary>
        public float UnitsPerScreenHeight => 2 * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * -camPos.z;

        float frameAspect = PhotoFit.FrameAspect;   // 写真で覆う枠の横÷縦（縦の書き出しは 9:16、横は 16:9）
        /// <summary>横長のとき、基準の画面の高さのどれだけを映すか</summary>
        const float LandscapeHeight = 0.62f;

        public bool HasPhoto => photoQuad != null;
        public float PhotoAspect => photoAspect;

        Lane MakeLane(Vector3[] pts, int dir, float halfW, float halfH, float dist, int wordChars, Vector3 pivot, Vector2 offset, float scale)
        {
            // 画面の外へ文字1.5個ぶんまでを残す
            float margin = ObiPlan.LetterH * 1.5f;
            int first = -1, last = -1;
            for (int i = 0; i < pts.Length; i++)
            {
                var q = pivot + (Vector3)offset + (pts[i] - pivot) * scale;   // 指で合わせたあとの位置で、見えるかを決める
                float k = dist / (q.z + dist);
                bool vis = Mathf.Abs(q.x * k) <= halfW + margin * scale && Mathf.Abs(q.y * k) <= halfH + margin * scale;
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

        // 帯の文字の書体：明朝（細くて長い字。ユーザーの指定）。その書体に無い字だけ設定の書体
        public static string BandFont = "ZenOldMinchoBlack";

        // 文字の向き：上の列は下から見上げ（厚みが下・奥へ）、下の列は上から見下ろす（厚みが上・奥へ）。どちらも少し横を向けて厚みを見せる
        static readonly Quaternion TopTurn = Quaternion.Euler(22, -24, 0);
        static readonly Quaternion BottomTurn = Quaternion.Euler(-18, -24, 0);

        // 字を斜めにする：上へ行くほど x をずらす（立体の点を直接動かす。1文字の型ごとに1回だけ）
        static void Shear(Mesh mesh, float k)
        {
            var v = mesh.vertices;
            float cy = mesh.bounds.center.y;
            for (int i = 0; i < v.Length; i++) v[i].x += (v[i].y - cy) * k;
            mesh.vertices = v;
            mesh.RecalculateBounds();
        }

        Material back;
        // キラキラ（描くもの・またたきのずれ・10 秒でまたたく回数・明るさ）
        readonly List<(Renderer r, float phase, int times, float bright)> stars = new List<(Renderer, float, int, float)>();
        MaterialPropertyBlock block;

        readonly List<Material> fires = new List<Material>();   // 帯の炎（時刻を入れて流す）

        // 字の回転（入れ物・もとの向き・並びの番号・10 秒の回数）
        readonly List<(Transform node, Quaternion turn, int slot, int count)> spins = new List<(Transform, Quaternion, int, int)>();

        public void Evaluate(float t)
        {
            foreach (var sp in spins)
                sp.node.localRotation = Quaternion.AngleAxis(ObiPlan.SpinAngle(t, sp.slot, sp.count), Vector3.up) * sp.turn;
            foreach (var f in fires) f.SetFloat("_T", t);
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
            // アプリの中では Destroy はこのコマの終わりまで待つので、先に見えなくする。
            // （書き出しを始めるとき、片づけた画面の帯が書き出しの最初の 2 コマに二重に写っていた）
            if (root != null) root.SetActive(false);
            if (volumeGo != null) volumeGo.SetActive(false);
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
            root = null; volumeGo = null; profile = null; cam = null; photoQuad = null; back = null;
            letters.Clear(); fires.Clear(); stars.Clear(); spins.Clear();
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
