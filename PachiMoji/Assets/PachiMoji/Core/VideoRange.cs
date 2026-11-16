using UnityEngine;

namespace PachiMoji
{
    /// <summary>背景に入れたものが写真か動画か（拡張子で見分ける。動画はアプリの中で mp4 に写す）</summary>
    public static class MediaKind
    {
        /// <summary>
        /// 背景に動画を選べるか。1.1 では選ばせない（iPhone で動画が映らず、読み込みも遅かったため）。
        /// 動画は、のちに作る動画編集の道具で扱い直す。true に戻すと、選ぶ画面（PachiPhoto.mm の絞り込みも）と文言が動画ありに戻る
        /// </summary>
        public static readonly bool VideoEnabled = false;

        /// <summary>画面に出す呼び名（「写真・動画」か「写真」）</summary>
        public static string Label => VideoEnabled ? "写真・動画" : "写真";

        public static bool IsVideo(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string p = path.ToLowerInvariant();
            return p.EndsWith(".mp4") || p.EndsWith(".mov") || p.EndsWith(".m4v");
        }
    }

    /// <summary>背景の動画の、使う区間（秒）。書き出す動画はこの区間の長さになる（上限なし）</summary>
    public static class VideoRange
    {
        /// <summary>区間の最短の長さ（秒）</summary>
        public const float MinLength = 0.5f;

        /// <summary>動画の長さ length の中に収め、最短の長さを保つ（動画がそれより短ければ全体）</summary>
        public static (float start, float end) Clamp(float start, float end, float length)
        {
            length = Mathf.Max(0.01f, length);
            if (length <= MinLength) return (0, length);
            start = Mathf.Clamp(start, 0, length);
            end = Mathf.Clamp(end, 0, length);
            if (end - start < MinLength)
            {
                end = Mathf.Min(length, start + MinLength);
                start = end - MinLength;
            }
            return (start, end);
        }
    }
}
