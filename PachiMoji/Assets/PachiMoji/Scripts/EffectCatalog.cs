using System;

namespace PachiMoji
{
    /// <summary>演出の一覧表の1行</summary>
    public class EffectEntry
    {
        public string Id;
        public string Name;
        public string RequiredProduct;   // 使うのに要る商品（null は無料）
        public Func<IPachiEffect> Create;
    }

    /// <summary>演出の一覧表。配り方はアプリの更新に入れる</summary>
    public static class EffectCatalog
    {
        public static readonly EffectEntry[] All =
        {
            new EffectEntry { Id = "oudou", Name = "王道昇格", RequiredProduct = null, Create = () => new OudouEffect() },
        };

        /// <summary>ID から探す。知らない ID なら最初の演出</summary>
        public static EffectEntry Get(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            return All[0];
        }
    }
}
