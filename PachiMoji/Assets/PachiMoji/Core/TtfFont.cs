using System;
using System.Collections.Generic;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// TrueType（glyf 形式）の書体ファイルを読んで、文字の輪郭を折れ線で取り出す。
    /// Unity は書体の輪郭を直接くれないので、自前で読む。
    /// </summary>
    public class TtfFont
    {
        readonly byte[] d;
        readonly Dictionary<string, int> tables = new Dictionary<string, int>();
        public readonly int UnitsPerEm;
        public readonly int Ascender, Descender;
        readonly int numGlyphs, numHMetrics, locaFormat;
        readonly int cmapSub; // 使う cmap サブテーブルの位置
        readonly int cmapFormat;

        public TtfFont(byte[] data)
        {
            d = data;
            int n = U16(4);
            for (int i = 0; i < n; i++)
            {
                int r = 12 + i * 16;
                string tag = System.Text.Encoding.ASCII.GetString(d, r, 4);
                tables[tag] = (int)U32(r + 8);
            }
            int head = tables["head"];
            UnitsPerEm = U16(head + 18);
            locaFormat = I16(head + 50);
            numGlyphs = U16(tables["maxp"] + 4);
            int hhea = tables["hhea"];
            Ascender = I16(hhea + 4);
            Descender = I16(hhea + 6);
            numHMetrics = U16(hhea + 34);

            // cmap：Unicode 全域（format 12）を優先し、無ければ BMP（format 4）
            int cmap = tables["cmap"];
            int nSub = U16(cmap + 2);
            int best = -1, bestFmt = 0;
            for (int i = 0; i < nSub; i++)
            {
                int pid = U16(cmap + 4 + i * 8), eid = U16(cmap + 6 + i * 8);
                int off = cmap + (int)U32(cmap + 8 + i * 8);
                int fmt = U16(off);
                bool unicode = pid == 0 || (pid == 3 && (eid == 1 || eid == 10));
                if (!unicode) continue;
                if (fmt == 12 && bestFmt != 12) { best = off; bestFmt = 12; }
                else if (fmt == 4 && bestFmt == 0) { best = off; bestFmt = 4; }
            }
            if (best < 0) throw new Exception("Unicode の cmap がない書体です");
            cmapSub = best; cmapFormat = bestFmt;
        }

        int U16(int o) => (d[o] << 8) | d[o + 1];
        short I16(int o) => (short)((d[o] << 8) | d[o + 1]);
        uint U32(int o) => (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);

        public int GlyphIndex(int codepoint)
        {
            int s = cmapSub;
            if (cmapFormat == 12)
            {
                uint nGroups = U32(s + 12);
                for (int i = 0; i < nGroups; i++)
                {
                    int g = s + 16 + i * 12;
                    uint a = U32(g), b = U32(g + 4);
                    if (codepoint >= a && codepoint <= b) return (int)(U32(g + 8) + (codepoint - a));
                }
                return 0;
            }
            if (codepoint > 0xFFFF) return 0;
            int segX2 = U16(s + 6);
            int ends = s + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
            for (int i = 0; i < segX2 / 2; i++)
            {
                int end = U16(ends + i * 2);
                if (codepoint > end) continue;
                int start = U16(starts + i * 2);
                if (codepoint < start) return 0;
                int delta = I16(deltas + i * 2);
                int ro = U16(ranges + i * 2);
                if (ro == 0) return (codepoint + delta) & 0xFFFF;
                int gi = U16(ranges + i * 2 + ro + (codepoint - start) * 2);
                return gi == 0 ? 0 : (gi + delta) & 0xFFFF;
            }
            return 0;
        }

        public int Advance(int glyph)
        {
            int hmtx = tables["hmtx"];
            int i = Math.Min(glyph, numHMetrics - 1);
            return U16(hmtx + i * 4);
        }

        int GlyphOffset(int glyph, out int length)
        {
            int loca = tables["loca"];
            int a, b;
            if (locaFormat == 0) { a = U16(loca + glyph * 2) * 2; b = U16(loca + glyph * 2 + 2) * 2; }
            else { a = (int)U32(loca + glyph * 4); b = (int)U32(loca + glyph * 4 + 4); }
            length = b - a;
            return tables["glyf"] + a;
        }

        /// <summary>文字の輪郭（閉じた折れ線の集まり）を書体の単位で返す。曲線は segs 分割する。</summary>
        public List<List<Vector2>> Outline(int glyph, int segs = 6)
        {
            var result = new List<List<Vector2>>();
            AppendOutline(glyph, Matrix2x3.Identity, segs, result, 0);
            return result;
        }

        struct Matrix2x3
        {
            public float a, b, c, d, e, f; // x' = a x + c y + e, y' = b x + d y + f
            public static Matrix2x3 Identity => new Matrix2x3 { a = 1, d = 1 };
            public Vector2 Apply(Vector2 p) => new Vector2(a * p.x + c * p.y + e, b * p.x + d * p.y + f);
            public Matrix2x3 Then(Matrix2x3 o) => new Matrix2x3
            {
                // まず this、つぎに o を当てる
                a = o.a * a + o.c * b, b = o.b * a + o.d * b,
                c = o.a * c + o.c * d, d = o.b * c + o.d * d,
                e = o.a * e + o.c * f + o.e, f = o.b * e + o.d * f + o.f,
            };
        }

        void AppendOutline(int glyph, Matrix2x3 m, int segs, List<List<Vector2>> outList, int depth)
        {
            if (depth > 8 || glyph >= numGlyphs) return;
            int o = GlyphOffset(glyph, out int len);
            if (len <= 0) return;
            int nc = I16(o);
            if (nc >= 0) { AppendSimple(o, nc, m, segs, outList); return; }

            // 複合グリフ（部品の組み合わせ）
            int p = o + 10;
            while (true)
            {
                int flags = U16(p), gi = U16(p + 2); p += 4;
                float dx, dy;
                if ((flags & 1) != 0) { dx = I16(p); dy = I16(p + 2); p += 4; }
                else { dx = (sbyte)d[p]; dy = (sbyte)d[p + 1]; p += 2; }
                var cm = Matrix2x3.Identity;
                if ((flags & 8) != 0) { cm.a = cm.d = F2Dot14(p); p += 2; }
                else if ((flags & 0x40) != 0) { cm.a = F2Dot14(p); cm.d = F2Dot14(p + 2); p += 4; }
                else if ((flags & 0x80) != 0) { cm.a = F2Dot14(p); cm.b = F2Dot14(p + 2); cm.c = F2Dot14(p + 4); cm.d = F2Dot14(p + 6); p += 8; }
                if ((flags & 2) != 0) { cm.e = dx; cm.f = dy; }
                AppendOutline(gi, cm.Then(m), segs, outList, depth + 1);
                if ((flags & 0x20) == 0) break;
            }
        }

        float F2Dot14(int o) => I16(o) / 16384f;

        void AppendSimple(int o, int nc, Matrix2x3 m, int segs, List<List<Vector2>> outList)
        {
            var endPts = new int[nc];
            for (int i = 0; i < nc; i++) endPts[i] = U16(o + 10 + i * 2);
            int nPts = nc > 0 ? endPts[nc - 1] + 1 : 0;
            int p = o + 10 + nc * 2;
            int insLen = U16(p); p += 2 + insLen;
            var flags = new byte[nPts];
            for (int i = 0; i < nPts;)
            {
                byte f = d[p++];
                flags[i++] = f;
                if ((f & 8) != 0) { int r = d[p++]; while (r-- > 0 && i < nPts) flags[i++] = f; }
            }
            var xs = new int[nPts]; var ys = new int[nPts];
            int v = 0;
            for (int i = 0; i < nPts; i++)
            {
                byte f = flags[i];
                if ((f & 2) != 0) { int dv = d[p++]; v += (f & 16) != 0 ? dv : -dv; }
                else if ((f & 16) == 0) { v += I16(p); p += 2; }
                xs[i] = v;
            }
            v = 0;
            for (int i = 0; i < nPts; i++)
            {
                byte f = flags[i];
                if ((f & 4) != 0) { int dv = d[p++]; v += (f & 32) != 0 ? dv : -dv; }
                else if ((f & 32) == 0) { v += I16(p); p += 2; }
                ys[i] = v;
            }

            int startPt = 0;
            for (int c = 0; c < nc; c++)
            {
                int end = endPts[c], count = end - startPt + 1;
                if (count < 2) { startPt = end + 1; continue; }
                // 曲線の制御点の間に、暗黙の「線上の点」を補って (on, off, on, ...) の並びにする
                var pts = new List<(Vector2 p, bool on)>();
                for (int i = 0; i < count; i++)
                {
                    int k = startPt + i, kn = startPt + (i + 1) % count;
                    bool on = (flags[k] & 1) != 0, onN = (flags[kn] & 1) != 0;
                    var pk = new Vector2(xs[k], ys[k]);
                    pts.Add((pk, on));
                    if (!on && !onN) pts.Add(((pk + new Vector2(xs[kn], ys[kn])) * 0.5f, true));
                }
                // 線上の点から始める
                int s0 = pts.FindIndex(t => t.on);
                if (s0 < 0) { startPt = end + 1; continue; }
                var poly = new List<Vector2>();
                int nn = pts.Count;
                for (int i = 0; i < nn; i++)
                {
                    var cur = pts[(s0 + i) % nn];
                    if (cur.on)
                    {
                        poly.Add(cur.p);
                    }
                    else
                    {
                        var prev = pts[(s0 + i - 1 + nn) % nn].p;
                        var next = pts[(s0 + i + 1) % nn].p;
                        for (int sIdx = 1; sIdx < segs; sIdx++)
                        {
                            float t = sIdx / (float)segs, u = 1 - t;
                            poly.Add(u * u * prev + 2 * u * t * cur.p + t * t * next);
                        }
                    }
                }
                for (int i = 0; i < poly.Count; i++) poly[i] = m.Apply(poly[i]);
                outList.Add(poly);
                startPt = end + 1;
            }
        }
    }
}
