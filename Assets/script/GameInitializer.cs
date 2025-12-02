using UnityEngine;
using TMPro;

public class GameInitializer : MonoBehaviour
{
    public GameObject serverPlayerPrefab; // gán ServerPlayerPrefab trong inspector
    public GameObject clientPlayerPrefab; // gán ClientPlayerPrefab trong inspector
    public TMP_Text scoreDisplay;
    public GameObject popupPanel;
    public TMP_Text winmessage;

    public Vector2 serverSpawn = new Vector2(-2f, 0f);
    public Vector2 clientSpawn = new Vector2(2f, 0f);
   void Start()
    {
        SpawnPlayers();
        SendScoreTextReference();
        SendPopupTextReference();
    }
void SendScoreTextReference()
    {
        var net = NetworkManagerTCP.Instance;
        if (net == null)
        {
            Debug.LogError("NetworkManagerTCP.Instance is null! Cannot update score UI.");
            return;
        }
        
        if (scoreDisplay == null)
        {
            Debug.LogError("Score Display is not assigned in GameInitializer Inspector!");
            return;
        }

        // Truyền đối tượng TMP_Text cho Network Manager
        net.SetScoreText(scoreDisplay);

        net.SetWinmessage(winmessage);
        // Cập nhật điểm số ban đầu (thường là 0-0)
        net.UpdateScoreUI(); 
    }

    void SendPopupTextReference()
    {
        var net = NetworkManagerTCP.Instance;
        if (net == null)
        {
            Debug.LogError("NetworkManagerTCP.Instance is null! Cannot update score UI.");
            return;
        }
        
        if (scoreDisplay == null)
        {
            Debug.LogError("Score Display is not assigned in GameInitializer Inspector!");
            return;
        }

        // Truyền đối tượng TMP_Text cho Network Manager
        net.SetpopupPanel(popupPanel);

        // Cập nhật điểm số ban đầu (thường là 0-0)
        net.UpdateScoreUI(); 
    }

void SpawnPlayers()
{
    var net = NetworkManagerTCP.Instance;
    if (net == null)
    {
        Debug.LogError("NetworkManagerTCP.Instance is null!");
        return;
    }

    if (net.isServer)
    {
        // server điều khiển ServerPlayer
        var p1 = Instantiate(serverPlayerPrefab, serverSpawn, Quaternion.identity);
        p1.GetComponent<PlayerController>().isLocalPlayer = true;
        p1.tag = "ServerPlayer";

        // client player (remote)
        var p2 = Instantiate(clientPlayerPrefab, clientSpawn, Quaternion.identity);
        p2.GetComponent<PlayerController>().isLocalPlayer = false;
        p2.tag = "ClientPlayer";
    }
    else
    {
        // client điều khiển ClientPlayer
        var p1 = Instantiate(serverPlayerPrefab, serverSpawn, Quaternion.identity);
        p1.GetComponent<PlayerController>().isLocalPlayer = false;
        p1.tag = "ServerPlayer";

        var p2 = Instantiate(clientPlayerPrefab, clientSpawn, Quaternion.identity);
        p2.GetComponent<PlayerController>().isLocalPlayer = true;
        p2.tag = "ClientPlayer";
    }
}

}
