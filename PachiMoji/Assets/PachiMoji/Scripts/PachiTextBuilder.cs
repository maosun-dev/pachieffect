using System;
using System.Collections.Generic;
using Clipper2Lib;
using LibTessDotNet;
using UnityEngine;
using UnityEngine.Rendering;
using Mesh = UnityEngine.Mesh;

namespace PachiMoji
{
    /// <summary>
    /// パチンコ文字の立体を作る。
    /// 文字の輪郭を少しずつ太らせた線（オフセット）を何本も作り、
    /// 線と線の間を「面取りの斜面」「黒い溝」「太い外フチ」として前後にずらしてつなぐ。
    /// 面はすべて平らな三角形のまま光らせるので、面ごとに明るさがパキッと変わる（カクカク）。
    /// 単位は 1文字＝1000（書体の em を 1000 にそろえる）。
    /// </summary>
    public static class PachiTextBuilder
    {
        /// <summary>部位ごとの材質の番号（Mesh のサブメッシュ番号）</summary>
        public enum Part { Face = 0, Line = 1, Dark = 2, Rim = 3, Side = 4 }
        public const int PartCount = 5;

        /// <summary>断面の1段。輪郭から inner だけ太らせた線（奥行き zIn）と outer だけ太らせた線（奥行き zOut）の間を埋める</summary>
        public struct Step
        {
            public float inner, zIn, outer, zOut;
            public Part part;
            public Step(float inner, float zIn, float outer, float zOut, Part part)
            { this.inner = inner; this.zIn = zIn; this.outer = outer; this.zOut = zOut; this.part = part; }
        }

        public class Style
        {
            public float faceInset = -4;     // 表の面は輪郭より少し内側
            // 表の面の丸み：輪郭から内側へ (offset, 奥行き) の段を重ねて、中ほどを手前へ盛り上げる（面ごとに映り込みが変わる）
            public Vector2[] faceBulge = { new Vector2(-4, 0), new Vector2(-14, -3), new Vector2(-30, -5) };
            // 側面の段々：外周から (offset, 奥行き) を順にたどる。offset が変わる所は面取りの段になる
            public Vector2[] sideProfile =
            {
                new Vector2(0, 32), new Vector2(0, 150), new Vector2(-12, 160), new Vector2(-12, 182), new Vector2(0, 194),
                new Vector2(0, 300), new Vector2(-12, 310), new Vector2(-12, 332), new Vector2(0, 344), new Vector2(0, 470),
            };
            public Step[] steps;             // 字の輪郭まわりの段（細い線・溝）。offset は字の輪郭から
            public float silhouette = 95;    // 言葉全体の外周（字の輪郭からこれだけ太らせ、穴は埋める）
            public float floorZ = 40;        // 字のすき間の、沈んだ床の奥行き
            public Step[] rimSteps;          // 外周の太いフチ。offset は外周から（マイナス＝内側）
            public float tracking = -60;     // 字間（マイナスで詰めて、フチをつなげる）
            public float lineGap = -40;      // 行間
        }

        /// <summary>
        /// 参考「確定」を見て決めた断面。
        /// 字の輪郭：黒線 → 細い明るい線 → 黒い溝 → 沈んだ暗い床（字のすき間）
        /// 言葉の外周：山形の太い金フチ → 黒いきわ → 側面
        /// </summary>
        public static Style DefaultStyle() => new Style
        {
            steps = new[]
            {
                new Step(-4, 0, 2, 5, Part.Dark),      // 表の面のきわの黒い線
                new Step(2, 5, 9, 11, Part.Line),      // 細い明るい線（面取り）
                new Step(9, 11, 17, 34, Part.Dark),    // 黒い溝（奥へ落ちる）
            },
            rimSteps = new[]
            {
                new Step(-58, 40, -34, 4, Part.Rim),   // 太いフチの内側の斜面（手前へ上がる）
                new Step(-34, 4, -22, 4, Part.Rim),    // 太いフチの細い平ら
                new Step(-22, 4, -7, 26, Part.Rim),    // 太いフチの外側の斜面
                new Step(-7, 26, 0, 32, Part.Dark),    // いちばん外の黒いきわ
            },
        };

