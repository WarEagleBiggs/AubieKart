using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Referee : MonoBehaviour
{
    public List<GameObject> MapsList;

    private void Start()
    {
        MapsList[Master.GetInstance.currMap].SetActive(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M) )
        {
            Master.GetInstance.currMap++;

            if (Master.GetInstance.currMap > MapsList.Count -1)
            {
                Master.GetInstance.currMap = 0;
            }
            
            SceneManager.LoadScene("BalloonBattle");
        }
    }
}
