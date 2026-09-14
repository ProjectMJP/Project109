using UnityEngine;

public enum TileState
{
    Empty,
    Full,
    Trap,
    Obstacle
}

public class Tile : MonoBehaviour
{
    [SerializeField]
    private Vector2Int coord;

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

    public GameObject canMoveAreaTextureObject;

    private void Awake()
    {
        UpdateLayerByState();
    }

    private void Start()
    {
        if (transform.childCount > 0)
        {
            canMoveAreaTextureObject = transform.GetChild(0).gameObject;
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
    /// 이전 호환용 더미 메서드 (추후 정리 예정)
    /// </summary>
    public void ChangeEffect()
    {
        // 비주얼 표시는 SetMoveIndicator를 사용하도록 권장합니다.
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
            return mover.capabilities.HasFlag(MoverCapability.PassObstacles);
        }

        // 그 외 일반 타일(Empty, Trap)은 누구든 진입 및 멈춤 가능
        return true;
    }
}