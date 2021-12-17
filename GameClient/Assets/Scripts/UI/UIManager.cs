using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RiptideNetworking;

public class UIManager : MonoBehaviour
{
    public GameObject menuCamera;
    private static UIManager _singleton;
    public static UIManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(UIManager)} instance already exists, destroying object!");
                Destroy(value);
            }
        }
    }

    public TMP_InputField usernameField;
    public TMP_InputField adressField;
    [SerializeField] private GameObject connectScreen;

    /// <summary>Attempts to connect to the server.</summary>
    public void ConnectToServer()
    {
        menuCamera.SetActive(false);
        connectScreen.SetActive(false);
        usernameField.interactable = false;
        adressField.interactable = false;
        if(string.IsNullOrEmpty(adressField.text)){
            NetworkManager.Singleton.ip = NetworkManager.Singleton.ip;
        }
        else {
            NetworkManager.Singleton.ip = adressField.text;
        }
        
        NetworkManager.Singleton.Connect();
    }

        public void BackToMain()
    {
        Cursor.lockState = CursorLockMode.Confined;
        menuCamera.SetActive(true);
        usernameField.interactable = true;
        adressField.interactable = true;
        connectScreen.SetActive(true);
    }

    private void Awake()
    {
        Singleton = this;
    }

    #region Messages
    public void SendName()
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ClientToServerId.playerName);
        message.Add(usernameField.text);
        NetworkManager.Singleton.Client.Send(message);
        
        DebugScreen.packetsUp++;        
        DebugScreen.bytesUp += message.WrittenLength;
    }    
    #endregion
}
