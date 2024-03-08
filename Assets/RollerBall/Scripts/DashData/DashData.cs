using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "Dash data", menuName = "New 'Dash Data'")]
public class DashData : ScriptableObject
{
    [SerializeField, Min(0.1f)] private float dashSpeed;
    [SerializeField, Min(0.1f)] private float dashDuration;
    [Tooltip("On dash start, freeze the player for this number of frames"),
    SerializeField, Min(0)] private int dashFreezeFrames;
    [SerializeField] private AnimationCurve dashAnimationCurve;
    [SerializeField] private UnityEvent OnDash;
}
