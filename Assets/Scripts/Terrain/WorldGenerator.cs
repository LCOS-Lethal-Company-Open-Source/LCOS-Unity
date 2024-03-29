using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

using RandInst = Unity.Mathematics.Random;

public class WorldGenerator : MonoBehaviour
{
    // INSPECTOR VARIABLES //
    [Header("Noise")]
    public NoiseOctave[] octaves;

    [Header("Mesh Details")]
    public int width;
    public int height;
    public float spacing;
    public float baseNoiseScale;

    [Header("Prefabs")]
    public GameObject mountain;
    public GameObject[] treeVariants;
    public GameObject[] candyVariants;

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

    [Header("Miscellaneous")]
    public NoiseOctave[] treeOctaves;
    public NoiseOctave[] candyOctaves;
    public int treeCount;
    public int candyCount;

    // PRIVATE VARIABLES //
    Mesh baseMesh;
    Mesh groundMesh;

    Vector3[] waterVerts;
    List<int3> waterTris;

    Vector3[] groundVerts;
    List<int3> baseTris;

    Vector2 shipPoint;
    Vector2 mansionPoint;

    Vector2 Size => new(width, height);

    RandInst random;
    int2 noiseOff;

    public static int[] FormatTriangles(List<int3> tris)
    {
        var result = new int[tris.Count * 3];

        for (int i = 0; i < tris.Count; i++)
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
        waterVerts = new Vector3[width * height];
        baseTris = new();

        noiseOff = random.NextInt2(0, 10000);

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                var basePositionXZ = GetWorldPosition(i, j);
                var basePosition = basePositionXZ.InvXZ();
                var totalNoise = octaves.Sample(baseNoiseScale, basePositionXZ, noiseOff);

                var idx = Index(i, j);
                waterVerts[idx] = basePosition + Vector3.up * totalNoise;

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
        groundVerts = waterVerts.ToArray();
        waterTris = baseTris.ToList();
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

    public int3 GetTriangle(Vector2 xz)
    {
        var (minx, miny) = GetVertexIndex(xz, out var left);

        var maxx = minx + 1;
        var maxy = miny + 1;

        // Get the triangle this vertex is a part of
        if (left)
        {
            return math.int3(
                Index(minx, miny),
                Index(maxx, miny),
                Index(minx, maxy)
            );
        }
        else
        {
            return math.int3(
                Index(minx, miny),
                Index(maxx, miny),
                Index(maxx, maxy)
            );
        }
    }

    public Vector3 GetBarycentric(Vector2 xz, int3 tri)
    {
        // Use barycentric interpolation to calculate the
        // point's position on the mesh
        return MathUtils.Barycentric(
            waterVerts[tri.x].XZ(),
            waterVerts[tri.y].XZ(),
            waterVerts[tri.z].XZ(),
            xz
        );
    }

    public Vector3 SampleBarycentric(Vector3[] points, Vector3 bary, int3 tri)
    {
        return bary.x * points[tri.x]
             + bary.y * points[tri.y]
             + bary.z * points[tri.z];
    }

    public Vector3 SamplePointFrom(Vector3[] points, Vector2 xz)
    {
        // Get the vertex indices for this position
        var tri = GetTriangle(xz);
        var bary = GetBarycentric(xz, tri);
        return SampleBarycentric(points, bary, tri);
    }

    public Vector3 SampleGroundPoint(Vector2 xz)
        => SamplePointFrom(groundVerts, xz);

    public void SampleGroundAndBasePoint(Vector2 xz, out Vector3 ground, out Vector3 @base)
    {
        // Get the vertex indices for this position
        var tri = GetTriangle(xz);
        var bary = GetBarycentric(xz, tri);

        ground = SampleBarycentric(groundVerts, bary, tri);
        @base = SampleBarycentric(waterVerts, bary, tri);
    }

    public void CreateDivet(Vector2 xz, float size, float depth)
    {
        // Calculate index values
        var (cx, cy) = GetVertexIndex(xz, out _);

        int idxWidth = Mathf.CeilToInt(size / spacing);

        // Loop through relevant vertices
        for (int x = Mathf.Max(cx - idxWidth, 0); x < Mathf.Min(cx + idxWidth, width); x++)
        {
            for (int y = Mathf.Max(cy - idxWidth, 0); y < Mathf.Min(cy + idxWidth, height); y++)
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

        // Randomly place a set of mountains
        var count = random.NextInt(minMountains, maxMountains);
        for (int i = 0; i < count; i++)
        {
            var pos = mansionPoint
                    + fwd * (mountainDistance + random.NextFloat(-mountainRange, mountainRange))
                    + per * (random.NextFloat(-mountainRange, mountainRange));

            var realpos = SampleGroundPoint(pos);

            GameObject.Instantiate(mountain, realpos, mountain.transform.rotation);
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

            if (waterVerts[index].y - waterOffset < groundVerts[index].y)
                return false;

            return true;
        }

        for (int i = 0; i < width * height; i++)
        {
            if (Valid(i))
                continue;

            bool hasValidNeighbor = false;

            for (int dx = -1; dx <= 1 && !hasValidNeighbor; dx++)
            {
                for (int dy = -1; dy <= 1 && !hasValidNeighbor; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    if (Valid(i + Index(dx, dy)))
                    {
                        hasValidNeighbor = true;
                    }
                }
            }

            if (!hasValidNeighbor)
            {
                removed.Add(i);
            }
        }

        // Rebuild the vertex list without these vertices
        var newBaseVerts = new Vector3[waterVerts.Length - removed.Count];
        var newIndex = 0;

        for (int i = 0; i < waterVerts.Length; i++)
        {
            if (removed.Contains(i))
                continue;

            newBaseVerts[newIndex] = waterVerts[i];

            newIndex++;
        }

        waterVerts = newBaseVerts;

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

        baseMesh.vertices = waterVerts;
        baseMesh.triangles = FormatTriangles(baseTris);

        baseMesh.RecalculateBounds();

        water.transform.position = new Vector3(0, -waterOffset);

        // Create ground mesh
        groundMesh = new Mesh();
        ground.mesh = groundMesh;

        groundMesh.vertices = groundVerts;
        groundMesh.triangles = FormatTriangles(waterTris);

        groundMesh.RecalculateBounds();
    }

    public void GenerateWorldPoints()
    {
        // Place the ship and mansion in the world
        shipPoint = Vector2.zero;
        mansionPoint = random.NextFloat2Direction()
            * random.NextFloat(minTravelDistance, maxTravelDistance);

        // Update the ship and mansion models to hover above the ground
        ship.transform.position = SampleGroundPoint(shipPoint);
        mansion.transform.position = SampleGroundPoint(mansionPoint);
    }

    public void GenerateDecorations(GameObject[] variants, NoiseOctave[] octaves, int count)
    {
        int placed = 0;

        while(placed < count)
        {
            // Generate a random point within the bounds of the world
            var pos = random.NextFloat2(
                GetWorldPosition(1, 1),
                GetWorldPosition(width-1, height-1)
            );

            // Check if the position is in a divet and do not place if it is
            SampleGroundAndBasePoint(pos, out var groundPos, out var basePos);

            if(groundPos.y <= basePos.y - waterOffset)
            {
                continue;
            }

            // If the random chance says so, place the thingy.
            if(random.NextFloat(-0.5f, 0.5f) > octaves.Sample(baseNoiseScale, pos, noiseOff))
            {
                // Generate a random variant in this place.
                var variant = random.NextInt(variants.Length);
                GameObject.Instantiate(variants[variant], groundPos, variants[variant].transform.rotation);

                placed++;
            }
        }
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
        GenerateDecorations(treeVariants, treeOctaves, treeCount);
        GenerateDecorations(candyVariants, candyOctaves, candyCount);

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