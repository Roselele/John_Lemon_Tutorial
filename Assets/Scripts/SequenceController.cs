using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SequenceController : MonoBehaviour
{
    [Header("References")]
    public GridBuilder gridBuilder;
    public DangerTileSpawner dangerTileSpawner;

    [Header("Timing")]
    public float stepDuration = 3f;
    public float handStartHeight = 3f;
    public float handDropDuration = 0.5f;

    [Header("Hand trap")]
    public GameObject handPrefab;
    public Transform player;
    public GameEnding gameEnding;
    public GameObject wall;
    public float wallLiftHeight = 3f;

    private List<GridCell> safePathCells = new List<GridCell>();
    private bool wallHasBeenLifted;

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
        // 安全提示和 Hand 波次完成后重新开始，形成循环玩法。
        while (true)
        {
            // 第一阶段：每轮只保留一个安全格，其余格子显示危险图案。
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
                    if (cell != safePathCells[i] && dangerTileSpawner != null)
                    {
                        dangerTileSpawner.ShowDangerTileForCell(cell);
                    }
                }

                yield return new WaitForSeconds(stepDuration);
            }

            // 第二阶段开始前，清除所有安全格和危险图案。
            foreach (var cell in gridBuilder.WalkableCells)
            {
                cell.SetVisual(false);
                if (dangerTileSpawner != null)
                {
                    dangerTileSpawner.HideDangerTileForCell(cell);
                }
            }

            // 第三阶段：依次用当前安全格作为唯一避开的格子，生成 Hand 波次。
            for (int i = 0; i < safePathCells.Count; i++)
            {
                yield return StartCoroutine(DropHandWave(safePathCells[i]));
                yield return new WaitForSeconds(stepDuration);
            }
        }
    }

    private IEnumerator DropHandWave(GridCell safeCell)
    {
        if (handPrefab == null)
        {
            yield break;
        }

        // Wall 只在整个游戏流程第一次进入 Hand 阶段时移动。
        if (!wallHasBeenLifted)
        {
            LiftWall();
        }

        // 当前安全格不生成 Hand，其余所有可行走格子同时生成 Hand。
        foreach (GridCell cell in gridBuilder.WalkableCells)
        {
            if (cell == null || cell == safeCell)
            {
                continue;
            }

            Vector3 startPosition = cell.worldCenter + Vector3.up * handStartHeight;
            GameObject hand = Instantiate(handPrefab, startPosition, handPrefab.transform.rotation, transform);
            cell.handObject = hand;

            TrapTrigger trigger = hand.GetComponentInChildren<TrapTrigger>();
            if (trigger != null)
            {
                trigger.playerObject = player != null ? player.gameObject : null;
                trigger.gameEnding = gameEnding;
            }
        }

        // 所有 Hand 同步从上方下降到各自格子的地面位置。
        float elapsed = 0f;
        while (elapsed < handDropDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / handDropDuration);
            foreach (GridCell cell in gridBuilder.WalkableCells)
            {
                if (cell == null || cell == safeCell || cell.handObject == null)
                {
                    continue;
                }

                Vector3 startPosition = cell.worldCenter + Vector3.up * handStartHeight;
                Vector3 groundPosition = cell.worldCenter + Vector3.up * 0.05f;
                cell.handObject.transform.position = Vector3.Lerp(startPosition, groundPosition, progress);
            }

            yield return null;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, stepDuration - handDropDuration));

        // 本轮没有触发玩家的 Hand 在下一轮前全部销毁。
        foreach (GridCell cell in gridBuilder.WalkableCells)
        {
            if (cell == null || cell == safeCell)
            {
                continue;
            }

            if (cell.handObject != null)
            {
                Destroy(cell.handObject);
                cell.handObject = null;
            }
        }
    }

    private void LiftWall()
    {
        if (wall == null)
        {
            return;
        }

        wall.transform.position += Vector3.up * wallLiftHeight;
        wallHasBeenLifted = true;
    }
}
