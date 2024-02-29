using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class TriggerBehaviour : MonoBehaviour
{
    [SerializeField] private UnityEvent OnEnter;
    [SerializeField] private UnityEvent OnStay;
    [SerializeField] private UnityEvent OnExit;
    
    private void OnTriggerEnter(Collider other)
    {
        OnEnter.Invoke();
    }

    private void OnTriggerStay(Collider other)
    {
        OnStay.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        OnExit.Invoke();
    }

}
