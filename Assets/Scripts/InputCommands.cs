using UnityEngine;

public class InputCommands
{
    private PlacementController controller;

    public InputCommands(PlacementController placementController)
    {
        controller = placementController;
    }

    public void Place(Vector2Int cell, Quaternion rotation)
    {
        if (controller.pieces.ContainsKey(cell))
        {
            Debug.Log($"[Place] Cell {cell} already occupied.");
            return;
        }

        controller.pieces[cell] = new PlacementController.PlacedPiece
        {
            pos = controller.CellToWorld(cell),
            rot = rotation
        };

        Debug.Log($"[Place] Placed block at {cell} with rotation {rotation.eulerAngles.y}°");
    }

    public void Delete(Vector2Int cell)
    {
        if (controller.pieces.Remove(cell))
        {
            Debug.Log($"[Delete] Removed block at {cell}");
            return;
        }

        // fallback: nearest within radius
        Vector3 center = controller.CellToWorld(cell);
        float best = controller.DeleteRadius * controller.DeleteRadius;
        Vector2Int bestKey = default;
        bool found = false;

        foreach (var kv in controller.pieces)
        {
            float d2 = (kv.Value.pos - center).sqrMagnitude;
            if (d2 < best)
            {
                best = d2;
                bestKey = kv.Key;
                found = true;
            }
        }

        if (found)
        {
            controller.pieces.Remove(bestKey);
            Debug.Log($"[Delete] Removed nearest block at {bestKey} to {cell}");
        }
        else
        {
            Debug.Log($"[Delete] No block found near {cell}");
        }
    }

    public void RotateLeft(Vector2Int cell)
    {
        if (!controller.pieces.TryGetValue(cell, out var piece))
        {
            Debug.Log($"[RotateLeft] No block at {cell}");
            return;
        }

        piece.rot *= Quaternion.Euler(0, -90, 0);
        controller.pieces[cell] = piece;
        Debug.Log($"[RotateLeft] Rotated block at {cell} to {piece.rot.eulerAngles.y}°");
    }

    public void RotateRight(Vector2Int cell)
    {
        if (!controller.pieces.TryGetValue(cell, out var piece))
        {
            Debug.Log($"[RotateRight] No block at {cell}");
            return;
        }

        piece.rot *= Quaternion.Euler(0, 90, 0);
        controller.pieces[cell] = piece;
        Debug.Log($"[RotateRight] Rotated block at {cell} to {piece.rot.eulerAngles.y}°");
    }
}
