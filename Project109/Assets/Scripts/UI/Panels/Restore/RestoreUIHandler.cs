using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum RestoreUICategory
{
    Heal,
    Upgrade
}

public class RestoreUIHandler : UIPanelBase
{
    public TextMeshProUGUI description;

    public Button restoreButton;
    public Button upgradeButton;

    void Start()
    {
        if (restoreButton != null)
        {
            restoreButton.onClick.RemoveAllListeners();
            restoreButton.onClick.AddListener(HealHP);
            AddPointerEvent(restoreButton.gameObject, RestoreUICategory.Heal);
        }
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(EnableUpgradeUI);
            AddPointerEvent(upgradeButton.gameObject, RestoreUICategory.Upgrade);
        }
    }

    public void EnableUpgradeUI()
    {
        if (UIManager.instance != null)
        {
            UIManager.instance.OpenUI(UIConstants.PANEL_UPGRADE_CARD_DECK, UILayerType.Normal, true);
        }

        Close();
    }

    public void HealHP()
    {
        // 플레이어의 체력 회복 (최대 체력의 30%)
        Player player = GameSceneManager.instance != null ? GameSceneManager.instance.player : null;
        if (player != null && player.character != null)
        {
            float maxHp = player.character.maxHealth;
            int healAmount = Mathf.RoundToInt(maxHp * 0.3f);
            player.character.TakeHeal(new EventStructs.HealInfo(null, player.character, healAmount, EventStructs.HealFlag.Normal));
            Debug.Log($"[RestoreUIHandler] Player Hp Healed: +{healAmount} (Current: {player.character.curHealth}/{player.character.maxHealth})");
        }

        Close();
    }


    /// <summary>
    /// UI 버튼들에 이벤트를 추가함
    /// </summary>
    void AddPointerEvent(GameObject buttonGameObject, RestoreUICategory uiCategory)
    {
        // EventTrigger 컴포넌트 가져오기 또는 추가하기
        EventTrigger eventTrigger = buttonGameObject.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = buttonGameObject.AddComponent<EventTrigger>();
        }

        // PointerEnter 이벤트 트리거 엔트리 생성
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((eventData) => { OnPointerEnterAction(eventData, uiCategory); });

        // PointerExit 이벤트 트리거 엔트리 생성
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((eventData) => { OnPointerExitAction(eventData); });

        // EventTrigger에 추가
        eventTrigger.triggers.Add(entry);
        eventTrigger.triggers.Add(exitEntry);

        Debug.Log($"{buttonGameObject.name}에 OnPointer 이벤트가 성공적으로 등록되었습니다.");
    }

    public void OnPointerEnterAction(BaseEventData eventData, RestoreUICategory uiCategory)
    {
        if (description == null)
            return;

        // PointerEventData로 캐스팅 후 정보 획득
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData != null)
        {
            switch (uiCategory)
            {
                case RestoreUICategory.Heal:
                    description.text = "체력을 회복합니다. ( 30% )";
                    break;
                case RestoreUICategory.Upgrade:
                    description.text = "카드를 강화합니다";
                    break;
                default:
                    description.text = "";
                    break;
            }
        }
        else
        {
            Debug.Log("OnPointerEnterAction 호출됨!");
        }
    }

    public void OnPointerExitAction(BaseEventData eventData)
    {
        if (description == null)
            return;

        // PointerEventData로 캐스팅 후 정보 획득
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData != null)
        {
            description.text = "";  //글 초기화
        }
        else
        {
            Debug.Log("OnPointerExitAction 호출됨!");
        }
    }
}
