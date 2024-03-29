using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.IO;

public class World : MonoBehaviour
{
    public Settings settings;

    [Header("World Generation Values")]
    public BiomeAttributes[] biomes;

    [Range(0, 1)]
    public float globalLightLevel;
    public Color day;
    public Color night;

    public Transform player;
    public Player _player;
    public Vector3 spawnPosition;

    public Material material;
    public Material transparentMaterial;
    public Material waterMaterial;
    public BlockType[] blockTypes;

    Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    public ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    List<Chunk> chunksToUpdate = new List<Chunk>();
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();

    bool applyingModifications = false;

    Queue<Queue<VoxelMod>> modifications = new Queue<Queue<VoxelMod>>();

    private bool _inUI = false;

    public Clouds clouds;

    public GameObject debugScreen;

    public GameObject creativeInventoryWindow;
    public GameObject cursorSlot;

    Thread ChunkUpdateThread;
    public object ChunkUpdateThreadLock = new object();
    public object ChunkListThreadLock = new object();

    private static World instance;
    public static World Instance { get { return instance; } }

    public WorldData worldData;

    public string appPath;

    void Awake()
    {
        if (instance != null && instance != this)
            Destroy(gameObject);
        else
            instance = this;

        appPath = Application.persistentDataPath;

        _player = player.GetComponent<Player>();
    }

    void Start()
    {
        Debug.Log("Generating new world using seed " + VoxelData.seed);

        worldData = SaveSystem.LoadWorld("Prototype");

        string jsonImport = File.ReadAllText(Application.dataPath + "/settings.cfg");
        settings = JsonUtility.FromJson<Settings>(jsonImport);

        Random.InitState(VoxelData.seed);

        Shader.SetGlobalFloat("minGlobalLightLevel", VoxelData.minLightLevel);
        Shader.SetGlobalFloat("maxGlobalLightLevel", VoxelData.maxLightLevel);

        LoadWorld();

        SetGlobalLightValue();
        spawnPosition = new Vector3(VoxelData.WorldCentre, VoxelData.ChunkHeight - 50, VoxelData.WorldCentre);
        player.position = spawnPosition;
        CheckViewDistance();
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);

        if (settings.enableThreading)
        {
            ChunkUpdateThread = new Thread(new ThreadStart(ThreadedUpdate));
            ChunkUpdateThread.Start();
        }

