using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticlesController : MonoBehaviour
{
    private ParticleSystem particles;

    private void Start()
    {
        particles = GetComponent<ParticleSystem>();
    }

    public void ConditionalPlay(Transform transf)
    {
        if (!particles.isPlaying)
        {
            particles.transform.position = transf.position;
            particles.Play();
        }
    }
}
