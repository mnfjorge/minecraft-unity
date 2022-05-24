using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.IO;

public class World : MonoBehaviour
{
    public Settings settings;

    [Header("World Generation Values")]
    public BiomeAttributes biome;

    [Range(0, 1)]
    public float globalLightLevel;
    public Color day;
    public Color night;

    public Transform player;
    public Vector3 spawnPosition;

    public Material material;
    public Material transparentMaterial;
    public BlockType[] blockTypes;

    Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    public ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    public List<Chunk> chunksToUpdate = new List<Chunk>();
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();

    bool applyingModifications = false;

    Queue<Queue<VoxelMod>> modifications = new Queue<Queue<VoxelMod>>();

    private bool _inUI = false;

    public GameObject debugScreen;

    public GameObject creativeInventoryWindow;
    public GameObject cursorSlot;

    Thread ChunkUpdateThread;
    public object ChunkUpdateThreadLock = new object();

    void Start()
    {
        //string jsonExport = JsonUtility.ToJson(this.settings);
        //File.WriteAllText(Application.dataPath + "/settings.cfg", jsonExport);

        string jsonImport = File.ReadAllText(Application.dataPath + "/settings.cfg");
        settings = JsonUtility.FromJson<Settings>(jsonImport);

        Random.InitState(settings.seed);

        Shader.SetGlobalFloat("minGlobalLightLevel", VoxelData.minLightLevel);
        Shader.SetGlobalFloat("maxGlobalLightLevel", VoxelData.maxLightLevel);

        if (settings.enableThreading)
        {
            ChunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
            ChunkUpdateThread.Start();
        }

        SetGlobalLightValue();
        spawnPosition = new Vector3((VoxelData.WorldSizeInChunks / 2) * VoxelData.ChunkWidth, VoxelData.ChunkHeight - 50, (VoxelData.WorldSizeInChunks / 2) * VoxelData.ChunkWidth);
        GenerateWorld();
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);
    }

    public void SetGlobalLightValue()
    {
        Shader.SetGlobalFloat("GlobalLightLevel", globalLightLevel);
        Camera.main.backgroundColor = Color.Lerp(night, day, globalLightLevel);
    }

    void Update()
    {
        playerChunkCoord = GetChunkCoordFromVector3(player.position);

        if (!playerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();

        if (chunksToCreate.Count > 0)
            CreateChunk();

        if (chunksToDraw.Count > 0)
        {
            if (chunksToDraw.Peek().isEditable)
                chunksToDraw.Dequeue().CreateMesh();
        }

        if (!settings.enableThreading)
        {
            if (!applyingModifications)
                ApplyModifications();

            if (chunksToUpdate.Count > 0)
                UpdateChunks();
        }

        if (Input.GetKeyDown(KeyCode.F3))
            debugScreen.SetActive(!debugScreen.activeSelf);
    }

    void GenerateWorld()
    {
        for (int x = (VoxelData.WorldSizeInChunks / 2) - settings.ViewDistance; x < (VoxelData.WorldSizeInChunks / 2) + settings.ViewDistance; x++)
        {
            for (int z = (VoxelData.WorldSizeInChunks / 2) - settings.ViewDistance; z < (VoxelData.WorldSizeInChunks / 2) + settings.ViewDistance; z++)
            {
                var newChunk = new ChunkCoord(x, z);
                chunks[x, z] = new Chunk(newChunk, this);
                chunksToCreate.Add(newChunk);
            }
        }

        player.position = spawnPosition;
        CheckViewDistance();
    }

    void CreateChunk()
    {
        var c = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        chunks[c.x, c.z].Init();
    }

    void UpdateChunks()
    {
        bool updated = false;
        int index = 0;

        lock (ChunkUpdateThreadLock)
        {
            while (!updated && index < chunksToUpdate.Count - 1)
            {
                if (chunksToUpdate[index].isEditable)
                {
                    chunksToUpdate[index].UpdateChunk();
                    if (!activeChunks.Contains(chunksToUpdate[index].coord))
                        activeChunks.Add(chunksToUpdate[index].coord);
                    chunksToUpdate.RemoveAt(index);
                    updated = true;
                }
                else
                {
                    index++;
                }
            }
        }
    }

    void ThreadedUpdate()
    {
        while (true)
        {
            if (!applyingModifications)
                ApplyModifications();

            if (chunksToUpdate.Count > 0)
                UpdateChunks();
        }
    }

    void OnDisable()
    {
        if (settings.enableThreading)
        {
            ChunkUpdateThread.Abort();
        }
    }

    void ApplyModifications()
    {
        applyingModifications = true;

        while (modifications.Count > 0)
        {
            Queue<VoxelMod> queue = modifications.Dequeue();

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();

                ChunkCoord c = GetChunkCoordFromVector3(v.position);

                if (chunks[c.x, c.z] == null)
                {
                    chunks[c.x, c.z] = new Chunk(c, this);
                    chunksToCreate.Add(c);
                }

                chunks[c.x, c.z].modifications.Enqueue(v);
            }
        }

        applyingModifications = false;
    }

    ChunkCoord GetChunkCoordFromVector3(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);

        return new ChunkCoord(x, z);
    }

    public Chunk GetChunkFromVector3(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);

        return chunks[x, z];
    }

    void CheckViewDistance()
    {
        ChunkCoord coord = GetChunkCoordFromVector3(player.position);

        playerLastChunkCoord = playerChunkCoord;

        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);

        activeChunks.Clear();

        for (int x = coord.x - settings.ViewDistance; x < coord.x + settings.ViewDistance; x++)
        {
            for (int z = coord.z - settings.ViewDistance; z < coord.z + settings.ViewDistance; z++)
            {
                var thisChunkCoord = new ChunkCoord(x, z);

                if (IsChunkInWorld(thisChunkCoord))
                {
                    if (chunks[x, z] == null)
                    {
                        chunks[x, z] = new Chunk(thisChunkCoord, this);
                        chunksToCreate.Add(thisChunkCoord);
                    }
                    else if (!chunks[x, z].IsActive)
                        chunks[x, z].IsActive = true;

                    activeChunks.Add(thisChunkCoord);
                }

                for (int i = 0; i < previouslyActiveChunks.Count; i++)
                {
                    if (previouslyActiveChunks[i].Equals(thisChunkCoord))
                    {
                        previouslyActiveChunks.RemoveAt(i);
                    }
                }
            }
        }

        foreach (var c in previouslyActiveChunks)
        {
            chunks[c.x, c.z].IsActive = false;
        }
    }

    public bool CheckForVoxel(Vector3 pos)
    {
        ChunkCoord thisChunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false;

        if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isEditable)
            return blockTypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos).id].isSolid;

        return blockTypes[GetVoxel(pos)].isSolid;
    }

    public VoxelState GetVoxelState(Vector3 pos)
    {
        ChunkCoord thisChunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return null;

        if (chunks[thisChunk.x, thisChunk.z] != null && chunks[thisChunk.x, thisChunk.z].isEditable)
            return chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos);

        return new VoxelState(GetVoxel(pos));
    }

    public bool inUI
    {
        get { return _inUI; }
        set
        {
            _inUI = value;
            if (_inUI)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                creativeInventoryWindow.SetActive(true);
                cursorSlot.SetActive(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                creativeInventoryWindow.SetActive(false);
                cursorSlot.SetActive(false);
            }
        }
    }

    public byte GetVoxel(Vector3 pos)
    {

        int yPos = Mathf.FloorToInt(pos.y);

        /* IMMUTABLE PASS */

        // outside the world. 0 = air
        if (!IsVoxelInWorld(pos))
            return 0;

        // bottom tile. 1 = bedrock
        if (yPos == 0)
            return 1;

        /* BASIC TERRAIN PASS */

        int terrainHeight = Mathf.FloorToInt(biome.terrainHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 500, biome.terrainScale)) + biome.solidGroundHeight;
        byte voxelValue;

        if (yPos == terrainHeight)
            voxelValue = 3;
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = 5;
        else if (yPos > terrainHeight)
            return 0;
        else
            voxelValue = 2;

        /* SECOND PASS */

        if (voxelValue == 2)
        {
            foreach (var lode in biome.lodes)
            {
                if (yPos > lode.minHeight && yPos < lode.maxHeight)
                {
                    if (Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold))
                    {
                        voxelValue = lode.blockId;
                    }
                }
            }
        }

        /* TREE PASS */

        if (yPos == terrainHeight)
        {
            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treeZoneScale) > biome.treeZoneThreshold)
            {
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > biome.placementThreshold)
                {
                    modifications.Enqueue(Structure.MakeTree(pos, biome.minTreeHeight, biome.maxTreeHeight));
                }
            }
        }

        return voxelValue;
    }

    bool IsChunkInWorld(ChunkCoord coord)
    {
        if (coord.x > 0 && coord.x < VoxelData.WorldSizeInChunks - 1 && coord.z > 0 && coord.z < VoxelData.WorldSizeInChunks - 1)
            return true;

        return false;
    }

    bool IsVoxelInWorld(Vector3 pos)
    {
        if (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels
            && pos.y >= 0 && pos.y < VoxelData.ChunkHeight
            && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels)
            return true;

        return false;
    }
}

