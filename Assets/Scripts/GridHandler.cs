using UnityEngine;
using UnityEngine.UIElements;

public class GridHandler : MonoBehaviour {

    [SerializeField] private Transform focus;     // camera pivot or player
    [SerializeField] private float y = 0f;        // keep it slightly above ground if z-fighting
    [SerializeField] private float snap = 1f;     // snap to cell size (1 = 1m). Set to 0 to disable.

    private Renderer rend;

    void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    void LateUpdate()
    {
        if (!focus) return;

        var p = focus.position;

        if (snap > 0f)
        {
            p.x = Mathf.Floor(p.x / snap) * snap;
            p.z = Mathf.Floor(p.z / snap) * snap;
        }

        transform.position = new Vector3(p.x, y, p.z);
    }

    public void SetVisible(bool visible)
    {
        if (!rend) rend = GetComponent<Renderer>();
        rend.enabled = visible;
    }

    public void ToggleVisible()
    {
        if (!rend) rend = GetComponent<Renderer>();
        rend.enabled = !rend.enabled;
    }
}
