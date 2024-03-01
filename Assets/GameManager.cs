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

    public int score { get; private set; } = 0;
    #region 🡹WTF IS THAT? PUBLIC GET, PRIVATE SET
    /* "public get" does mean that any class can read the value.
     * "private set" means that only the class that declares the property (in this case, the Game Manager) can change the value.
     * For all other classes, this property behaves as a “read-only” variable.
     */
    #endregion

    // Events
    public UnityEvent OnScoreEvent;
    public UnityEvent GameOverEvent;

    public void RestartScene()
    {
        EditorSceneManager.LoadScene(0);
        Time.timeScale = 1f;
        score = 0;
    }
}
