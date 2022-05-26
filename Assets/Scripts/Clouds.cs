using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Clouds : MonoBehaviour
{
    public int cloudHeight = 100;
    public int cloudDepth = 4;

    [SerializeField]
    Texture2D cloudPattern = null;
    [SerializeField]
    Material cloudMaterial = null;
    [SerializeField]
    World world = null;
    bool[,] cloudData;

    int cloudTexWidth;

    int cloudTileSize;
    Vector3Int offset;

    Dictionary<Vector2Int, GameObject> clouds = new Dictionary<Vector2Int, GameObject>();

    void Start()
    {
        cloudTexWidth = cloudPattern.width;
        cloudTileSize = VoxelData.ChunkWidth;
        offset = new Vector3Int(-(cloudTexWidth / 2), 0, -(cloudTexWidth / 2));

        transform.position = new Vector3(VoxelData.WorldCentre, cloudHeight, VoxelData.WorldCentre);

        LoadCloudData();
        CreateClouds();
    }

    void LoadCloudData()
    {
        cloudData = new bool[cloudTexWidth, cloudTexWidth];
        Color[] cloudTex = cloudPattern.GetPixels();

        for (int x = 0; x < cloudTexWidth; x++)
        {
            for (int y = 0; y < cloudTexWidth; y++)
            {
                cloudData[x, y] = cloudTex[y * cloudTexWidth + x].a > 0;
            }
        }
    }

    void CreateClouds()
    {
        if (world.settings.clouds == CloudStyle.Off)
            return;

        for (int x = 0; x < cloudTexWidth; x += cloudTileSize)
        {
            for (int y = 0; y < cloudTexWidth; y += cloudTileSize)
            {
                Mesh cloudMesh;

                if (world.settings.clouds == CloudStyle.Fast)
                    cloudMesh = CreateFastCloudMesh(x, y);
                else
                    cloudMesh = CreateFancyCloudMesh(x, y);

                var position = new Vector3(x, cloudHeight, y);
                position += transform.position - new Vector3(cloudTexWidth / 2f, 0, cloudTexWidth / 2f);
                position.y = cloudHeight;
                clouds.Add(CloudTilePosFromV3(position), CreateCloudTile(cloudMesh, position));
            }
        }
    }

    public void UpdateClouds()
    {
        if (world.settings.clouds == CloudStyle.Off)
            return;

        for (int x = 0; x < cloudTexWidth; x += cloudTileSize)
        {
            for (int y = 0; y < cloudTexWidth; y += cloudTileSize)
            {
                var position = world.player.position + new Vector3(x, 0, y) + offset;
                position = new Vector3(RoundToCloud(position.x), cloudHeight, RoundToCloud(position.z));
                var cloudPosition = CloudTilePosFromV3(position);
                clouds[cloudPosition].transform.position = position;
            }
        }
    }

    int RoundToCloud(float value)
    {
        return Mathf.FloorToInt(value / cloudTileSize) * cloudTileSize;
    }

    Mesh CreateFastCloudMesh(int x, int z)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        int vertCount = 0;

        for (int xIncrement = 0; xIncrement < cloudTileSize; xIncrement++)
        {
            for (int zIncrement = 0; zIncrement < cloudTileSize; zIncrement++)
            {
                int xVal = x + xIncrement;
                int zVal = z + zIncrement;

                if (cloudData[xVal, zVal])
                {
                    vertices.Add(new Vector3(xIncrement, 0, zIncrement));
                    vertices.Add(new Vector3(xIncrement, 0, zIncrement + 1));
                    vertices.Add(new Vector3(xIncrement + 1, 0, zIncrement + 1));
                    vertices.Add(new Vector3(xIncrement + 1, 0, zIncrement));

                    // we only look clouds from below (bottom)
                    for (int i = 0; i < 4; i++)
                        normals.Add(Vector3.down);

                    // first triangle
                    triangles.Add(vertCount + 1);
                    triangles.Add(vertCount);
                    triangles.Add(vertCount + 2);

                    // second triangle
                    triangles.Add(vertCount + 2);
                    triangles.Add(vertCount);
                    triangles.Add(vertCount + 3);

                    vertCount += 4;
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();

        return mesh;
    }

    Mesh CreateFancyCloudMesh(int x, int z)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        int vertCount = 0;

        for (int xIncrement = 0; xIncrement < cloudTileSize; xIncrement++)
        {
            for (int zIncrement = 0; zIncrement < cloudTileSize; zIncrement++)
            {
                int xVal = x + xIncrement;
                int zVal = z + zIncrement;

                if (cloudData[xVal, zVal])
                {
                    // loop through faces
                    for (int p = 0; p < 6; p++)
                    {
                        // check if neighbor has a cloud. if it has, dont create the face
                        if (!CheckCloudData(new Vector3Int(xVal, 0, zVal) + VoxelData.faceChecks[p]))
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                Vector3 vert = new Vector3Int(xIncrement, 0, zIncrement);
                                vert += VoxelData.voxelVerts[VoxelData.voxelTris[p, i]];
                                vert.y *= cloudDepth;
                                vertices.Add(vert);
                            }

                            for (int i = 0; i < 4; i++)
                                normals.Add(VoxelData.faceChecks[p]);

                            triangles.Add(vertCount + 0);
                            triangles.Add(vertCount + 1);
                            triangles.Add(vertCount + 2);
                            triangles.Add(vertCount + 2);
                            triangles.Add(vertCount + 1);
                            triangles.Add(vertCount + 3);

                            vertCount += 4;
                        }
                    }
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();

        return mesh;
    }

    bool CheckCloudData(Vector3Int point)
    {
        // clouds only have 1 point height - anything different means there are no clouds
        if (point.y != 0)
            return false;

        int x = point.x;
        int z = point.z;

        if (point.x < 0) x = cloudTexWidth - 1;
        if (point.x > cloudTexWidth - 1) x = 0;
        if (point.z < 0) z = cloudTexWidth - 1;
        if (point.z > cloudTexWidth - 1) z = 0;

        return cloudData[x, z];
    }

    GameObject CreateCloudTile(Mesh mesh, Vector3 position)
    {
        var newCloudTile = new GameObject();
        newCloudTile.transform.position = position;
        newCloudTile.transform.parent = transform;
        newCloudTile.name = "Cloud " + position.x + "," + position.z;

        var meshFilter = newCloudTile.AddComponent<MeshFilter>();
        var meshRenderer = newCloudTile.AddComponent<MeshRenderer>();

        meshRenderer.material = cloudMaterial;
        meshFilter.mesh = mesh;

        return newCloudTile;
    }

    Vector2Int CloudTilePosFromV3(Vector3 pos)
    {
        return new Vector2Int(CloudTileCoordFromFloat(pos.x), CloudTileCoordFromFloat(pos.z));
    }

    int CloudTileCoordFromFloat(float value)
    {
        float a = value / cloudTexWidth;
        a -= Mathf.FloorToInt(a);
        int b = Mathf.FloorToInt(cloudTexWidth * a);
        return b;
    }
}

public enum CloudStyle
{
    Off,
    Fast,
    Fancy
}