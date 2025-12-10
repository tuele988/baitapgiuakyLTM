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
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;


public class NetworkManagerTCP : MonoBehaviour
{
    [Header("UI")]
    public Button serverButton;
    public Button clientButton;
    public TMP_InputField ipInputField;
    public GameObject popupPanel;
    public TMP_Text popupText;

    private TcpListener tcpListener;
    private TcpClient client;
    private SslStream sslStream;
    public bool isServer = false;
    public bool isConnected = false;

    public GameObject serverPlayerPrefab;
    public GameObject clientPlayerPrefab;
    public TMP_Text scoreText;
    public TMP_Text winmessage;

    private Vector3 lastSentPos;
    private float sendInterval = 0.05f;
    private float sendTimer = 0f;
    public int serverScore = 0;
    public int clientScore = 0;
    private const int WINNING_SCORE = 3;

    public static NetworkManagerTCP Instance;
    [Header("SSL/TLS Settings")]
    public X509Certificate2 serverCertificate; 
    public string certificatePath = "server.pfx"; 
    public string certificatePassword = "1234";

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

        // 🔑 LOGIC TẢI CHỨNG CHỈ (Khắc phục lỗi Missing Certificate)
        try
        {
            // Nếu bạn đặt file server.pfx trong thư mục gốc của project (Assets), 
            // chỉ cần dùng certificatePath. Nếu không, hãy dùng đường dẫn đầy đủ:
            serverCertificate = new X509Certificate2(certificatePath, certificatePassword);
            Debug.Log("✅ Server Certificate loaded successfully.");
        }
        catch (Exception ex)
        {
            // Nếu lỗi, Server không thể khởi động chế độ SSL an toàn.
            Debug.LogError($"❌ FAILED to load server certificate. Check path/password and ensure .pfx includes private key. Error: {ex.Message}");
            serverCertificate = null;
        }

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
    // Cần kiểm tra chứng chỉ (Giả định serverCertificate đã được tải trong Awake())
    if (serverCertificate == null)
    {
        popupPanel?.SetActive(true);
        popupText.text = "❌ Cannot start server: Server certificate is missing.";
        return;
    }

    isServer = true;
    popupPanel?.SetActive(true);
    popupText.text = "🚀 Starting server...";

    tcpListener = new TcpListener(IPAddress.Any, 8888);
    tcpListener.Start();

    popupText.text = "Server started. Waiting for client (with SSL)...";

    try
    {
        client = await tcpListener.AcceptTcpClientAsync();
        client.NoDelay = true;
        
        // 1. Tạo SslStream từ NetworkStream
        // THAY THẾ stream = client.GetStream();
        sslStream = new SslStream(client.GetStream(), false); // Giả sử bạn đổi tên biến từ stream thành sslStream
        
        // 2. Thực hiện Server-side SSL/TLS Handshake
        await sslStream.AuthenticateAsServerAsync(serverCertificate, 
                                                 clientCertificateRequired: false, 
                                                 checkCertificateRevocation: true);
        
        isConnected = true;
        popupText.text = "✅ Client connected via SSL/TLS!";
        
        _ = ListenForMessagesAsync();
        await Task.Delay(200);

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

        // Chỉ cần gọi ListenForMessagesAsync() một lần
    }
    catch (Exception ex)
    {
        popupText.text = "❌ Server SSL Error: " + ex.Message;
        Debug.LogError("Server SSL Handshake Error: " + ex.Message);
        isConnected = false;
    }
}

    // ==================== CLIENT ====================
    public async void StartClientAsync()
{
    isServer = false;
    string serverIP = ipInputField.text;
    popupPanel?.SetActive(true);
    popupText.text = $"🔗 Connecting to {serverIP} (with SSL)...";

    try
    {
        client = new TcpClient();
        await client.ConnectAsync(serverIP, 8888);
        client.NoDelay = true;
        
        // 1. Tạo SslStream, truyền hàm xác thực chứng chỉ Server
        // THAY THẾ stream = client.GetStream();
        sslStream = new SslStream(client.GetStream(), false, ValidateServerCertificate); // Giả sử bạn đổi tên biến từ stream thành sslStream

        // 2. Thực hiện Client-side SSL/TLS Handshake
        // Tên host phải khớp với Common Name (CN) trong chứng chỉ Server
        string hostName = serverIP == "127.0.0.1" ? "localhost" : serverIP; 
        await sslStream.AuthenticateAsClientAsync(hostName); 
        
        isConnected = true;
        popupText.text = "✅ Connected via SSL/TLS!";

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
        popupText.text = "❌ Connection failed (SSL): " + ex.Message;
        Debug.LogError("Client SSL Handshake Error: " + ex.Message);
        isConnected = false;
    }
}

