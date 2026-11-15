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
            Material[] hotMats = null;
            float maxH = 0.001f, refCy = 0;
            foreach (var ch in chars)
            {
                if (templates.ContainsKey(ch)) continue;
                var go = PachiText.Create(new[] { ch }, Tier.Gold, settings.FontName);
                go.transform.SetParent(st.root.transform, false);
                // 塗りを「焼けた金・赤い縁・赤い厚み」に替える（王道昇格の黒い縁どりの金とは別の塗り）
                var mr = go.GetComponent<MeshRenderer>();
                foreach (var old in mr.sharedMaterials) Kill(old);
                hotMats ??= HotMaterials();
                mr.sharedMaterials = hotMats;
                go.GetComponent<PachiText.Rig>().mats = hotMats;
                var b = go.GetComponent<MeshRenderer>().bounds;
                go.SetActive(false);
                templates[ch] = (go, new Vector3(b.center.x, b.center.y, b.min.z));
                if (b.size.y > maxH) { maxH = b.size.y; refCy = b.center.y; }
            }
            float scale = ObiPlan.LetterH / maxH;

            // 帯：画面に見える範囲（＋少し外）だけに切る（短いほど目安の速さで 10 秒につながる）
            var lanes = new[]
            {
                st.MakeLane(ObiPlan.ArcPoints(ObiPlan.TopFor(aspect), true, 600), -1, halfW, halfH, dist, chars.Count),
                st.MakeLane(ObiPlan.ArcPoints(ObiPlan.BottomFor(aspect), false, 600), +1, halfW, halfH, dist, chars.Count),
            };
            int unit = chars.Count + 1;
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

        // 文字の材質（サブメッシュ順：表の面・細い線・きわの溝・外フチ・側面）
        static Material[] HotMaterials()
        {
            Material M(string ramp, float boost, bool side = false)
            {
                var m = new Material(Shader.Find("PachiMoji/Metal"));
                m.SetTexture("_Ramp", PachiRamps.Get(ramp));
                m.SetFloat("_Boost", boost);
                if (side) { m.SetFloat("_Horizon", -0.7f); m.SetFloat("_Diffuse", 0.5f); }
                return m;
            }
            return new[] { M("hot", 2.6f), M("hotLine", 3f), M("hotGroove", 1.2f), M("hotRim", 2.2f), M("hotSide", 1.4f, true) };
        }

        readonly List<Material> fires = new List<Material>();   // 帯の炎（時刻を入れて流す）

        public void Evaluate(float t)
        {
            foreach (var f in fires) f.SetFloat("_T", t);
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
            root = null; volumeGo = null; profile = null; cam = null; photoQuad = null;
            letters.Clear();
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
