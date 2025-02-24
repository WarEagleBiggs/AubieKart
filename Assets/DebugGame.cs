using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DebugGame : MonoBehaviour
{
    public GameObject KART;
    public GameObject REF;

    public GameObject map1;
    public GameObject map2;

    private void Update()
    {
        REF.transform.position = new Vector3(KART.transform.position.x, KART.transform.position.y + 10,
            KART.transform.position.z);
    }

    public void ResetGame()
    {
        SceneManager.LoadScene("Game");
    }
    public void Stuck()
    {
        KART.transform.position = REF.transform.position;
        KART.transform.rotation = REF.transform.rotation;

    }

    public void ToggleMap()
    {
        if (!map1.activeSelf)
        {
            Stuck();
            map1.SetActive(true);
            map2.SetActive(false);
        }
        else
        {
            Stuck();
            map1.SetActive(false);
            map2.SetActive(true);
        }
    }
}
