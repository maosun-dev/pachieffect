using System;
using System.Collections.Generic;

namespace PachiMoji
{
    public enum InputProblem { None, Empty, TooLong, MissingGlyph }

    /// <summary>入力を確かめた結果。Lines は前後の空白を落とし、空の行を抜いたもの</summary>
    public readonly struct InputCheck
    {
        public readonly InputProblem Problem;
        public readonly string[] Lines;
        public readonly string BadChar;   // 書体に無かった最初の1字（MissingGlyph のときだけ）
        public readonly int BadLine;      // その字がある入力欄の番号（1 か 2）
        public readonly int BadIndex;     // 入力欄の中で何文字目か（1 から。見たままの位置）
        public bool CanPlay => Problem == InputProblem.None;

        public InputCheck(InputProblem problem, string[] lines, string badChar = null, int badLine = 0, int badIndex = 0)
        {
            Problem = problem; Lines = lines; BadChar = badChar; BadLine = badLine; BadIndex = badIndex;
        }
    }

    /// <summary>入力の決まり：2行まで・1行6文字まで・書体にない字は使えない</summary>
    public static class InputRules
    {
        public const int MaxLines = 2, MaxChars = 6;

        /// <param name="hasGlyph">その文字コードが今の書体にあるか</param>
        public static InputCheck Check(string line1, string line2, Func<int, bool> hasGlyph)
        {
            var lines = new List<string>(MaxLines);
            foreach (var raw in new[] { line1, line2 })
            {
                var s = (raw ?? "").Trim();
                if (s.Length > 0) lines.Add(s);
            }
            var arr = lines.ToArray();
            if (arr.Length == 0) return new InputCheck(InputProblem.Empty, arr, null);

            foreach (var s in arr)
                if (Codepoints(s).Count > MaxChars) return new InputCheck(InputProblem.TooLong, arr, null);

            // 書体を確かめるのは、入力欄に打ったままの文字列で（場所を見たままの番号で知らせるため）
            var raws = new[] { line1, line2 };
            for (int li = 0; li < raws.Length; li++)
            {
                var cps = Codepoints(raws[li] ?? "");
                for (int i = 0; i < cps.Count; i++)
                {
                    int cp = cps[i];
                    if (cp <= 0xFFFF && char.IsWhiteSpace((char)cp)) continue;
                    // 0xFFFF を超える字（絵文字など）は、舞台が1文字ずつ切り出せないので断る
                    if (cp > 0xFFFF || !hasGlyph(cp))
                        return new InputCheck(InputProblem.MissingGlyph, arr,
                            cp > 0xFFFF ? char.ConvertFromUtf32(cp) : ((char)cp).ToString(), li + 1, i + 1);
                }
            }
            return new InputCheck(InputProblem.None, arr, null);
        }

        /// <summary>
        /// 画面に出す文（問題が無ければ空）。showChar が偽なら、出せない字そのものの代わりに場所で知らせる
        /// （絵文字などは説明文の書体でも出せず、□ になるため）
        /// </summary>
        public static string Message(InputCheck c, bool showChar = true) => c.Problem switch
        {
            InputProblem.Empty => "言葉を入れると PUSH できます",
            InputProblem.TooLong => "1行は6文字までです",
            InputProblem.MissingGlyph when showChar => $"「{c.BadChar}」はこの書体で出せません。ほかの字にしてください",
            InputProblem.MissingGlyph => $"{c.BadLine}行目の{c.BadIndex}文字目は、この書体で出せません。ほかの字にしてください",
            _ => "",
        };

        // サロゲートの組を1文字として数える（片方だけ残った壊れた字も1文字）
        static List<int> Codepoints(string s)
        {
            var list = new List<int>(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    list.Add(char.ConvertToUtf32(s[i], s[i + 1]));
                    i++;
                }
                else list.Add(s[i]);
            }
            return list;
        }
    }
}