private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
{
    // Đây là ví dụ chấp nhận chứng chỉ tự ký cho mục đích học tập/thử nghiệm.
    if (sslPolicyErrors == SslPolicyErrors.None)
        return true;

    Debug.LogWarning($"Certificate error encountered: {sslPolicyErrors}. Accepting for test purposes.");
    
    // Rất quan trọng: Trong môi trường học tập, ta chấp nhận lỗi chứng chỉ để kết nối.
    // Trong môi trường thực tế, bạn sẽ trả về false nếu có lỗi.
    return true; 
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
        if (!isConnected || sslStream == null || (client != null && !client.Connected))
            return;

        try
        {
            string msg = $"POS|{pos.x}|{pos.y}";
            byte[] data = Encoding.UTF8.GetBytes(msg);
            sslStream.Write(data, 0, data.Length);
            sslStream.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogError("SendPosition error: " + ex.Message);
            isConnected = false;
        }
    }

    async Task ListenForMessagesAsync()
{
    await Task.Delay(200); // tránh bị dispose khi load scene

    byte[] buffer = new byte[1024];

    try
    {
        while (isConnected && client != null && client.Client != null && client.Connected)
        {
            int bytesRead = 0;

            try
            {
                bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);
            }
            catch (ObjectDisposedException)
            {
                Debug.LogWarning("Socket disposed safely. Stopping listen.");
                isConnected = false;
                break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Listen error: " + ex.Message);
                isConnected = false;
                break;
            }

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
        Debug.LogWarning("Fatal ListenForMessages error: " + ex.Message);
    }
}


    void HandleMessage(string msg)
{
    if (msg.StartsWith("POS|"))
    {
        // Logic xử lý vị trí cũ
        string[] parts = msg.Split('|');
        if (parts.Length < 3) return;

        if (float.TryParse(parts[1], out float x) && float.TryParse(parts[2], out float y))
        {
            GameObject other = GameObject.FindGameObjectWithTag(isServer ? "ClientPlayer" : "ServerPlayer");
            if (other != null)
                other.transform.position = new Vector3(x, y, 0);
        }
    }
    else if (msg.StartsWith("FLAG|"))
    {
        // CHỈ SERVER MỚI XỬ LÝ GÓI TIN FLAG TỪ CẢ 2 PHÍA
        Debug.Log($"update isServer");
        string[] parts = msg.Split('|');
        if (parts.Length >= 2)
        {
            string capturedBy = parts[1]; // "ServerPlayer" hoặc "ClientPlayer"
            ProcessFlagCapture(capturedBy);
        }
    }
    else if (msg.StartsWith("SCORE|"))
    {
        // Cả Server và Client đều xử lý gói tin cập nhật điểm từ Server
        string[] parts = msg.Split('|');
        if (parts.Length >= 3 && int.TryParse(parts[1], out int sScore) && int.TryParse(parts[2], out int cScore))
        {
            serverScore = sScore;
            clientScore = cScore;
            UpdateScoreUI();
        }
    }
    else if (msg.StartsWith("FLAG_RESET")) // <-- THÊM DÒNG NÀY
    {
           Debug.Log($"update condition FLAG_RESET");
        // Cả Server và Client đều xử lý lệnh reset cờ từ Server
        HandleFlagReset();
    }
    else if (msg.StartsWith("GAMEOVER|"))
    {
        // Cả Server và Client đều xử lý gói tin kết thúc game từ Server
        string[] parts = msg.Split('|');
        if (parts.Length >= 2)
        {
            string winner = parts[1];
            HandleGameOver(winner);
        }
    }
}

// TRONG NetworkManagerTCP.cs

void HandleFlagReset()
{
    UnityMainThreadDispatcher.Enqueue(() =>
    {
        FlagHandler flag = FlagHandler.Instance; 
        if (flag != null)
        {
            flag.ResetFlagPosition();
        }
        
        GameObject serverPlayer = GameObject.FindGameObjectWithTag("ServerPlayer");
        if (serverPlayer != null)
        {
            var pC1 = serverPlayer.GetComponent<PlayerController>();
            if (pC1 != null)
                pC1.ResetToSpawnPosition();
        }

        GameObject clientPlayer = GameObject.FindGameObjectWithTag("ClientPlayer");
        if (clientPlayer != null)
        {
            var pC2 = clientPlayer.GetComponent<PlayerController>();
            if (pC2 != null)
                pC2.ResetToSpawnPosition();
        }
        
        Debug.Log("Flag and Players have been reset for new round.");
    });
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
            sslStream?.Close();
            client?.Close();
            tcpListener?.Stop();
        }
        catch { }
    }
    
    public void SendFlagCaptured()
{
    if (!isConnected || sslStream == null || (client != null && !client.Connected))
        return;

    try
    {
        // Gửi thông báo đến Server (Server sẽ tự xử lý, Client gửi đến Server)
        string playerTag = isServer ? "ServerPlayer" : "ClientPlayer";
        string msg = $"FLAG|{playerTag}"; // Ví dụ: FLAG|ServerPlayer hoặc FLAG|ClientPlayer
        byte[] data = Encoding.UTF8.GetBytes(msg);
        
        // Sử dụng TCP để đảm bảo gói tin FLAG được nhận
        sslStream.Write(data, 0, data.Length);
        sslStream.Flush();
        
        Debug.Log($"Sent: {msg}");
    }
    catch (Exception ex)
    {
        Debug.LogError("SendFlagCaptured error: " + ex.Message);
        isConnected = false;
    }
}

