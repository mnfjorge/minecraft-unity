using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;

public static class SaveSystem
{
    public static void SaveWorld(WorldData world)
    {
        string savePath = World.Instance.appPath + "/saves/" + world.worldName + "/";

        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        Debug.Log("Saving world " + savePath);

        var formatter = new BinaryFormatter();
        var stream = new FileStream(savePath + "world.world", FileMode.Create);

        formatter.Serialize(stream, world);
        stream.Close();

        var thread = new Thread(new ThreadStart(() => SaveChunks(world)));
        thread.Start();
    }

    public static void SaveChunks(WorldData world)
    {
        var chunks = new List<ChunkData>(world.modifiedChunks);
        world.modifiedChunks.Clear();

        int count = 0;
        foreach (var chunk in chunks)
        {
            SaveSystem.SaveChunk(chunk, world.worldName);
            count++;
        }

        Debug.Log(count + " chunks saved");
    }

    public static WorldData LoadWorld(string worldName, int seed = 0)
    {
        string loadPath = World.Instance.appPath + "/saves/" + worldName + "/";

        if (File.Exists(loadPath + "world.world"))
        {
            var formatter = new BinaryFormatter();
            var stream = new FileStream(loadPath + "world.world", FileMode.Open);

            var world = formatter.Deserialize(stream) as WorldData;
            stream.Close();
            return new WorldData(world);
        }
        else
        {
            var world = new WorldData(worldName, seed);
            SaveWorld(world);
            return world;
        }
    }

    public static void SaveChunk(ChunkData chunk, string worldName)
    {
        string chunkName = chunk.position.x + "-" + chunk.position.y;

        string savePath = World.Instance.appPath + "/saves/" + worldName + "/chunks/";

        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        var formatter = new BinaryFormatter();
        var stream = new FileStream(savePath + chunkName + ".chunk", FileMode.Create);

        formatter.Serialize(stream, chunk);
        stream.Close();
    }

    public static ChunkData LoadChunk(string worldName, Vector2Int position)
    {
        string chunkName = position.x + "-" + position.y;

        string loadPath = World.Instance.appPath + "/saves/" + worldName + "/chunks/" + chunkName + ".chunk";

        if (File.Exists(loadPath))
        {
            var formatter = new BinaryFormatter();
            var stream = new FileStream(loadPath, FileMode.Open);

            var chunk = formatter.Deserialize(stream) as ChunkData;
            stream.Close();
            return chunk;
        }

        return null;
    }
}
