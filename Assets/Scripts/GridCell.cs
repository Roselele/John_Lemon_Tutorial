using UnityEngine;

[System.Serializable]
public class GridCell
{
    public Vector2Int coord;
    public Vector3 worldCenter;
    public bool isWalkable;

    public GameObject visualObject;
    public GameObject trapObject;

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
}
