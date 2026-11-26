using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>실제 상품 프리팹의 밀도·긴 상태 문구·스크롤 도달성을 검사한다. 계정과 구매/광고 API를 열지 않는다.</summary>
    public static class ShopOfferDensityAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Shop density acceptance requires EditMode.");
            var checks = new List<string>();
            void Check(bool value, string name)
            {
                if (!value) throw new InvalidOperationException("Shop offer density: " + name);
                checks.Add(name);
            }
            var canvas = new GameObject("ShopOfferDensityAcceptance", typeof(RectTransform), typeof(Canvas));
            canvas.hideFlags = HideFlags.HideAndDontSave;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPopupPrefabGen.PrefabPath);
                Check(prefab != null, "Actual shop prefab exists");
                var view = Object.Instantiate(prefab, canvas.transform, false).GetComponent<ShopPopupView>();
                var root = (RectTransform)view.transform;
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                var hud = (RectTransform)new GameObject("HudTopBoundary", typeof(RectTransform)).transform;
                hud.SetParent(root, false);
                hud.anchorMin = new Vector2(0, 1); hud.anchorMax = Vector2.one; hud.pivot = new Vector2(.5f, 1);
                hud.sizeDelta = new Vector2(0, UguiTheme.HudTopHeight);
                hud.anchoredPosition = Vector2.zero;
                Check(view.offers.Select(x => x.offerId).OrderBy(x => x).SequenceEqual(ShopCatalog.Offers.Select(x => x.Id).OrderBy(x => x)),
                    "All existing stable offer IDs appear exactly once");
                var sizes = new[] { new Vector2(1080, 1920), new Vector2(1080, 2400), new Vector2(1080, 1440), new Vector2(1440, 1920) };
                foreach (var size in sizes)
                {
                    // 실제 CanvasScaler와 동일한 폭 1080 기준으로 짧은 폰·긴 폰·태블릿 비율을 대조한다.
                    ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, size.y * 1080 / size.x);
                    view.sheet.GetComponent<SheetSizeFitter>().SetTopBoundary(hud);
                    for (int tab = 0; tab < view.pages.Length; tab++)
                    {
                        for (int i = 0; i < view.pages.Length; i++) view.pages[i].SetActive(i == tab);
                        Rebuild(view);
                        view.scroll.verticalNormalizedPosition = 1;
                        Canvas.ForceUpdateCanvases();
                        string context = size + " tab=" + tab;
                        Check(view.scroll.vertical && view.scroll.viewport.rect.height >= 144, context + " retains an internal viewport at least one action high");
                        var offers = view.offers.Where(x => x.gameObject.activeInHierarchy).ToArray();
                        Check(offers.Length > 0, context + " contains its existing products");
                        foreach (var row in offers)
                        {
                            var offer = ShopCatalog.Find(row.offerId);
                            bool package = offer.EquipmentTickets > 0 && offer.MageTickets > 0;
                            var card = (RectTransform)row.transform;
                            float compactHeight = card.rect.height;
                            Check(compactHeight >= (package ? 300 : 240) - 1 && compactHeight < (package ? 410 : 330),
                                context + " retains minimum height and improves catalog card density " + row.offerId + " height=" + compactHeight);
                            Check(Inside(card, row.title.rectTransform) && (!row.detail.gameObject.activeSelf || Inside(card, row.detail.rectTransform)) &&
                                (!row.amount.gameObject.activeSelf || Inside(card, row.amount.rectTransform)) &&
                                Inside(card, (RectTransform)row.action.transform), context + " keeps all information and action inside " + row.offerId);
                            Check(((RectTransform)row.action.transform).rect.height >= 144 && ((RectTransform)row.action.transform).rect.width >= 330,
                                context + " preserves the 330x144 action target " + row.offerId);
                            Check(!row.detail.gameObject.activeSelf || RectBounds(card, row.detail.rectTransform).max.x <= RectBounds(card, (RectTransform)row.action.transform).min.x + 1,
                                context + " separates body text from the price action " + row.offerId);
                            Check(Fits(row.title) && (!row.detail.gameObject.activeSelf || Fits(row.detail)) && Fits(row.price), context + " fits catalog title, description and price " + row.offerId);
                            if (offer.Payment == ShopPayment.RewardedAd || offer.GoldMinutes > 0)
                            {
                                string previous = row.amount.text;
                                row.amount.text = offer.GoldMinutes > 0 ? "골드 " + NumberNotation.Format(long.MaxValue) + "개" :
                                    NumberNotation.Format(long.MaxValue) + "장 보유중";
                                Rebuild(view);
                                Check(Fits(row.amount) && Inside(card, row.amount.rectTransform) && Inside(card, (RectTransform)row.action.transform),
                                    context + " keeps long confirmed quantity/status readable " + row.offerId + Metrics(row.amount));
                                Check(!row.detail.gameObject.activeSelf || RectBounds(card, row.detail.rectTransform).min.y >= RectBounds(card, row.amount.rectTransform).max.y - 1,
                                    context + " expands long quantity without overlapping its description " + row.offerId);
                                row.amount.text = previous;
                                Rebuild(view);
                                Check(Mathf.Abs(card.rect.height - compactHeight) < 1,
                                    context + " restores compact height after long quantity/status " + row.offerId);
                            }
                            if (offer.Payment == ShopPayment.RewardedAd)
                            {
                                string previous = row.price.text;
                                foreach (string state in new[] { "광고 보고 받기", "처리 중…", "보상 다시 받기", "오늘 수령 완료" })
                                {
                                    row.price.text = state;
                                    Check(Fits(row.price), context + " fits existing ad state " + state + " " + row.offerId);
                                }
                                row.price.text = previous;
                            }
                            if (package)
                            {
                                var contents = card.Find("Contents");
                                var labels = contents.GetComponentsInChildren<TMP_Text>().Select(x => x.text).ToArray();
                                Check(labels.SequenceEqual(new[] { NumberNotation.Exact(offer.Coins), "장비 " + offer.EquipmentTickets, "마탑 " + offer.MageTickets }),
                                    context + " retains all three exact package quantities " + row.offerId);
                                Check(contents.GetComponentsInChildren<TMP_Text>().All(x => Fits(x) && Inside(card, x.rectTransform)),
                                    context + " keeps package chips readable and within their card " + row.offerId);
                            }
                            bool ready = offer.Payment == ShopPayment.RewardedAd;
                            Check(row.action.targetGraphic.color == (ready ? UguiTheme.ActionGold : ShopOfferView.PreviewActionColor),
                                context + " keeps connected-ad/preview action distinction " + row.offerId);
                        }
                        view.scroll.verticalNormalizedPosition = 0;
                        Canvas.ForceUpdateCanvases();
                        Check(Inside(view.scroll.viewport, (RectTransform)offers[offers.Length - 1].action.transform),
                            context + " reaches the final action through internal scrolling");
                    }
                }
            }
            finally { Object.DestroyImmediate(canvas); }
            return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks,
                ["scope"] = "Actual prefab clones only; no account, network, ad playback, purchase or reward operation" };
        }

        private static bool Fits(TMP_Text text)
        {
            var rect = text.rectTransform.rect;
            return rect.width > 0 && rect.height > 0 && text.GetPreferredValues(text.text, rect.width, Mathf.Infinity).y <= rect.height + 1;
        }

        private static string Metrics(TMP_Text text) => "; rect=" + text.rectTransform.rect +
            " preferred=" + text.GetPreferredValues(text.text, text.rectTransform.rect.width, Mathf.Infinity) + " fontSize=" + text.fontSize;

        private static void Rebuild(ShopPopupView view)
        {
            for (int i = 0; i < 2; i++)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.sheet);
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.scroll.content);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static bool Inside(RectTransform parent, RectTransform child)
        {
            var bounds = RectBounds(parent, child); var rect = parent.rect;
            return bounds.size.x > 0 && bounds.size.y > 0 && bounds.min.x >= rect.xMin - 1 && bounds.max.x <= rect.xMax + 1 &&
                bounds.min.y >= rect.yMin - 1 && bounds.max.y <= rect.yMax + 1;
        }

        private static Bounds RectBounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var bounds = new Bounds(parent.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(parent.InverseTransformPoint(corners[i]));
            return bounds;
        }
    }
}
