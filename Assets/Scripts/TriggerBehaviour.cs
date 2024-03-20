using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class TriggerBehaviour : MonoBehaviour
{
    [SerializeField] private string objectTag = "Player";
    [SerializeField] private UnityEvent OnEnter;
    [SerializeField] private UnityEvent OnStay;
    [SerializeField] private UnityEvent OnExit;
    
    private void OnTriggerEnter(Collider other)
    {
        if (string.Equals(other.tag, objectTag)) OnEnter.Invoke();
    }

    private void OnTriggerStay(Collider other)
    {
        if (string.Equals(other.tag, objectTag)) OnStay.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (string.Equals(other.tag, objectTag)) OnExit.Invoke();
    }

}
