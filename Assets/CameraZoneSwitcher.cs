using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraZoneSwitcher : MonoBehaviour
{
    public GameObject primaryCamera;
    public GameObject secondaryCamera;

    private void Start()
    {
        //secondaryCamera = GetComponentInChildren<GameObject>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            secondaryCamera.SetActive(true);
            Camera.main.orthographic = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            secondaryCamera.SetActive(false);
            Camera.main.orthographic = false;
        }
    }

}
