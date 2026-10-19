using UnityEngine;

namespace PachiMoji
{
    /// <summary>PachiVideo.mm が UnitySendMessage で返事を届ける受け口（名前で探されるので、名前を変えない）</summary>
    public class VideoShareBridge : MonoBehaviour
    {
        public const string Name = "PachiVideoBridge";

        void OnSaved(string result) => VideoShare.Reply(result);
    }
}
