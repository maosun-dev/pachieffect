using UnityEngine;

namespace PachiMoji
{
    /// <summary>PachiPhoto.mm が UnitySendMessage で返事を届ける受け口（名前で探されるので、名前を変えない）</summary>
    public class PhotoPickerBridge : MonoBehaviour
    {
        public const string Name = "PachiPhotoBridge";

        void OnPicked(string result) => PhotoPicker.Reply(result);
    }
}
