using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GridBuilder : MonoBehaviour
{
    // 多个 BoxCollider 的并集定义网格生成区域。
    [Header("Grid mask")]
    public List<BoxCollider> maskBoxes = new List<BoxCollider>();
    public Vector3 origin = Vector3.zero;
    public float tileSize = 1.5f;
    public float sampleMaxDistance = 0.75f;
    public LayerMask floorLayers = ~0;

    public bool showGridInScene = true;
    public bool showCellIndex = true;

    [Header("Visual / trap prefabs")]
    public GameObject safeTileVisualPrefab;
    public GameObject trapPrefab;

    [Header("Debug colors")]
    public Color walkableColor = new Color(0.2f, 1f, 0.4f, 0.45f);
    public Color blockedColor = new Color(1f, 0.2f, 0.2f, 0.25f);

    [Header("Safe route")]
    public List<Vector2Int> safePathCoords = new List<Vector2Int>();

    private readonly Dictionary<Vector2Int, GridCell> cells = new Dictionary<Vector2Int, GridCell>();
    public IReadOnlyDictionary<Vector2Int, GridCell> Cells => cells;
    public List<GridCell> WalkableCells => new List<GridCell>(cells.Values);

    private void Reset()
    {
        BuildGrid();
    }

    private void OnValidate()
    {
        if (maskBoxes == null || maskBoxes.Count == 0)
        {
            return;
        }

        BuildGrid();
    }

    private void OnDrawGizmos()
    {
        if (!showGridInScene)
        {
            return;
        }

        if (cells == null || cells.Count == 0)
        {
            BuildGrid();
        }

        if (cells == null || cells.Count == 0)
        {
            return;
        }

        // 只在 Scene 视图绘制调试网格，不额外生成可见的网格物体。
        foreach (var kvp in cells)
        {
            GridCell cell = kvp.Value;
            Vector3 pos = cell.worldCenter;

            Gizmos.color = cell.isWalkable ? walkableColor : blockedColor;
            Gizmos.DrawWireCube(pos, new Vector3(tileSize, 0.05f, tileSize));

            if (showCellIndex)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.Label(pos + Vector3.up * 0.15f, kvp.Key.ToString());
#endif
            }
        }
    }

    private void Awake()
    {
        BuildGrid();
    }

    private void Start()
    {
    }

    public void BuildGrid()
    {
        // 重建前清理旧的格子对象，避免修改参数后重复生成。
        ClearGrid();

        List<BoxCollider> activeMasks = new List<BoxCollider>();
        if (maskBoxes != null && maskBoxes.Count > 0)
        {
            activeMasks.AddRange(maskBoxes);
        }

        if (activeMasks.Count == 0)
        {
            BoxCollider selfBox = GetComponent<BoxCollider>();
            if (selfBox != null)
            {
                activeMasks.Add(selfBox);
            }
            else
            {
                var colliders = GetComponentsInChildren<BoxCollider>();
                if (colliders != null && colliders.Length > 0)
                {
                    activeMasks.AddRange(colliders);
                }
            }
        }

        foreach (var mask in activeMasks)
        {
            if (mask == null)
            {
                continue;
            }

            AddMaskArea(mask);
        }
    }

    public List<GridCell> GetCellsForCoords(List<Vector2Int> coords)
    {
        List<GridCell> result = new List<GridCell>();
        foreach (var coord in coords)
        {
            if (cells.TryGetValue(coord, out GridCell cell))
            {
                result.Add(cell);
            }
        }

        return result;
    }

    public GridCell GetCellByCoord(Vector2Int coord)
    {
        if (cells.TryGetValue(coord, out GridCell cell))
        {
            return cell;
        }

        return null;
    }

    private void AddMaskArea(BoxCollider mask)
    {
        // 使用 origin 和 tileSize 计算全局坐标，确保多个 Box 的格线无缝对齐。
        if (tileSize <= 0f)
        {
            Debug.LogError("GridBuilder 的 tileSize 必须大于 0。", this);
            return;
        }

        Bounds bounds = mask.bounds;

        int minX = Mathf.FloorToInt((bounds.min.x - origin.x) / tileSize);
        int maxX = Mathf.CeilToInt((bounds.max.x - origin.x) / tileSize) - 1;
        int minZ = Mathf.FloorToInt((bounds.min.z - origin.z) / tileSize);
        int maxZ = Mathf.CeilToInt((bounds.max.z - origin.z) / tileSize) - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector3 cellCenter = new Vector3(
                    origin.x + (x + 0.5f) * tileSize,
                    bounds.center.y,
                    origin.z + (z + 0.5f) * tileSize);

                // 只有格子中心位于 Mask 的 XZ 范围内时才生成该格。
                if (!ContainsXZ(bounds, cellCenter))
                {
                    continue;
                }

                if (!TryGetFloorPoint(cellCenter, out Vector3 floorPoint))
                {
                    // 边缘 Raycast 失败时，使用 NavMesh 作为高度采样的兜底。
                    NavMeshHit fallbackHit;
                    if (!NavMesh.SamplePosition(cellCenter + Vector3.up * 10f, out fallbackHit, 20f, NavMesh.AllAreas))
                    {
                        continue;
                    }

                    floorPoint = fallbackHit.position;
                }

                NavMeshHit navHit;
                if (!NavMesh.SamplePosition(floorPoint, out navHit, sampleMaxDistance, NavMesh.AllAreas))
                {
                    continue;
                }

                Vector2Int coord = new Vector2Int(x, z);

                if (cells.ContainsKey(coord))
                {
                    continue;
                }

                GridCell cell = new GridCell
                {
                    coord = coord,
                    worldCenter = new Vector3(
                        origin.x + coord.x * tileSize + tileSize * 0.5f,
                        navHit.position.y,
                        origin.z + coord.y * tileSize + tileSize * 0.5f),
                    isWalkable = true
                };

                if (safeTileVisualPrefab != null)
                {
                    GameObject visual = Instantiate(safeTileVisualPrefab, cell.worldCenter + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f), transform);
                    visual.transform.localScale = Vector3.one * tileSize;
                    visual.SetActive(false);
                    cell.visualObject = visual;
                }

                if (trapPrefab != null)
                {
                    GameObject trap = Instantiate(trapPrefab, cell.worldCenter + Vector3.up * 0.25f, Quaternion.identity, transform);
                    trap.SetActive(false);
                    cell.trapObject = trap;
                }

                cells.Add(coord, cell);
            }
        }
    }

    private bool ContainsXZ(Bounds bounds, Vector3 position)
    {
        return position.x >= bounds.min.x && position.x <= bounds.max.x
            && position.z >= bounds.min.z && position.z <= bounds.max.z;
    }

    private bool TryGetFloorPoint(Vector3 cellCenter, out Vector3 floorPoint)
    {
        // 忽略 mask 自身的 Collider，避免把 Box 当成地面。
        Ray ray = new Ray(cellCenter + Vector3.up * 10f, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 20f, floorLayers, QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        foreach (RaycastHit hit in hits)
        {
            // 玩家站在格子上时会先被射线命中，不能把玩家表面当成地面高度。
            if (hit.collider.GetComponentInParent<PlayerMovement>() != null || hit.collider.CompareTag("Player"))
            {
                continue;
            }

            bool isMaskCollider = false;
            foreach (BoxCollider mask in maskBoxes)
            {
                if (mask != null && hit.collider == mask)
                {
                    isMaskCollider = true;
                    break;
                }
            }

            if (!isMaskCollider)
            {
                floorPoint = hit.point;
                return true;
            }
        }

        floorPoint = default;
        return false;
    }

    public bool IsInsideAnyMask(Vector3 worldPosition)
    {
        if (maskBoxes == null || maskBoxes.Count == 0)
        {
            return true;
        }

        foreach (var box in maskBoxes)
        {
            if (box == null)
            {
                continue;
            }

            if (box.bounds.Contains(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearGrid()
    {
        foreach (var cell in cells.Values)
        {
            if (cell.visualObject != null)
            {
                Destroy(cell.visualObject);
            }

            if (cell.trapObject != null)
            {
                Destroy(cell.trapObject);
            }

            if (cell.handObject != null)
            {
                Destroy(cell.handObject);
            }
        }

        cells.Clear();
    }
}
