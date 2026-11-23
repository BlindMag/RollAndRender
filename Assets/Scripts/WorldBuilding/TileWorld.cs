using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System;

public class TileWorld : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private TileConfig config = new();

    [Header("Rendering")]
    //Drop in custom block mesh from the imported .fbx file (material must be extracted in order to use it)
    [SerializeField] private Mesh sourceMesh;
    [SerializeField] private Material blockMaterial;
    [SerializeField] private ShadowCastingMode shadows = ShadowCastingMode.On;
    [SerializeField] private bool receiveShadows = true;

    [Header("Performance")]
    //What we rendersess
    [SerializeField] private int renderDistanceChunks = 10;
    //What we preps
    [SerializeField] int buildPaddingChunks = 2;
    //How many cells we let ONE chunk chew through per frame while rebuilding (per chunk, so this can multiply fast)
    [SerializeField] private int rebuildCellBudgetPerFrame = 800; // tweak: 200..2000

    // --- Public API used by Kernel ---
    public bool InBounds(Vector3Int cell) => config.InBounds(cell);
    public Vector3Int WorldToCell(Vector3 w, int y) => config.WorldToCell(w, y);
    public Vector3 CellToWorld(Vector3Int cell) => config.CellToWorld(cell);
    public float GridSize => config.gridSize;

    private const int BatchSize = 1023;

    private Mesh mesh;
    private TileTerrain terrain;

    //Overrides --
    //Rotation
    private struct PlacedOverride
    {
        public byte id;
        public byte rot; // 0..3
    }
    //Player edit - put this block at this cell - it does not matter what the generator says   
    private readonly Dictionary<Vector3Int, PlacedOverride> playerPlacedBlocks = new();
    //Mark the spot empty - Never actually gets 'deleted' it gets marked as air here
    private readonly HashSet<Vector3Int> playerClearedCells = new();

    private readonly Dictionary<Vector3Int, TileChunkRendering> chunks = new();
    public bool IsSolid(Vector3Int cell) => TryGetBlock(cell, out _);

    public Vector3Int WorldToCellFloor(Vector3 world)
    {
        float g = config.gridSize;
        return new Vector3Int(
            Mathf.FloorToInt(world.x / g),
            Mathf.FloorToInt(world.y / g),
            Mathf.FloorToInt(world.z / g)
        );
    }
    void Awake()
    {
        mesh = sourceMesh;
        //If you didn't put in the material or mesh, return and give us a log message
        if (!mesh || !blockMaterial)
        {
            Debug.LogError("[World] Missing mesh or material.");
            return;
        }

        blockMaterial.enableInstancing = true;
        terrain = new TileTerrain(config);

        //Create chunk shells for the whole board up front (convenient, but lots of chunks live forever)
        ForEachChunkCoordInBoard(cc => GetOrCreateChunk(cc).needsRebuild = true);
    }

    //Each frame: find camera chunk -> loop the build ring -> rebuild a bit -> draw
    void Update()
    {
        var cam = Camera.main;
        if (!cam) return;

        Vector3Int camCell = config.WorldToCell(cam.transform.position, config.surfaceY);
        Vector3Int camChunk = config.CellToChunk(camCell);

        int drawR = renderDistanceChunks;
        int buildR = renderDistanceChunks + buildPaddingChunks;

        config.GetBoardChunkBounds(out int minCX, out int maxCX, out int minCZ, out int maxCZ);
        int minCY = config.MinChunkY();
        int maxCY = config.MaxChunkY();

        int buildFromX = Mathf.Max(minCX, camChunk.x - buildR);
        int buildToX = Mathf.Min(maxCX, camChunk.x + buildR);
        int buildFromZ = Mathf.Max(minCZ, camChunk.z - buildR);
        int buildToZ = Mathf.Min(maxCZ, camChunk.z + buildR);

        for (int cz = buildFromZ; cz <= buildToZ; cz++)
            for (int cx = buildFromX; cx <= buildToX; cx++)
                for (int cy = minCY; cy <= maxCY; cy++)
                {
                    var cc = new Vector3Int(cx, cy, cz);
                    var ch = GetOrCreateChunk(cc);

                    if (ch.needsRebuild && !ch.rebuilding)
                        BeginRebuildChunk(ch);

                    //Do a little bit of work this frame (NOTE: budget is per chunk, not global per frame)
                    if (ch.rebuilding)
                        StepRebuildChunk(ch, rebuildCellBudgetPerFrame);

                    //Draw whatever is currently available (old matrices while rebuilding, new after swap)
                    int dx = Mathf.Abs(cx - camChunk.x);
                    int dz = Mathf.Abs(cz - camChunk.z);

                    if (dx <= drawR && dz <= drawR)
                        DrawChunk(ch);
                }
    }

    public void PlaceBlock(Vector3Int cell, byte id, Quaternion rot)
    {
        if (!config.InBounds(cell)) return;

        //Clears the requirement for the cell to be air
        playerClearedCells.Remove(cell);

        byte r = (byte)(Mathf.RoundToInt(rot.eulerAngles.y / 90f) & 3);
        //Overwrites the previous state of this cell to clean up memory and set it to the current cell information
        playerPlacedBlocks[cell] = new PlacedOverride { id = id, rot = r };

        MarkRebuildAround(cell);
        //Before top block bias addition

        //if (!config.InBounds(cell)) return;

        //playerClearedCells.Remove(cell);
        //overrideBlocks[cell] = id;
        //MarkRebuildAround(cell);
    }
    public void DeleteBlock(Vector3Int cell)
    {
        if (!config.InBounds(cell)) return;
        //Removes the block
        playerPlacedBlocks.Remove(cell);

        if (terrain.BaseSolid(cell))
            //Adds the requirement for the cell to be 'air'
            playerClearedCells.Add(cell);

        MarkRebuildAround(cell);
    }

    // --- Chunk rebuild/draw --- Is this air? If so false it!
    bool TryGetBlock(Vector3Int cell, out byte rotIndex)
    {
        rotIndex = 0;

        if (!config.InBounds(cell)) return false;
        //Was this spot deleted? Place air
        if (playerClearedCells.Contains(cell)) return false;
        //If there's a block placed, what's the rotation?
        if (playerPlacedBlocks.TryGetValue(cell, out var o))
        {
            rotIndex = o.rot;
            return true;
        }

        //Base terrain (no rotation)
        return terrain.BaseSolid(cell);
    }
    //Check if this block has ANY exposed face. If its fully buried, skip it.
    bool IsExposed(Vector3Int cell)
    {
        Vector3Int[] dirs =
        {
            new( 1, 0, 0), new(-1, 0, 0),
            new( 0, 1, 0), new( 0,-1, 0),
            new( 0, 0, 1), new( 0, 0,-1),
        };

        foreach (var d in dirs)
            if (!TryGetBlock(cell + d, out _))
                return true;

        return false;
    }
    //Incremental rebuilding steps
    void BeginRebuildChunk(TileChunkRendering ch)
    {
        ch.needsRebuild = false;
        ch.rebuilding = true;

        ch.lx = ch.ly = ch.lz = 0;
        ch.buildList ??= new List<Matrix4x4>(512);
        ch.buildList.Clear();
    }

    void StepRebuildChunk(TileChunkRendering ch, int cellBudget)
    {
        if (!ch.rebuilding) return;

        Vector3Int origin = config.ChunkToCellOrigin(ch.coord);
        int size = config.chunkSize;

        int processed = 0;

        for (; ch.ly < size; ch.ly++)
        {
            for (; ch.lz < size; ch.lz++)
            {
                for (; ch.lx < size; ch.lx++)
                {
                    Vector3Int cell = new(origin.x + ch.lx, origin.y + ch.ly, origin.z + ch.lz);

                    if (TryGetBlock(cell, out byte rotIdx) && IsExposed(cell))
                    {
                        Quaternion q = Quaternion.Euler(0f, rotIdx * 90f, 0f);
                        ch.buildList.Add(Matrix4x4.TRS(config.CellToWorld(cell), q, Vector3.one));
                    }

                    processed++;
                    if (processed >= cellBudget)
                    {
                        ch.lx++; //resume next cell (so we dont redo the same spot forever and ever amen)
                        return;
                    }
                }
                ch.lx = 0;
            }
            ch.lz = 0;
        }

        //Finished: swap in one go (build into buildList, then replace matrices at the end)
        ch.matrices = ch.buildList.ToArray();
        int tempSize = Mathf.Max(1, Mathf.Min(ch.matrices.Length, BatchSize));
        if (ch.temp.Length != tempSize) ch.temp = new Matrix4x4[tempSize];

        ch.rebuilding = false;
    }

    //Draw this chunk with GPU instancing in 'batches'. Unity limits how many instances you can draw per once call - Usually it's 1023 per call that can be drawn
    void DrawChunk(TileChunkRendering ch)
    {
        int total = ch.matrices.Length;
        if (total == 0) return;

        int remaining = total, offset = 0;
        while (remaining > 0)
        {
            int batchCount = Mathf.Min(remaining, BatchSize);
            Array.Copy(ch.matrices, offset, ch.temp, 0, batchCount);
            Graphics.DrawMeshInstanced(mesh, 0, blockMaterial, ch.temp, batchCount, null, shadows, receiveShadows);
            remaining -= batchCount;
            offset += batchCount;
        }
    }

    void MarkRebuildAround(Vector3Int cell)
    {
        //Convert this cell to a chunk -> chunk = floor(cell / chunkSize)
        var cc = config.CellToChunk(cell);

        GetOrCreateChunk(cc).needsRebuild = true;

        Vector3Int[] neighbors =
        {
            new( 1, 0, 0), new(-1, 0, 0),
            new( 0, 1, 0), new( 0,-1, 0),
            new( 0, 0, 1), new( 0, 0,-1),
        };
        //Neighbor chunk coordinate (find which chunk the neighbor cell is in) (cell+d) - If it's different than the current cell, tag for rebuild too - By doing this we prevent missing faces, remove faces that should be hidden, or prevent seeing through seams
        foreach (var d in neighbors)
        {
            var ncc = config.CellToChunk(cell + d);
            if (ncc != cc) GetOrCreateChunk(ncc).needsRebuild = true;
        }
    }

    // --- Iteration + chunk storage ---
    TileChunkRendering GetOrCreateChunk(Vector3Int coord)
    {
        //If a chunk doesn't exist yet, create a new one and call for rebuild
        if (!chunks.TryGetValue(coord, out var ch))
        {
            ch = new TileChunkRendering
            {
                coord = coord,
                needsRebuild = true
            };
            chunks.Add(coord, ch);
        }
        //Return the chunk (new or existing) (chunks.Add ->Dictionary<Vector3Int, TileChunkRendering>)
        return ch;
    }
    //Loop assistant to be able to run function on every chunk
    void ForEachChunkCoordInBoard(Action<Vector3Int> fn)
    {
        config.GetBoardChunkBounds(out int minCX, out int maxCX, out int minCZ, out int maxCZ);
        int minCY = config.MinChunkY();
        int maxCY = config.MaxChunkY();
        //Find every chunk on the board on the Z, X, Y board and run the function on every chunk in min. max. board bounds
        for (int cz = minCZ; cz <= maxCZ; cz++)
            for (int cx = minCX; cx <= maxCX; cx++)
                for (int cy = minCY; cy <= maxCY; cy++)
                    fn(new Vector3Int(cx, cy, cz));
    }
}
