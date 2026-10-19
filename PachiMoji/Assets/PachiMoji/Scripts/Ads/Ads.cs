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
    /// iPhone 以外（Editor・Windows）では何もせず、ごほうびは見終えた扱いにする（手元確認で広告を動かさない）
    /// </summary>
    public class Ads
    {
        [DllImport("__Internal")] static extern void PachiTracking_Request();

        const float RetrySeconds = 30;   // ごほうび広告が読めなかったときに読み直すまで

        BannerView banner;
        RewardedAd rewarded;
        bool started, bannerWanted, rewardedWanted;

        public AdState Rewarded { get; private set; }
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
            MobileAds.RaiseAdEventsOnUnityMainThread = true;   // 広告の返事を Unity の流れの中で受ける
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null) Debug.LogWarning("[Ads] 同意の確認 " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] 同意の画面 " + formError.Message);
                    PachiTrackingBridge.Get(this);
                    PachiTracking_Request();   // 返事は OnTrackingDone
                });
            });
        }

        internal void OnTrackingDone()
        {
            if (started) return;
            MobileAds.Initialize(_ =>
            {
                started = true;
                if (bannerWanted) LoadBanner();
                if (rewardedWanted) LoadRewarded();
            });
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
            banner.OnBannerAdLoadFailed += e => Debug.LogWarning("[Ads] バナーを読めない " + e.GetMessage());
            banner.LoadAd(new AdRequest());
        }

        void LoadRewarded()
        {
            SetRewarded(AdState.Loading);
            RewardedAd.Load(AdIds.Rewarded, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[Ads] ごほうび広告を読めない " + error?.GetMessage());
                    SetRewarded(AdState.Failed);
                    PachiTrackingBridge.Get(this).StartCoroutine(Retry());
                    return;
                }
                rewarded = ad;
                SetRewarded(AdState.Ready);
            });
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
            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                done(earned);
                if (rewardedWanted) LoadRewarded();
            };
            ad.OnAdFullScreenContentFailed += e =>
            {
                Debug.LogWarning("[Ads] ごほうび広告を出せない " + e.GetMessage());
                ad.Destroy();
                done(false);
                if (rewardedWanted) LoadRewarded();
            };
            ad.Show(_ => earned = true);
        }

        void SetRewarded(AdState s)
        {
            Rewarded = s;
            Changed?.Invoke();
        }
    }
}
