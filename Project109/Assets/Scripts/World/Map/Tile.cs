using UnityEngine;

public enum TileState
{
    Empty,
    Full,        // 통행 불가능한 벽 (Wall)
    Trap,        // 함정
    Obstacle,    // 장애물 (파괴 가능한 상자 등)

    // 가독성 및 의도 명확화를 위한 별칭
    Wall = Full
}

public class Tile : MonoBehaviour
{
    [SerializeField]
    private Vector2Int coord;

    private GameMap _ownerMap;
    public GameMap ownerMap
    {
        get
        {
            if (_ownerMap == null)
            {
                var parentMap = GetComponentInParent<GameMap>();
                _ownerMap = parentMap != null ? parentMap : GameMap.current;
            }
            return _ownerMap;
        }
        set => _ownerMap = value;
    }

    [SerializeField]
    private TileState _tileState;
    public TileState tileState
    {
        get => _tileState;
        set
        {
            _tileState = value;
            UpdateLayerByState();
        }
    }

    /// <summary>벽(Full) 또는 장애물(Obstacle)로 인해 통행이 차단된 타일인지 여부</summary>
    public bool isBlocked => _tileState == TileState.Full || _tileState == TileState.Obstacle;

    /// <summary>기본적으로 보행 가능한 타일인지 여부 (Empty 또는 Trap)</summary>
    public bool isWalkable => !isBlocked;

    private Character _occupyingCharacter;
    public Character occupyingCharacter
    {
        get => _occupyingCharacter;
        set => _occupyingCharacter = value;
    }

    private DestructibleObject _occupyingDestructible;
    public DestructibleObject occupyingDestructible
    {
        get => _occupyingDestructible;
        set => _occupyingDestructible = value;
    }

    private ITileHazard _occupyingHazard;
    public ITileHazard occupyingHazard
    {
        get
        {
            if (!IsHazardAlive(_occupyingHazard))
            {
                _occupyingHazard = null;
            }
            return _occupyingHazard;
        }
        set => _occupyingHazard = value;
    }

    public GameObject canMoveAreaTextureObject;

    private void Awake()
    {
        UpdateLayerByState();
    }

    private void OnDestroy()
    {
        ClearTile();
    }

    private void Start()
    {
        if (transform.childCount > 0)
        {
            canMoveAreaTextureObject = transform.GetChild(0).gameObject;
            // 구형 10x10 CanMoveArea Plane이 이동 인디케이터와 중복 표시되지 않도록 비활성화 보장
            if (canMoveAreaTextureObject != null)
            {
                canMoveAreaTextureObject.SetActive(false);
            }
        }
    }

    public void SetCoord(int column, int row)
    {
        coord = new Vector2Int(column, row);
    }

    public Vector2Int GetCoord()
    {
        return coord;
    }

    public string GetCoordToString()
    {
        return coord.x + ", " + coord.y;
    }

    /// <summary>
    /// 타일 이동 범위 표시(비주얼) 켜고 끄기
    /// </summary>
    public void SetMoveIndicator(bool isActive)
    {
        if (canMoveAreaTextureObject != null)
        {
            canMoveAreaTextureObject.SetActive(isActive);
        }
    }

    /// <summary>
    /// 타일 자체의 레이어는 언제나 클릭 감지를 위해 Tile로 고정하고,
    /// 하위 비주얼 요소들의 레이어만 상태에 맞춰 관리합니다.
    /// </summary>
    public void UpdateLayerByState()
    {
        // 타일 자신은 항상 Tile 레이어로 고정하여 마우스 클릭 레이캐스트가 언제든 닿을 수 있게 보장합니다.
        gameObject.layer = LayerMask.NameToLayer("Tile");
        
        int targetChildLayer = (_tileState == TileState.Full || _tileState == TileState.Obstacle) 
            ? LayerMask.NameToLayer("Map") 
            : LayerMask.NameToLayer("Tile");

        // 하위 자식(장애물 모델 등)들의 레이어만 변경하여 물리 가림 및 투과 클릭 필터링이 먹히게 처리합니다.
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != this.transform)
            {
                child.gameObject.layer = targetChildLayer;
            }
        }
    }


    /// <summary>
    /// 캐릭터 이동 기믹을 고려하여 이 타일에 해당 캐릭터가 멈춰 설 수 있는지 판단합니다.
    /// </summary>
    public bool CanEnter(CharacterMove mover)
    {
        if (mover == null) return false;

        // 벽(Full) 타일 내부 멈춤은 벽 통과(PassWalls) 비트가 있더라도 항상 차단합니다.
        if (tileState == TileState.Full)
            return false;

        // 장애물(Obstacle) 타일 위에 올라서려면 PassObstacles 비트가 켜져 있어야 합니다.
        if (tileState == TileState.Obstacle)
        {
            return (mover.capabilities & MoverCapability.PassObstacles) != 0;
        }

        // 다른 생존 유닛이 이미 점유 중인 타일에는 올라설 수 없음 (자기 자신 제외)
        if (occupyingCharacter != null && !occupyingCharacter.isDead)
        {
            if (mover.character != null && occupyingCharacter != mover.character)
            {
                return false;
            }
        }

        // 그 외 일반 타일(Empty, Trap)은 누구든 진입 및 멈춤 가능
        return true;
    }

    /// <summary>
    /// 두 타일 간의 육각 격자 거리(Cube 거리)를 계산합니다.
    /// </summary>
    public static int GetDistance(Tile t1, Tile t2)
    {
        if (t1 == null || t2 == null) return int.MaxValue;
        return HexMath.Distance(t1.coord, t2.coord);
    }

    /// <summary>
    /// 다른 타일까지의 육각 격자 거리를 계산합니다.
    /// </summary>
    public int GetDistanceTo(Tile other)
    {
        return GetDistance(this, other);
    }

    /// <summary>
    /// 유닛이 이 타일에 진입했을 때 점유 중인 위험 요소(함정 등)에 알립니다.
    /// </summary>
    public void NotifyUnitEntered(Character unit)
    {
        if (unit == null) return;
        ITileHazard hazard = occupyingHazard;
        if (hazard != null && hazard.IsActive)
        {
            hazard.OnUnitEnter(unit);
        }
    }

    /// <summary>
    /// 유닛이 이 타일에서 이탈했을 때 점유 중인 위험 요소(함정 등)에 알립니다.
    /// </summary>
    public void NotifyUnitExited(Character unit)
    {
        if (unit == null) return;
        ITileHazard hazard = occupyingHazard;
        if (hazard != null)
        {
            hazard.OnUnitExit(unit);
        }
    }

    /// <summary>
    /// 타일이 리셋/정리될 때 모든 점유 참조를 해제합니다.
    /// </summary>
    public void ClearTile()
    {
        _occupyingCharacter = null;
        _occupyingDestructible = null;
        _occupyingHazard = null;
    }

    private static bool IsHazardAlive(ITileHazard hazard)
    {
        if (hazard == null) return false;
        if (hazard is UnityEngine.Object unityObj)
        {
            return unityObj != null;
        }
        return true;
    }
}