[System.Serializable]
public class BlockType
{
    public string blockName;
    public bool isSolid;
    public bool renderNeighborFaces;
    public float transparency;
    public Sprite icon;

    [Header("Texture Values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    public int GetTextureId(int faceIndex)
    {
        switch (faceIndex)
        {
            case 0:
                return backFaceTexture;
            case 1:
                return frontFaceTexture;
            case 2:
                return topFaceTexture;
            case 3:
                return bottomFaceTexture;
            case 4:
                return leftFaceTexture;
            case 5:
                return rightFaceTexture;
            default:
                Debug.LogError("[GetTextureId] Invalid faceIndex");
                return 0;
        }
    }

}

public class VoxelMod
{
    public Vector3 position;
    public byte id;

    public VoxelMod()
    {
        position = new Vector3();
        id = 0;
    }

    public VoxelMod(Vector3 position, byte id)
    {
        this.position = position;
        this.id = id;
    }
}

[System.Serializable]
public class Settings
{
    [Header("Game Data")]
    public string version;

    [Header("Performance")]
    public int ViewDistance = 5;
    public bool enableThreading = true;
    public bool enableAnimatedChunks = false;

    [Header("Controls")]
    [Range(0.1f, 10f)]
    public float mouseSensitivity = 2;

    [Header("World Gen")]
    public int seed;
}
