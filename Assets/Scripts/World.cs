using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour {

    public int seed;
    public BiomeAttributes biome;

    public Transform player;
    public Vector3 spawnPosition;

    public Material material;
    public BlockType[] blockTypes;

    Chunk[,] chunks = new Chunk[VoxelData.WorldSizeInChunks, VoxelData.WorldSizeInChunks];

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();

    ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    void Start() {

        Random.InitState(seed);

        spawnPosition = new Vector3((VoxelData.WorldSizeInChunks / 2) * VoxelData.ChunkWidth, VoxelData.ChunkHeight - 50, (VoxelData.WorldSizeInChunks / 2) * VoxelData.ChunkWidth);
        GenerateWorld();
    }

    void Update() {
        playerChunkCoord = GetChunkCoordFromVector3(player.position);

        //if (!playerChunkCoord.Equals(playerLastChunkCoord))
        //    CheckViewDistance();
    }

    void GenerateWorld() {
        for (int x = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunk; x < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunk; x++) {
            for (int z = (VoxelData.WorldSizeInChunks / 2) - VoxelData.ViewDistanceInChunk; z < (VoxelData.WorldSizeInChunks / 2) + VoxelData.ViewDistanceInChunk; z++) {
                CreateNewChunk(x, z);
            }
        }

        player.position = spawnPosition;
    }

    ChunkCoord GetChunkCoordFromVector3(Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);

        return new ChunkCoord(x, z);
    }

    void CheckViewDistance() {
        ChunkCoord coord = GetChunkCoordFromVector3(player.position);

        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);

        for (int x = coord.x - VoxelData.ViewDistanceInChunk; x < coord.x + VoxelData.ViewDistanceInChunk; x++) {
            for (int z = coord.z - VoxelData.ViewDistanceInChunk; z < coord.z + VoxelData.ViewDistanceInChunk; z++) {
                if (IsChunkInWorld(new ChunkCoord(x, z))) {
                    if (chunks[x, z] == null) {
                        CreateNewChunk(x, z);
                    } else if (!chunks[x, z].IsActive) {
                        chunks[x, z].IsActive = true;
                        activeChunks.Add(new ChunkCoord(x, z));
                    }
                }

                for (int i = 0; i < previouslyActiveChunks.Count; i++) {
                    if (previouslyActiveChunks[i].Equals(new ChunkCoord(x, z))) {
                        previouslyActiveChunks.RemoveAt(i);
                    }
                }
            }
        }

        foreach (var c in previouslyActiveChunks) {
            chunks[c.x, c.z].IsActive = false;
        }
    }

    public bool CheckForVoxel(float _x, float _y, float _z) {
        int xCheck = Mathf.FloorToInt(_x);
        int yCheck = Mathf.FloorToInt(_y);
        int zCheck = Mathf.FloorToInt(_z);

        int xChunk = xCheck / VoxelData.ChunkWidth;
        int zChunk = zCheck / VoxelData.ChunkWidth;

        xCheck -= (xChunk * VoxelData.ChunkWidth);
        zCheck -= (zChunk * VoxelData.ChunkWidth);

        return blockTypes[chunks[xChunk, zChunk].voxelMap[xCheck, yCheck, zCheck]].isSolid;
    }

    public byte GetVoxel(Vector3 pos) {

        int yPos = Mathf.FloorToInt(pos.y);

        /* IMMUTABLE PASS */

        // outside the world. 0 = air
        if (!IsVoxelInWorld(pos))
            return 0;

        // bottom tile. 1 = bedrock
        if (yPos == 0)
            return 1;

        /* BASIC TERRAIN PASS */

        int terrainHeight = Mathf.FloorToInt(biome.terrainHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale)) + biome.solidGroundHeight;
        byte voxelValue = 0;

        if (yPos == terrainHeight)
            voxelValue = 3;
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            voxelValue = 5;
        else if (yPos > terrainHeight)
            return 0;
        else
            voxelValue = 2;

        /* SECOND PASS */

        if (voxelValue == 2) {
            foreach (var lode in biome.lodes) {
                if (yPos > lode.minHeight && yPos < lode.maxHeight) {
                    if (Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold)) {
                        voxelValue = lode.blockId;
                    }
                }
            }
        }

        return voxelValue;
    }

    void CreateNewChunk(int x, int z) {
        var coord = new ChunkCoord(x, z);
        chunks[x, z] = new Chunk(coord, this);
        activeChunks.Add(coord);
    }

    bool IsChunkInWorld(ChunkCoord coord) {
        if (coord.x > 0 && coord.x < VoxelData.WorldSizeInChunks - 1 && coord.z > 0 && coord.z < VoxelData.WorldSizeInChunks - 1)
            return true;

        return false;
    }

    bool IsVoxelInWorld(Vector3 pos) {
        if (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels
            && pos.y >= 0 && pos.y < VoxelData.ChunkHeight
            && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels)
            return true;

        return false;
    }
}

[System.Serializable]
public class BlockType {
    public string blockName;
    public bool isSolid;

    [Header("Texture Values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    public int GetTextureId(int faceIndex) {
        switch (faceIndex) {
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
