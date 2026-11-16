namespace PachiMoji
{
    /// <summary>ごほうび広告の読み込み具合</summary>
    public enum AdState { Loading, Ready, Failed }

    /// <summary>「書き出す」を押したときにすること</summary>
    public enum GateAction { ExportNow, ShowAd, Wait, NeedNetwork }

    /// <summary>書き出しの前の判定：広告を消した人はすぐ書く。ほかは広告を最後まで見てから。ネット・広告が無ければ書けない</summary>
    public static class ExportGate
    {
        public const string NotWatched = "最後まで見ると書き出せます";

        public static GateAction Decide(bool showAds, bool online, AdState ad)
        {
            if (!showAds) return GateAction.ExportNow;
            if (!online || ad == AdState.Failed) return GateAction.NeedNetwork;
            return ad == AdState.Ready ? GateAction.ShowAd : GateAction.Wait;
        }

        /// <summary>
        /// 終わりの画面の知らせを、今の判定で決め直す。広告を途中で閉じた知らせ（NotWatched）は、
        /// ネットが切れたとき以外は残す（次に「書き出す」を押したら PachiApp が消す）。
        /// 待ち・ネットの知らせは、直ったら消す。ほかの知らせ（書き出しの失敗など）は残す
        /// </summary>
        public static string NoticeFor(GateAction a, string current)
        {
            string msg = Message(a);
            if (current == NotWatched && a != GateAction.NeedNetwork) return NotWatched;
            if (msg != "") return msg;
            return current == Message(GateAction.NeedNetwork) || current == Message(GateAction.Wait) ? "" : current;
        }

        public static string Message(GateAction a) => a switch
        {
            GateAction.NeedNetwork => "ネットにつなぐと書き出せます",
            GateAction.Wait => "広告を読み込んでいます。少し待ってから、もう一度「書き出す」を押してください",
            _ => "",
        };
    }

    /// <summary>
    /// 広告を見て使う演出（激アツの帯）を選ぶときの知らせ。判定は書き出しと同じ ExportGate.Decide
    /// （広告を消した人はすぐ選べる。ほかは広告を最後まで見てから。ネット・広告が無ければ選べない）
    /// </summary>
    public static class PickGate
    {
        public const string NotWatched = "広告を最後まで見ると、この演出を選べます";
        public const string AdHint = "選ぶときに広告が流れます";

        public static string Message(GateAction a) => a switch
        {
            GateAction.NeedNetwork => "ネットにつなぐと、この演出を選べます",
            GateAction.Wait => "広告を読み込んでいます。少し待ってから、もう一度押してください",
            _ => "",
        };

        /// <summary>広告の読み込み具合が変わったとき：待ち・ネットの知らせだけ今の判定で決め直す。ほかの知らせは残す</summary>
        public static string NoticeFor(GateAction a, string current)
        {
            bool stale = current == Message(GateAction.Wait) || current == Message(GateAction.NeedNetwork);
            return stale ? Message(a) : current;
        }
    }
}
