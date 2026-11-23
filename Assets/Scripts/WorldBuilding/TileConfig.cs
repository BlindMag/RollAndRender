using UnityEngine;

[System.Serializable]
public class TileConfig
{
    public int chunkSize = 16;
    public int boardChunksX = 25;
    public int boardChunksZ = 25;
    public int buildHeightBlocks = 64;
    public bool centerBoardOnOrigin = true;

    public int surfaceY = 0;
    public int depthBlocks = 64;

    public float gridSize = 1f; // 1 cell = 5ft conceptually

    public void GetBoardChunkBounds(out int minCX, out int maxCX, out int minCZ, out int maxCZ)
    {
        if (centerBoardOnOrigin)
        {
            int halfX = boardChunksX / 2;
            int halfZ = boardChunksZ / 2;

            minCX = -halfX;
            maxCX = minCX + boardChunksX - 1;

            minCZ = -halfZ;
            maxCZ = minCZ + boardChunksZ - 1;
        }
        else
        {
            minCX = 0; maxCX = boardChunksX - 1;
            minCZ = 0; maxCZ = boardChunksZ - 1;
        }
    }

    public void GetBoardCellBounds(out int minX, out int maxX, out int minZ, out int maxZ)
    {
        GetBoardChunkBounds(out int minCX, out int maxCX, out int minCZ, out int maxCZ);
        minX = minCX * chunkSize;
        maxX = (maxCX * chunkSize) + (chunkSize - 1);
        minZ = minCZ * chunkSize;
        maxZ = (maxCZ * chunkSize) + (chunkSize - 1);
    }

    public int MinChunkY()
        => Mathf.FloorToInt((float)(surfaceY - depthBlocks) / chunkSize);

    public int MaxChunkY()
        => Mathf.FloorToInt((float)(surfaceY + buildHeightBlocks) / chunkSize);

    public bool InBounds(Vector3Int cell)
    {
        if (cell.y > surfaceY + buildHeightBlocks || cell.y < surfaceY - depthBlocks) return false;

        GetBoardCellBounds(out int minX, out int maxX, out int minZ, out int maxZ);
        return cell.x >= minX && cell.x <= maxX && cell.z >= minZ && cell.z <= maxZ;
    }

    public Vector3Int CellToChunk(Vector3Int cell)
    {
        return new Vector3Int(
            Mathf.FloorToInt((float)cell.x / chunkSize),
            Mathf.FloorToInt((float)cell.y / chunkSize),
            Mathf.FloorToInt((float)cell.z / chunkSize)
        );
    }

    public Vector3Int ChunkToCellOrigin(Vector3Int chunkCoord)
        => chunkCoord * chunkSize;

    public Vector3Int WorldToCell(Vector3 world, int layerY)
    {
        int x = Mathf.RoundToInt(world.x / gridSize);
        int z = Mathf.RoundToInt(world.z / gridSize);
        return new Vector3Int(x, layerY, z);
    }

    public Vector3 CellToWorld(Vector3Int cell)
    {
        float offset = gridSize * 0.5f;
        return new Vector3(
            cell.x * gridSize + offset,
            cell.y * gridSize,
            cell.z * gridSize + offset
        );
    }
}