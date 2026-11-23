using System;
using System.Collections.Generic;
using UnityEngine;

public class TileChunkRendering
{
    public Vector3Int coord;

    public bool needsRebuild = true;

    public Matrix4x4[] matrices = Array.Empty<Matrix4x4>();
    public Matrix4x4[] temp = Array.Empty<Matrix4x4>();    
    // incremental rebuild state
    public bool rebuilding;
    public int lx, ly, lz;                 // progress counters
    public List<Matrix4x4> buildList;      // working list
}
