using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEditor.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    #region 🡹WTF IS THAT? THE SINGLETON PATTERN
    /* This step is a bit technical. You don't need to understand the code, only the concept.
     * The "singleton pattern" is a design pattern that ensures that only one instance of a certain class is created,
     * and provides a global access point to this instance.
     * You can indeed access the public variables or public methods of the game manager
     * by writing GameManager.instance, followed by the name of the public variable or method.
     * The game requires a single game manager, and no more than one can exist.
     * Unity, by default, assigns a cog icon when a script named GameManager is created.
     */
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(this.gameObject);
        }
        else if (instance != this)
        {
            Destroy(this.gameObject);
        }
    }
    #endregion

    private Vector3 checkpoint;
    private GameObject player;

    [SerializeField] private UnityEvent OnReloadCheckpoint;
    [SerializeField] private UnityEvent OnGameOver;


    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        checkpoint = player.transform.position;
    }

    public void SetCheckpoint(Transform newTransform)
    {
        checkpoint = newTransform.position;
    }

    public void ReloadCheckpoint()
    {
        player.transform.position = checkpoint;
    }

    public void RestartScene()
    {
        EditorSceneManager.LoadScene(0);
    }
}
