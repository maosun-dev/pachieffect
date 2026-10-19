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

        public static string Message(GateAction a) => a switch
        {
            GateAction.NeedNetwork => "ネットにつなぐと書き出せます",
            GateAction.Wait => "広告を読み込んでいます。少し待ってから、もう一度「書き出す」を押してください",
            _ => "",
        };
    }
}
