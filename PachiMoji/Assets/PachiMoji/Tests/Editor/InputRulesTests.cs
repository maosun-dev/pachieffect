using System;
using NUnit.Framework;
using UnityEngine;

namespace PachiMoji.Tests
{
    /// <summary>入力の決まり（2行まで・1行6文字まで・書体にない字）</summary>
    public class InputRulesTests
    {
        static readonly Func<int, bool> All = _ => true;

        [Test] public void 二行とも空なら押せない() => Assert.AreEqual(InputProblem.Empty, InputRules.Check("", "", All).Problem);
        [Test] public void 空白だけも空() => Assert.AreEqual(InputProblem.Empty, InputRules.Check(" 　", "　", All).Problem);
        [Test] public void 前後の空白は落とす() => CollectionAssert.AreEqual(new[] { "定時", "退社" }, InputRules.Check(" 定時　", "退社 ", All).Lines);
        [Test] public void 一行目が空なら二行目を一行にする() => CollectionAssert.AreEqual(new[] { "退社" }, InputRules.Check("", "退社", All).Lines);
        [Test] public void 六文字ちょうどは通る() => Assert.IsTrue(InputRules.Check("脳汁ドバドバ", "", All).CanPlay);
        [Test] public void 七文字は長すぎ() => Assert.AreEqual(InputProblem.TooLong, InputRules.Check("脳汁ドバドバ！", "", All).Problem);
        [Test] public void 二行目の七文字も長すぎ() => Assert.AreEqual(InputProblem.TooLong, InputRules.Check("定時", "脳汁ドバドバ！", All).Problem);
        [Test] public void 中の空白は一文字に数える() => Assert.AreEqual(InputProblem.TooLong, InputRules.Check("脳汁 ドバドバ", "", All).Problem);
        [Test] public void 何も入っていなくても落ちない() => Assert.AreEqual(InputProblem.Empty, InputRules.Check(null, null, All).Problem);

        [Test]
        public void 書体にない字を知らせる()
        {
            var c = InputRules.Check("定時", "退社", cp => cp != '社');
            Assert.AreEqual(InputProblem.MissingGlyph, c.Problem);
            Assert.AreEqual("社", c.BadChar);
            StringAssert.Contains("「社」", InputRules.Message(c));
        }

        [Test]
        public void 空白は書体を確かめない()
            => Assert.IsTrue(InputRules.Check("定 時", "", cp => cp != ' ').CanPlay);

        [Test]
        public void 絵文字は二つ分の文字コードでも一文字として知らせる()
        {
            // 0xFFFF を超える字は、舞台が1文字ずつ切り出せないので書体にあっても断る
            var c = InputRules.Check("激アツ😀", "", All);
            Assert.AreEqual(InputProblem.MissingGlyph, c.Problem);
            Assert.AreEqual("😀", c.BadChar);
        }

        [Test]
        public void 絵文字は六文字の数えでも一文字() => Assert.AreEqual(InputProblem.MissingGlyph, InputRules.Check("脳汁ドバド😀", "", All).Problem);

        [Test]
        public void 片方だけの壊れた字でも落ちずに知らせる()
        {
            string broken = "定" + (char)0xD83D;
            Assert.AreEqual(InputProblem.MissingGlyph, InputRules.Check(broken, "", cp => cp < 0xD800 || cp > 0xDFFF).Problem);
        }

        [Test]
        public void 書体にない字の場所を入力欄の行と文字の番号で知らせる()
        {
            var c = InputRules.Check("", " 退社", cp => cp != '社');   // 1行目が空でも、行は入力欄の番号のまま
            Assert.AreEqual(2, c.BadLine);
            Assert.AreEqual(3, c.BadIndex);                             // 先頭の空白も1文字と数える（見たままの位置）
            Assert.AreEqual("2行目の3文字目は、この書体で出せません。ほかの字にしてください", InputRules.Message(c, false));
        }

        [Test]
        public void 文はそれぞれ決まったもの()
        {
            Assert.AreEqual("言葉を入れると PUSH できます", InputRules.Message(InputRules.Check("", "", All)));
            Assert.AreEqual("1行は6文字までです", InputRules.Message(InputRules.Check("脳汁ドバドバ！", "", All)));
            Assert.AreEqual("", InputRules.Message(InputRules.Check("定時", "", All)));
        }

        [Test]
        public void 本物の書体で確かめる()
        {
            var reggae = new TtfFont(Resources.Load<TextAsset>("Fonts/ReggaeOne").bytes);
            Func<int, bool> has = cp => reggae.GlyphIndex(cp) != 0;
            Assert.IsTrue(InputRules.Check("定時", "退社", has).CanPlay);
            Assert.AreEqual(InputProblem.MissingGlyph, InputRules.Check("♨", "", has).Problem);
        }
    }
}
