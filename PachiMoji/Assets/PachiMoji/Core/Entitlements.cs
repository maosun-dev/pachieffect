using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PachiMoji
{
    /// <summary>買い切りの商品の ID（App Store Connect に登録するものと同じ）</summary>
    public static class ProductIds
    {
        public const string NoMark = "com.maosundev.noujiru.nomark";   // 印を消す 300円
        public const string NoAds = "com.maosundev.noujiru.noads";     // 広告を消す 300円
        public const string Set = "com.maosundev.noujiru.set";         // セット 500円（上の2つとも）
        public static readonly string[] All = { NoMark, NoAds, Set };
    }

    /// <summary>
    /// 買ったもの → 印を出すか・広告を出すか・設定で見せる商品。
    /// 端末にも残し、ネットにつながっていなくても買ったものが効くようにする
    /// </summary>
    public class Entitlements
    {
        readonly HashSet<string> owned;

        public Entitlements(IEnumerable<string> ids)
        {
            owned = new HashSet<string>(ids.Where(id => ProductIds.All.Contains(id)));
        }

        public bool Owns(string id) => owned.Contains(id) || (id != ProductIds.Set && owned.Contains(ProductIds.Set));

        bool NoMark => Owns(ProductIds.NoMark);
        bool NoAds => Owns(ProductIds.NoAds);

        public bool ShowWatermark => !NoMark;
        public bool ShowAds => !NoAds;

        /// <summary>見せる商品：何も買っていなければ3つ、片方だけなら残りの1つ、両方なら無し（セットは何も買っていない人だけ）</summary>
        public string[] ShopItems =>
            !NoMark && !NoAds ? new[] { ProductIds.NoMark, ProductIds.NoAds, ProductIds.Set }
            : !NoMark ? new[] { ProductIds.NoMark }
            : !NoAds ? new[] { ProductIds.NoAds }
            : new string[0];

        /// <summary>1つ足した新しいもの（同じものが何度届いても同じ）</summary>
        public Entitlements With(string id) => new Entitlements(owned.Append(id));

        public void Save(string key = "ent.owned")
        {
            PlayerPrefs.SetString(key, string.Join(",", owned.OrderBy(x => x)));
            PlayerPrefs.Save();
        }

        public static Entitlements Load(string key = "ent.owned") =>
            new Entitlements(PlayerPrefs.GetString(key, "").Split(','));
    }
}