        StartCoroutine(Tick());
    }

    public void SetGlobalLightValue()
    {
        Shader.SetGlobalFloat("GlobalLightLevel", globalLightLevel);
        Camera.main.backgroundColor = Color.Lerp(night, day, globalLightLevel);
    }

    IEnumerator Tick()
    {
        while (true)
        {
            foreach (var c in activeChunks)
            {
                chunks[c.x, c.z].TickUpdate();
            }

            yield return new WaitForSeconds(VoxelData.tickLength);
        }
    }

    void Update()
    {
        playerChunkCoord = GetChunkCoordFromVector3(player.position);

        if (!playerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();

        if (chunksToDraw.Count > 0)
        {
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

        if (Input.GetKeyDown(KeyCode.F1))
            SaveSystem.SaveWorld(worldData);
    }

    void LoadWorld()
    {
        for (int x = (VoxelData.WorldSizeInChunks / 2) - settings.loadDistance; x < (VoxelData.WorldSizeInChunks / 2) + settings.loadDistance; x++)
        {
            for (int z = (VoxelData.WorldSizeInChunks / 2) - settings.loadDistance; z < (VoxelData.WorldSizeInChunks / 2) + settings.loadDistance; z++)
            {
                worldData.LoadChunk(new Vector2Int(x, z));
            }
        }
    }

    public void AddChunkToUpdate(Chunk chunk)
    {
        AddChunkToUpdate(chunk, false);
    }

    public void AddChunkToUpdate(Chunk chunk, bool insert)
    {
        lock (ChunkUpdateThreadLock)
        {
            if (!chunksToUpdate.Contains(chunk))
            {
                if (insert)
                    chunksToUpdate.Insert(0, chunk);
                else
                    chunksToUpdate.Add(chunk);
            }
        }
    }

    void UpdateChunks()
    {
        lock (ChunkUpdateThreadLock)
        {
            chunksToUpdate[0].UpdateChunk();
            if (!activeChunks.Contains(chunksToUpdate[0].coord))
                activeChunks.Add(chunksToUpdate[0].coord);
            chunksToUpdate.RemoveAt(0);
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

                worldData.SetVoxel(v.position, v.id, 1);
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
        clouds.UpdateClouds();

        ChunkCoord coord = GetChunkCoordFromVector3(player.position);

        playerLastChunkCoord = playerChunkCoord;

        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);

        activeChunks.Clear();

        for (int x = coord.x - settings.viewDistance; x < coord.x + settings.viewDistance; x++)
        {
            for (int z = coord.z - settings.viewDistance; z < coord.z + settings.viewDistance; z++)
            {
                var thisChunkCoord = new ChunkCoord(x, z);

                if (IsChunkInWorld(thisChunkCoord))
                {
                    if (chunks[x, z] == null)
                        chunks[x, z] = new Chunk(thisChunkCoord);

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
        VoxelState voxel = worldData.GetVoxel(pos);
        return blockTypes[voxel.id].isSolid;
    }

    public VoxelState GetVoxelState(Vector3 pos)
    {
        return worldData.GetVoxel(pos);
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

    bool IsVoxelInWorld(Vector3 pos)
    {
        if (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels
            && pos.y >= 0 && pos.y < VoxelData.ChunkHeight
            && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels)
            return true;

        return false;
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

        /* BIOME SELECTION PASS */

        int solidGroundHeight = 42;
        float sumOfHeights = 0f;
        int count = 0;
        float strongestWeight = 0;
        int strongestBiomeIndex = 0;

        for (int i = 0; i < biomes.Length; i++)
        {
            float weight = Noise.Get2DPerlin(new Vector2(pos.x, pos.z), biomes[i].offset, biomes[i].scale);

            // Keep track of which weight is strongest
            if (weight > strongestWeight)
            {
                strongestWeight = weight;
                strongestBiomeIndex = i;
            }

            // Get height of terrain and times its weight
            float height = biomes[i].terrainHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 500, biomes[i].terrainScale) * weight;

            // If height is greater than 0 add it to the sumOfHeights
            if (height > 0)
            {
                sumOfHeights += height;
                count++;
            }
        }

        // set biome to the strongest weight
        var biome = biomes[strongestBiomeIndex];

        // Get avg of heights
        sumOfHeights /= count;

        int terrainHeight = Mathf.FloorToInt(sumOfHeights + solidGroundHeight);

        /* BASIC TERRAIN PASS */

        byte voxelValue;

        if (yPos == terrainHeight)
            voxelValue = biome.surfaceBlock;
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = biome.subSurfaceBlock;
        else if (yPos > terrainHeight)
        {
            // water level
            if (yPos < 51)
                return 14;
            else
                return 0;
        }
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

        /* MAJOR FLORA PASS */

        if (yPos == terrainHeight && biome.placeMajorFlora)
        {
            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.majorFloraZoneScale) > biome.majorFloraZoneThreshold)
            {
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.majorFloraPlacementScale) > biome.majorFloraPlacementThreshold)
                {
                    modifications.Enqueue(Structure.GenerateMajorFlora(biome.majorFloraIndex, pos, biome.minHeight, biome.maxHeight));
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
}

[System.Serializable]
public class BlockType
{
    public string blockName;
    public bool isSolid;
    public VoxelMeshData meshData;
    public bool renderNeighborFaces;
    public bool isWater;
    public byte opacity;
    public Sprite icon;
    public bool isActive;

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
    public string version = "0.0.1";

    [Header("Performance")]
    public int loadDistance = 16;
    public int viewDistance = 8;
    public bool enableThreading = true;
    public bool enableAnimatedChunks = false;
    public CloudStyle clouds = CloudStyle.Fancy;

    [Header("Controls")]
    [Range(0.1f, 10f)]
    public float mouseSensitivity = 2;
}
