using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System;
using System.Text;
using System.Collections;

public class NetworkManagerTCP : MonoBehaviour
{
    [Header("UI")]
    public Button serverButton;
    public Button clientButton;
    public TMP_InputField ipInputField;
    public GameObject popupPanel;
    private TMP_Text popupText;

    private TcpListener tcpListener;
    private TcpClient client;
    private NetworkStream stream;
    public bool isServer = false;
    public bool isConnected = false;

    public GameObject serverPlayerPrefab;
    public GameObject clientPlayerPrefab;

    private Vector3 lastSentPos;
    private float sendInterval = 0.05f;
    private float sendTimer = 0f;

    public static NetworkManagerTCP Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (FindObjectOfType<UnityMainThreadDispatcher>() == null)
        {
            GameObject disp = new GameObject("MainThreadDispatcher");
            disp.AddComponent<UnityMainThreadDispatcher>();
        }

        if (popupPanel != null)
            popupText = popupPanel.GetComponentInChildren<TMP_Text>();

        Application.runInBackground = true;
    }

    void Start()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);

        serverButton.onClick.AddListener(StartServerAsync);
        clientButton.onClick.AddListener(StartClientAsync);
    }

    // ==================== SERVER ====================
    public async void StartServerAsync()
    {
        isServer = true;
        popupPanel?.SetActive(true);
        popupText.text = "🚀 Starting server...";

        tcpListener = new TcpListener(IPAddress.Any, 8888);
        tcpListener.Start();

        popupText.text = "Server started. Waiting for client...";

        try
        {
            client = await tcpListener.AcceptTcpClientAsync();
            client.NoDelay = true;
            stream = client.GetStream();
            isConnected = true;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                popupPanel.SetActive(false);
                GameObject[] menuObjects = GameObject.FindGameObjectsWithTag("MainMenu");
                foreach (var obj in menuObjects)
                {
                    obj.SetActive(false);
                }
                SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
                StartCoroutine(SpawnPlayersAfterLoad());
            });

            _ = ListenForMessagesAsync();
        }
        catch (Exception ex)
        {
            popupText.text = "❌ Server Error: " + ex.Message;
        }
    }

    // ==================== CLIENT ====================
    public async void StartClientAsync()
    {
        isServer = false;
        string serverIP = ipInputField.text;
        popupPanel?.SetActive(true);
        popupText.text = $"🔗 Connecting to {serverIP}...";

        try
        {
            client = new TcpClient();
            await client.ConnectAsync(serverIP, 8888);
            client.NoDelay = true;
            stream = client.GetStream();
            isConnected = true;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                popupPanel.SetActive(false);
                GameObject[] menuObjects = GameObject.FindGameObjectsWithTag("MainMenu");
                foreach (var obj in menuObjects)
                {
                    obj.SetActive(false);
                }
                SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
                StartCoroutine(SpawnPlayersAfterLoad());
            });

            _ = ListenForMessagesAsync();
        }
        catch (Exception ex)
        {
            popupText.text = "❌ Connection failed: " + ex.Message;
        }
    }

    // ==================== SPAWN NHÂN VẬT ====================
    IEnumerator SpawnPlayersAfterLoad()
    {
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "SampleScene");

        yield return new WaitForSeconds(0.2f); // đợi object load xong

        if (isServer)
        {
            Instantiate(serverPlayerPrefab, new Vector3(-2f, 0f, 0f), Quaternion.identity).tag = "ServerPlayer";
            Instantiate(clientPlayerPrefab, new Vector3(2f, 0f, 0f), Quaternion.identity).tag = "ClientPlayer";
        }
        else
        {
            Instantiate(clientPlayerPrefab, new Vector3(2f, 0f, 0f), Quaternion.identity).tag = "ClientPlayer";
            Instantiate(serverPlayerPrefab, new Vector3(-2f, 0f, 0f), Quaternion.identity).tag = "ServerPlayer";
        }

        Debug.Log("Players spawned.");
    }

    // ==================== TRUYỀN DỮ LIỆU ====================
    public void SendPosition(Vector3 pos)
    {
        if (!isConnected || stream == null || (client != null && !client.Connected))
            return;

        try
        {
            string msg = $"POS|{pos.x}|{pos.y}";
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogError("SendPosition error: " + ex.Message);
            isConnected = false;
        }
    }

    async Task ListenForMessagesAsync()
    {
        byte[] buffer = new byte[1024];
        try
        {
            while (isConnected)
            {
                if (stream == null || !client.Connected)
                {
                    isConnected = false;
                    break;
                }

                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead <= 0)
                {
                    isConnected = false;
                    break;
                }

                string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                UnityMainThreadDispatcher.Enqueue(() => HandleMessage(msg));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ListenForMessages error: " + ex.Message);
            isConnected = false;
        }
    }

    void HandleMessage(string msg)
    {
        if (!msg.StartsWith("POS|")) return;

        string[] parts = msg.Split('|');
        if (parts.Length < 3) return;

        if (float.TryParse(parts[1], out float x) && float.TryParse(parts[2], out float y))
        {
            GameObject other = GameObject.FindGameObjectWithTag(isServer ? "ClientPlayer" : "ServerPlayer");
            if (other != null)
                other.transform.position = new Vector3(x, y, 0);
        }
    }

    void LateUpdate()
    {
        if (!isConnected) return;

        sendTimer += Time.deltaTime;
        GameObject localPlayer = GameObject.FindGameObjectWithTag(isServer ? "ServerPlayer" : "ClientPlayer");
        if (localPlayer == null) return;

        if (sendTimer >= sendInterval)
        {
            sendTimer = 0f;
            SendPosition(localPlayer.transform.position);
        }
    }

    void OnApplicationQuit()
    {
        isConnected = false;
        try
        {
            stream?.Close();
            client?.Close();
            tcpListener?.Stop();
        }
        catch { }
    }
}
