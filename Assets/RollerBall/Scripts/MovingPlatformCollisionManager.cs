using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatformCollisionManager : MonoBehaviour
{
    private GameObject player;

    private void Start()
    {
        // Trova l'oggetto con il tag "Player"
        player = GameObject.FindGameObjectWithTag("Player");
    }

    private void OnCollisionStay(Collision collision)
    {
        // Controlla se l'oggetto in collisione è il giocatore
        if (collision.gameObject == player)
        {
            // Controlla se la normale del contatto è uguale al vettore della gravità
            foreach (ContactPoint contact in collision.contacts)
            {
                if (Vector3.Dot(contact.normal, Physics.gravity.normalized) >= .9f)
                {
                    // Rende il giocatore un figlio della piattaforma
                    player.transform.parent = transform;
                    break;
                }
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        // Quando il giocatore esce dalla collisione, rimuove il giocatore come figlio della piattaforma
        if (collision.gameObject == player)
        {
            player.transform.parent = null;
        }
    }
}
