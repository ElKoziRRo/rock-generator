using System;
using System.Runtime.CompilerServices;
using MeshDecimator;
using MeshDecimator.Algorithms;
using MeshDecimator.Math;
using RockGen.Primitive;

namespace RockGen
{
public class RockGenerator
{
    public RockGenerationSettings Settings
    {
        get => settings;
        set
        {
            if (settings.StockDensity != value.StockDensity)
                UpdateStockMesh(value);

            if (!settings.GridSettings.Equals(value.GridSettings))
                MakeGrid(value.GridSettings);

            settings = value;
        }
    }

    public Mesh LatestMesh { get; private set; }

    // For debugging
    public event Action<Vector3d, Vector3d, Vector3d> foundNearest;

    public VoronoiGrid Grid { get; private set; }

    readonly SphereCubeGenerator sphereCubeGenerator;
    RockGenerationSettings       settings;
    Mesh                         stockMesh;

    public RockGenerator()
    {
        sphereCubeGenerator = new SphereCubeGenerator {
            Radius = .5f
        };
    }

    void MakeGrid(VoronoiGridSettings gridSettings)
    {
        Grid = new VoronoiGrid(gridSettings);
    }

    public void UpdateStockMesh(RockGenerationSettings settings)
    {
        sphereCubeGenerator.NumSubDivX = (int) Math.Round(settings.StockDensity * settings.Scale.X);
        sphereCubeGenerator.NumSubDivY = (int) Math.Round(settings.StockDensity * settings.Scale.Y);
        sphereCubeGenerator.NumSubDivZ = (int) Math.Round(settings.StockDensity * settings.Scale.Z);

        stockMesh = sphereCubeGenerator.MakeSphere();
    }

    public Mesh MakeRock()
    {
        var vertices = new Vector3d[stockMesh.VertexCount];

        var distort = Settings.Distortion;

        for (var i = 0; i < vertices.Length; i++)
        {
            var worldPos    = Transform(Settings.Transform, stockMesh.Vertices[i]);
            var worldNormal = TransformDir(Settings.Transform, stockMesh.Normals[i]);

            var (nearest, nearestDS) = Grid.Nearest(worldPos * Settings.PatternSize);

            var worldResult = worldPos + worldNormal * ((nearestDS - .5) * distort);

            // Keep the scale and rotation
            vertices[i] = worldResult - new Vector3d(Settings.Transform.M14,
                                                     Settings.Transform.M24,
                                                     Settings.Transform.M34);

            OnFoundNearest(worldResult, worldNormal, nearest);
        }

        var mesh = new Mesh(
            vertices,
            stockMesh.Indices
        );

        var simplifier = new FastQuadricMeshSimplification();
        simplifier.Initialize(mesh);
        simplifier.DecimateMesh(Settings.TargetTriangleCount);

        mesh = simplifier.ToMesh();

        mesh.RecalculateNormals();

        // CalcUV(mesh);

        LatestMesh = mesh;
        return mesh;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Vector3d Transform(Matrix4x4 m, Vector3d v)
    {
        return new Vector3d(
            m.M11 * v.x + m.M12 * v.y + m.M13 * v.z + m.M14,
            m.M21 * v.x + m.M22 * v.y + m.M23 * v.z + m.M24,
            m.M31 * v.x + m.M32 * v.y + m.M33 * v.z + m.M34
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Vector3d TransformDir(Matrix4x4 m, Vector3d v)
    {
        return new Vector3d(
            m.M11 * v.x + m.M12 * v.y + m.M13 * v.z,
            m.M21 * v.x + m.M22 * v.y + m.M23 * v.z,
            m.M31 * v.x + m.M32 * v.y + m.M33 * v.z
        ).Normalized;
    }

    // void CalcUV(Mesh mesh)
    // {
    //     var vertices = mesh.vertices;
    //     var normals  = mesh.normals;
    //     var uv       = new Vector2[vertices.Length];
    //
    //     var center         = transform.position;
    //     var corners        = new Vector3[8];
    //     var cornersForward = new Vector3[8];
    //     grid.GetCellCorners(center, corners);
    //
    //     for (var i = 0; i < corners.Length; i++)
    //     {
    //         cornersForward[i] = (center - corners[i]).normalized;
    //     }
    //
    //     for (int i = 0; i < vertices.Length; i++)
    //     {
    //         var worldPos    = transform.TransformPoint(vertices[i]);
    //         var worldNormal = transform.TransformDirection(normals[i]);
    //
    //         var smallestD    = float.PositiveInfinity;
    //         var nearestIndex = 0;
    //
    //         for (var j = 0; j < cornersForward.Length; j++)
    //         {
    //             float d = Dot(cornersForward[j], worldNormal);
    //
    //             if (d < smallestD)
    //             {
    //                 smallestD    = d;
    //                 nearestIndex = j;
    //             }
    //         }
    //
    //         var normal    = cornersForward[nearestIndex];
    //         var projected = ProjectPointOnPlane(worldPos, corners[nearestIndex], normal);
    //
    //         var tangent  = Cross(normal,  up);
    //         var binormal = Cross(tangent, normal);
    //         tangent = Cross(normal, binormal);
    //
    //         Debug.DrawRay(worldPos, tangent * .1f,  Color.red);
    //         Debug.DrawRay(worldPos, binormal * .1f, Color.green);
    //
    //         Debug.DrawLine(worldPos, corners[nearestIndex], Color.gray);
    //
    //         uv[i] = new Vector2(
    //             Dot(projected, tangent),
    //             Dot(projected, binormal)
    //         );
    //     }
    //
    //     mesh.uv = uv;
    // }
    //
    // static Vector3 ProjectPointOnPlane(Vector3 point, Vector3 planeOrig, Vector3 planeNormal)
    // {
    //     var v    = point - planeOrig;
    //     var dist = Dot(v, planeNormal);
    //     return point - dist * planeNormal;
    // }

    void OnFoundNearest(Vector3d vertex, Vector3d normal, Vector3d nearest)
    {
        foundNearest?.Invoke(vertex, normal, nearest);
    }
}
}
