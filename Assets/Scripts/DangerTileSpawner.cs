using System.Collections.Generic;
using UnityEngine;

public class DangerTileSpawner : MonoBehaviour
{
    [Header("References")]
    public GridBuilder gridBuilder;
    public GameObject dangerTilePrefab;

    [Header("Settings")]
    public float tileHeight = 0.08f;
    public float tileScaleMultiplier = 0.92f;
    public bool spawnOnStart = true;

    private readonly List<GameObject> spawnedDangerTiles = new List<GameObject>();

    private void Start()
    {
    }

    public void SpawnDangerTiles()
    {
        ClearDangerTiles();

        if (gridBuilder == null)
        {
            gridBuilder = GetComponent<GridBuilder>();
        }

        if (gridBuilder == null || dangerTilePrefab == null)
        {
            return;
        }

        foreach (var cell in gridBuilder.WalkableCells)
        {
            if (cell == null)
            {
                continue;
            }

            GameObject tile = Instantiate(dangerTilePrefab, cell.worldCenter + Vector3.up * tileHeight, Quaternion.identity, transform);
            tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            tile.transform.localScale = new Vector3(gridBuilder.tileSize * tileScaleMultiplier, gridBuilder.tileSize * tileScaleMultiplier, 1f);
            tile.SetActive(false);
            cell.trapObject = tile;
            spawnedDangerTiles.Add(tile);
        }
    }

    public void ShowDangerTileForCell(GridCell cell)
    {
        if (cell == null)
        {
            return;
        }

        if (cell.trapObject != null)
        {
            cell.trapObject.SetActive(true);
        }
    }

    public void HideDangerTileForCell(GridCell cell)
    {
        if (cell == null)
        {
            return;
        }

        if (cell.trapObject != null)
        {
            cell.trapObject.SetActive(false);
        }
    }

    public void ClearDangerTiles()
    {
        foreach (var tile in spawnedDangerTiles)
        {
            if (tile != null)
            {
                Destroy(tile);
            }
        }

        spawnedDangerTiles.Clear();
    }
}
