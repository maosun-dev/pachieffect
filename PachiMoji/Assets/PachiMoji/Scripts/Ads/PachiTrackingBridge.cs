using UnityEngine;

namespace PachiMoji
{
    /// <summary>
    /// PachiTracking.mm が UnitySendMessage で返事を届ける受け口。読み直しの待ちと、
    /// 広告の部品の返事を Unity の流れで動かす待ち行列（Queue）の取り出しにも使う（名前で探されるので変えない）
    /// </summary>
    public class PachiTrackingBridge : MonoBehaviour
    {
        public const string Name = "PachiTrackingBridge";
        Ads ads;
        public readonly MainThreadQueue Queue = new MainThreadQueue();

        public static PachiTrackingBridge Get(Ads ads)
        {
            var go = GameObject.Find(Name);
            if (go == null)
            {
                go = new GameObject(Name);
                DontDestroyOnLoad(go);
            }
            var b = go.GetComponent<PachiTrackingBridge>();
            if (b == null) b = go.AddComponent<PachiTrackingBridge>();   // Unity の部品は ?? では「無い」を正しく見分けられない
            b.ads = ads;
            return b;
        }

        void OnTracking(string _) => ads?.OnTrackingDone();

        void Update() => Queue.Drain();
    }
}
