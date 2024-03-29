using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AtlasPacker : EditorWindow
{
    int blockSize = 16; // block size in pixels
    int atlasSizeInBlocks = 16;
    int atlasSize;

    Object[] rawTextures = new Object[256];
    List<Texture2D> sortedTextures = new List<Texture2D>();
    Texture2D atlas;

    [MenuItem("Minecraft Clone/Atlas Packer")]

    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(AtlasPacker));
    }

    void OnGUI()
    {
        atlasSize = blockSize * atlasSizeInBlocks;

        GUILayout.Label("Minecraft Clone Texture Atlas Packer", EditorStyles.boldLabel);

        blockSize = EditorGUILayout.IntField("Block Size", blockSize);
        atlasSizeInBlocks = EditorGUILayout.IntField("Atlas Size (in blocks)", atlasSizeInBlocks);

        GUILayout.Label(atlas);

        if (GUILayout.Button("Load Textures"))
        {
            LoadTextures();
            PackAtlas();

            Debug.Log("Atlas Packer: Textures loaded");
        }

        if (GUILayout.Button("Clear Textures"))
        {
            atlas = new Texture2D(atlasSize, atlasSize);
            Debug.Log("Atlas Packer: Textures cleared");
        }

        if (GUILayout.Button("Save Atlas"))
        {
            byte[] bytes = atlas.EncodeToPNG();

            try
            {
                File.WriteAllBytes(Application.dataPath + "/Textures/Packed_Atlas.png", bytes);

                Debug.Log("Atlas Packer: Atlas saved");
            }
            catch
            {
                Debug.LogError("Atlas Packer: Error to save atlas");
            }
        }
    }

    void LoadTextures()
    {
        sortedTextures.Clear();

        rawTextures = Resources.LoadAll("AtlasPacker", typeof(Texture2D));

        int index = 0;
        foreach (var tex in rawTextures)
        {
            var t = (Texture2D)tex;

            if (t.width == blockSize && t.height == blockSize)
                sortedTextures.Add(t);
            else
                Debug.Log("Asset Packer: `" + tex.name + "` incorrect size. Not loaded.");

            index++;
        }

        Debug.Log("Atlas Packer: " + sortedTextures.Count + " textures added");
    }

    void PackAtlas()
    {
        atlas = new Texture2D(atlasSize, atlasSize);
        var pixels = new Color[atlasSize * atlasSize];

        for (int x = 0; x < atlasSize; x++)
        {
            for (int y = 0; y < atlasSize; y++)
            {
                // get the current block we're looking at
                int currentBlockX = x / blockSize;
                int currentBlockY = y / blockSize;

                int index = currentBlockY * atlasSizeInBlocks + currentBlockX;

                // get the pixel in the current block
                int currentPixelX = x - (currentBlockX * blockSize);
                int currentPixelY = y - (currentBlockY * blockSize);

                if (index < sortedTextures.Count)
                    pixels[(atlasSize - y - 1) * atlasSize + x] = sortedTextures[index].GetPixel(x, blockSize - y - 1);
                else
                    pixels[(atlasSize - y - 1) * atlasSize + x] = new Color(0, 0, 0, 0);
            }

            atlas.SetPixels(pixels);
            atlas.Apply();
        }
    }
}
