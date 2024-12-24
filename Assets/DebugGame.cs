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
    public void ResetGame()
    {
        SceneManager.LoadScene("Game");
    }
    public void Stuck()
    {
        KART.transform.position = REF.transform.position;
        KART.transform.rotation = REF.transform.rotation;

    }
}
