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
    public Vector2Int startSafeCoord = Vector2Int.zero;
    public Vector2Int endSafeCoord = Vector2Int.zero;
    [Min(2)] public int minimumSafePathCount = 2;
    [Min(0)] public int safePathCountVariance = 3;
    [Min(1f)] public float maxStepDistanceInTiles = 2.828427f;
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
        if (tileSize <= 0f)
        {
            tileSize = 0.1f;
        }

        if (!Application.isPlaying)
        {
            // 参数变化后清空旧索引，Scene Gizmo 下次绘制时会按新 Origin 重建。
            cells.Clear();
#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGridInScene)
        {
            return;
        }

        if (cells == null || cells.Count == 0)
        {
            BuildGrid(false);
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
        BuildGrid(true);
    }

    [ContextMenu("Rebuild Grid Preview")]
    private void RebuildGridPreview()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("请在 Play Mode 外刷新 Grid 预览。", this);
            return;
        }

        cells.Clear();
        BuildGrid(false);
#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void BuildGrid(bool createObjects)
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

            AddMaskArea(mask, createObjects);
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

    public bool GenerateRandomSafePath()
    {
        if (!cells.ContainsKey(startSafeCoord) || !cells.ContainsKey(endSafeCoord))
        {
            Debug.LogError("安全路线起点或终点不在可行走网格中，请检查 startSafeCoord 和 endSafeCoord。", this);
            safePathCoords.Clear();
            return false;
        }

        if (startSafeCoord == endSafeCoord)
        {
            Debug.LogError("安全路线起点和终点不能是同一个格子。", this);
            safePathCoords.Clear();
            return false;
        }

        int minCount = Mathf.Max(2, minimumSafePathCount);
        int maxCount = Mathf.Min(cells.Count, minCount + Mathf.Max(0, safePathCountVariance));
        float maxDistance = maxStepDistanceInTiles * tileSize;
        List<int> targetCounts = new List<int>();
        for (int count = minCount; count <= maxCount; count++)
        {
            targetCounts.Add(count);
        }

        // 随机选择优先尝试的路线格数，若不可达再尝试区间内其他长度。
        for (int i = targetCounts.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            int temp = targetCounts[i];
            targetCounts[i] = targetCounts[swapIndex];
            targetCounts[swapIndex] = temp;
        }

        List<Vector2Int> generatedPath = null;
        foreach (int targetCount in targetCounts)
        {
            List<Vector2Int> candidatePath = new List<Vector2Int> { startSafeCoord };
            HashSet<Vector2Int> visited = new HashSet<Vector2Int> { startSafeCoord };

            if (FindRandomizedPath(startSafeCoord, endSafeCoord, targetCount, maxDistance, visited, candidatePath))
            {
                generatedPath = candidatePath;
                break;
            }
        }

        if (generatedPath == null)
        {
            Debug.LogError("无法在设定格数范围和相邻距离限制下生成安全路线，请降低最少格数/浮动范围或放宽距离限制。", this);
            safePathCoords.Clear();
            return false;
        }

        safePathCoords.Clear();
        safePathCoords.AddRange(generatedPath);
        return true;
    }

    private bool FindRandomizedPath(
        Vector2Int current,
        Vector2Int destination,
        int targetCount,
        float maxDistance,
        HashSet<Vector2Int> visited,
        List<Vector2Int> path)
    {
        if (current == destination)
        {
            return path.Count == targetCount;
        }

        if (path.Count >= targetCount)
        {
            return false;
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        foreach (Vector2Int coord in cells.Keys)
        {
            if (visited.Contains(coord))
            {
                continue;
            }

            Vector3 currentPosition = cells[current].worldCenter;
            Vector3 candidatePosition = cells[coord].worldCenter;
            float distance = Vector2.Distance(
                new Vector2(currentPosition.x, currentPosition.z),
                new Vector2(candidatePosition.x, candidatePosition.z));

            if (distance <= maxDistance)
            {
                candidates.Add(coord);
            }
        }

        // 随机打乱候选邻格的尝试顺序，从而产生不同的有效路线。
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            Vector2Int temp = candidates[i];
            candidates[i] = candidates[swapIndex];
            candidates[swapIndex] = temp;
        }

        foreach (Vector2Int next in candidates)
        {
            if (next == destination && path.Count + 1 != targetCount)
            {
                continue;
            }

            visited.Add(next);
            path.Add(next);

            if (FindRandomizedPath(next, destination, targetCount, maxDistance, visited, path))
            {
                return true;
            }

            visited.Remove(next);
            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    private void AddMaskArea(BoxCollider mask, bool createObjects)
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

                // 只查询 NavMesh，不用物理射线，因此玩家、幽灵、石像鬼等 Collider 不会遮挡格子采样。
                NavMeshHit navHit;
                if (!NavMesh.SamplePosition(cellCenter, out navHit, 20f, NavMesh.AllAreas))
                {
                    continue;
                }

                Vector2 centerOffset = new Vector2(navHit.position.x - cellCenter.x, navHit.position.z - cellCenter.z);
                if (centerOffset.sqrMagnitude > sampleMaxDistance * sampleMaxDistance)
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

                if (createObjects && safeTileVisualPrefab != null)
                {
                    GameObject visual = Instantiate(safeTileVisualPrefab, cell.worldCenter + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f), transform);
                    visual.transform.localScale = Vector3.one * tileSize;
                    visual.SetActive(false);
                    cell.visualObject = visual;
                }

                if (createObjects && trapPrefab != null)
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
