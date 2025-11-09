using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine.SceneManagement;

public class NetworkManagerTCP : MonoBehaviour
{
    [Header("UI")]
    public Button serverButton;
    public Button clientButton;
    public TMP_InputField ipInputField;
    public GameObject popupPanel;
    private TMP_Text popupText;

    private TcpListener tcpListener;
    private Thread serverThread;

    private TcpClient client;
    private Thread clientThread;

    void Awake()
    {
        if (popupPanel != null)
            popupText = popupPanel.GetComponentInChildren<TMP_Text>();
    }

    void Start()
    {
        popupPanel.SetActive(false);

        serverButton.onClick.AddListener(StartServer);
        clientButton.onClick.AddListener(StartClient);
    }

    // ==================== SERVER ====================
    public void StartServer()
    {
        if (popupPanel == null || popupText == null)
        {
            Debug.LogError("UI chưa gán!");
            return;
        }

        popupPanel.SetActive(true);
        popupText.text = "🚀 Starting server...";

        serverThread = new Thread(() =>
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, 7777);
                tcpListener.Start();
                
                UnityMainThreadDispatcher.Enqueue(() =>
                    popupText.text = "✅ Server started. Waiting for client..."
                );

                TcpClient newClient = tcpListener.AcceptTcpClient();
                Debug.LogError("Client connected!");
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    popupText.text = "🎮 Client connected!";
                    SceneManager.LoadScene("GameScene");
                });
            }
            catch (System.Exception ex)
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                    popupText.text = "❌ Server Error: " + ex.Message
                );
            }
        });
        serverThread.Start();
    }

    // ==================== CLIENT ====================
    public void StartClient()
    {
        if (ipInputField == null)
        {
            Debug.LogError("UI chưa gán!");
            return;
        }

        string serverIP = ipInputField.text;
        popupPanel.SetActive(true);
        popupText.text = $"🔗 Connecting to {serverIP}...";

        clientThread = new Thread(() =>
        {
            try
            {
                client = new TcpClient();
                 Debug.LogError("222222222222222");
                client.Connect(serverIP, 7777);
                Debug.LogError("11111111111111222111");
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    popupText.text = "✅ Connected to server!";
                    SceneManager.LoadScene("GameScene");
                });
            }
            catch (System.Exception ex)
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    popupText.text = "❌ Connection failed: " + ex.Message;
                });
            }
        });
        clientThread.Start();
    }

    void OnApplicationQuit()
    {
        tcpListener?.Stop();
        serverThread?.Abort();
        client?.Close();
        clientThread?.Abort();
    }
}
