using System.Collections.Generic;
using GameItem.Types;
using UnityEngine;
using XLua;

/// <summary>
/// 은신처의 상호작용 가능한 시설 종류를 구분합니다.
/// </summary>
public enum HideoutFacilityType
{
    WeaponShowcase = 0,
    MemoryAltar = 1,
    CompendiumArchive = 2
}

/// <summary>
/// 은신처에 배치되는 시설(무기 진열대, 기억의 제단, 기록 보관소) 오브젝트의 클릭 상호작용과
/// WorldUI 라벨, 임시 도감 카드 리소스 수명주기를 관리합니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HideoutFacilityObject : MonoBehaviour, IInteractable
{
    [Header("은신처 시설 설정")]
    [SerializeField] private HideoutFacilityType _facilityType;
    [SerializeField] private bool _requiresCameraFocus = false;
    [SerializeField] private HideoutWorldLabel _worldLabel = null;
    [SerializeField] private string _labelText = string.Empty;

    private readonly List<Card> _cachedCompendiumCards = new List<Card>();

    public bool RequiresCameraFocus => _requiresCameraFocus;

    private void Awake()
    {
        EnsureNPCLayer();
        if (_worldLabel == null)
        {
            TryGetComponent<HideoutWorldLabel>(out _worldLabel);
        }
        LuaManager.OnBeforeLuaEnvDispose -= DisposeCompendiumCards;
        LuaManager.OnBeforeLuaEnvDispose += DisposeCompendiumCards;
    }

    private void Start()
    {
        ApplyWorldLabel();
    }

    private void OnDestroy()
    {
        LuaManager.OnBeforeLuaEnvDispose -= DisposeCompendiumCards;
        DisposeCompendiumCards();
    }

    public void OnInteract()
    {
        switch (_facilityType)
        {
            case HideoutFacilityType.WeaponShowcase:
                OpenWeaponShowcase();
                break;
            case HideoutFacilityType.MemoryAltar:
                OpenMemoryAltar();
                break;
            case HideoutFacilityType.CompendiumArchive:
                OpenCompendiumArchive();
                break;
            default:
                Debug.LogWarning($"[Hideout] 알 수 없는 은신처 시설 타입입니다: {_facilityType}");
                break;
        }
    }

    private void EnsureNPCLayer()
    {
        // PlayerExploreController 클릭 레이캐스트("Player", "Enemy", "NPC")가 감지하도록 루트 콜라이더 레이어 보장
        int npcLayer = LayerMask.NameToLayer("NPC");
        if (npcLayer != -1)
        {
            gameObject.layer = npcLayer;
        }
    }

    private void ApplyWorldLabel()
    {
        if (_worldLabel == null)
        {
            Debug.LogError($"[HideoutFacilityObject] '{gameObject.name}' 프리팹에 HideoutWorldLabel이 누락되었습니다.", this);
            return;
        }

        _worldLabel.SetLabelText(GetEffectiveLabelText());
    }

    private string GetEffectiveLabelText()
    {
        if (!string.IsNullOrEmpty(_labelText))
        {
            return _labelText;
        }

        switch (_facilityType)
        {
            case HideoutFacilityType.WeaponShowcase:
                return "무기 진열대";
            case HideoutFacilityType.MemoryAltar:
                return "기억의 제단";
            case HideoutFacilityType.CompendiumArchive:
                return "기록 보관소 (도감)";
            default:
                return "시설";
        }
    }

    private void OpenWeaponShowcase()
    {
        if (UIManager.instance == null)
        {
            Debug.LogWarning("[Hideout] UIManager 인스턴스가 없어 무기 진열대를 열 수 없습니다.");
            return;
        }

        bool isCached = UIManager.instance.GetUI<UIPanelBase>(UIConstants.PANEL_STARTER_KIT_SHOWCASE) != null;
        bool isRegistered = AssetCacheManager.instance != null &&
                            AssetCacheManager.instance.TryGetUI(UIConstants.PANEL_STARTER_KIT_SHOWCASE, out _);

        if (!isCached && !isRegistered)
        {
            Debug.Log($"[Hideout] 무기 진열대({UIConstants.PANEL_STARTER_KIT_SHOWCASE}) 프리팹이 아직 등록되지 않았습니다. (준비 중)");
            return;
        }

        UIManager.instance.OpenUI(UIConstants.PANEL_STARTER_KIT_SHOWCASE, UILayerType.Popup, true);
    }

    private void OpenMemoryAltar()
    {
        int memoryShards = 0;
        if (GameSceneManager.instance != null &&
            GameSceneManager.instance.player != null &&
            GameSceneManager.instance.player.playerStat != null)
        {
            memoryShards = GameSceneManager.instance.player.playerStat.InGameCurrencyMemorySharp;
        }

        string title = "기억의 제단";
        string message = $"기억의 제단(메타 성장) 기능은 현재 준비 중입니다.\n보유 기억의 조각: {memoryShards}개";

        bool hasConfirmPrefab = UIManager.instance != null &&
                                (UIManager.instance.GetUI<UIPanelBase>(UIConstants.PANEL_CONFIRM_DIALOG) != null ||
                                 (AssetCacheManager.instance != null &&
                                  AssetCacheManager.instance.TryGetUI(UIConstants.PANEL_CONFIRM_DIALOG, out _)));

        if (hasConfirmPrefab)
        {
            UIManager.instance.ShowConfirmDialog(title, message, null, null);
            return;
        }

        bool hasDialogueUI = DialogueManager.Instance != null &&
                             !DialogueManager.Instance.IsDialogueActive &&
                             AssetCacheManager.instance != null &&
                             AssetCacheManager.instance.TryGetUI(UIConstants.PANEL_DIALOGUE, out _);

        if (hasDialogueUI)
        {
            DialogueData noticeData = new DialogueData
            {
                dialogueID = "Hideout_MemoryAltar_Notice",
                startNodeID = "START",
                nodes = new List<DialogueNode>
                {
                    new DialogueNode
                    {
                        nodeID = "START",
                        speakerName = title,
                        lines = new List<string> { message },
                        choices = new List<ChoiceData>
                        {
                            new ChoiceData
                            {
                                description = "확인",
                                nextNodeID = string.Empty
                            }
                        }
                    }
                }
            };

            DialogueManager.Instance.StartDialogue(noticeData);
            return;
        }

        Debug.Log($"[Hideout] {title} - {message}");
    }

    private void OpenCompendiumArchive()
    {
        if (UIManager.instance == null)
        {
            Debug.LogWarning("[Hideout] UIManager 인스턴스가 없어 기록 보관소를 열 수 없습니다.");
            return;
        }

        bool hasDeckPanel = UIManager.instance.GetUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK) != null ||
                            (AssetCacheManager.instance != null &&
                             AssetCacheManager.instance.TryGetUI(UIConstants.PANEL_CARD_DECK, out _));

        if (!hasDeckPanel)
        {
            Debug.LogWarning($"[Hideout] {UIConstants.PANEL_CARD_DECK} 프리팹이 등록되지 않아 기록 보관소를 열 수 없습니다.");
            return;
        }

        EnsureCompendiumCardsBuilt();

        CardDeckViewPanel deckPanel = UIManager.instance.OpenUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK, UILayerType.Popup, false);
        if (deckPanel == null)
        {
            Debug.LogWarning("[Hideout] CardDeckViewPanel을 열 수 없습니다.");
            return;
        }

        if (!deckPanel.gameObject.activeSelf)
        {
            deckPanel.uiLayerType = UILayerType.Popup;
            UIManager.instance.PushActiveUIPanel(deckPanel);
        }

        deckPanel.OpenCompendium("기록 보관소 (도감)", _cachedCompendiumCards);
    }

    private void EnsureCompendiumCardsBuilt()
    {
        if (_cachedCompendiumCards.Count > 0) return;

        if (ModLoader.Instance == null || ModLoader.Instance.CardDatabase == null)
        {
            Debug.LogWarning("[Hideout] ModLoader.CardDatabase가 로드되지 않아 도감 카드를 구성할 수 없습니다.");
            return;
        }

        List<CardData> sortedCards = new List<CardData>(ModLoader.Instance.CardDatabase.Count);
        foreach (CardData cardData in ModLoader.Instance.CardDatabase.Values)
        {
            if (cardData != null && cardData.obtainSource != ItemObtainSource.TestOnly)
            {
                sortedCards.Add(cardData);
            }
        }

        sortedCards.Sort(CompareCardDataForCompendium);

        for (int i = 0; i < sortedCards.Count; i++)
        {
            // 도감 전용 임시 카드이므로 owner는 null로 두고 음수 runtimeID를 부여해 덱 카드와 충돌 방지
            Card compendiumCard = ModObjectFactory.CreateCard(sortedCards[i], null, -(i + 1));
            if (compendiumCard != null)
            {
                _cachedCompendiumCards.Add(compendiumCard);
            }
        }
    }

    private void DisposeCompendiumCards()
    {
        for (int i = 0; i < _cachedCompendiumCards.Count; i++)
        {
            Card card = _cachedCompendiumCards[i];
            if (card != null)
            {
                card.Dispose();
            }
        }
        _cachedCompendiumCards.Clear();
    }

    private static int CompareCardDataForCompendium(CardData a, CardData b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        int rarityCompare = a.rarity.CompareTo(b.rarity);
        if (rarityCompare != 0) return rarityCompare;

        int typeCompare = string.CompareOrdinal(a.cardType, b.cardType);
        if (typeCompare != 0) return typeCompare;

        int nameCompare = string.CompareOrdinal(a.LocalizedName, b.LocalizedName);
        if (nameCompare != 0) return nameCompare;

        return string.CompareOrdinal(a.cardName, b.cardName);
    }
}
