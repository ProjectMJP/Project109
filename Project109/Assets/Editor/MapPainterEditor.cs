using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class MapPainterEditor : EditorWindow
{
    private MapDataSO targetMapData;    //현재 수정 중인 맵 데이터
    private bool isEditing = false; //편집 모드 활성화 여부
    private bool showGridLines = true; //편집 중 그리드 선 시각화 여부

    // 레이어 시스템 추가
    public enum EditLayer { Terrain, Object, Event }
    private EditLayer currentLayer = EditLayer.Terrain;

    private List<string> currentTerrainPalette = new List<string>();
    private List<string> currentObjectPalette = new List<string>();
    private List<string> currentEventPalette = new List<string>();

    // 칠할 ID (현재는 직접 타이핑하지만 이후 버튼으로 바꿀 예정)
    private string currentBrushID = "Empty";
    private int selectedPaletteIndex = 0;

    private Vector2 paletteScrollPos;
    private const float LEFT_PANEL_WIDTH = 300f; // 좌측 설정 패널의 고정 너비

    private GameObject previewRoot; // 가상 오브젝트들을 담을 투명한 부모 폴더
    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>(); // 렉 방지용 프리팹 캐시

    private Dictionary<Vector3Int, GameObject> spawnedPreviews = new Dictionary<Vector3Int, GameObject>();

    // 프리팹이 위치한 폴더 경로 
    private readonly string TERRAIN_FOLDER = "Assets/Prefab/Map/Terrain";
    private readonly string OBJECT_FOLDER = "Assets/Prefab/Map/Object";

    [MenuItem("Tools/Map Painter")]
    public static void ShowWindow()
    {
        var window = GetWindow<MapPainterEditor>("Map Painter");
        window.minSize = new Vector2(700, 500);
    }


    // 에디터 창이 열릴 때 씬 뷰 이벤트 구독
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        LoadPalette(); // 창이 열릴 때 팔레트 데이터 로딩
        RefreshFull3DPreview();
    }
    // 에디터 창이 닫힐 때 구독 해제
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        if (previewRoot != null) DestroyImmediate(previewRoot);
        spawnedPreviews.Clear();
    }

    private GameObject GetPrefabForID(string id)
    {
        if (string.IsNullOrEmpty(id) || id == "Empty") return null;
        if (prefabCache.TryGetValue(id, out GameObject cached)) return cached;

        string[] guids = AssetDatabase.FindAssets(id + " t:Prefab");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            prefabCache[id] = prefab;
            return prefab;
        }
        return null;
    }

    //선택된 셀 하나만 프리뷰 갱신
    private void UpdateSingleCellPreview(CellData cell, EditLayer layer)
    {
        if (layer == EditLayer.Event) return; // 이벤트는 프리팹 생성을 안 함

        Vector3Int key = new Vector3Int(cell.position.x, cell.position.y, (int)layer);

        // 1. 기존에 생성된 오브젝트가 있다면 삭제
        if (spawnedPreviews.ContainsKey(key))
        {
            if (spawnedPreviews[key] != null) DestroyImmediate(spawnedPreviews[key]);
            spawnedPreviews.Remove(key);
        }

        string targetID = layer == EditLayer.Terrain ? cell.terrainID : cell.objectID;
        GameObject prefab = GetPrefabForID(targetID);

        // 2. 새 오브젝트 생성 및 추적 딕셔너리에 등록
        if (prefab != null)
        {
            Vector3 pos = new Vector3(
                cell.position.x * targetMapData.cellSize + targetMapData.gridOffset.x,
                targetMapData.gridOffset.y,
                cell.position.y * targetMapData.cellSize + targetMapData.gridOffset.z
            );

            GameObject spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            spawned.transform.position = pos;
            spawned.transform.SetParent(previewRoot.transform);

            spawnedPreviews[key] = spawned;
        }
    }

    //전체 프리뷰 갱신
    private void RefreshFull3DPreview()
    {
        if (targetMapData == null) return;

        if (previewRoot == null)
        {
            previewRoot = new GameObject("MapEditor_PreviewRoot");
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
        }

        while (previewRoot.transform.childCount > 0) DestroyImmediate(previewRoot.transform.GetChild(0).gameObject);
        spawnedPreviews.Clear();

        foreach (var cell in targetMapData.cells)
        {
            UpdateSingleCellPreview(cell, EditLayer.Terrain);
            UpdateSingleCellPreview(cell, EditLayer.Object);
        }
    }

    // 1. 팔레트 목록 로딩 (하드코딩 + 외부 파일)
    private void LoadPalette()
    {
        // A. 기본 하드코딩 데이터 (필수 요소들)
        currentTerrainPalette = new List<string> { "Empty" };
        currentObjectPalette = new List<string> { "Empty" };
        currentEventPalette = new List<string> { "Empty", "PlayerSpawn", "EnemySpawn", "BossSpawn", "NPCSpawn", "Block", "Trap" };

        // 지정된 폴더가 없으면 생성
        if (!AssetDatabase.IsValidFolder(TERRAIN_FOLDER)) Directory.CreateDirectory(TERRAIN_FOLDER);
        if (!AssetDatabase.IsValidFolder(OBJECT_FOLDER)) Directory.CreateDirectory(OBJECT_FOLDER);

        // 폴더 내부의 프리팹 자동 스캔
        ScanFolderForPrefabs(TERRAIN_FOLDER, currentTerrainPalette);
        ScanFolderForPrefabs(OBJECT_FOLDER, currentObjectPalette);

        //YAML 파일에서 팔레트 데이터 읽어오기
        string yamlPath = Application.dataPath + "/StreamingAssets/ModPaletteData.yaml";
        string schemaPath = Application.dataPath + "/StreamingAssets/ModPaletteDataSchema.yaml";

        string yamlText = File.ReadAllText(yamlPath);
        string schemaYamlText = File.ReadAllText(schemaPath);

        //YAML -> Json으로 변환
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithNodeTypeResolver(new NumericTypeResolver())
            .Build();
        var yamlObject = deserializer.Deserialize(new StringReader(yamlText));

        var jsonSerializer = new SerializerBuilder()
            .JsonCompatible()
            .Build();
        string jsonText = jsonSerializer.Serialize(yamlObject);

        //YAML Schema -> Json Schema로 변환
        var schemaYamlObject = deserializer.Deserialize(new StringReader(schemaYamlText));
        string schemaJsonText = jsonSerializer.Serialize(schemaYamlObject);

        JSchema schema = JSchema.Parse(schemaJsonText);

        JObject jsonObj = JObject.Parse(jsonText);
        if (!jsonObj.IsValid(schema, out IList<string> errorMessages))
        {
            Debug.LogError("❌ YAML validation failed:");
            foreach (var error in errorMessages)
                Debug.LogError(error);
            return;
        }

        Debug.Log("YAML validation Success:");

        //검증에 성공하면 ScriptableObject 생성
        var rawData = deserializer.Deserialize<RootPaletteData>(yamlText);

        foreach (var palette in rawData.paletteCollection)
        {
            // 중복을 방지하며 하드코딩 리스트에 외부 데이터 추가
            if (palette.terrainIDs != null)
                foreach (var id in palette.terrainIDs) if (!currentTerrainPalette.Contains(id)) currentTerrainPalette.Add(id);

            if (palette.objectIDs != null)
                foreach (var id in palette.objectIDs) if (!currentObjectPalette.Contains(id)) currentObjectPalette.Add(id);

            if (palette.eventIDs != null)
                foreach (var id in palette.eventIDs) if (!currentEventPalette.Contains(id)) currentEventPalette.Add(id);
        }


        Debug.Log("모드 팔레트 데이터를 성공적으로 불러왔습니다!");
    }

    private void ScanFolderForPrefabs(string folderPath, List<string> paletteList)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string prefabName = Path.GetFileNameWithoutExtension(path);
            if (!paletteList.Contains(prefabName))
            {
                paletteList.Add(prefabName);
            }
        }
    }

    private void OnGUI()
    {
        // 전체 창을 가로로 분할 (좌측: 설정 / 우측: 팔레트)
        GUILayout.BeginHorizontal();

        // ================= 좌측 패널 (고정 너비) =================
        GUILayout.BeginVertical(GUILayout.Width(LEFT_PANEL_WIDTH), GUILayout.ExpandHeight(true));
        DrawLeftPanel();
        GUILayout.EndVertical();

        // 패널 사이의 구분선
        GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

        // ================= 우측 패널 (가변 너비, 스크롤) =================
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawRightPanel();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        GUILayout.Label("맵 설정", EditorStyles.boldLabel);
        GUILayout.Label("1. 맵 데이터 연결", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        targetMapData = (MapDataSO)EditorGUILayout.ObjectField("Map Data SO", targetMapData, typeof(MapDataSO), false);

        // 에셋 슬롯에 새로운 파일이 들어오거나 변경되었다면
        if (EditorGUI.EndChangeCheck())
        {
            if (targetMapData != null)
            {
                // 씬 뷰를 즉시 다시 그리도록 명령
                SceneView.RepaintAll();
                RefreshFull3DPreview();
                Debug.Log($"{targetMapData.name} 데이터를 불러왔습니다. 셀 개수: {targetMapData.cells.Count}");
            }
        }

        if (targetMapData == null)
        {
            EditorGUILayout.HelpBox("프로젝트 창에서 MapDataSO를 생성한 뒤 여기에 끌어다 놓으세요.", MessageType.Warning);
            return;
        }

        if (GUILayout.Button("빈 그리드로 초기화"))
        {
            targetMapData.InitializeGrid();
            EditorUtility.SetDirty(targetMapData); // 변경사항 유니티에 알림
        }

        GUILayout.Space(15);
        GUILayout.Label("2. 그리드 설정", EditorStyles.boldLabel);
        targetMapData.cellSize = EditorGUILayout.FloatField("셀 크기(Size)", targetMapData.cellSize);
        targetMapData.gridOffset = EditorGUILayout.Vector3Field("그리드 오프셋(Offset)", targetMapData.gridOffset);
        showGridLines = EditorGUILayout.Toggle("그리드 선 표시(Grid)", showGridLines);

        GUILayout.Space(15);
        GUILayout.Label("3. 레이어 선택", EditorStyles.boldLabel);

        // 탭 전환 감지 (탭이 바뀌면 선택된 브러시 인덱스 초기화)
        EditorGUI.BeginChangeCheck();
        string[] layerNames = { "지형 (Terrain)", "오브젝트 (Object)", "이벤트 (Event)" };
        currentLayer = (EditLayer)GUILayout.Toolbar((int)currentLayer, layerNames, GUILayout.Height(30));
        if (EditorGUI.EndChangeCheck())
        {
            selectedPaletteIndex = 0;
            currentBrushID = "Empty";
        }

        GUILayout.Space(15);
        GUI.backgroundColor = isEditing ? Color.green : Color.white;
        if (GUILayout.Button(isEditing ? "페인팅 모드 ON (클릭하여 끄기)" : "페인팅 모드 OFF (클릭하여 켜기)", GUILayout.Height(40)))
        {
            isEditing = !isEditing;
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("팔레트 새로고침 (Yaml 파일 다시 읽기)"))
        {
            LoadPalette();
        }
    }

    private void DrawRightPanel()
    {
        GUILayout.Label("팔레트", EditorStyles.boldLabel);
        GUILayout.Label($"[{currentLayer}] 팔레트 (선택된 ID: {currentBrushID})", EditorStyles.boldLabel);

        List<string> activePalette = currentLayer switch
        {
            EditLayer.Terrain => currentTerrainPalette,
            EditLayer.Object => currentObjectPalette,
            EditLayer.Event => currentEventPalette,
            _ => currentTerrainPalette
        };

        // 스크롤 뷰 시작
        paletteScrollPos = GUILayout.BeginScrollView(paletteScrollPos);

        // 텍스트 대신 이미지(Texture2D)를 띄우기 위해 GUIContent 배열 사용
        GUIContent[] gridContents = new GUIContent[activePalette.Count];
        for (int i = 0; i < activePalette.Count; i++)
        {
            // 버튼 아이콘을 해당 프리팹의 미리보기 이미지를 가져오도록 설정
            Texture2D icon = null;
            if (activePalette[i] != "Empty")
            {
                GameObject prefab = GetPrefabForID(activePalette[i]);
                if (prefab != null) icon = AssetPreview.GetAssetPreview(prefab); // 유니티 내장 프리팹 썸네일
            }
            gridContents[i] = new GUIContent(activePalette[i], icon, activePalette[i]);
        }

        // 버튼 크기를 고정(예: 80x80)하여 썸네일 보기처럼 구성
        int columns = Mathf.Max(1, (int)(position.width - LEFT_PANEL_WIDTH - 30) / 100);

        // 아이콘이 잘 보이도록 GUIStyle 수정
        GUIStyle gridStyle = new GUIStyle(GUI.skin.button);
        gridStyle.imagePosition = ImagePosition.ImageAbove; // 텍스트 위에 이미지 배치

        selectedPaletteIndex = GUILayout.SelectionGrid(
            selectedPaletteIndex,
            gridContents,
            columns,
            gridStyle,
            GUILayout.Height(Mathf.CeilToInt((float)activePalette.Count / columns) * 85)
        );

        if (selectedPaletteIndex >= 0 && selectedPaletteIndex < activePalette.Count)
        {
            currentBrushID = activePalette[selectedPaletteIndex];
        }

        // 스크롤 뷰 종료
        GUILayout.EndScrollView();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isEditing || targetMapData == null) return;

        DrawGridLines();
        DrawEventLayerHandles();

        // 마우스 입력 처리 (레이캐스트)
        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, targetMapData.gridOffset);

        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);

            // 셀 크기와 오프셋을 반영하여 마우스 좌표를 인덱스로 변환
            int gridX = Mathf.FloorToInt((hitPoint.x - targetMapData.gridOffset.x) / targetMapData.cellSize + 0.5f);
            int gridY = Mathf.FloorToInt((hitPoint.z - targetMapData.gridOffset.z) / targetMapData.cellSize + 0.5f);

            // 하이라이트 박스 크기 cellSize에 맞게 조절
            Vector3 snappedPos = new Vector3(
                gridX * targetMapData.cellSize + targetMapData.gridOffset.x,
                targetMapData.gridOffset.y,
                gridY * targetMapData.cellSize + targetMapData.gridOffset.z
            );

            // 마우스 커서 위치에 하이라이트 박스 그리기
            Handles.color = new Color(1, 1, 0, 0.5f); // 반투명 노란색
            Handles.DrawWireCube(snappedPos + Vector3.up * 0.5f, new Vector3(targetMapData.cellSize, 1, targetMapData.cellSize));

            // 마우스 드래그나 클릭 시 색칠
            if ((e.type == EventType.MouseDrag || e.type == EventType.MouseDown) && e.button == 0)
            {
                PaintCell(gridX, gridY);
                e.Use(); // 유니티의 다른 클릭 이벤트(오브젝트 선택 등)를 무시하도록 처리
            }
        }

        // 에디터 화면 강제 갱신
        sceneView.Repaint();
    }

    private void PaintCell(int x, int y)
    {
        if (x < 0 || x >= targetMapData.width || y < 0 || y >= targetMapData.height) return;

        CellData cell = targetMapData.cells.Find(c => c.position.x == x && c.position.y == y);

        if (cell != null)
        {
            bool isChanged = false;

            // 현재 선택된 탭(레이어)에 맞춰서 데이터를 덮어씌움
            switch (currentLayer)
            {
                case EditLayer.Terrain:
                    if (cell.terrainID != currentBrushID) { cell.terrainID = currentBrushID; isChanged = true; }
                    break;
                case EditLayer.Object:
                    if (cell.objectID != currentBrushID) { cell.objectID = currentBrushID; isChanged = true; }
                    break;
                case EditLayer.Event:
                    if (cell.eventID != currentBrushID) { cell.eventID = currentBrushID; isChanged = true; }
                    break;
            }

            if (isChanged)
            {
                Undo.RecordObject(targetMapData, "Paint Map Cell");
                EditorUtility.SetDirty(targetMapData);

                UpdateSingleCellPreview(cell, currentLayer);
            }
        }
    }

    private void DrawEventLayerHandles()
    {
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontStyle = FontStyle.Bold;

        foreach (var cell in targetMapData.cells)
        {
            if (cell.eventID != "Empty")
            {
                Vector3 pos = new Vector3(
                    cell.position.x * targetMapData.cellSize + targetMapData.gridOffset.x,
                    targetMapData.gridOffset.y,
                    cell.position.y * targetMapData.cellSize + targetMapData.gridOffset.z
                );

                // 프리팹 위에 둥둥 떠 있도록 높이(Y)를 1.5만큼 올려줍니다.
                Vector3 floatingPos = pos + Vector3.up * (targetMapData.cellSize * 1.5f);

                Handles.color = new Color(0.8f, 0.2f, 0.8f, 0.6f); // 반투명 보라색
                Handles.CubeHandleCap(0, floatingPos, Quaternion.identity, targetMapData.cellSize * 0.8f, EventType.Repaint);

                Handles.Label(floatingPos + Vector3.up * 0.5f, cell.eventID, labelStyle);
            }
        }
    }

    /// <summary>
    /// 편집 모드 시 전체 맵의 너비와 높이, 셀 크기 및 오프셋을 반영한 격자선(그리드)을 렌더링합니다.
    /// </summary>
    private void DrawGridLines()
    {
        if (!showGridLines || targetMapData == null) return;
        if (targetMapData.width <= 0 || targetMapData.height <= 0 || targetMapData.cellSize <= 0) return;

        float cellSize = targetMapData.cellSize;
        Vector3 offset = targetMapData.gridOffset;
        float y = offset.y + 0.01f; // 바닥 메쉬와 겹쳐 깜빡이는(Z-fighting) 현상 방지

        float minX = -0.5f * cellSize + offset.x;
        float maxX = (targetMapData.width - 0.5f) * cellSize + offset.x;
        float minZ = -0.5f * cellSize + offset.z;
        float maxZ = (targetMapData.height - 0.5f) * cellSize + offset.z;

        // 1. 내부 격자선 렌더링 (반투명 밝은 회색)
        Handles.color = new Color(0.9f, 0.9f, 0.9f, 0.35f);

        // 가로선 (Z축 기준, X축으로 뻗는 선들)
        for (int yIdx = 1; yIdx < targetMapData.height; yIdx++)
        {
            float z = (yIdx - 0.5f) * cellSize + offset.z;
            Handles.DrawLine(new Vector3(minX, y, z), new Vector3(maxX, y, z));
        }

        // 세로선 (X축 기준, Z축으로 뻗는 선들)
        for (int xIdx = 1; xIdx < targetMapData.width; xIdx++)
        {
            float x = (xIdx - 0.5f) * cellSize + offset.x;
            Handles.DrawLine(new Vector3(x, y, minZ), new Vector3(x, y, maxZ));
        }

        // 2. 외곽 경계선 렌더링 (시인성 높은 밝은 청록색)
        Handles.color = new Color(0.2f, 0.9f, 1.0f, 0.85f);
        Handles.DrawLine(new Vector3(minX, y, minZ), new Vector3(maxX, y, minZ));
        Handles.DrawLine(new Vector3(minX, y, maxZ), new Vector3(maxX, y, maxZ));
        Handles.DrawLine(new Vector3(minX, y, minZ), new Vector3(minX, y, maxZ));
        Handles.DrawLine(new Vector3(maxX, y, minZ), new Vector3(maxX, y, maxZ));
    }

    public class RootPaletteData
    {
        public List<PaletteEntry> paletteCollection { get; set; }
    }

    public class PaletteEntry
    {
        public List<string> terrainIDs { get; set; }
        public List<string> objectIDs { get; set; }
        public List<string> eventIDs { get; set; }
    }
}