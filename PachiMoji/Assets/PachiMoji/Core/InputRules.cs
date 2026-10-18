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
        public bool CanPlay => Problem == InputProblem.None;

        public InputCheck(InputProblem problem, string[] lines, string badChar)
        {
            Problem = problem; Lines = lines; BadChar = badChar;
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

            foreach (var s in arr)
                foreach (int cp in Codepoints(s))
                {
                    if (cp <= 0xFFFF && char.IsWhiteSpace((char)cp)) continue;
                    // 0xFFFF を超える字（絵文字など）は、舞台が1文字ずつ切り出せないので断る
                    if (cp > 0xFFFF || !hasGlyph(cp))
                        return new InputCheck(InputProblem.MissingGlyph, arr, cp > 0xFFFF ? char.ConvertFromUtf32(cp) : ((char)cp).ToString());
                }
            return new InputCheck(InputProblem.None, arr, null);
        }

        /// <summary>画面に出す文（問題が無ければ空）</summary>
        public static string Message(InputCheck c) => c.Problem switch
        {
            InputProblem.Empty => "言葉を入れると PUSH できます",
            InputProblem.TooLong => "1行は6文字までです",
            InputProblem.MissingGlyph => $"「{c.BadChar}」はこの書体で出せません。ほかの字にしてください",
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
