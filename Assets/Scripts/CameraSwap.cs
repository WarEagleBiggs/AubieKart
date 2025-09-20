using UnityEngine;

public class CameraSwap : MonoBehaviour
{
    public Camera[] cameras = new Camera[3];

    public int startIndex = 0;

    public KeyCode cycleKey = KeyCode.Q;

    int _current;

    void Awake()
    {
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

                var listener = cameras[i].GetComponent<AudioListener>();
                if (listener) listener.enabled = active;
            }
        }
    }
}
