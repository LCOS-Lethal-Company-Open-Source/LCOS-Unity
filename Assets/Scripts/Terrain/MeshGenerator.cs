using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

using Rand = UnityEngine.Random;

public class MeshGenerator : MonoBehaviour
{
    [Serializable]
    public struct OctaveInfo
    {
        public Vector2 NoiseScale;
        public Vector2 NoiseOffset;
        public float OutputScale;
    }


    [Header("Noise")]
    public OctaveInfo[] octaves;

    [Header("Mesh Details")]
    public int width;
    public int height;
    public float spacing;
    public float baseNoiseScale;

    [Header("Preview")]
    public MeshFilter preview;

    public Mesh Generate()
    {
        var vertices = new Vector3[width * height];
        var triangles = new List<int>();

        var offX = Rand.Range(0, 10000);
        var offY = Rand.Range(0, 10000);

        for (int i = 0; i < width; i++)
        {
            for(int j = 0; j < height; j++)
            {
                var ceni = i - width / 2;
                var cenj = j - height / 2;

                var basePosition = new Vector3(ceni * spacing, 0, cenj * spacing);

                var totalNoise = 0f;
                for(int k = 0; k < octaves.Length; k++)
                {
                    var info = octaves[k];

                    var noise = Mathf.PerlinNoise(
                        info.NoiseScale.x * i * baseNoiseScale * spacing + info.NoiseOffset.x + offX,
                        info.NoiseScale.y * j * baseNoiseScale * spacing + info.NoiseOffset.y + offY
                    );

                    totalNoise += noise * info.OutputScale - info.OutputScale / 2;
                }

                var idx = i * width + j;
                vertices[idx] = basePosition + Vector3.up * totalNoise;

                if (i != width - 1 && j != height - 1)
                {
                    triangles.Add(idx + 0);
                    triangles.Add(idx + 1);
                    triangles.Add(idx + width);
                    triangles.Add(idx + width + 1);
                    triangles.Add(idx + width);
                    triangles.Add(idx + 1);
                }
            }
        }

        var mesh = new Mesh();

        if (preview)
        {
            preview.mesh = mesh;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();

        mesh.RecalculateBounds();

        return mesh;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MeshGenerator))]
public class MeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Regenerate"))
        {
            (target as MeshGenerator).Generate();
        }
    }
}


#endif