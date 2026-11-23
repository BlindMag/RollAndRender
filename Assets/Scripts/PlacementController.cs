using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PlacementController : MonoBehaviour
{
    [Header("Rendering")]
    [SerializeField] private Mesh sourceMesh;
    [SerializeField] private Material blockMaterial;

    private Mesh mesh;
    private Matrix4x4[] matrices = Array.Empty<Matrix4x4>();
    private Matrix4x4[] temp;
    private const int BatchSize = 1023;

    [Header("Grid")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float yLevel = 0f;
    [SerializeField] private bool snapMode = true;
    [SerializeField] private bool centerCells = true; // NEW: aligns with GridMesh
    public bool CenterCells => centerCells;
    public float GridSize => gridSize;

    [Header("Delete")]
    [SerializeField] private float deleteRadius = 0.45f;
    public float DeleteRadius => deleteRadius;

    private readonly Dictionary<Vector2Int, PlacedPiece> _pieces = new();
    public Dictionary<Vector2Int, PlacedPiece> pieces => _pieces;

    public struct PlacedPiece
    {
        public Vector3 pos;
        public Quaternion rot;
    }

    public float YLevel => yLevel;

    public void AddPiece(Vector2Int cell, PlacedPiece piece) => _pieces[cell] = piece;
    public bool RemovePiece(Vector2Int cell) => _pieces.Remove(cell);
    public bool HasPiece(Vector2Int cell) => _pieces.ContainsKey(cell);
    public PlacedPiece GetPiece(Vector2Int cell) => _pieces[cell];
    public void SetPiece(Vector2Int cell, PlacedPiece piece) => _pieces[cell] = piece;

    public Vector2Int GetNearestPiece(Vector2Int cell)
    {
        Vector3 center = CellToWorld(cell);
        float bestDist = deleteRadius * deleteRadius;
        Vector2Int bestKey = default;
        bool found = false;

        foreach (var kv in _pieces)
        {
            float d2 = (kv.Value.pos - center).sqrMagnitude;
            if (d2 < bestDist)
            {
                bestDist = d2;
                bestKey = kv.Key;
                found = true;
            }
        }

        return found ? bestKey : default;
    }

    private void Awake()
    {
        mesh = sourceMesh;
        if (!mesh || !blockMaterial)
        {
            Debug.LogError("PlacementController missing mesh or material");
            enabled = false;
            return;
        }

        blockMaterial.enableInstancing = true;
        RebuildMatrices();
    }

    // Grid helpers
    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 pos = snapMode ? Snap(world) : world;

        // REMOVE any offset here
        int x = Mathf.RoundToInt(pos.x / gridSize);
        int z = Mathf.RoundToInt(pos.z / gridSize);
        return new Vector2Int(x, z);
    }

    private Vector3 Snap(Vector3 pos)
    {
        // Just round, don't apply center offset
        return new Vector3(
            Mathf.Round(pos.x / gridSize) * gridSize,
            yLevel,
            Mathf.Round(pos.z / gridSize) * gridSize
        );
    }

    // CellToWorld keeps the center offset
    public Vector3 CellToWorld(Vector2Int cell)
    {
        float offset = centerCells ? gridSize * 0.5f : 0f;
        return new Vector3(cell.x * gridSize + offset, yLevel, cell.y * gridSize + offset);
    }

    // Rendering
    public void RebuildMatrices()
    {
        int count = _pieces.Count;
        if (matrices.Length != count) matrices = new Matrix4x4[count];

        int i = 0;
        foreach (var piece in _pieces.Values)
            matrices[i++] = Matrix4x4.TRS(piece.pos, piece.rot, Vector3.one);

        if (temp == null || temp.Length < Mathf.Min(count, BatchSize))
            temp = new Matrix4x4[Mathf.Max(1, Math.Min(count, BatchSize))];
    }

    private void Update() => Draw();

    private void Draw()
    {
        int total = matrices.Length;
        if (total == 0) return;

        int remaining = total, offset = 0;
        while (remaining > 0)
        {
            int batchCount = Math.Min(remaining, BatchSize);
            Array.Copy(matrices, offset, temp, 0, batchCount);

            Graphics.DrawMeshInstanced(mesh, 0, blockMaterial, temp, batchCount, null, ShadowCastingMode.On, true);

            remaining -= batchCount;
            offset += batchCount;
        }
    }
}
