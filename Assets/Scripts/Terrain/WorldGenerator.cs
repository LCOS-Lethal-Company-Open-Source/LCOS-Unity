using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

using RandInst = Unity.Mathematics.Random;

public class WorldGenerator : MonoBehaviour
{
    [Serializable]
    public struct OctaveInfo
    {
        public Vector2 NoiseScale;
        public Vector2 NoiseOffset;
        public float OutputScale;
    }

    // INSPECTOR VARIABLES //
    [Header("Noise")]
    public OctaveInfo[] octaves;

    [Header("Mesh Details")]
    public int width;
    public int height;
    public float spacing;
    public float baseNoiseScale;

    [Header("Prefabs")]
    public GameObject mountain;

    [Header("Objects")]
    public GameObject mansion;
    public GameObject ship;
    public MeshFilter ground;
    public MeshFilter water;

    [Header("Positioning")]
    public float minTravelDistance;
    public float maxTravelDistance;
    public float waterOffset;
    public float waterDepth;
    public float waterWidth;

    [Header("Mountains")]
    public int minMountains;
    public int maxMountains;
    public float mountainDistance;
    public float mountainRange;

    // PRIVATE VARIABLES //
    List<GameObject> placedMountains = new();
    Mesh baseMesh;
    Mesh groundMesh;

    Vector3[] baseVerts;
    List<int3> groundTris;

    Vector3[] groundVerts;
    List<int3> baseTris;

    Vector2 shipPoint;
    Vector2 mansionPoint;

    Vector2 Size => new(width, height);

    RandInst random;

    public static int[] FormatTriangles(List<int3> tris)
    {
        var result = new int[tris.Count * 3];

        for(int i = 0; i < tris.Count; i++)
        {
            var j = i * 3;
            result[j + 0] = tris[i].x;
            result[j + 1] = tris[i].y;
            result[j + 2] = tris[i].z;
        }

        return result;
    }

    public Vector2 GetWorldPosition(int i, int j)
    {
        // Calculate the 'centered' indices
        var ceni = i - width / 2;
        var cenj = j - height / 2;

        // Calculate the world spaced indices
        return new(ceni * spacing, cenj * spacing);
    }

    public int Index(int i, int j)
        => i * width + j;

    public void EditorClean()
    {
        foreach (var obj in GameObject.FindObjectsByType<GeneratedObject>(FindObjectsSortMode.None))
            GameObject.DestroyImmediate(obj.gameObject);
    }

    public void GenerateGround()
    {
        baseVerts = new Vector3[width * height];
        baseTris = new();

        var offX = random.NextInt(0, 10000);
        var offY = random.NextInt(0, 10000);

        for (int i = 0; i < width; i++)
        {
            for(int j = 0; j < height; j++)
            {
                var basePosition = GetWorldPosition(i, j).InvXZ();

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

                var idx = Index(i, j);
                baseVerts[idx] = basePosition + Vector3.up * totalNoise;

                if (i != width - 1 && j != height - 1)
                {
                    baseTris.Add(new(
                        idx + 0,
                        idx + 1,
                        idx + width
                    ));
                    baseTris.Add(new(
                        idx + width + 1,
                        idx + width,
                        idx + 1
                    ));
                }
            }
        }

        // Copy base information to ground information
        groundVerts = baseVerts.ToArray();
        groundTris = baseTris.ToList();
    }

    public (int, int) GetVertexIndex(Vector2 xz, out bool leftTri)
    {
        // Calculate normalized position
        var norm = xz / spacing + Size / 2;

        // Check if index is in the left triangle on the mesh
        leftTri = norm.x + norm.y % 1 > 0.5f;

        // Return the normalized indices
        return ((int)norm.x, (int)norm.y);
    }

