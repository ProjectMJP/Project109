using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 메인 메뉴(타이틀 씬)의 UI 입력 및 3개 세이브 슬롯 로직을 관리하는 컨트롤러입니다.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Menu Buttons")]
    [SerializeField] private Button startButton; // 새로하기 버튼 (또는 시작 버튼)
    [SerializeField] private Button settingsButton; // 설정 버튼
    [SerializeField] private Button quitButton; // 종료 버튼
    [SerializeField] private Button creditsButton; // 제작 크레딧 버튼

    [Header("Save Slot UI")]
    [SerializeField] private GameObject slotSelectionPanel; // 슬롯 선택 패널
    [SerializeField] private Button[] slotButtons = new Button[3]; // 슬롯 1, 2, 3 버튼들
    [SerializeField] private Text[] slotTexts = new Text[3]; // 슬롯들의 상태 텍스트
    [SerializeField] private Button closeButton; // 슬롯 선택 창 닫기 버튼


    [Header("Default Starter Kit")]
    [SerializeField] private string defaultStarterKitId = "warrior_starter";

    // 런타임 동적 빌드 Awake는 제거하고 인스펙터에서 바인딩된 컴포넌트들을 직접 사용합니다.

    private void Start()
    {
        // 1. 에셋 로딩 대기를 위해 시작 시 버튼 비활성화 (AssetCacheManager가 있는 경우)
        SetMenuButtonsInteractable(false);

        // 2. 버튼 리스너 바인딩
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);
        if (creditsButton != null) creditsButton.onClick.AddListener(OnCreditsButtonClicked);

        // 슬롯 선택 패널 초기화 및 버튼 매핑
        if (slotSelectionPanel != null) slotSelectionPanel.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseButtonClicked);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slotIndex = i + 1; // 1-based index
            if (slotButtons[i] != null)
            {
                slotButtons[i].onClick.AddListener(() => OnSlotClicked(slotIndex));
            }
        }


        // 3. 에셋 로드 완료 감지를 위한 폴링 코루틴 혹은 상태 감지
        StartCoroutine(WaitForAssetLoadComplete());
    }

    private IEnumerator WaitForAssetLoadComplete()
    {
        // AssetCacheManager가 씬에 배치되었는지 확인하고 대기 (최대 2초 대기 후 없으면 스킵)
        float maxWait = 2.0f;
        float elapsed = 0.0f;
        while (AssetCacheManager.instance == null && elapsed < maxWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // AssetCacheManager가 존재하면 데이터 로딩이 완전히 완료될 때까지 대기
        if (AssetCacheManager.instance != null)
        {
            while (!AssetCacheManager.instance.isLoadComplete)
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.1f);

        SetMenuButtonsInteractable(true);
        RefreshSlotUI();
    }

    private void SetMenuButtonsInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
        if (settingsButton != null) settingsButton.interactable = interactable;
        if (quitButton != null) quitButton.interactable = interactable;
        if (creditsButton != null) creditsButton.interactable = interactable;
    }

    // [시작/새로하기] 버튼 클릭 시 -> 슬롯 선택 창 활성화
    private void OnStartButtonClicked()
    {
        Debug.Log("[MainMenuController] 시작 버튼 클릭됨. 슬롯 선택 패널을 표시합니다.");
        if (slotSelectionPanel != null)
        {
            slotSelectionPanel.SetActive(true);
            RefreshSlotUI();
        }
        else
        {
            // 슬롯 패널이 설계되지 않은 심플 모드의 경우, 1번 슬롯 강제 연동
            OnSlotClicked(1);
        }
    }

    // 슬롯 UI 상태 새로고침
    private void RefreshSlotUI()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slotIndex = i + 1;
            bool hasSave = SaveSystem.HasSaveData(slotIndex);

            if (slotTexts[i] != null)
            {
                if (hasSave)
                {
                    RunSaveData saveData = SaveSystem.LoadGameData(slotIndex);
                    if (saveData != null)
                    {
                        slotTexts[i].text = $"슬롯 {slotIndex}\n[이어서 하기]\n스테이지: {saveData.currentStageLevel} / 골드: {saveData.playerGold}";
                    }
                    else
                    {
                        slotTexts[i].text = $"슬롯 {slotIndex}\n[이어서 하기] (데이터 있음)";
                    }
                }
                else
                {
                    slotTexts[i].text = $"슬롯 {slotIndex}\n[새로 하기] (비어있음)";
                }
            }
        }
    }

    // 특정 슬롯을 클릭했을 때의 제어 로직 (사용자 설계 구현)
    private void OnSlotClicked(int slotIndex)
    {
        bool hasSave = SaveSystem.HasSaveData(slotIndex);
        Debug.Log($"[MainMenuController] 슬롯 {slotIndex} 클릭됨. 세이브 데이터 존재 여부: {hasSave}");

        // 선택한 세이브 슬롯을 GameSceneManager에 전달
        GameSceneManager.selectedSaveSlot = slotIndex;

        // 인게임 메인 Persistent 씬(GameScene) 로딩
        string gameScenePath = "Assets/Scenes/Release/GameScene.unity";
        if (SceneLoadManager.instance != null)
        {
            SceneLoadManager.instance.LoadScene(gameScenePath);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameScenePath);
        }
    }

    /// <summary>
    /// 특정 슬롯의 세이브 데이터를 삭제하고 슬롯 UI 텍스트를 갱신합니다.
    /// </summary>
    public void OnDeleteSlotClicked(int slotIndex)
    {
        Debug.Log($"[MainMenuController] 슬롯 {slotIndex} 삭제 요청.");
        SaveSystem.DeleteSaveFile(slotIndex);
        RefreshSlotUI();
    }

    private void OnCloseButtonClicked()
    {
        Debug.Log("[MainMenuController] 닫기 버튼 클릭됨. 슬롯 선택 패널을 비활성화합니다.");
        if (slotSelectionPanel != null)
        {
            slotSelectionPanel.SetActive(false);
        }
    }

    private void OnSettingsButtonClicked()
    {
        Debug.Log("[MainMenuController] 설정 창 오픈 시도.");
        // TODO: 설정 패널 활성화
    }


    private void OnQuitButtonClicked()
    {
        Debug.Log("[MainMenuController] 게임 종료.");
        Application.Quit();
    }

    private void OnCreditsButtonClicked()
    {
        Debug.Log("[MainMenuController] 크레딧 창 오픈 시도.");
        // TODO: 크레딧 패널 활성화
    }
}