// Logic tính điểm CHỈ CHẠY TRÊN SERVER
void ProcessFlagCapture(string capturedBy)
{
    Debug.Log($"{capturedBy} captured the flag!");
    
    // Tăng điểm
    if (capturedBy == "ServerPlayer")
    {
        serverScore++;
        Debug.Log($"Score ServerPlayer{serverScore}"); 
    }
    else if (capturedBy == "ClientPlayer")
    {
        clientScore++;
    }

    // Kiểm tra thắng
    if (serverScore >= WINNING_SCORE)
    {
        // Gửi thông báo thắng đến tất cả
        BroadcastMessage($"GAMEOVER|ServerPlayer");
    }
    else if (clientScore >= WINNING_SCORE)
    {
        // Gửi thông báo thắng đến tất cả
        BroadcastMessage($"GAMEOVER|ClientPlayer");
    }
    else
    {
        // Gửi thông báo cập nhật điểm
        BroadcastMessage($"SCORE|{serverScore}|{clientScore}");
        Debug.Log($"update FLAG_RESET");
        BroadcastMessage($"FLAG_RESET");
    }
}

// Hàm gửi thông điệp đến tất cả (Hiện tại chỉ là Client duy nhất)
void BroadcastMessage(string msg)
{
    // Trong game 2 người chơi TCP đơn giản này, ta chỉ cần gửi đến Client đang kết nối
    if (!isConnected || sslStream == null || (client != null && !client.Connected))
        return;

    try
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);
        sslStream.Write(data, 0, data.Length);
        sslStream.Flush();
        Debug.Log($"Broadcast: {msg}");
        
        // Cập nhật cho Server tự mình (vì Server không tự nhận gói tin qua stream)
        if (msg.StartsWith("SCORE|"))
        {
            string[] parts = msg.Split('|');
            serverScore = int.Parse(parts[1]);
            clientScore = int.Parse(parts[2]);
            Debug.Log($"{parts} serverScore: {serverScore} clientScore:{clientScore}");
            UpdateScoreUI();
        }
        else if (msg.StartsWith("GAMEOVER|"))
        {
            HandleGameOver(msg.Split('|')[1]);
        }
        else if (msg.StartsWith("FLAG_RESET")) // <-- THÊM LOGIC NÀY
        {
            // Server tự gọi hàm reset cờ của mình
            HandleFlagReset(); 
        }
    }
    catch (Exception ex)
    {
        Debug.LogError("BroadcastMessage error: " + ex.Message);
    }
}

// Cập nhật UI (Chạy trên cả Server và Client)
public void UpdateScoreUI()
{
    if (scoreText != null)
    {
        scoreText.text = $"Core 1: {serverScore} - Core 2: {clientScore}";
    }
}
public void SetScoreText(TMP_Text textComponent)
{
    scoreText = textComponent;
    Debug.Log("Score UI reference received successfully.");
}

public void SetWinmessage(TMP_Text textComponent)
{
    winmessage = textComponent;
}

public void SetpopupPanel(GameObject textComponent)
{
    popupPanel = textComponent;
    Debug.Log("Score UI reference received successfully.");
}
// Xử lý khi Game Over
public async void HandleGameOver(string winner)
{
    Debug.Log("Handle Game Over  ");
    UpdateScoreUI(); // Cập nhật điểm cuối cùng

    string message = (winner == (isServer ? "ServerPlayer" : "ClientPlayer")) 
                     ? "🏆 YOU WIN!" 
                     : "😔 YOU LOSE!";

    // Hiển thị Popup trên main thread
    UnityMainThreadDispatcher.Enqueue(() =>
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
            winmessage.text = message;
        }
    });

    // 1️⃣ Ngừng vòng lặp ListenForMessagesAsync
    isConnected = false;

    // 2️⃣ Đợi vòng lặp ListenForMessagesAsync thoát
    await Task.Delay(200); // hoặc lưu Listen task và await

    // 3️⃣ Chỉ sau khi Listen đã thoát mới đóng socket
    try
    {
        sslStream?.Close();
        client?.Close();
        tcpListener?.Stop();
        Debug.Log("Network connection safely closed after game over.");
    }
    catch (Exception ex)
    {
        Debug.LogWarning("Error closing network: " + ex.Message);
    }
}

}