        // ───────── 文字の並びから輪郭を作る ─────────

        public static PathsD LayoutOutline(TtfFont font, IList<string> lines, Style style, int curveSegs = 6)
        {
            float k = 1000f / font.UnitsPerEm;
            var all = new PathsD();
            float lineH = 1000 + style.lineGap;
            var lineWidths = new List<float>();
            var perLine = new List<PathsD>();
            foreach (var line in lines)
            {
                var lp = new PathsD();
                float x = 0;
                int count = 0;
                foreach (int cp in Codepoints(line))
                {
                    int g = font.GlyphIndex(cp);
                    foreach (var contour in font.Outline(g, curveSegs))
                    {
                        var path = new PathD(contour.Count);
                        foreach (var p in contour) path.Add(new PointD(x + p.x * k, p.y * k));
                        lp.Add(path);
                    }
                    x += font.Advance(g) * k + style.tracking;
                    count++;
                }
                if (count > 0) x -= style.tracking;
                lineWidths.Add(x);
                perLine.Add(lp);
            }
            // 行ごとに中央そろえ、全体を原点中心に
            float totalH = lines.Count * lineH - style.lineGap;
            float baseCenter = (font.Ascender + font.Descender) * k * 0.5f;
            for (int i = 0; i < perLine.Count; i++)
            {
                float ox = -lineWidths[i] * 0.5f;
                float oy = totalH * 0.5f - lineH * i - 500 - baseCenter + 0;
                foreach (var path in perLine[i])
                {
                    var moved = new PathD(path.Count);
                    foreach (var p in path) moved.Add(new PointD(p.x + ox, p.y + oy));
                    all.Add(moved);
                }
            }
            // 重なりや向きの乱れをならす
            return Clipper.Union(all, FillRule.NonZero);
        }

