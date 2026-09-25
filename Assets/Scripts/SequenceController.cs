using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SequenceController : MonoBehaviour
{
    [Header("References")]
    public GridBuilder gridBuilder;
    public DangerTileSpawner dangerTileSpawner;

    [Header("Timing")]
    public float stepDuration = 3f;

    private List<GridCell> safePathCells = new List<GridCell>();

    private void Start()
    {
        if (gridBuilder == null)
        {
            gridBuilder = GetComponent<GridBuilder>();
        }

        if (dangerTileSpawner == null)
        {
            dangerTileSpawner = GetComponent<DangerTileSpawner>();
        }

        if (gridBuilder == null)
        {
            Debug.LogWarning("SequenceController needs a GridBuilder reference.");
            return;
        }

        gridBuilder.BuildGrid();
        safePathCells = gridBuilder.GetCellsForCoords(gridBuilder.safePathCoords);

        if (dangerTileSpawner != null)
        {
            dangerTileSpawner.SpawnDangerTiles();
        }

        if (safePathCells.Count > 0)
        {
            StartCoroutine(PlayPathSequence());
        }
    }

    private IEnumerator PlayPathSequence()
    {
        while (true)
        {
            for (int i = 0; i < safePathCells.Count; i++)
            {
                foreach (var cell in gridBuilder.WalkableCells)
                {
                    cell.SetVisual(false);
                    if (dangerTileSpawner != null)
                    {
                        dangerTileSpawner.HideDangerTileForCell(cell);
                    }
                }

                safePathCells[i].SetVisual(true);

                foreach (var cell in gridBuilder.WalkableCells)
                {
                    if (cell != safePathCells[i])
                    {
                        if (dangerTileSpawner != null)
                        {
                            dangerTileSpawner.ShowDangerTileForCell(cell);
                        }
                    }
                }

                yield return new WaitForSeconds(stepDuration);
            }
        }
    }
}
