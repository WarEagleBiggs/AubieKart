using UnityEngine;

public class CameraSwap : MonoBehaviour
{
    [Tooltip("Assign exactly 3 cameras in the order you want to cycle.")]
    public Camera[] cameras = new Camera[3];

    [Tooltip("Start with this camera index (0-2).")]
    public int startIndex = 0;

    [Tooltip("Cycle on this key.")]
    public KeyCode cycleKey = KeyCode.Q;

    int _current;

    void Awake()
    {
        // Fallback: if not assigned, try to grab up to 3 from the scene.
        if (cameras == null || cameras.Length == 0)
        {
            cameras = new Camera[Mathf.Min(3, Camera.allCamerasCount)];
            Camera.GetAllCameras(cameras);
        }
    }

    void Start()
    {
        _current = Mathf.Clamp(startIndex, 0, Mathf.Max(0, cameras.Length - 1));
        ApplyActiveCamera(_current);
    }

    void Update()
    {
        if (Input.GetKeyDown(cycleKey) && cameras != null && cameras.Length > 0)
        {
            _current = (_current + 1) % cameras.Length;
            ApplyActiveCamera(_current);
        }
    }

    void ApplyActiveCamera(int index)
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            bool active = (i == index) && cameras[i] != null;
            if (cameras[i] != null)
            {
                cameras[i].gameObject.SetActive(active);

                // Ensure only one AudioListener is active to avoid Unity warnings.
                var listener = cameras[i].GetComponent<AudioListener>();
                if (listener) listener.enabled = active;
            }
        }
    }
}