        static IEnumerable<int> Codepoints(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length) { yield return char.ConvertToUtf32(s[i], s[i + 1]); i++; }
                else yield return s[i];
            }
        }

        // ───────── 立体を組み立てる ─────────

        class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] tris = new List<int>[PartCount];
            public Builder() { for (int i = 0; i < PartCount; i++) tris[i] = new List<int>(); }

            /// <summary>平らな三角形を1枚足す。向きが want と逆なら裏返す。</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 want, Part part, float shade = 0)
            {
                var nn = Vector3.Cross(b - a, c - a);
                if (nn.sqrMagnitude < 1e-8f) return;
                if (Vector3.Dot(nn, want) < 0) { var t = b; b = c; c = t; nn = -nn; }
                nn.Normalize();
                int i0 = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                n.Add(nn); n.Add(nn); n.Add(nn);
                uv.Add(new Vector2(shade, 0)); uv.Add(new Vector2(shade, 0)); uv.Add(new Vector2(shade, 0));
                // Unity では Cross(b-a, c-a) が見る人の側を向く並び＝時計回り＝表
                tris[(int)part].Add(i0); tris[(int)part].Add(i0 + 1); tris[(int)part].Add(i0 + 2);
            }
        }

        static PathsD Offset(PathsD src, float d)
        {
            if (Mathf.Abs(d) < 0.01f) return Clipper.SimplifyPaths(src, 0.4);
            // 角を丸めずに尖らせる（Miter）。尖りすぎる角だけ切る
            var r = Clipper.InflatePaths(src, d, JoinType.Miter, EndType.Polygon, 2.5);
            return Clipper.SimplifyPaths(r, 0.4);
        }

        static long Key(double x, double y) => ((long)Math.Round(x * 100) << 32) ^ ((long)Math.Round(y * 100) & 0xffffffffL);

        /// <summary>言葉全体の外周（穴を埋めたもの）</summary>
        public static PathsD Silhouette(PathsD outline, Style style)
        {
            var big = Offset(outline, style.silhouette);
            var solid = new PathsD();
            foreach (var p in big) if (Clipper.IsPositive(p)) solid.Add(p);
            return Clipper.Union(solid, FillRule.NonZero);
        }

        public static Mesh Build(PathsD outline, Style style, float scale = 0.001f)
        {
            var b = new Builder();
            var cache = new Dictionary<float, PathsD>();
            PathsD Off(float dd)
            {
                if (!cache.TryGetValue(dd, out var p)) { p = Offset(outline, dd); cache[dd] = p; }
                return p;
            }
            var sil = Silhouette(outline, style);
            var silCache = new Dictionary<float, PathsD>();
            PathsD OffS(float dd)
            {
                if (!silCache.TryGetValue(dd, out var p)) { p = Offset(sil, dd); silCache[dd] = p; }
                return p;
            }
            var front = new Vector3(0, 0, -1);

            // 表の面（内側へ段を重ねて中ほどを盛り上げる）
            var fb = style.faceBulge;
            for (int i = 0; i + 1 < fb.Length; i++)
                Fill(b, Off(fb[i].x), Off(fb[i + 1].x), fb[i].y, fb[i + 1].y, front, Part.Face);
            Fill(b, Off(fb[fb.Length - 1].x), null, fb[fb.Length - 1].y, fb[fb.Length - 1].y, front, Part.Face);

            // 字の輪郭まわりの段（細い線・溝）
            foreach (var s in style.steps)
                Fill(b, Off(s.outer), Off(s.inner), s.zOut, s.zIn, front, s.part);

            // 字のすき間の沈んだ床（太いフチの内側〜溝の底）
            var lastIn = style.steps[style.steps.Length - 1];
            var firstRim = style.rimSteps[0];
            Fill(b, OffS(firstRim.inner), Off(lastIn.outer), style.floorZ, lastIn.zOut, front, Part.Dark);

            // 外周の太いフチ
            foreach (var s in style.rimSteps)
                Fill(b, OffS(s.outer), OffS(s.inner), s.zOut, s.zIn, front, s.part);

            // 側面（押し出し）：段々に面取りしながら奥へ。最後に裏ふた
            var sp = style.sideProfile;
            for (int i = 0; i + 1 < sp.Length; i++)
            {
                var p0 = sp[i]; var p1 = sp[i + 1];
                if (Mathf.Approximately(p0.x, p1.x)) Wall(b, OffS(p0.x), p0.y, p1.y);
                else if (p1.x < p0.x) Fill(b, OffS(p0.x), OffS(p1.x), p0.y, p1.y, new Vector3(0, 0, 1), Part.Side);   // 内へ入る段（奥向き）
                else Fill(b, OffS(p1.x), OffS(p0.x), p1.y, p0.y, front, Part.Rim);                                    // 外へ出る段（手前向き・光る）
            }
            var back = sp[sp.Length - 1];
            Fill(b, OffS(back.x), null, back.y, back.y, new Vector3(0, 0, 1), Part.Side);

            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            for (int i = 0; i < b.v.Count; i++) b.v[i] *= scale;
            mesh.SetVertices(b.v);
            mesh.SetNormals(b.n);
            mesh.SetUVs(0, b.uv);
            mesh.subMeshCount = PartCount;
            for (int i = 0; i < PartCount; i++) mesh.SetTriangles(b.tris[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>外フチの尖った角（キラッと光らせる場所）を、離れたところから max 個選ぶ</summary>
        public static List<Vector3> GlintPoints(PathsD outline, Style style, int max, float scale = 0.001f)
        {
            var ridge = style.rimSteps[0];   // 太いフチの山の頂上
            var paths = Offset(Silhouette(outline, style), ridge.outer);
            var cands = new List<(float sharp, Vector2 p)>();
            foreach (var path in paths)
            {
                if (!Clipper.IsPositive(path)) continue; // 穴の縁は使わない
                int c = path.Count;
                for (int i = 0; i < c; i++)
                {
                    var a = path[(i - 1 + c) % c]; var p = path[i]; var q = path[(i + 1) % c];
                    var d0 = new Vector2((float)(p.x - a.x), (float)(p.y - a.y)).normalized;
                    var d1 = new Vector2((float)(q.x - p.x), (float)(q.y - p.y)).normalized;
                    float cross = d0.x * d1.y - d0.y * d1.x;   // 正なら外に尖った角（反時計回りの輪郭）
                    float turn = Vector2.Angle(d0, d1);
                    if (cross > 0 && turn > 70) cands.Add((turn + (float)p.y * 0.02f, new Vector2((float)p.x, (float)p.y)));
                }
            }
            cands.Sort((x, y) => y.sharp.CompareTo(x.sharp));
            var picked = new List<Vector3>();
            foreach (var cnd in cands)
            {
                bool far = true;
                foreach (var pk in picked) if (Vector2.Distance(pk / scale, cnd.p) < 420) { far = false; break; }
                if (!far) continue;
                picked.Add(new Vector3(cnd.p.x, cnd.p.y, ridge.zOut - 30) * scale);
                if (picked.Count >= max) break;
            }
            return picked;
        }

        /// <summary>輪郭をまっすぐ奥へ伸ばした壁（z0 → z1）</summary>
        static void Wall(Builder b, PathsD edge, float z0, float z1)
        {
            foreach (var path in edge)
            {
                int c = path.Count;
                for (int i = 0; i < c; i++)
                {
                    var p = path[i]; var q = path[(i + 1) % c];
                    var a0 = new Vector3((float)p.x, (float)p.y, z0);
                    var b0 = new Vector3((float)q.x, (float)q.y, z0);
                    var a1 = new Vector3((float)p.x, (float)p.y, z1);
                    var b1 = new Vector3((float)q.x, (float)q.y, z1);
                    // Clipper の外周は反時計回り（面積が正）なので、外向きは (dy, -dx)
                    var want = new Vector3((float)(q.y - p.y), (float)-(q.x - p.x), 0);
                    b.Tri(a0, b0, b1, want, Part.Side, 1);
                    b.Tri(a0, b1, a1, want, Part.Side, 1);
                }
            }
        }

        /// <summary>outer の内側から inner を抜いた領域を三角形で埋める。inner 側の点は zIn、outer 側の点は zOut に置く（＝斜面になる）</summary>
        static void Fill(Builder b, PathsD outer, PathsD inner, float zOut, float zIn, Vector3 want, Part part)
        {
            PathsD region = inner == null ? outer : Clipper.Difference(outer, inner, FillRule.NonZero);
            if (region.Count == 0) return;
            var innerKeys = new HashSet<long>();
            if (inner != null)
                foreach (var path in inner) foreach (var p in path) innerKeys.Add(Key(p.x, p.y));

            var tess = new Tess();
            foreach (var path in region)
            {
                var cv = new ContourVertex[path.Count];
                for (int i = 0; i < path.Count; i++) cv[i] = new ContourVertex(new Vec3((float)path[i].x, (float)path[i].y, 0));
                tess.AddContour(cv);
            }
            tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3);
            var vs = tess.Vertices; var el = tess.Elements;
            for (int t = 0; t < tess.ElementCount; t++)
            {
                var p = new Vector3[3];
                for (int j = 0; j < 3; j++)
                {
                    var pos = vs[el[t * 3 + j]].Position;
                    bool isInner = inner != null && innerKeys.Contains(Key(pos.X, pos.Y));
                    p[j] = new Vector3(pos.X, pos.Y, isInner ? zIn : zOut);
                }
                b.Tri(p[0], p[1], p[2], want, part);
            }
        }
    }
}