    public Vector3 SamplePoint(Vector2 xz)
    {
        // Get the vertex indices for this position
        var (minx, miny) = GetVertexIndex(xz, out var left);

        var maxx = minx + 1;
        var maxy = miny + 1;

        // Get the triangle this vertex is a part of
        int a, b, c;

        if(left)
        {
            a = Index(minx, miny);
            b = Index(maxx, miny);
            c = Index(minx, maxy);
        }
        else
        {
            a = Index(minx, miny);
            b = Index(maxx, miny);
            c = Index(maxx, maxy);
        }

        // Debugging
        #if false
        Debug.DrawLine(
            xz.InvXZ() - Vector3.up * 1000,
            xz.InvXZ() + Vector3.up * 1000,
            Color.cyan,
            10
        );
        Debug.DrawLine(
            verts[a],
            verts[b],
            Color.red,
            10
        ); Debug.DrawLine(
            verts[b],
            verts[c],
            Color.red,
            10
        ); Debug.DrawLine(
            verts[c],
            verts[a],
            Color.red,
            10
        );
        #endif

        // Use barycentric interpolation to calculate the
        // point's position on the mesh
        var bary = MathUtils.Barycentric(
            baseVerts[a].XZ(),
            baseVerts[b].XZ(),
            baseVerts[c].XZ(),
            xz
        );

        return bary.x * baseVerts[a] 
             + bary.y * baseVerts[b]
             + bary.z * baseVerts[c];
    }

    public void CreateDivet(Vector2 xz, float size, float depth)
    {
        // Calculate index values
        var (cx, cy) = GetVertexIndex(xz, out _);

        int idxWidth = Mathf.CeilToInt(size / spacing);

        // Loop through relevant vertices
        for(int x = Mathf.Max(cx - idxWidth, 0); x < Mathf.Min(cx + idxWidth, width); x++)
        {
            for(int y = Mathf.Max(cy - idxWidth, 0); y < Mathf.Min(cy + idxWidth, height); y++)
            {
                // Calculate world space coords
                var world = GetWorldPosition(x, y);

                // Calculate normalized distance
                var dist = Vector2.Distance(world, xz);
                var cdist = Mathf.Clamp(dist, 0, size);

                // Calculate divet shape
                var cos = Mathf.Cos(Mathf.PI * cdist / size);
                var fac = (1 + cos) / 2;
                var hgt = depth * fac;

                // Offset vertex by appropriate amount
                groundVerts[Index(x, y)] += hgt * Vector3.down;
            }
        }
    }

    public void GenerateRiver(Vector2 a, Vector2 b)
    {
        // Calculate various helper vectors
        var p = a;
        var ba = b - a;
        var dba = ba.magnitude;

        // Normalize normal vector
        ba /= dba;

        // Track angular offset from target
        var angle = random.NextFloat(-1, 1) / 2;
        var dist = Vector2.Distance(p, b);

        // Repeatedly create divets to create the river shape
        while (dist > 1)
        {
            // Get adjustment variables for shaping
            var widthAdjust = Mathf.PerlinNoise(p.x * 0.01f, p.y * 0.01f);

            // Calculate adjusted values
            var realwidth = waterWidth * (widthAdjust + 2) / 3;

            // Create divet in place.
            CreateDivet(p, realwidth, (waterDepth + waterOffset) / waterWidth);

            // March forward with a random angular offset
            angle += random.NextFloat(-1, 1) / 5;
            angle = Mathf.Clamp(angle, -2, 2);
            var angleAdjust = dist / dba;

            var deltap = Quaternion.Euler(0, 0, angleAdjust * angle * 45) * (b - p).normalized;

            p += (Vector2)deltap;
            dist = Vector2.Distance(p, b);
        }
    }

    public void GenerateMountainRange()
    {
        // Calculate normals of range
        var fwd = (mansionPoint - shipPoint).normalized;
        var per = new Vector2(-fwd.y, fwd.x);

        placedMountains.Clear();

        // Randomly place a set of mountains
        var count = random.NextInt(minMountains, maxMountains);
        for(int i = 0; i < count; i++)
        {
            var pos = mansionPoint
                    + fwd * (mountainDistance + random.NextFloat(-mountainRange, mountainRange))
                    + per * (random.NextFloat(-mountainRange, mountainRange));

            var realpos = SamplePoint(pos);

            var newMountain = GameObject.Instantiate(mountain, realpos, mountain.transform.rotation);
            placedMountains.Add(newMountain);
        }
    }

