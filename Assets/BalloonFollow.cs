using TreeEditor;
using UnityEngine;

public class BalloonFollow : MonoBehaviour
{
    public Transform startPos;
    public Transform endPos;
    public Transform endPos2;
    public Transform endPos3;
    
    public LineRenderer line;
  

    void Update()
    {
        if (line != null)
        {
            line.SetPosition(0, startPos.position);
            line.SetPosition(1, endPos.position);
            
            line.SetPosition(2, startPos.position);
            line.SetPosition(3, endPos2.position);
            
            line.SetPosition(4, startPos.position);
            line.SetPosition(5, endPos3.position);
        }
    }
}