using UnityEngine;
using System.Collections;

public class FlagHandler : MonoBehaviour
{
    private bool isCaptured = false; 
    public static FlagHandler Instance { get; private set; }
    private Vector3 initialPosition;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); 
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("ServerPlayer") || other.CompareTag("ClientPlayer"))
        {
            if (NetworkManagerTCP.Instance.isConnected && !isCaptured)
            {
                isCaptured = true;
                Debug.Log($"{other.tag} touched the flag.");

                NetworkManagerTCP.Instance.SendFlagCaptured();
                
                StartCoroutine(ResetCaptureState());

                gameObject.SetActive(false); 
            }
        }
    }
    
    public void ResetFlagPosition()
    {
        
        gameObject.SetActive(true);

    }
    
    IEnumerator ResetCaptureState()
    {
        yield return new WaitForSeconds(1.0f); 
        isCaptured = false;
    }
}