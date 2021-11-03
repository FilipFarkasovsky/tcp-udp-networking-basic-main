using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    public GameObject startMenu;
    public InputField usernameField;
    public InputField adressField;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.Log("Instance already exists, destroying object!");
            Destroy(this);
        }
    }

    /// <summary>Attempts to connect to the server.</summary>
    public void ConnectToServer()
    {
        startMenu.SetActive(false);
        usernameField.interactable = false;
        adressField.interactable = false;
        Client.instance.ip = adressField.text;
        Client.instance.ConnectToServer();
    }
}
