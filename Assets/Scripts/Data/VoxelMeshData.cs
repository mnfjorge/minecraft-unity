using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Voxel Mesh Data", menuName = "MinecraftTutorial/Voxel Mesh Data")]
public class VoxelMeshData : ScriptableObject
{
    public string blockName;
    public FaceMeshData[] faces;
}

[System.Serializable]
public class VertData
{
    public Vector3 position;
    public Vector2 uv;

    public VertData(Vector3 position, Vector2 uv)
    {
        this.position = position;
        this.uv = uv;
    }

    public Vector3 GetRotatedPosition(Vector3 angles)
    {
        var centre = new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 direction = position - centre;
        direction = Quaternion.Euler(angles) * direction;
        return direction + centre;
    }
}

[System.Serializable]
public class FaceMeshData
{
    public string direction;
    public VertData[] vertData;
    public int[] triangles;

    public VertData GetVertData(int index)
    {
        return vertData[index];
    }
}