using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanel : UIPanelBase
{
    private static readonly Color NormalPriceColor = Color.white;
    private static readonly Color DiscountPriceColor = new Color(0.35f, 1f, 0.45f, 1f);
    private static readonly Color FeedbackErrorColor = new Color(1f, 0.45f, 0.40f, 1f);

    public GameObject cardCollection;
    public GameObject relicCollection;
    public GameObject potionCollection;

    public List<CardUI> cardList = new List<CardUI>();
    public List<RelicUI> relicList = new List<RelicUI>();
    public List<GameObject> potionList = new List<GameObject>();

    public Button upgradeCardButton;
    public Button closeUIButton;
    public Button headerCloseButton;

    [Header("Reroll & Feedback Controls")]
    public Button rerollButton;
    public TextMeshProUGUI rerollButtonText;
    public TextMeshProUGUI feedbackText;

    private ShopNPC _boundShop;
    private readonly Dictionary<Component, TextMeshProUGUI> _slotPriceTexts = new Dictionary<Component, TextMeshProUGUI>();

    public ShopNPC BoundShop => _boundShop;

    private void Awake()
    {
        EnsureRuntimeControls();
        BindButtonListeners();
    }

    private void Start()
    {
        EnsureRuntimeControls();
        BindButtonListeners();
    }

    private void OnDestroy()
    {
        UnbindButtonListeners();
        _slotPriceTexts.Clear();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        EnsureRuntimeControls();
        BindButtonListeners();
        HideFeedback();
        RefreshShopDisplay();
    }

    public override void OnClose()
    {
        base.OnClose();
        HideFeedback();
    }

    public void BindShop(ShopNPC ownerShop)
    {
        _boundShop = ownerShop;
        EnsureRuntimeControls();
        BindButtonListeners();
        RefreshRerollUI();
    }

    private void BindButtonListeners()
    {
        if (upgradeCardButton != null)
        {
            upgradeCardButton.onClick.RemoveListener(OpenUpgradeCardUI);
            upgradeCardButton.onClick.AddListener(OpenUpgradeCardUI);
        }

        if (closeUIButton != null)
        {
            closeUIButton.onClick.RemoveListener(CloseUI);
            closeUIButton.onClick.AddListener(CloseUI);
        }

        if (headerCloseButton != null)
        {
            headerCloseButton.onClick.RemoveListener(CloseUI);
            headerCloseButton.onClick.AddListener(CloseUI);
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(OnRerollClicked);
            rerollButton.onClick.AddListener(OnRerollClicked);
        }
    }

    private void UnbindButtonListeners()
    {
        if (upgradeCardButton != null)
        {
            upgradeCardButton.onClick.RemoveListener(OpenUpgradeCardUI);
        }

        if (closeUIButton != null)
        {
            closeUIButton.onClick.RemoveListener(CloseUI);
        }

        if (headerCloseButton != null)
        {
            headerCloseButton.onClick.RemoveListener(CloseUI);
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(OnRerollClicked);
        }
    }

    /// <summary>
    /// 상점에 생성될 카드, 유물, 포션의 슬롯을 동적으로 확보하고 기존 슬롯을 재사용합니다.
    /// 부족한 수량만 추가 인스턴스화하고 남는 슬롯은 비활성화하여 무한 증식을 방지합니다.
    /// </summary>
    public void CreateStoreItemCollections(int cardNumber, int relicNumber, int potionNumber)
    {
        if (cardList == null) cardList = new List<CardUI>();
        if (relicList == null) relicList = new List<RelicUI>();
        if (potionList == null) potionList = new List<GameObject>();

        cardNumber = Mathf.Max(0, cardNumber);
        relicNumber = Mathf.Max(0, relicNumber);
        potionNumber = Mathf.Max(0, potionNumber);

        EnsureComponentSlots(cardList, cardNumber, cardCollection, "ActionCard", "카드");
        EnsureComponentSlots(relicList, relicNumber, relicCollection, "Relic", "유물");

        if (potionNumber > 0 && potionCollection != null)
        {
            EnsureGameObjectSlots(potionList, potionNumber, potionCollection, "Potion", "포션");
        }
        else
        {
            for (int i = 0; i < potionList.Count; i++)
            {
                if (potionList[i] != null)
                {
                    potionList[i].SetActive(false);
                }
            }
        }
    }

    private void EnsureComponentSlots<T>(
        List<T> slotList,
        int targetCount,
        GameObject container,
        string assetKey,
        string slotLabel) where T : Component
    {
        for (int i = slotList.Count - 1; i >= 0; i--)
        {
            if (slotList[i] == null)
            {
                slotList.RemoveAt(i);
            }
        }

        if (slotList.Count < targetCount)
        {
            if (container == null)
            {
                Debug.LogError($"[ShopPanel] {slotLabel} 컬렉션 컨테이너가 연결되지 않았습니다.");
            }
            else if (AssetCacheManager.instance == null)
            {
                Debug.LogError("[ShopPanel] AssetCacheManager is null!");
            }
            else if (AssetCacheManager.instance.TryGetUI(assetKey, out GameObject prefabObj) && prefabObj != null)
            {
                int needed = targetCount - slotList.Count;
                for (int i = 0; i < needed; i++)
                {
                    GameObject instance = GameObject.Instantiate(prefabObj, container.transform);
                    T component = instance != null ? instance.GetComponent<T>() : null;
                    if (component != null)
                    {
                        slotList.Add(component);
                    }
                    else
                    {
                        Debug.LogError($"[ShopPanel] Failed to get {typeof(T).Name} component from {assetKey} prefab!");
                        if (instance != null)
                        {
                            DestroyOrImmediate(instance);
                        }
                        break;
                    }
                }
            }
            else
            {
                Debug.LogError($"[ShopPanel] Failed to load {assetKey} prefab from AssetCacheManager!");
            }
        }

        for (int i = 0; i < slotList.Count; i++)
        {
            if (slotList[i] != null)
            {
                slotList[i].gameObject.SetActive(i < targetCount);
            }
        }
    }

    private void EnsureGameObjectSlots(
        List<GameObject> slotList,
        int targetCount,
        GameObject container,
        string assetKey,
        string slotLabel)
    {
        for (int i = slotList.Count - 1; i >= 0; i--)
        {
            if (slotList[i] == null)
            {
                slotList.RemoveAt(i);
            }
        }

        if (slotList.Count < targetCount)
        {
            if (container == null)
            {
                Debug.LogError($"[ShopPanel] {slotLabel} 컬렉션 컨테이너가 연결되지 않았습니다.");
            }
            else if (AssetCacheManager.instance == null)
            {
                Debug.LogError("[ShopPanel] AssetCacheManager is null!");
            }
            else if (AssetCacheManager.instance.TryGetUI(assetKey, out GameObject prefabObj) && prefabObj != null)
            {
                int needed = targetCount - slotList.Count;
                for (int i = 0; i < needed; i++)
                {
                    GameObject instance = GameObject.Instantiate(prefabObj, container.transform);
                    if (instance != null)
                    {
                        slotList.Add(instance);
                    }
                }
            }
            else
            {
                Debug.LogError($"[ShopPanel] Failed to load {assetKey} prefab from AssetCacheManager!");
            }
        }

        for (int i = 0; i < slotList.Count; i++)
        {
            if (slotList[i] != null)
            {
                slotList[i].SetActive(i < targetCount);
            }
        }
    }

    public static int GetCardPrice(CardData card)
    {
        return ShopNPC.CalculateCardPrice(card, false);
    }

    public static int GetCardPrice(CardData card, Player player)
    {
        return ShopNPC.CalculateCardPrice(card, player);
    }

    public static int GetRelicPrice(RelicData relic)
    {
        return ShopNPC.CalculateRelicPrice(relic, false);
    }

    public static int GetRelicPrice(RelicData relic, Player player)
    {
        return ShopNPC.CalculateRelicPrice(relic, player);
    }

    public void RefreshShopDisplay()
    {
        EnsureRuntimeControls();

        if (_boundShop != null)
        {
            UpdateCardList(_boundShop.actionCards);
            UpdateRelicList(_boundShop.relics);
        }
        else
        {
            RefreshActiveSlotPrices();
        }

        RefreshRerollUI();
    }

    public void UpdateCardList(List<CardData> cardData)
    {
        if (cardList == null) cardList = new List<CardUI>();

        int cardCount = cardData != null ? cardData.Count : 0;
        Player fallbackPlayer = ResolveFallbackPlayer();
        bool hasDiscount = _boundShop != null
            ? _boundShop.IsDiscountActive()
            : ShopNPC.HasDiscountRelic(fallbackPlayer);

        for (int i = 0; i < cardList.Count; i++)
        {
            if (cardList[i] == null) continue;

            cardList[i].OnCardClick.RemoveAllListeners();
            bool isPurchased = _boundShop != null && _boundShop.IsCardSlotPurchased(i);

            if (i < cardCount && cardData[i] != null && !isPurchased)
            {
                CardData currentCardData = cardData[i];
                cardList[i].gameObject.SetActive(true);
                cardList[i].UpdateCardData(currentCardData);

                int index = i;
                cardList[i].OnCardClick.AddListener(() => PurchaseCard(currentCardData, index));

                int price = _boundShop != null
                    ? _boundShop.GetCardPrice(currentCardData)
                    : GetCardPrice(currentCardData, fallbackPlayer);
                UpdateSlotPriceLabel(cardList[i], price, hasDiscount, isCardSlot: true);
            }
            else
            {
                cardList[i].gameObject.SetActive(false);
            }
        }
    }

    public void UpdateRelicList(List<RelicData> relicData)
    {
        if (relicList == null) relicList = new List<RelicUI>();

        int relicCount = relicData != null ? relicData.Count : 0;
        Player fallbackPlayer = ResolveFallbackPlayer();
        bool hasDiscount = _boundShop != null
            ? _boundShop.IsDiscountActive()
            : ShopNPC.HasDiscountRelic(fallbackPlayer);

        for (int i = 0; i < relicList.Count; i++)
        {
            if (relicList[i] == null) continue;

            relicList[i].OnRelicClick.RemoveAllListeners();
            bool isPurchased = _boundShop != null && _boundShop.IsRelicSlotPurchased(i);

            if (i < relicCount && relicData[i] != null && !isPurchased)
            {
                RelicData currentRelicData = relicData[i];
                relicList[i].gameObject.SetActive(true);
                relicList[i].UpdateRelicData(currentRelicData);

                int index = i;
                relicList[i].OnRelicClick.AddListener(() => PurchaseRelic(currentRelicData, index));

                int price = _boundShop != null
                    ? _boundShop.GetRelicPrice(currentRelicData)
                    : GetRelicPrice(currentRelicData, fallbackPlayer);
                UpdateSlotPriceLabel(relicList[i], price, hasDiscount, isCardSlot: false);
            }
            else
            {
                relicList[i].gameObject.SetActive(false);
            }
        }
    }

    private void RefreshActiveSlotPrices()
    {
        Player fallbackPlayer = ResolveFallbackPlayer();
        bool hasDiscount = _boundShop != null
            ? _boundShop.IsDiscountActive()
            : ShopNPC.HasDiscountRelic(fallbackPlayer);

        if (cardList != null)
        {
            for (int i = 0; i < cardList.Count; i++)
            {
                CardUI cardUI = cardList[i];
                if (cardUI == null || !cardUI.gameObject.activeSelf) continue;

                CardData data = cardUI.GetCardData();
                if (data == null) continue;

                int price = _boundShop != null
                    ? _boundShop.GetCardPrice(data)
                    : GetCardPrice(data, fallbackPlayer);
                UpdateSlotPriceLabel(cardUI, price, hasDiscount, isCardSlot: true);
            }
        }

        if (relicList != null)
        {
            for (int i = 0; i < relicList.Count; i++)
            {
                RelicUI relicUI = relicList[i];
                if (relicUI == null || !relicUI.gameObject.activeSelf) continue;

                RelicData data = relicUI.relicData;
                if (data == null) continue;

                int price = _boundShop != null
                    ? _boundShop.GetRelicPrice(data)
                    : GetRelicPrice(data, fallbackPlayer);
                UpdateSlotPriceLabel(relicUI, price, hasDiscount, isCardSlot: false);
            }
        }
    }

    private void RefreshRerollUI()
    {
        EnsureRuntimeControls();
        if (rerollButtonText == null) return;

        if (_boundShop != null)
        {
            int rerollCost = _boundShop.GetCurrentRerollCost();
            bool hasDiscount = _boundShop.IsDiscountActive();
            rerollButtonText.text = $"리롤 ({rerollCost}G)";
            rerollButtonText.color = hasDiscount ? DiscountPriceColor : Color.white;
        }
        else
        {
            Player fallbackPlayer = ResolveFallbackPlayer();
            bool hasDiscount = ShopNPC.HasDiscountRelic(fallbackPlayer);
            int fallbackCost = hasDiscount ? Mathf.RoundToInt(50 * 0.7f) : 50;
            rerollButtonText.text = $"리롤 ({fallbackCost}G)";
            rerollButtonText.color = hasDiscount ? DiscountPriceColor : Color.white;
        }
    }

    private void PurchaseCard(CardData cardData, int slotIndex)
    {
        if (_boundShop != null)
        {
            if (_boundShop.TryPurchaseCard(slotIndex, out string failMessage))
            {
                HideFeedback();
                if (cardList != null && slotIndex >= 0 && slotIndex < cardList.Count && cardList[slotIndex] != null)
                {
                    cardList[slotIndex].OnCardClick.RemoveAllListeners();
                    cardList[slotIndex].gameObject.SetActive(false);
                }

                RefreshActiveSlotPrices();
                RefreshRerollUI();
            }
            else
            {
                ShowFeedback(failMessage);
                Debug.LogWarning($"[ShopPanel] {failMessage}");
            }
            return;
        }

        if (cardData == null) return;

        Player player = ResolveFallbackPlayer();
        if (player == null || player.playerStat == null || player.deck == null)
        {
            ShowFeedback("상점 또는 플레이어 정보가 연결되지 않았습니다.");
            return;
        }

        int price = GetCardPrice(cardData, player);
        if (player.playerStat.InGameCurrencyGold < price)
        {
            string msg = $"골드가 부족합니다! (필요: {price}G, 보유: {player.playerStat.InGameCurrencyGold}G)";
            ShowFeedback(msg);
            Debug.LogWarning($"[ShopPanel] {msg}");
            return;
        }

        HideFeedback();
        player.playerStat.InGameCurrencyGold -= price;
        player.deck.AddCard(cardData);
        Debug.Log($"[ShopPanel] {cardData.cardName} 카드를 {price}골드에 구매했습니다.");

        if (cardList != null && slotIndex >= 0 && slotIndex < cardList.Count && cardList[slotIndex] != null)
        {
            cardList[slotIndex].OnCardClick.RemoveAllListeners();
            cardList[slotIndex].gameObject.SetActive(false);
        }

        RefreshActiveSlotPrices();
        RefreshRerollUI();
    }

    private void PurchaseRelic(RelicData relicData, int slotIndex)
    {
        if (_boundShop != null)
        {
            if (_boundShop.TryPurchaseRelic(slotIndex, out string failMessage))
            {
                HideFeedback();
                if (relicList != null && slotIndex >= 0 && slotIndex < relicList.Count && relicList[slotIndex] != null)
                {
                    relicList[slotIndex].OnRelicClick.RemoveAllListeners();
                    relicList[slotIndex].gameObject.SetActive(false);
                }

                RefreshActiveSlotPrices();
                RefreshRerollUI();
            }
            else
            {
                ShowFeedback(failMessage);
                Debug.LogWarning($"[ShopPanel] {failMessage}");
            }
            return;
        }

        if (relicData == null) return;

        Player player = ResolveFallbackPlayer();
        if (player == null || player.playerStat == null || player.relicManager == null)
        {
            ShowFeedback("상점 또는 플레이어 정보가 연결되지 않았습니다.");
            return;
        }

        int price = GetRelicPrice(relicData, player);
        if (player.playerStat.InGameCurrencyGold < price)
        {
            string msg = $"골드가 부족합니다! (필요: {price}G, 보유: {player.playerStat.InGameCurrencyGold}G)";
            ShowFeedback(msg);
            Debug.LogWarning($"[ShopPanel] {msg}");
            return;
        }

        HideFeedback();
        player.playerStat.InGameCurrencyGold -= price;
        player.AddRelic(relicData.relicName);
        Debug.Log($"[ShopPanel] {relicData.relicName} 유물을 {price}골드에 구매했습니다.");

        if (relicList != null && slotIndex >= 0 && slotIndex < relicList.Count && relicList[slotIndex] != null)
        {
            relicList[slotIndex].OnRelicClick.RemoveAllListeners();
            relicList[slotIndex].gameObject.SetActive(false);
        }

        RefreshActiveSlotPrices();
        RefreshRerollUI();
    }

    public void OnRerollClicked()
    {
        if (_boundShop == null)
        {
            ShowFeedback("리롤할 상점 정보가 연결되지 않았습니다.");
            return;
        }

        if (_boundShop.TryReroll(out string failMessage))
        {
            HideFeedback();
            if (_boundShop.GetShopUI() != this)
            {
                int potionCount = potionList != null ? potionList.Count : 0;
                CreateStoreItemCollections(_boundShop.actionCards.Count, _boundShop.relics.Count, potionCount);
                RefreshShopDisplay();
            }
        }
        else
        {
            ShowFeedback(failMessage);
            Debug.LogWarning($"[ShopPanel] {failMessage}");
        }
    }

    public void ShowFeedback(string message)
    {
        EnsureRuntimeControls();
        if (feedbackText != null)
        {
            feedbackText.text = message != null ? message : string.Empty;
            feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }

    public void HideFeedback()
    {
        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }
    }

    public void OpenUpgradeCardUI()
    {
        if (UIManager.instance != null)
        {
            UIManager.instance.OpenUI("UpgradeCardDeckCanvas", UILayerType.Normal, true);
        }
        Debug.Log("Card Upgrade is Process!");
    }

    public void CloseUI()
    {
        Close();
    }

    private void EnsureRuntimeControls()
    {
        TMP_FontAsset fontAsset = TMPFontUtility.ResolveDefaultFont(rerollButtonText, feedbackText);

        if (headerCloseButton == null)
        {
            Transform existingHeaderClose = FindChildRecursive(transform, "HeaderCloseButton");
            if (existingHeaderClose != null)
            {
                headerCloseButton = existingHeaderClose.GetComponent<Button>();
            }
        }

        if (rerollButton == null)
        {
            Transform existingReroll = FindChildRecursive(transform, "RerollButton");
            if (existingReroll != null)
            {
                rerollButton = existingReroll.GetComponent<Button>();
            }
        }

        if (rerollButton == null)
        {
            GameObject btnObj = CreateUIObject("RerollButton", transform);
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.63f, 0.31f);
            btnRect.anchorMax = new Vector2(0.975f, 0.39f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = Vector2.zero;

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.18f, 0.28f, 0.42f, 0.96f);

            rerollButton = btnObj.AddComponent<Button>();
            rerollButton.targetGraphic = btnImg;
        }

        if (rerollButton != null && rerollButtonText == null)
        {
            rerollButtonText = rerollButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (rerollButtonText == null)
            {
                rerollButtonText = CreateTMPText(
                    "RerollButtonText",
                    rerollButton.transform,
                    "리롤 (50G)",
                    26f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center,
                    Color.white,
                    fontAsset);
                rerollButtonText.enableAutoSizing = true;
                rerollButtonText.fontSizeMin = 14f;
                rerollButtonText.fontSizeMax = 28f;

                RectTransform txtRect = rerollButtonText.GetComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.offsetMin = new Vector2(8f, 4f);
                txtRect.offsetMax = new Vector2(-8f, -4f);
            }
        }

        if (feedbackText == null)
        {
            Transform existingFeedback = FindChildRecursive(transform, "FeedbackText");
            if (existingFeedback != null)
            {
                feedbackText = existingFeedback.GetComponent<TextMeshProUGUI>();
            }
        }

        if (feedbackText == null)
        {
            feedbackText = CreateTMPText(
                "FeedbackText",
                transform,
                string.Empty,
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                FeedbackErrorColor,
                fontAsset);
            feedbackText.enableAutoSizing = true;
            feedbackText.fontSizeMin = 14f;
            feedbackText.fontSizeMax = 26f;

            RectTransform fbRect = feedbackText.GetComponent<RectTransform>();
            fbRect.anchorMin = new Vector2(0.05f, 0.015f);
            fbRect.anchorMax = new Vector2(0.95f, 0.085f);
            fbRect.pivot = new Vector2(0.5f, 0.5f);
            fbRect.anchoredPosition = Vector2.zero;
            fbRect.sizeDelta = Vector2.zero;

            feedbackText.gameObject.SetActive(false);
        }
    }

    private void UpdateSlotPriceLabel(Component slotComponent, int price, bool isDiscounted, bool isCardSlot)
    {
        TextMeshProUGUI priceTmp = GetOrCreatePriceText(slotComponent, isCardSlot);
        if (priceTmp != null)
        {
            priceTmp.text = $"{price}G";
            priceTmp.color = isDiscounted ? DiscountPriceColor : NormalPriceColor;
        }
    }

    private TextMeshProUGUI GetOrCreatePriceText(Component slotComponent, bool isCardSlot)
    {
        if (slotComponent == null) return null;

        if (_slotPriceTexts.TryGetValue(slotComponent, out TextMeshProUGUI cached) && cached != null)
        {
            return cached;
        }

        Transform existing = FindChildRecursive(slotComponent.transform, "PriceText");
        if (existing != null)
        {
            TextMeshProUGUI existingTmp = existing.GetComponent<TextMeshProUGUI>();
            if (existingTmp != null)
            {
                _slotPriceTexts[slotComponent] = existingTmp;
                return existingTmp;
            }
        }

        GameObject badgeObj = CreateUIObject("PriceBadge", slotComponent.transform);
        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.05f, 0f);
        badgeRect.anchorMax = new Vector2(0.95f, 0f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -4f);
        badgeRect.sizeDelta = new Vector2(0f, isCardSlot ? 28f : 24f);

        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0.08f, 0.09f, 0.13f, 0.90f);
        badgeBg.raycastTarget = false;

        TextMeshProUGUI priceTmp = CreateTMPText(
            "PriceText",
            badgeObj.transform,
            string.Empty,
            isCardSlot ? 20f : 16f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            NormalPriceColor,
            TMPFontUtility.ResolveDefaultFont(rerollButtonText, feedbackText));
        priceTmp.enableAutoSizing = true;
        priceTmp.fontSizeMin = 11f;
        priceTmp.fontSizeMax = isCardSlot ? 22f : 18f;

        RectTransform textRect = priceTmp.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 1f);
        textRect.offsetMax = new Vector2(-4f, -1f);

        _slotPriceTexts[slotComponent] = priceTmp;
        return priceTmp;
    }

    private static Player ResolveFallbackPlayer()
    {
        return GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;
            if (child.name == targetName) return child;

            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null) return nested;
        }

        return null;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateTMPText(
        string name,
        Transform parent,
        string content,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color,
        TMP_FontAsset fontAsset)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }
        return tmp;
    }

    private static void DestroyOrImmediate(GameObject target)
    {
        if (target == null) return;
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
