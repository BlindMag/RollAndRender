using UnityEngine;

public class TileTerrain
{
    private readonly TileConfig cfg;

    public TileTerrain(TileConfig config) => cfg = config;

    public bool BaseSolid(Vector3Int cell)
        => cell.y <= cfg.surfaceY && cell.y >= (cfg.surfaceY - cfg.depthBlocks);
}
