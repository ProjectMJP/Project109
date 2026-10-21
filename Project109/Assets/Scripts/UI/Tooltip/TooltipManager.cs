using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using XLua;

public struct TooltipData
{
    public string header;
    public string body;

    public TooltipData(string header, string body)
    {
        this.header = header;
        this.body = body;
    }
}

[RequireComponent(typeof(RectTransform))]
public class TooltipManager : MonoBehaviour
{
    private static TooltipManager instance;
    public static TooltipManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TooltipManager>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    Debug.LogError("[TooltipManager] TooltipManager가 씬에 존재하지 않습니다! UIManager.prefab 구성을 확인하세요.");
                }
            }
            return instance;
        }
    }

    // 네임스페이스 키워드 정규식: [타입.ID] (예: [effect.burning], [cardtag.exhaust] 3)
    private static readonly Regex KeywordRegex = new Regex(@"\[(effect|card|relic|cardtag)\.([a-zA-Z0-9_]+)\](?:\s*(\d+))?", RegexOptions.Compiled);

    [Header("Layout Settings")]
    [SerializeField] private Vector2 mouseOffset = new Vector2(15, -15);
    [SerializeField] private float padding = 15f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private VerticalLayoutGroup layoutGroup;
    private ContentSizeFitter sizeFitter;

    [Header("Prefabs")]
    [SerializeField] private TooltipPanel panelTemplatePrefab;
    private List<TooltipPanel> activePanels = new List<TooltipPanel>();
    private List<TooltipPanel> panelPool = new List<TooltipPanel>();

    // 고정 배치 타겟 임시 보관
    private RectTransform currentTargetRect;
    private readonly Vector3[] cornersCache = new Vector3[4];

    /// <summary>
    /// [type.id] 키워드 텍스트를 유저 화면용 컬러/이름으로 치환해줍니다.
    /// 예: [effect.burning] ➡ <color=#FF7F00>[화상]</color>
    /// </summary>
    public static string ReplaceKeywordsForDisplay(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return KeywordRegex.Replace(text, match =>
        {
            string type = match.Groups[1].Value;
            string id = match.Groups[2].Value;

            string displayName = GetDisplayName(type, id);
            if (string.IsNullOrEmpty(displayName))
            {
                return match.Value; // 찾지 못한 경우 원본 복원
            }

            string colorHex = GetColorForType(type);
            return $"<color={colorHex}>{displayName}</color>";
        });
    }

    private static string GetDisplayName(string type, string id)
    {
        switch (type.ToLower())
        {
            case "effect":
                if (ModLoader.Instance != null && ModLoader.Instance.EffectDatabase != null)
                {
                    if (ModLoader.Instance.EffectDatabase.TryGetValue(id, out EffectData effectData))
                    {
                        return effectData.displayName;
                    }
                }
                break;
            case "card":
                if (ModLoader.Instance != null && ModLoader.Instance.CardDatabase != null)
                {
                    if (ModLoader.Instance.CardDatabase.TryGetValue(id, out CardData cardData))
                    {
                        return cardData.cardName;
                    }
                }
                break;
            case "relic":
                if (ModLoader.Instance != null && ModLoader.Instance.RelicDatabase != null)
                {
                    if (ModLoader.Instance.RelicDatabase.TryGetValue(id, out RelicData relicData))
                    {
                        return relicData.relicName;
                    }
                }
                break;
            case "cardtag":
                if (LuaManager.Instance != null)
                {
                    LuaTable proto = LuaManager.Instance.GetCardTagPrototype(id);
                    if (proto != null)
                    {
                        var getDisplayName = proto.Get<System.Func<LuaTable, string>>("GetDisplayName");
                        string str = getDisplayName?.Invoke(proto);
                        if (!string.IsNullOrEmpty(str))
                        {
                            return str;
                        }
                    }
                }
                break;
        }
        return null;
    }

    private static string GetColorForType(string type)
    {
        switch (type.ToLower())
        {
            case "effect": return "#FF7F00"; // 주황 (버프/디버프)
            case "card": return "#32CD32";   // 연두 (카드 링크)
            case "relic": return "#DA70D6";  // 연보라 (유물 링크)
            case "cardtag": return "#FFCC00";    // 노랑 (카드 태그)
            default: return "#FFFFFF";
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            InitializeComponents();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializeComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        layoutGroup = GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            Debug.LogError("[TooltipManager] VerticalLayoutGroup이 누락되었습니다! TooltipContainer 설정을 확인하세요.", this);
        }

        sizeFitter = GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            Debug.LogError("[TooltipManager] ContentSizeFitter가 누락되었습니다! TooltipContainer 설정을 확인하세요.", this);
        }

        // 캐시 매니저를 통한 백업 로드 (프리팹에 미할당된 경우)
        if (panelTemplatePrefab == null && AssetCacheManager.instance != null)
        {
            if (AssetCacheManager.instance.TryGetUI("TooltipPanel", out GameObject panelPrefab))
            {
                panelTemplatePrefab = panelPrefab.GetComponent<TooltipPanel>();
            }
        }

        if (panelTemplatePrefab == null)
        {
            Debug.LogError("[TooltipManager] panelTemplatePrefab이 바인딩되지 않았습니다! TooltipManager 인스펙터를 확인하세요.", this);
        }
    }

    public void ShowTooltip(string title, string content, RectTransform targetRect = null)
    {
        // 1. 본문의 [type.id] 키워드를 유저용 강조 텍스트로 치환
        string displayTitle = ReplaceKeywordsForDisplay(title);
        string displayContent = ReplaceKeywordsForDisplay(content);

        List<TooltipData> tooltips = new List<TooltipData> { new TooltipData(displayTitle, displayContent) };
        
        // 2. 원본 본문(content) 스캔 후 추가 키워드 검출
        if (!string.IsNullOrEmpty(content))
        {
            MatchCollection matches = KeywordRegex.Matches(content);
            HashSet<string> scannedKeywords = new HashSet<string>();

            foreach (Match match in matches)
            {
                string type = match.Groups[1].Value;
                string id = match.Groups[2].Value;
                string valueStr = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;
                
                string uniqueKey = $"{type}.{id}".ToLower();
                if (!scannedKeywords.Add(uniqueKey)) continue; // 중복 추가 방지

                string subTitle = string.Empty;
                string subDescription = string.Empty;

                switch (type.ToLower())
                {
                    case "effect":
                        if (ModLoader.Instance != null && ModLoader.Instance.EffectDatabase != null)
                        {
                            if (ModLoader.Instance.EffectDatabase.TryGetValue(id, out EffectData effectData))
                            {
                                subTitle = effectData.displayName;
                                subDescription = effectData.description ?? string.Empty;

                                // 이펙트 템플릿의 {stacks} 등 플레이스홀더 치환
                                if (!string.IsNullOrEmpty(valueStr))
                                {
                                    subDescription = Regex.Replace(subDescription, @"\{stacks?\}", valueStr, RegexOptions.IgnoreCase);
                                    subDescription = Regex.Replace(subDescription, @"\{values?\}", valueStr, RegexOptions.IgnoreCase);
                                    subDescription = Regex.Replace(subDescription, @"\{amt\}", valueStr, RegexOptions.IgnoreCase);
                                }
                            }
                        }
                        break;

                    case "card":
                        if (ModLoader.Instance != null && ModLoader.Instance.CardDatabase != null)
                        {
                            if (ModLoader.Instance.CardDatabase.TryGetValue(id, out CardData cardData))
                            {
                                subTitle = cardData.cardName;
                                subDescription = cardData.GetDescription();
                            }
                        }
                        break;

                    case "relic":
                        if (ModLoader.Instance != null && ModLoader.Instance.RelicDatabase != null)
                        {
                            if (ModLoader.Instance.RelicDatabase.TryGetValue(id, out RelicData relicData))
                            {
                                subTitle = relicData.relicName;
                                subDescription = relicData.description ?? string.Empty;
                            }
                        }
                        break;

                    case "cardtag":
                        if (LuaManager.Instance != null)
                        {
                            LuaTable proto = LuaManager.Instance.GetCardTagPrototype(id);
                            if (proto != null)
                            {
                                var getDisplayName = proto.Get<System.Func<LuaTable, string>>("GetDisplayName");
                                var getDescription = proto.Get<System.Func<LuaTable, string>>("GetDescription");
                                
                                string dispName = getDisplayName?.Invoke(proto);
                                string descText = getDescription?.Invoke(proto);

                                subTitle = !string.IsNullOrEmpty(dispName) ? dispName : id;
                                subDescription = descText ?? string.Empty;
                            }
                        }
                        break;
                }

                if (!string.IsNullOrEmpty(subTitle))
                {
                    // 서브 툴팁 본문에 포함된 키워드도 렌더링 치환 적용
                    string finalSubTitle = ReplaceKeywordsForDisplay(subTitle);
                    string finalSubDescription = ReplaceKeywordsForDisplay(subDescription);
                    tooltips.Add(new TooltipData(finalSubTitle, finalSubDescription));
                }
            }
        }

        ShowTooltips(tooltips, targetRect);
    }

    public void ShowTooltips(List<TooltipData> tooltips, RectTransform targetRect = null)
    {
        if (tooltips == null || tooltips.Count == 0)
        {
            HideTooltip();
            return;
        }

        gameObject.SetActive(true);
        ClearCurrentPanels();

        currentTargetRect = targetRect;

        foreach (var data in tooltips)
        {
            if (string.IsNullOrEmpty(data.header) && string.IsNullOrEmpty(data.body)) continue;

            TooltipPanel panel = GetOrCreatePanel();
            if (panel != null)
            {
                panel.Setup(data.header, data.body);
                panel.transform.SetAsLastSibling();
                panel.gameObject.SetActive(true);
                activePanels.Add(panel);
            }
        }

        // 레이아웃 즉시 계산 및 위치 지정
        Canvas.ForceUpdateCanvases();
        UpdatePosition();
    }

    public void HideTooltip()
    {
        ClearCurrentPanels();
        currentTargetRect = null;
        gameObject.SetActive(false);
    }

    private void ClearCurrentPanels()
    {
        foreach (var panel in activePanels)
        {
            panel.gameObject.SetActive(false);
            panelPool.Add(panel);
        }
        activePanels.Clear();
    }

    private TooltipPanel GetOrCreatePanel()
    {
        if (panelTemplatePrefab == null) return null;

        if (panelPool.Count > 0)
        {
            TooltipPanel panel = panelPool[panelPool.Count - 1];
            panelPool.RemoveAt(panelPool.Count - 1);
            return panel;
        }

        TooltipPanel inst = Instantiate(panelTemplatePrefab, transform, false);
        return inst;
    }

    private void Update()
    {
        if (gameObject.activeSelf)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        if (rectTransform == null || parentCanvas == null) return;

        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        // 1. 캔버스 로컬 좌표계 기준 화면 경계 획득
        float minScreenX = canvasRect.rect.xMin;
        float maxScreenX = canvasRect.rect.xMax;
        float minScreenY = canvasRect.rect.yMin;
        float maxScreenY = canvasRect.rect.yMax;
        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;

        // 2. 전체 툴팁 뭉치의 스케일 보정 및 높이 획득
        float H = rectTransform.rect.height;
        float W = rectTransform.rect.width;

        float maxAvailableHeight = screenHeight - (padding * 2f);
        if (H > maxAvailableHeight)
        {
            float scaleFactor = maxAvailableHeight / H;
            transform.localScale = Vector3.one * scaleFactor;
            H = maxAvailableHeight;
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        // [마우스 실시간 추적] -> 캔버스 로컬 좌표로 변환
        Vector2 mousePos = Vector2.zero;
        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            mousePos = Input.mousePosition;
        }

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 canvasMousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mousePos, eventCamera, out canvasMousePos);

        float targetCenterX = canvasMousePos.x;
        float targetCenterY = canvasMousePos.y;

        // 3. X축 배치 결정 (오른쪽 vs 왼쪽 피벗 반전 정렬)
        float pivotX = 0f;
        float finalX = 0f;

        // 마우스 기준 배치
        float expectedRightX = targetCenterX + mouseOffset.x;
        if (expectedRightX + W > maxScreenX - padding)
        {
            pivotX = 1f;
            finalX = targetCenterX - mouseOffset.x;
        }
        else
        {
            pivotX = 0f;
            finalX = expectedRightX;
        }

        // X축 최종 화면 밖 이탈 Clamping 방어 코드 (캔버스 로컬 바운더리 기준)
        if (pivotX == 0f)
        {
            finalX = Mathf.Clamp(finalX, minScreenX + padding, maxScreenX - padding - W);
        }
        else
        {
            finalX = Mathf.Clamp(finalX, minScreenX + padding + W, maxScreenX - padding);
        }

        // 4. Y축 배치 결정 (상단 정렬 기본 적용 및 하단 이탈 시 피벗 반전 정렬)
        float pivotY = 1f; // 기본: 상단 정렬
        float finalY = targetCenterY;

        // 아래로 뻗은 툴팁이 화면 하단을 벗어나면 하단 정렬로 반전
        if (finalY - H < minScreenY + padding)
        {
            pivotY = 0f;
            finalY = targetCenterY;
        }

        // Y축 최종 화면 밖 이탈 Clamping 방어 코드 (캔버스 로컬 바운더리 기준)
        if (pivotY == 1f)
        {
            finalY = Mathf.Clamp(finalY, minScreenY + padding + H, maxScreenY - padding);
        }
        else
        {
            finalY = Mathf.Clamp(finalY, minScreenY + padding, maxScreenY - padding - H);
        }

        // 5. 위치 및 피벗 적용
        rectTransform.pivot = new Vector2(pivotX, pivotY);
        rectTransform.anchoredPosition = new Vector2(finalX, finalY);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
