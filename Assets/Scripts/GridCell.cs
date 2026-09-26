using UnityEngine;

[System.Serializable]
public class GridCell
{
    // 格子的全局坐标、世界位置和可行走状态。
    public Vector2Int coord;
    public Vector3 worldCenter;
    public bool isWalkable;

    // 该格子对应的安全提示、危险图案和下降机关。
    public GameObject visualObject;
    public GameObject trapObject;
    public GameObject handObject;

    // 控制安全格提示的显示状态。
    public void SetVisual(bool active)
    {
        if (visualObject != null)
        {
            visualObject.SetActive(active);
        }
    }

    public void SetTrap(bool active)
    {
        if (trapObject != null)
        {
            trapObject.SetActive(active);
        }
    }

    public void SetHand(bool active)
    {
        if (handObject != null)
        {
            handObject.SetActive(active);
        }
    }
}
