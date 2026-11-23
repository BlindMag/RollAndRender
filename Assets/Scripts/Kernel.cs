using UnityEngine;
using UnityEngine.InputSystem;

//Kernel uses DDA - Digital Differential Analyzer which is an algorythm that walks through all the 3D grid cells along the ray path one at a time until we find which block hits first before the max distance
//
//Why use DDA?
//This gets us away from physics/colliders and deals directly with voxels/grid data which frees up memory and cpu. Staying away from colliders scales better as blocks grow, preventing stuttering, speeds up load times, and prevents us from having to create/manage lots of physics data.

public class Kernel : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TileWorld world;
    [SerializeField] private GameObject ghostPrefab;

    [Header("Build")]
    [SerializeField] private float maxPickDistance = 200f;

    private GameObject ghostInstance;

    private const int MaxRotIndex = 3;
    private int rotationIndex = 0; //0..3

    //Variables for bias of top block placement
    private Vector3Int lastPlaceCell;
    private Vector3Int candidateCell;
    private int candidateFrames;

    //Cache camera once (Camera.main is a tag lookup, unity will do work to find it)
    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (!world || !ghostPrefab) return;

        //If the camera gets swapped/reloaded, refresh it (dont crash just because unity did unity things)
        if (!EnsureCamera()) return;

        EnsureGhost();

        HandleRotationInput();

        //Pick a block using DDA (function returns the face direction (face normal) and the cell that we hit)
        if (!TryPickBlock(out float hitT, out float yInCell, out Vector3Int hitCell, out Vector3Int hitNormal))
        {
            ghostInstance.SetActive(false);
            return;
        }

        //Direction of the Camera
        Vector3 dir = cam.transform.forward;

        //Raw normal BEFORE any bias so we still know what face DDA actually picked
        Vector3Int rawNormal = hitNormal;

        //--- REVAMP: Decide TOP/BOTTOM from hit location in the cell, not camera pitch ---
        //If we hit exactly on a boundary, yInCell can be ~0 even when it's the top edge. Treat that as top.
        if (yInCell < 0.0001f) yInCell = 1f;

        const float TopEdge = 0.60f;     //lower = easier to snap to top - This has been such a big issue
        const float BottomEdge = 0.20f;  //higher = easier to snap to bottom

        Vector3Int preferredNormal = rawNormal;

        //If DDA says "side", but the hit point is near top/bottom of the cell, treat it as top/bottom
        if (rawNormal.y == 0)
        {
            if (yInCell > TopEdge) preferredNormal = Vector3Int.up;
            else if (yInCell < BottomEdge) preferredNormal = Vector3Int.down;
        }

        //Pick the cell next to the face you hit (using preferred normal)
        Vector3Int surfacePlaceCell = hitCell + preferredNormal;

        //Default to normal placement
        Vector3Int placeCell = surfacePlaceCell;

        //--- Hole priority: only when looking generally DOWN (diagonal ok), and only if we have air to place into ---
        if (CanHoleSnap(dir, surfacePlaceCell))
        {
            //Try to snap into a hole from the air cell we’d normally place into
            placeCell = PlaceTest(surfacePlaceCell);
        }

        //Ghost ACTIVATE!!
        if (!CanPlace(placeCell))
        {
            ghostInstance.SetActive(false);
            return;
        }

        ShowGhost(placeCell);

        HandleMouseActions(placeCell, hitCell);

        //Last cell cursor is over
        lastPlaceCell = placeCell;

        //If cell changed make a new possbile current(candidate) cell we might accept and if we do have a new candidate, reset the counter, otherwise we count another candidateFrames which gives us just a moment to make sure there's not jitter & also to accept the current cell that we hover over
        UpdateCandidate(placeCell);
    }

    // ---------------- Helpers ----------------

    private bool EnsureCamera()
    {
        if (!cam) cam = Camera.main;
        return cam != null;
    }

    private void EnsureGhost()
    {
        if (!ghostInstance) ghostInstance = Instantiate(ghostPrefab);
    }

    private void HandleRotationInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.qKey.wasPressedThisFrame) rotationIndex = RotLeft(rotationIndex);
        if (Keyboard.current.eKey.wasPressedThisFrame) rotationIndex = RotRight(rotationIndex);
    }

    private static int RotLeft(int i)
    {
        //0 -> 3, otherwise i-1
        if (i == 0) return MaxRotIndex;
        return i - 1;
    }

    private static int RotRight(int i)
    {
        //3 -> 0, otherwise i+1
        if (i == MaxRotIndex) return 0;
        return i + 1;
    }

    private bool CanPlace(Vector3Int cell)
    {
        //Simple gate: if it's outside the world or already solid, dont bother.
        if (!world.InBounds(cell)) return false;
        if (world.IsSolid(cell)) return false;
        return true;
    }

    private bool CanHoleSnap(Vector3 camDir, Vector3Int surfacePlaceCell)
    {
        //Only when looking generally DOWN (diagonal ok), and only if we have air to place into
        const float DownGate = -0.10f; //allow diagonals into holes
        if (camDir.y >= DownGate) return false;

        if (!world.InBounds(surfacePlaceCell)) return false;
        if (world.IsSolid(surfacePlaceCell)) return false;

        return true;
    }

    private void ShowGhost(Vector3Int placeCell)
    {
        ghostInstance.SetActive(true);

        //Ghost is now on the placeCell, rotate it accordingly//Use the same rotation everywhere
        Quaternion rot = Quaternion.Euler(0f, rotationIndex * 90f, 0f);
        ghostInstance.transform.SetPositionAndRotation(world.CellToWorld(placeCell), rot);
    }

    private void HandleMouseActions(Vector3Int placeCell, Vector3Int hitCell)
    {
        //Only do this if the key is currenly being pressed - prevents sticky issue
        if (Mouse.current == null) return;

        Quaternion rot = Quaternion.Euler(0f, rotationIndex * 90f, 0f);

        if (Mouse.current.leftButton.wasPressedThisFrame)
            world.PlaceBlock(placeCell, 1, rot);

        if (Mouse.current.rightButton.wasPressedThisFrame)
            world.DeleteBlock(hitCell);
    }

    private void UpdateCandidate(Vector3Int placeCell)
    {
        if (placeCell != candidateCell)
        {
            candidateCell = placeCell;
            candidateFrames = 0;
        }
        candidateFrames++;
    }

    // ---------------- Hole helpers ----------------

    private Vector3Int PlaceTest(Vector3Int placeCell)
    {
        if (!world.InBounds(placeCell))
        {
            return placeCell;
        }

        Vector3Int[] seeds =
        {
            placeCell,
            placeCell + Vector3Int.right,
            placeCell + Vector3Int.left,
            placeCell + Vector3Int.forward,
            placeCell + Vector3Int.back,
            placeCell + Vector3Int.up,     //helps at rim/edge
            placeCell + Vector3Int.down    //helps when you’re slightly above the hole
        };

        bool found = false;
        Vector3Int best = default;

        foreach (var s in seeds)
        {
            if (TryFindHole(s, out var fill))
            {
                if (!found || fill.y < best.y) //lowest wins
                {
                    best = fill;
                    found = true;
                }
            }
        }

        //ok so this was the part that was lying to us before:
        //if the normal placement cell is fine (in bounds + air), we keep it.
        //we only "hole snap" when the normal placement is NOT valid (aka you're aiming into a void/edge situation).
        if (world.InBounds(placeCell) && !world.IsSolid(placeCell))
            return placeCell;

        return found ? best : placeCell;
    }

    //Hole bias
    private bool TryFindHole(Vector3Int aimedCell, out Vector3Int fillCell)
    {
        fillCell = default;

        //If the aimedCell is a solid or is out of bounds, determine that we should stop looking
        if (!world.InBounds(aimedCell)) return false;
        if (world.IsSolid(aimedCell)) return false;

        Vector3Int below = aimedCell + Vector3Int.down;

        //Check through the area beneth around this area
        fillCell = aimedCell;
        while (world.InBounds(aimedCell) && world.InBounds(below) && !world.IsSolid(aimedCell))
        {
            //Cell below what we're looping through
            if (world.IsSolid(below))
            {
                fillCell = aimedCell;
                return true;
            }

            aimedCell = below;
            below = aimedCell + Vector3Int.down;
        }

        return false;
    }

    // ---------------- Picking (DDA) ----------------

    //Looks for where our mouse is point at and returns the hit cell and hit normal to the caller
    private bool TryPickBlock(out float hitT, out float yInCell, out Vector3Int hitCell, out Vector3Int hitNormal)
    {
        hitCell = default;
        hitNormal = default;
        hitT = 0f;
        yInCell = 0f;

        if (!cam || Mouse.current == null) return false;

        //This makes a ray from mouse pointer to the world
        Vector2 mp = Mouse.current.position.ReadValue();

        //If the mouse coords aren't inside the Game view pixel range, don't pick.
        //(This happens in-editor when the Game view isn't the focused window / scaling mismatch.)
        if (!MouseInGameView(mp)) return false;

        Ray ray = cam.ScreenPointToRay(mp);

        //Run the DDA raycast through grid cells to return if we hit a block and what the hitCell and hit Normal info is
        if (!BlockDDA.Raycast(
            ray,
            maxPickDistance,
            world.GridSize,
            world.WorldToCellFloor,
            world.InBounds,
            world.IsSolid,
            out hitT,
            out hitCell,
            out hitNormal
        ))
            return false;

        Vector3 hitPointWS = ray.origin + ray.direction * (hitT * world.GridSize);
        float cellY = hitPointWS.y / world.GridSize;
        yInCell = cellY - Mathf.Floor(cellY);

        //If we hit exactly on a cell boundary, treat it as "top of the cell"
        if (yInCell < 0.0001f)
            yInCell = 1f;

        return true;
    }

    private static bool MouseInGameView(Vector2 mp)
    {
        if (mp.x < 0 || mp.x > Screen.width) return false;
        if (mp.y < 0 || mp.y > Screen.height) return false;
        return true;
    }

    //-------------------------------------------------------
    //DDA / Fast voxel traversal (Amanatides & Woo style)
    //https://m4xc.dev/articles/amanatides-and-woo/
    //-------------------------------------------------------
    private static class BlockDDA
    {
        //Basically laser point through blocks and tell us if we hit - Which grid line do I hit next? -> step into that neighbor cell -> repeat.
        public static bool Raycast(
            Ray ray,
            float maxDistance,
            float cellSize,
            System.Func<Vector3, Vector3Int> worldToCellFloor,
            System.Func<Vector3Int, bool> inBounds,
            System.Func<Vector3Int, bool> isSolid,
            out float hitT,
            out Vector3Int hitCell,
            out Vector3Int hitNormal)
        {
            hitT = 0f;
            hitCell = default;
            hitNormal = default;

            Vector3 dir = ray.direction;
            //If the magnitude of the direction we are going to is less than 1e-8f return false
            if (dir.sqrMagnitude < 1e-8f) return false;
            dir.Normalize();

            //Convert to "cell space" (where each cell is size 1)
            Vector3 originCS = ray.origin / cellSize;
            Vector3 dirCS = dir;

            Vector3Int cell = worldToCellFloor(ray.origin);

            //If you start inside a solid cell, treat it as a hit
            if (inBounds(cell) && isSolid(cell))
            {
                hitCell = cell;
                hitNormal = Vector3Int.zero;
                hitT = 0f;
                return true;
            }

            int stepX = dirCS.x >= 0 ? 1 : -1;
            int stepY = dirCS.y >= 0 ? 1 : -1;
            int stepZ = dirCS.z >= 0 ? 1 : -1;

            float tMaxX = IntBound(originCS.x, dirCS.x);
            float tMaxY = IntBound(originCS.y, dirCS.y);
            float tMaxZ = IntBound(originCS.z, dirCS.z);

            float tDeltaX = dirCS.x == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dirCS.x);
            float tDeltaY = dirCS.y == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dirCS.y);
            float tDeltaZ = dirCS.z == 0 ? float.PositiveInfinity : Mathf.Abs(1f / dirCS.z);

            float maxT = maxDistance / cellSize;

            //Traverse cells
            for (float t = 0f; t <= maxT;)
            {
                //Step to next cell boundary (tie-safe; prefer Y on ties)
                const float eps = 1e-5f;

                if (tMaxY <= tMaxX + eps && tMaxY <= tMaxZ + eps)
                {
                    cell.y += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                    hitNormal = new Vector3Int(0, -stepY, 0);
                }
                else if (tMaxX <= tMaxZ + eps)
                {
                    cell.x += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                    hitNormal = new Vector3Int(-stepX, 0, 0);
                }
                else
                {
                    cell.z += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                    hitNormal = new Vector3Int(0, 0, -stepZ);
                }

                if (!inBounds(cell)) continue;

                if (isSolid(cell))
                {
                    hitCell = cell;
                    hitT = t;
                    return true;
                }
            }

            return false;
        }

        private static float IntBound(float s, float ds)
        {
            if (ds == 0) return float.PositiveInfinity;

            //distance to next integer boundary
            if (ds > 0)
                return (Mathf.Floor(s + 1f) - s) / ds;
            else
                return (s - Mathf.Floor(s)) / -ds;
        }
    }
}
