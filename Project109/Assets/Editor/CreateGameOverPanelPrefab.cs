using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CreateGameOverPanelPrefab
{
    [MenuItem("Tools/UI/Create GameOverPanel Prefab")]
    public static void CreatePrefab()
    {
        string directoryPath = "Assets/Prefab/UI";
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string prefabPath = Path.Combine(directoryPath, "GameOverPanel.prefab");

        // 1. 루트 오브젝트 생성 (GameOverPanel)
        GameObject root = new GameObject("GameOverPanel", typeof(RectTransform), typeof(GameOverPanel));
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        rootRt.pivot = new Vector2(0.5f, 0.5f);

        // 2. 배경 오버레이 (Dark Dim Background)
        GameObject bgOverlay = new GameObject("BackgroundOverlay", typeof(RectTransform), typeof(Image));
        bgOverlay.transform.SetParent(root.transform, false);
        RectTransform bgRt = bgOverlay.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgOverlay.GetComponent<Image>();
        bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);
        bgImg.raycastTarget = true;

        // 3. 중앙 카드 컨테이너 (ContentCard)
        GameObject contentCard = new GameObject("ContentCard", typeof(RectTransform), typeof(Image));
        contentCard.transform.SetParent(root.transform, false);
        RectTransform cardRt = contentCard.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(720f, 480f);
        cardRt.anchoredPosition = Vector2.zero;
        Image cardImg = contentCard.GetComponent<Image>();
        cardImg.color = new Color(0.12f, 0.12f, 0.16f, 0.96f);

        // 4. 타이틀 텍스트 (TitleText: "DEFEAT")
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(contentCard.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -40f);
        titleRt.sizeDelta = new Vector2(-40f, 65f);
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "DEFEAT";
        titleTmp.fontSize = 52f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(0.92f, 0.22f, 0.25f, 1f); // Blood Red

        // 5. 서브타이틀 텍스트 (SubtitleText)
        GameObject subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGo.transform.SetParent(contentCard.transform, false);
        RectTransform subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f);
        subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -105f);
        subRt.sizeDelta = new Vector2(-60f, 32f);
        TextMeshProUGUI subTmp = subGo.GetComponent<TextMeshProUGUI>();
        subTmp.text = "던전 탐험 중 전투에서 쓰러졌습니다...";
        subTmp.fontSize = 20f;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.75f, 0.75f, 0.8f, 1f);

        // 6. 구분선 (Divider)
        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(contentCard.transform, false);
        RectTransform divRt = divider.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0.1f, 1f);
        divRt.anchorMax = new Vector2(0.9f, 1f);
        divRt.pivot = new Vector2(0.5f, 0.5f);
        divRt.anchoredPosition = new Vector2(0f, -150f);
        divRt.sizeDelta = new Vector2(0f, 2f);
        Image divImg = divider.GetComponent<Image>();
        divImg.color = new Color(0.3f, 0.3f, 0.38f, 0.6f);

        // 7. 결과 통계 박스 (StatsBox & ResultStatsText)
        GameObject statsBox = new GameObject("StatsBox", typeof(RectTransform), typeof(Image));
        statsBox.transform.SetParent(contentCard.transform, false);
        RectTransform boxRt = statsBox.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.anchoredPosition = new Vector2(0f, -15f);
        boxRt.sizeDelta = new Vector2(600f, 110f);
        Image boxImg = statsBox.GetComponent<Image>();
        boxImg.color = new Color(0.08f, 0.08f, 0.11f, 0.8f);

        GameObject statsTextGo = new GameObject("ResultStatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statsTextGo.transform.SetParent(statsBox.transform, false);
        RectTransform statsRt = statsTextGo.GetComponent<RectTransform>();
        statsRt.anchorMin = Vector2.zero;
        statsRt.anchorMax = Vector2.one;
        statsRt.offsetMin = new Vector2(25f, 12f);
        statsRt.offsetMax = new Vector2(-25f, -12f);
        TextMeshProUGUI statsTmp = statsTextGo.GetComponent<TextMeshProUGUI>();
        statsTmp.text = "• 도달 층수: 1F (전투 구역)\n• 보유 골드: 0 G";
        statsTmp.fontSize = 22f;
        statsTmp.lineSpacing = 18f;
        statsTmp.alignment = TextAlignmentOptions.MidlineLeft;
        statsTmp.color = new Color(0.9f, 0.9f, 0.92f, 1f);

        // 8. 하단 버튼 영역
        // 8-1. 은신처로 복귀 버튼 (ReturnToHideoutButton)
        GameObject hideoutBtnGo = new GameObject("ReturnToHideoutButton", typeof(RectTransform), typeof(Image), typeof(Button));
        hideoutBtnGo.transform.SetParent(contentCard.transform, false);
        RectTransform hideoutRt = hideoutBtnGo.GetComponent<RectTransform>();
        hideoutRt.anchorMin = new Vector2(0.5f, 0f);
        hideoutRt.anchorMax = new Vector2(0.5f, 0f);
        hideoutRt.pivot = new Vector2(0.5f, 0f);
        hideoutRt.anchoredPosition = new Vector2(-155f, 35f);
        hideoutRt.sizeDelta = new Vector2(270f, 54f);
        Image hideoutImg = hideoutBtnGo.GetComponent<Image>();
        hideoutImg.color = new Color(0.68f, 0.16f, 0.18f, 1f); // Dark Crimson Red
        Button hideoutBtn = hideoutBtnGo.GetComponent<Button>();

        GameObject hideoutBtnText = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        hideoutBtnText.transform.SetParent(hideoutBtnGo.transform, false);
        RectTransform hTextRt = hideoutBtnText.GetComponent<RectTransform>();
        hTextRt.anchorMin = Vector2.zero;
        hTextRt.anchorMax = Vector2.one;
        hTextRt.offsetMin = Vector2.zero;
        hTextRt.offsetMax = Vector2.zero;
        TextMeshProUGUI hTmp = hideoutBtnText.GetComponent<TextMeshProUGUI>();
        hTmp.text = "은신처로 복귀";
        hTmp.fontSize = 22f;
        hTmp.fontStyle = FontStyles.Bold;
        hTmp.alignment = TextAlignmentOptions.Center;
        hTmp.color = Color.white;

        // 8-2. 타이틀로 이동 버튼 (ReturnToTitleButton)
        GameObject titleBtnGo = new GameObject("ReturnToTitleButton", typeof(RectTransform), typeof(Image), typeof(Button));
        titleBtnGo.transform.SetParent(contentCard.transform, false);
        RectTransform titleBtnRt = titleBtnGo.GetComponent<RectTransform>();
        titleBtnRt.anchorMin = new Vector2(0.5f, 0f);
        titleBtnRt.anchorMax = new Vector2(0.5f, 0f);
        titleBtnRt.pivot = new Vector2(0.5f, 0f);
        titleBtnRt.anchoredPosition = new Vector2(155f, 35f);
        titleBtnRt.sizeDelta = new Vector2(270f, 54f);
        Image titleBtnImg = titleBtnGo.GetComponent<Image>();
        titleBtnImg.color = new Color(0.24f, 0.25f, 0.3f, 1f); // Charcoal
        Button titleBtn = titleBtnGo.GetComponent<Button>();

        GameObject titleBtnText = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleBtnText.transform.SetParent(titleBtnGo.transform, false);
        RectTransform tTextRt = titleBtnText.GetComponent<RectTransform>();
        tTextRt.anchorMin = Vector2.zero;
        tTextRt.anchorMax = Vector2.one;
        tTextRt.offsetMin = Vector2.zero;
        tTextRt.offsetMax = Vector2.zero;
        TextMeshProUGUI tTmp = titleBtnText.GetComponent<TextMeshProUGUI>();
        tTmp.text = "메인 메뉴로";
        tTmp.fontSize = 22f;
        tTmp.fontStyle = FontStyles.Bold;
        tTmp.alignment = TextAlignmentOptions.Center;
        tTmp.color = Color.white;

        // 9. GameOverPanel 컴포넌트 직렬화 필드 연결 (동적 요소만 바인딩)
        GameOverPanel panel = root.GetComponent<GameOverPanel>();
        SerializedObject serializedObj = new SerializedObject(panel);
        serializedObj.FindProperty("resultStatsText").objectReferenceValue = statsTmp;
        serializedObj.FindProperty("returnToHideoutButton").objectReferenceValue = hideoutBtn;
        serializedObj.FindProperty("returnToTitleButton").objectReferenceValue = titleBtn;
        serializedObj.ApplyModifiedPropertiesWithoutUndo();

        // 10. 프리팹 에셋으로 저장
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan>[GameOverPanel] 프리팹 생성 완료: {prefabPath}</color>");

        // 11. Addressables에 자동 등록
        AddressableAutoRegister.RegisterAllUIPrefabs();
    }
}
