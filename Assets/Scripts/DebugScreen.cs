using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugScreen : MonoBehaviour
{
    World world;
    Text text;

    float frameRate;
    float timer;

    int halfWorldSizeInVoxel;
    int halfWorldSizeInChunks;

    void Start()
    {
        world = GameObject.Find("World").GetComponent<World>();
        text = GetComponent<Text>();

        halfWorldSizeInVoxel = VoxelData.WorldSizeInVoxels / 2;
        halfWorldSizeInChunks = VoxelData.WorldSizeInChunks / 2;
    }

    // Update is called once per frame
    void Update()
    {
        string debugText = "Debugging...";
        debugText += "\n";
        debugText += frameRate + " fps";
        debugText += "\n\n";
        debugText += "XYZ: " + (Mathf.FloorToInt(world.player.transform.position.x) - halfWorldSizeInVoxel) + "," + Mathf.FloorToInt(world.player.transform.position.y) + "," + (Mathf.FloorToInt(world.player.transform.position.z) - halfWorldSizeInVoxel);
        debugText += "\n";
        debugText += "Chunk: " + (world.playerChunkCoord.x - halfWorldSizeInChunks) + "," + (world.playerChunkCoord.z - halfWorldSizeInChunks);

        string direction = "";
        switch (world._player.orientation)
        {
            case 0:
                direction = "South";
                break;
            case 1:
                direction = "North";
                break;
            case 5:
                direction = "West";
                break;
            case 4:
                direction = "East";
                break;
        }

        debugText += "\n";
        debugText += "Direction Facing: " + direction;

        text.text = debugText;

        if (timer > 1f)
        {
            frameRate = (int)(1f / Time.unscaledDeltaTime);
            timer = 0;
        }
        else
        {
            timer += Time.deltaTime;
        }
    }
}