    public void MinimizeBaseMesh()
    {
        // Create a set of all vertices to remove from the base mesh
        var removed = new SortedSet<int>();

        bool Valid(int index)
        {
            if (index < 0 || index >= width * height)
                return false;

            if (baseVerts[index].y - waterOffset < groundVerts[index].y)
                return false;

            return true;
        }

        for(int i = 0; i < width*height; i++)
        {
            if (Valid(i))
                continue;

            bool hasValidNeighbor = false;

            for(int dx = -1; dx <= 1 && !hasValidNeighbor; dx++)
            {
                for(int dy = -1; dy <= 1 && !hasValidNeighbor; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    if (Valid(i + Index(dx, dy)))
                    {
                        hasValidNeighbor = true;
                    }
                }
            }

            if(!hasValidNeighbor)
            {
                removed.Add(i);
                Debug.DrawLine(baseVerts[i], baseVerts[i] + Vector3.up * 0.5f, Color.red, 10);
            }
        }

        // Rebuild the vertex list without these vertices
        var newBaseVerts = new Vector3[baseVerts.Length - removed.Count];
        var newIndex = 0;

        for(int i = 0; i < baseVerts.Length; i++)
        {
            if (removed.Contains(i))
                continue;

            newBaseVerts[newIndex] = baseVerts[i];

            newIndex++;
        }

        baseVerts = newBaseVerts;

        // Rebuild triangle list with vertex ids adjusted and with deleted triangles removed
        baseTris.RemoveAll(tri =>
            removed.Contains(tri.x) ||
            removed.Contains(tri.y) ||
            removed.Contains(tri.z)
        );

        var removedList = removed.ToList();

        int AdjustIndex(int index)
        {
            // Use binary search to efficiently find the number of previously
            // skipped vertices
            var search = removedList.BinarySearch(index);
            if (search < 0)
                return index - ~search;

            return index - search;
        }

        for (int i = 0; i < baseTris.Count; i++)
        {
            baseTris[i] = new(
                AdjustIndex(baseTris[i].x),
                AdjustIndex(baseTris[i].y),
                AdjustIndex(baseTris[i].z)
            );
        }
    }

    public void GenerateMeshes()
    {
        // Create base mesh
        baseMesh = new Mesh();
        water.mesh = baseMesh;

        baseMesh.vertices = baseVerts;
        baseMesh.triangles = FormatTriangles(baseTris);

        baseMesh.RecalculateBounds();

        water.transform.position = new Vector3(0, -waterOffset);

        // Create ground mesh
        groundMesh = new Mesh();
        ground.mesh = groundMesh;

        groundMesh.vertices = groundVerts;
        groundMesh.triangles = FormatTriangles(groundTris);

        groundMesh.RecalculateBounds();
    }

    public void GenerateWorldPoints()
    {
        // Place the ship and mansion in the world
        shipPoint = Vector2.zero;
        mansionPoint = random.NextFloat2Direction()
            * random.NextFloat(minTravelDistance, maxTravelDistance);

        // Update the ship and mansion models to hover above the ground
        ship.transform.position = SamplePoint(shipPoint);
        mansion.transform.position = SamplePoint(mansionPoint);
    }

    public void Generate(uint seed)
    {
        // Setup
        random = new(seed);

        // Cleanup
        if (Application.isEditor)
            EditorClean();

        // Generation steps
        GenerateGround();
        GenerateWorldPoints();
        GenerateRiver(shipPoint, mansionPoint);
        GenerateMountainRange();

        // Mesh handling
        MinimizeBaseMesh();
        GenerateMeshes();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WorldGenerator))]
public class MeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var target = this.target as WorldGenerator;

        GUILayout.Space(10);
        if(GUILayout.Button("Regenerate"))
        {
            target.Generate((uint)Environment.TickCount);
        }
    }
}


#endif