using System;
using System.Collections;
using System.Runtime.InteropServices;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// 広告（Google Mobile Ads）の包み：同意（ヨーロッパの人だけ画面が出る）→ 追跡の許可（ATT）→ 始める →
    /// 下のバナーと、書き出し前のごほうび広告。
    /// 広告の部品の返事はすべて OnMain で Unity の流れに移してから扱う（部品の「返事を Unity の流れで受ける」設定は、
    /// 始める前の同意の確認の返事を止めてしまう。2026-10-21 に TestFlight で広告が出なかった原因）
    /// iPhone 以外（Editor・Windows）では何もせず、ごほうびは見終えた扱いにする（手元確認で広告を動かさない）
    /// </summary>
    public class Ads
    {
        [DllImport("__Internal")] static extern void PachiTracking_Request();

        const float RetrySeconds = 30;   // ごほうび広告が読めなかったときに読み直すまで

        PachiTrackingBridge bridge;
        BannerView banner;
        RewardedAd rewarded;
        bool started, bannerWanted, rewardedWanted;

        public AdState Rewarded { get; private set; }

        /// <summary>いまどの段階か（テスト用の版で画面に出して、iPhone の中で止まっている場所を見る）</summary>
        public string Stage { get; private set; } = "未開始";
        string lastError = "";
        public string Diag => Stage + (lastError != "" ? "／" + lastError : "");
        void Step(string s) => Stage = s;
        void Fail(string s) => lastError = s;
        public event Action Changed;   // ごほうび広告の読み込み具合が変わった

        static bool OnDevice => Application.platform == RuntimePlatform.IPhonePlayer;

        public Ads()
        {
            Rewarded = OnDevice ? AdState.Loading : AdState.Ready;
        }

        /// <summary>showAds が false（広告を消した人）なら、同意と許可だけ済ませて広告は読まない</summary>
        public void Start(bool showAds)
        {
            bannerWanted = rewardedWanted = showAds;
            if (!OnDevice) return;
            bridge = PachiTrackingBridge.Get(this);   // 返事を Unity の流れで動かす受け口を、先に Unity の流れで作る
            Step("1 同意の確認中");
            ConsentInformation.Update(new ConsentRequestParameters(), updateError => OnMain(() =>
            {
                if (updateError != null) { Debug.LogWarning("[Ads] 同意の確認 " + updateError.Message); Fail("同意:" + updateError.Message); }
                Step("2 同意の画面");
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMain(() =>
                {
                    if (formError != null) { Debug.LogWarning("[Ads] 同意の画面 " + formError.Message); Fail("同意画面:" + formError.Message); }
                    Step("3 追跡の許可の返事待ち");
                    PachiTracking_Request();   // 返事は OnTrackingDone
                }));
            }));
        }

        // 広告の部品の返事は Unity の流れとは別の流れで来ることがある。Unity の物に触る前に移す
        void OnMain(Action a) => bridge.Queue.Post(a);

        internal void OnTrackingDone()
        {
            if (started) return;
            Step("4 広告の部品を始める");
            MobileAds.Initialize(_ => OnMain(() =>
            {
                started = true;
                Step("5 始まった");
                if (bannerWanted) LoadBanner();
                if (rewardedWanted) LoadRewarded();
            }));
        }

        /// <summary>広告を消したら、すぐ false にする</summary>
        public void SetBanner(bool show)
        {
            bannerWanted = show;
            rewardedWanted = show;
            if (!show)
            {
                banner?.Destroy();
                banner = null;
            }
            else if (started && banner == null) LoadBanner();
        }

        void LoadBanner()
        {
            banner = new BannerView(AdIds.Banner, AdSize.Banner, AdPosition.Bottom);   // 320×50。下の 62 の場所に収まる
            banner.OnBannerAdLoadFailed += e => OnMain(() => { Debug.LogWarning("[Ads] バナーを読めない " + e.GetMessage()); Fail("バナー:" + e.GetMessage()); });
            banner.OnBannerAdLoaded += () => OnMain(() => Step(Stage + "・バナーOK"));
            banner.LoadAd(new AdRequest());
        }

        void LoadRewarded()
        {
            SetRewarded(AdState.Loading);
            RewardedAd.Load(AdIds.Rewarded, new AdRequest(), (ad, error) => OnMain(() =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[Ads] ごほうび広告を読めない " + error?.GetMessage());
                    Fail("ごほうび:" + error?.GetMessage());
                    SetRewarded(AdState.Failed);
                    bridge.StartCoroutine(Retry());
                    return;
                }
                rewarded = ad;
                SetRewarded(AdState.Ready);
            }));
        }

        IEnumerator Retry()
        {
            yield return new WaitForSecondsRealtime(RetrySeconds);
            if (rewardedWanted && rewarded == null) LoadRewarded();
        }

        /// <summary>ごほうび広告を見せる。done(true) は最後まで見た。閉じたら次を読み込み始める</summary>
        public void ShowRewarded(Action<bool> done)
        {
            if (!OnDevice)
            {
                done(true);
                return;
            }
            if (rewarded == null || !rewarded.CanShowAd())
            {
                // 読み込んでから時間が経つと（約1時間）期限が切れて出せない。捨てて読み直す
                if (rewarded != null)
                {
                    rewarded.Destroy();
                    rewarded = null;
                    if (rewardedWanted) LoadRewarded();
                }
                done(false);
                return;
            }
            bool earned = false;
            var ad = rewarded;
            rewarded = null;
            SetRewarded(AdState.Loading);
            ad.OnAdFullScreenContentClosed += () => OnMain(() =>
            {
                ad.Destroy();
                done(earned);
                if (rewardedWanted) LoadRewarded();
            });
            ad.OnAdFullScreenContentFailed += e => OnMain(() =>
            {
                Debug.LogWarning("[Ads] ごほうび広告を出せない " + e.GetMessage());
                ad.Destroy();
                done(false);
                if (rewardedWanted) LoadRewarded();
            });
            ad.Show(_ => OnMain(() => earned = true));   // 閉じた知らせより先に届く（同じ行列で順に動く）
        }

        void SetRewarded(AdState s)
        {
            Rewarded = s;
            Changed?.Invoke();
        }
    }
}
