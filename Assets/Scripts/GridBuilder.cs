using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GridBuilder : MonoBehaviour
{
    [Header("Grid mask")]
    public List<BoxCollider> maskBoxes = new List<BoxCollider>();
    public Vector3 origin = Vector3.zero;
    public float tileSize = 1.5f;
    public float sampleMaxDistance = 0.75f;

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
        Bounds bounds = mask.bounds;

        for (float x = bounds.min.x; x < bounds.max.x; x += tileSize)
        {
            for (float z = bounds.min.z; z < bounds.max.z; z += tileSize)
            {
                Vector3 cellCenter = new Vector3(x + tileSize * 0.5f, 0f, z + tileSize * 0.5f);
                if (!mask.bounds.Contains(cellCenter))
                {
                    continue;
                }

                Ray ray = new Ray(cellCenter + Vector3.up * 10f, Vector3.down);
                if (!Physics.Raycast(ray, out RaycastHit hit, 20f))
                {
                    continue;
                }

                NavMeshHit navHit;
                if (!NavMesh.SamplePosition(hit.point, out navHit, sampleMaxDistance, NavMesh.AllAreas))
                {
                    continue;
                }

                Vector2Int coord = new Vector2Int(
                    Mathf.FloorToInt((cellCenter.x - origin.x) / tileSize),
                    Mathf.FloorToInt((cellCenter.z - origin.z) / tileSize));

                if (cells.ContainsKey(coord))
                {
                    continue;
                }

                GridCell cell = new GridCell
                {
                    coord = coord,
                    worldCenter = new Vector3(
                        origin.x + coord.x * tileSize + tileSize * 0.5f,
                        hit.point.y,
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
        }

        cells.Clear();
    }
}
