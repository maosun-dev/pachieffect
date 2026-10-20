namespace PachiMoji
{
    /// <summary>
    /// AdMob の広告枠の ID。組み立て中はテスト広告（Google が用意しているテスト用の枠）、
    /// ストアに出す版（組み立てるときに -pachiProduction を付けて PACHI_PRODUCTION が付いた版）だけ本物。
    /// 本物の ID は AdMob のアプリ「脳汁メーカー」の「下のバナー」「書き出し前」（2026-10-21 作成）
    /// </summary>
    public static class AdIds
    {
        public const string TestAppId = "ca-app-pub-3940256099942544~1458002511";
        public const string TestBanner = "ca-app-pub-3940256099942544/2934735716";
        public const string TestRewarded = "ca-app-pub-3940256099942544/1712485313";

        public const string RealAppId = "ca-app-pub-7017663942238206~9479419593";
        public const string RealBanner = "ca-app-pub-7017663942238206/5037436694";
        public const string RealRewarded = "ca-app-pub-7017663942238206/3724355025";

#if PACHI_PRODUCTION
        public const bool Production = true;
#else
        public const bool Production = false;
#endif

        public static string AppId => Production ? RealAppId : TestAppId;
        public static string Banner => Production ? RealBanner : TestBanner;
        public static string Rewarded => Production ? RealRewarded : TestRewarded;

        /// <summary>本物の ID がまだ仮のままか</summary>
        public static bool RealIdsMissing => (RealAppId + RealBanner + RealRewarded).Contains("TODO");
    }
}
