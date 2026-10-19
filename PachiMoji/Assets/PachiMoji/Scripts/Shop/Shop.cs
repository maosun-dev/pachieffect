using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace PachiMoji
{
    public enum ShopState { Connecting, Ready, Unavailable }

    /// <summary>
    /// 課金（Unity IAP 5）の包み：ストアにつなぐ・値段・買う・復元。
    /// 届いた買い物は、先に端末へ記録してからストアに「受け取った」と確定する（記録の前に落ちても、次に開いたとき届き直す）。
    /// 記録は「持っているか」だけなので、同じ買い物が何度届いても同じ
    /// </summary>
    public class Shop
    {
        StoreController store;
        bool restoring;

        public ShopState State { get; private set; } = ShopState.Connecting;
        public Entitlements Owned { get; private set; }
        public event Action Changed;
        public event Action<string> Notice;

        /// <summary>端末に残っていた買ったもので始める（ネットなしでも効く）</summary>
        public Shop(Entitlements saved)
        {
            Owned = saved;
        }

        static List<ProductDefinition> Catalog()
        {
            var list = new List<ProductDefinition>();
            foreach (var id in ProductIds.All) list.Add(new ProductDefinition(id, ProductType.NonConsumable));
            return list;
        }

        public async Task Connect()
        {
            try
            {
                store = UnityIAPServices.StoreController();
                // つなぐ前にすべての知らせを受ける（前に届きかけていた買い物が、つないだ直後に来ることがある）
                store.OnPurchasePending += OnPending;
                store.OnPurchaseConfirmed += order =>
                {
                    if (order is FailedOrder f) Debug.LogWarning($"[Shop] 確定できなかった {f.FailureReason} {f.Details}");
                };
                store.OnPurchaseFailed += OnFailed;
                store.OnPurchaseDeferred += _ => Notice?.Invoke("保護者の承認を待っています。承認されると自動で使えるようになります");
                store.OnStoreConnected += () =>
                {
                    store.FetchProducts(Catalog());
                    store.FetchPurchases();
                };
                store.OnStoreDisconnected += f =>
                {
                    Debug.LogWarning("[Shop] ストアにつながらない " + f.Message);
                    SetState(ShopState.Unavailable);
                };
                store.OnProductsFetched += _ => SetState(ShopState.Ready);
                store.OnProductsFetchFailed += f =>
                {
                    Debug.LogWarning("[Shop] 商品を取れない " + f.FailureReason);
                    SetState(ShopState.Unavailable);
                };
                store.OnPurchasesFetched += orders =>
                {
                    foreach (var o in orders.ConfirmedOrders) GrantAll(o);
                };
                store.OnPurchasesFetchFailed += f => Debug.LogWarning("[Shop] 買ったものを取れない " + f.Message);
                await store.Connect();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetState(ShopState.Unavailable);
            }
        }

        /// <summary>ストアの値段の文字（例「¥300」）。まだ取れていなければ null</summary>
        public string PriceOf(string id)
        {
            if (State != ShopState.Ready || store == null) return null;
            return store.GetProductById(id)?.metadata.localizedPriceString;
        }

        public void Buy(string id)
        {
            if (State != ShopState.Ready) return;
            store.PurchaseProduct(id);
        }

        public void Restore()
        {
            if (store == null || State == ShopState.Connecting) return;
            restoring = true;
            store.RestoreTransactions((ok, error) =>
            {
                restoring = false;
                if (!ok)
                {
                    Debug.LogWarning("[Shop] 復元できない " + error);
                    Notice?.Invoke("復元できませんでした。ネットにつないで、もう一度お試しください");
                    return;
                }
                bool any = Owned.Owns(ProductIds.NoMark) || Owned.Owns(ProductIds.NoAds);
                Notice?.Invoke(any ? "購入を復元しました" : "復元できる購入はありませんでした");
            });
        }

        /// <summary>買ったものに足して端末に保存し、Changed で知らせる（届いた買い物と手元確認の入口）</summary>
        public void Grant(string id)
        {
            if (Owned.Owns(id)) return;
            Owned = Owned.With(id);
            Owned.Save();
            Changed?.Invoke();
        }

        void OnPending(PendingOrder order)
        {
            GrantAll(order);   // 端末へ記録してから
            store.ConfirmPurchase(order);   // ストアに「受け取った」と伝える
            if (!restoring) Notice?.Invoke("ありがとうございます。購入しました");
        }

        void GrantAll(Order order)
        {
            foreach (var item in order.CartOrdered.Items())
                if (item.Product != null) Grant(item.Product.definition.id);
        }

        void OnFailed(FailedOrder f)
        {
            Debug.LogWarning($"[Shop] 買えなかった {f.FailureReason} {f.Details}");
            if (f.FailureReason == PurchaseFailureReason.UserCancelled) return;   // 自分でやめたときは何も言わない
            Notice?.Invoke("買えませんでした。少し待ってから、もう一度お試しください");
        }

        void SetState(ShopState s)
        {
            State = s;
            Changed?.Invoke();
        }
    }
}
