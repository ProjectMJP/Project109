using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupBattleUIPanelPrefab
{
    [MenuItem("Tools/UI/Setup BattleUIPanel Prefab References")]
    public static void SetupReferences()
    {
        string prefabPath = "Assets/Prefab/UI/Battle/BattleUIPanel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError($"[SetupBattleUIPanel] 프리팹을 불러오지 못했습니다: {prefabPath}");
            return;
        }

        // 0. TMP 기본 폰트 에셋 로드 (기존 텍스트에서 폰트 에셋 추출)
        TMP_FontAsset baseFont = null;
        TextMeshProUGUI existingTmp = root.GetComponentInChildren<TextMeshProUGUI>();
        if (existingTmp != null)
        {
            baseFont = existingTmp.font;
        }
        if (baseFont == null)
        {
            baseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Default.asset");
        }

        // 1. 루트 BattleUIPanel 컴포넌트 구성
        BattleHandPanel oldRootHand = root.GetComponent<BattleHandPanel>();
        BattleUIPanel battleUIPanel = root.GetComponent<BattleUIPanel>();
        if (battleUIPanel == null)
        {
            battleUIPanel = root.AddComponent<BattleUIPanel>();
        }

        RectTransform rootRect = root.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        // 2. BattleHandPanel 자식 오브젝트 구성
        Transform handPanelTrans = root.transform.Find("BattleHandPanel");
        GameObject handPanelObj;
        if (handPanelTrans == null)
        {
            handPanelObj = new GameObject("BattleHandPanel");
            handPanelObj.transform.SetParent(root.transform, false);
        }
        else
        {
            handPanelObj = handPanelTrans.gameObject;
        }

        RectTransform handRect = handPanelObj.GetComponent<RectTransform>();
        if (handRect == null) handRect = handPanelObj.AddComponent<RectTransform>();
        handRect.anchorMin = new Vector2(0f, 0f);
        handRect.anchorMax = new Vector2(1f, 1f);
        handRect.pivot = new Vector2(0.5f, 0f);
        handRect.offsetMin = Vector2.zero;
        handRect.offsetMax = Vector2.zero;

        BattleHandPanel handPanel = handPanelObj.GetComponent<BattleHandPanel>();
        if (handPanel == null) handPanel = handPanelObj.AddComponent<BattleHandPanel>();

        // 기존 DrawPileButton, UsedPileButton이 루트에 있다면 BattleHandPanel 하위로 이동
        Transform drawPileTransform = root.transform.Find("DrawPileButton");
        if (drawPileTransform == null) drawPileTransform = handPanelObj.transform.Find("DrawPileButton");
        if (drawPileTransform != null) drawPileTransform.SetParent(handPanelObj.transform, true);

        Transform usedPileTransform = root.transform.Find("UsedPileButton");
        if (usedPileTransform == null) usedPileTransform = handPanelObj.transform.Find("UsedPileButton");
        if (usedPileTransform != null) usedPileTransform.SetParent(handPanelObj.transform, true);

        Button drawBtn = drawPileTransform != null ? drawPileTransform.GetComponent<Button>() : null;
        TextMeshProUGUI drawTmp = drawPileTransform != null ? drawPileTransform.GetComponentInChildren<TextMeshProUGUI>() : null;
        Button usedBtn = usedPileTransform != null ? usedPileTransform.GetComponent<Button>() : null;
        TextMeshProUGUI usedTmp = usedPileTransform != null ? usedPileTransform.GetComponentInChildren<TextMeshProUGUI>() : null;

        if (drawTmp != null) drawTmp.text = "0";
        if (usedTmp != null) usedTmp.text = "0";

        // 손패 카드 컨테이너(HandContent) 확인 및 생성
        Transform handContentTrans = handPanelObj.transform.Find("HandContent");
        if (handContentTrans == null)
        {
            GameObject handContentObj = new GameObject("HandContent");
            handContentObj.transform.SetParent(handPanelObj.transform, false);
            handContentTrans = handContentObj.transform;
            RectTransform hcRect = handContentObj.AddComponent<RectTransform>();
            hcRect.anchorMin = new Vector2(0.5f, 0f);
            hcRect.anchorMax = new Vector2(0.5f, 0f);
            hcRect.pivot = new Vector2(0.5f, 0f);
            hcRect.anchoredPosition = new Vector2(0f, 60f);
            hcRect.sizeDelta = new Vector2(1000f, 220f);
        }

        // 이동 모드 전환 버튼(SwitchToMoveButton) 확인 및 생성
        Transform moveBtnTrans = handPanelObj.transform.Find("SwitchToMoveButton");
        GameObject moveBtnObj;
        if (moveBtnTrans == null)
        {
            moveBtnObj = new GameObject("SwitchToMoveButton");
            moveBtnObj.transform.SetParent(handPanelObj.transform, false);
        }
        else
        {
            moveBtnObj = moveBtnTrans.gameObject;
        }

        RectTransform mbRect = moveBtnObj.GetComponent<RectTransform>();
        if (mbRect == null) mbRect = moveBtnObj.AddComponent<RectTransform>();
        mbRect.anchorMin = new Vector2(0f, 0f);
        mbRect.anchorMax = new Vector2(0f, 0f);
        mbRect.pivot = new Vector2(0.5f, 0.5f);
        mbRect.anchoredPosition = new Vector2(75f, 155f);
        mbRect.sizeDelta = new Vector2(110f, 45f);

        Image mbImage = moveBtnObj.GetComponent<Image>();
        if (mbImage == null) mbImage = moveBtnObj.AddComponent<Image>();
        mbImage.color = new Color(0.2f, 0.55f, 0.85f, 0.95f);

        Button mbButton = moveBtnObj.GetComponent<Button>();
        if (mbButton == null) mbButton = moveBtnObj.AddComponent<Button>();

        Transform mbTextTrans = moveBtnObj.transform.Find("Text (TMP)");
        GameObject mbTextObj;
        if (mbTextTrans == null)
        {
            mbTextObj = new GameObject("Text (TMP)");
            mbTextObj.transform.SetParent(moveBtnObj.transform, false);
        }
        else
        {
            mbTextObj = mbTextTrans.gameObject;
        }

        RectTransform mbtRect = mbTextObj.GetComponent<RectTransform>();
        if (mbtRect == null) mbtRect = mbTextObj.AddComponent<RectTransform>();
        mbtRect.anchorMin = Vector2.zero;
        mbtRect.anchorMax = Vector2.one;
        mbtRect.offsetMin = Vector2.zero;
        mbtRect.offsetMax = Vector2.zero;

        TextMeshProUGUI mbTmp = mbTextObj.GetComponent<TextMeshProUGUI>();
        if (mbTmp == null) mbTmp = mbTextObj.AddComponent<TextMeshProUGUI>();
        if (baseFont != null) mbTmp.font = baseFont;
        mbTmp.text = "이동 모드";
        mbTmp.fontSize = 20;
        mbTmp.alignment = TextAlignmentOptions.Center;
        mbTmp.color = Color.white;

        // BattleHandPanel 직렬화 바인딩
        SerializedObject serializedHand = new SerializedObject(handPanel);
        serializedHand.FindProperty("contentTransform").objectReferenceValue = handContentTrans;
        serializedHand.FindProperty("drawPileButton").objectReferenceValue = drawBtn;
        serializedHand.FindProperty("drawCountText").objectReferenceValue = drawTmp;
        serializedHand.FindProperty("usedPileButton").objectReferenceValue = usedBtn;
        serializedHand.FindProperty("usedCountText").objectReferenceValue = usedTmp;
        serializedHand.FindProperty("switchToMoveModeButton").objectReferenceValue = mbButton;
        serializedHand.FindProperty("modeSwitchButtonText").objectReferenceValue = mbTmp;
        serializedHand.ApplyModifiedPropertiesWithoutUndo();

        // 3. BattleMovePanel 자식 오브젝트 구성
        Transform movePanelTrans = root.transform.Find("BattleMovePanel");
        GameObject movePanelObj;
        if (movePanelTrans == null)
        {
            movePanelObj = new GameObject("BattleMovePanel");
            movePanelObj.transform.SetParent(root.transform, false);
        }
        else
        {
            movePanelObj = movePanelTrans.gameObject;
        }

        RectTransform movePanelRect = movePanelObj.GetComponent<RectTransform>();
        if (movePanelRect == null) movePanelRect = movePanelObj.AddComponent<RectTransform>();
        movePanelRect.anchorMin = new Vector2(0.5f, 0f);
        movePanelRect.anchorMax = new Vector2(0.5f, 0f);
        movePanelRect.pivot = new Vector2(0.5f, 0f);
        movePanelRect.anchoredPosition = new Vector2(0f, 40f);
        movePanelRect.sizeDelta = new Vector2(580f, 90f);

        Image movePanelBg = movePanelObj.GetComponent<Image>();
        if (movePanelBg == null) movePanelBg = movePanelObj.AddComponent<Image>();
        movePanelBg.color = new Color(0.1f, 0.12f, 0.16f, 0.92f);

        BattleMovePanel movePanel = movePanelObj.GetComponent<BattleMovePanel>();
        if (movePanel == null) movePanel = movePanelObj.AddComponent<BattleMovePanel>();

        // 이동 횟수 텍스트(MoveCountGroup)
        Transform moveCountTrans = movePanelObj.transform.Find("MoveCountText");
        GameObject moveCountObj;
        if (moveCountTrans == null)
        {
            moveCountObj = new GameObject("MoveCountText");
            moveCountObj.transform.SetParent(movePanelObj.transform, false);
        }
        else
        {
            moveCountObj = moveCountTrans.gameObject;
        }

        RectTransform mcRect = moveCountObj.GetComponent<RectTransform>();
        if (mcRect == null) mcRect = moveCountObj.AddComponent<RectTransform>();
        mcRect.anchorMin = new Vector2(0f, 0.5f);
        mcRect.anchorMax = new Vector2(0f, 0.5f);
        mcRect.pivot = new Vector2(0f, 0.5f);
        mcRect.anchoredPosition = new Vector2(25f, 15f);
        mcRect.sizeDelta = new Vector2(360f, 30f);

        TextMeshProUGUI mcTmp = moveCountObj.GetComponent<TextMeshProUGUI>();
        if (mcTmp == null) mcTmp = moveCountObj.AddComponent<TextMeshProUGUI>();
        if (baseFont != null) mcTmp.font = baseFont;
        mcTmp.text = "남은 이동 횟수: <color=#FFD54F>1</color>";
        mcTmp.fontSize = 20;
        mcTmp.color = Color.white;

        // 최대 이동 거리 텍스트(MaxTilesText)
        Transform maxTilesTrans = movePanelObj.transform.Find("MaxTilesText");
        GameObject maxTilesObj;
        if (maxTilesTrans == null)
        {
            maxTilesObj = new GameObject("MaxTilesText");
            maxTilesObj.transform.SetParent(movePanelObj.transform, false);
        }
        else
        {
            maxTilesObj = maxTilesTrans.gameObject;
        }

        RectTransform mtRect = maxTilesObj.GetComponent<RectTransform>();
        if (mtRect == null) mtRect = maxTilesObj.AddComponent<RectTransform>();
        mtRect.anchorMin = new Vector2(0f, 0.5f);
        mtRect.anchorMax = new Vector2(0f, 0.5f);
        mtRect.pivot = new Vector2(0f, 0.5f);
        mtRect.anchoredPosition = new Vector2(25f, -15f);
        mtRect.sizeDelta = new Vector2(360f, 30f);

        TextMeshProUGUI mtTmp = maxTilesObj.GetComponent<TextMeshProUGUI>();
        if (mtTmp == null) mtTmp = maxTilesObj.AddComponent<TextMeshProUGUI>();
        if (baseFont != null) mtTmp.font = baseFont;
        mtTmp.text = "1회 최대 이동: <color=#80D8FF>3 타일</color>";
        mtTmp.fontSize = 20;
        mtTmp.color = Color.white;

        // 카드 모드 복귀 버튼(SwitchToCardButton)
        Transform cardBtnTrans = movePanelObj.transform.Find("SwitchToCardButton");
        GameObject cardBtnObj;
        if (cardBtnTrans == null)
        {
            cardBtnObj = new GameObject("SwitchToCardButton");
            cardBtnObj.transform.SetParent(movePanelObj.transform, false);
        }
        else
        {
            cardBtnObj = cardBtnTrans.gameObject;
        }

        RectTransform cbRect = cardBtnObj.GetComponent<RectTransform>();
        if (cbRect == null) cbRect = cardBtnObj.AddComponent<RectTransform>();
        cbRect.anchorMin = new Vector2(1f, 0.5f);
        cbRect.anchorMax = new Vector2(1f, 0.5f);
        cbRect.pivot = new Vector2(1f, 0.5f);
        cbRect.anchoredPosition = new Vector2(-20f, 0f);
        cbRect.sizeDelta = new Vector2(130f, 48f);

        Image cbImage = cardBtnObj.GetComponent<Image>();
        if (cbImage == null) cbImage = cardBtnObj.AddComponent<Image>();
        cbImage.color = new Color(0.85f, 0.35f, 0.25f, 0.95f);

        Button cbButton = cardBtnObj.GetComponent<Button>();
        if (cbButton == null) cbButton = cardBtnObj.AddComponent<Button>();

        Transform cbTextTrans = cardBtnObj.transform.Find("Text (TMP)");
        GameObject cbTextObj;
        if (cbTextTrans == null)
        {
            cbTextObj = new GameObject("Text (TMP)");
            cbTextObj.transform.SetParent(cardBtnObj.transform, false);
        }
        else
        {
            cbTextObj = cbTextTrans.gameObject;
        }

        RectTransform cbtRect = cbTextObj.GetComponent<RectTransform>();
        if (cbtRect == null) cbtRect = cbTextObj.AddComponent<RectTransform>();
        cbtRect.anchorMin = Vector2.zero;
        cbtRect.anchorMax = Vector2.one;
        cbtRect.offsetMin = Vector2.zero;
        cbtRect.offsetMax = Vector2.zero;

        TextMeshProUGUI cbTmp = cbTextObj.GetComponent<TextMeshProUGUI>();
        if (cbTmp == null) cbTmp = cbTextObj.AddComponent<TextMeshProUGUI>();
        if (baseFont != null) cbTmp.font = baseFont;
        cbTmp.text = "카드 복귀";
        cbTmp.fontSize = 18;
        cbTmp.alignment = TextAlignmentOptions.Center;
        cbTmp.color = Color.white;

        // BattleMovePanel 직렬화 바인딩
        SerializedObject serializedMove = new SerializedObject(movePanel);
        serializedMove.FindProperty("moveCountText").objectReferenceValue = mcTmp;
        serializedMove.FindProperty("maxTilesText").objectReferenceValue = mtTmp;
        serializedMove.FindProperty("switchToCardModeButton").objectReferenceValue = cbButton;
        serializedMove.ApplyModifiedPropertiesWithoutUndo();

        // 4. 루트 BattleUIPanel 직하위에 EndTurnButton(턴 종료 버튼) 구성
        Transform endTurnTrans = root.transform.Find("EndTurnButton");
        GameObject endTurnObj;
        if (endTurnTrans == null)
        {
            endTurnObj = new GameObject("EndTurnButton");
            endTurnObj.transform.SetParent(root.transform, false);
        }
        else
        {
            endTurnObj = endTurnTrans.gameObject;
        }

        RectTransform etRect = endTurnObj.GetComponent<RectTransform>();
        if (etRect == null) etRect = endTurnObj.AddComponent<RectTransform>();
        etRect.anchorMin = new Vector2(1f, 0f);
        etRect.anchorMax = new Vector2(1f, 0f);
        etRect.pivot = new Vector2(0.5f, 0.5f);
        etRect.anchoredPosition = new Vector2(-75f, 155f);
        etRect.sizeDelta = new Vector2(110f, 45f);

        Image etImage = endTurnObj.GetComponent<Image>();
        if (etImage == null) etImage = endTurnObj.AddComponent<Image>();
        etImage.color = new Color(0.85f, 0.35f, 0.15f, 0.95f);

        Button etButton = endTurnObj.GetComponent<Button>();
        if (etButton == null) etButton = endTurnObj.AddComponent<Button>();

        Transform etTextTrans = endTurnObj.transform.Find("Text (TMP)");
        GameObject etTextObj;
        if (etTextTrans == null)
        {
            etTextObj = new GameObject("Text (TMP)");
            etTextObj.transform.SetParent(endTurnObj.transform, false);
        }
        else
        {
            etTextObj = etTextTrans.gameObject;
        }

        RectTransform ettRect = etTextObj.GetComponent<RectTransform>();
        if (ettRect == null) ettRect = etTextObj.AddComponent<RectTransform>();
        ettRect.anchorMin = Vector2.zero;
        ettRect.anchorMax = Vector2.one;
        ettRect.offsetMin = Vector2.zero;
        ettRect.offsetMax = Vector2.zero;

        TextMeshProUGUI etTmp = etTextObj.GetComponent<TextMeshProUGUI>();
        if (etTmp == null) etTmp = etTextObj.AddComponent<TextMeshProUGUI>();
        if (baseFont != null) etTmp.font = baseFont;
        etTmp.text = "턴 종료";
        etTmp.fontSize = 22;
        etTmp.fontStyle = FontStyles.Bold;
        etTmp.alignment = TextAlignmentOptions.Center;
        etTmp.color = Color.white;

        // 5. 루트 BattleUIPanel 직렬화 바인딩
        SerializedObject serializedRoot = new SerializedObject(battleUIPanel);
        serializedRoot.FindProperty("battleHandPanel").objectReferenceValue = handPanel;
        serializedRoot.FindProperty("battleMovePanel").objectReferenceValue = movePanel;
        serializedRoot.FindProperty("endTurnButton").objectReferenceValue = etButton;
        serializedRoot.ApplyModifiedPropertiesWithoutUndo();

        // 루트에 남아있던 구버전 BattleHandPanel 컴포넌트 제거 (자식으로 이전 완료)
        if (oldRootHand != null)
        {
            Object.DestroyImmediate(oldRootHand, true);
        }

        // 초기 상태: HandPanel 켜짐, MovePanel 꺼짐
        handPanelObj.SetActive(true);
        movePanelObj.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[BattleUIPanel] 계층 구조(BattleHandPanel + BattleMovePanel 토글) 및 필드 바인딩 완벽 구성 완료!</color>");
    }
}
