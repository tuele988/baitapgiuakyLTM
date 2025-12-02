using UnityEngine;
using System.Collections;

public class FlagHandler : MonoBehaviour
{
    private bool isCaptured = false; 
    public static FlagHandler Instance { get; private set; }
    private Vector3 initialPosition;
    private Coroutine resetCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            initialPosition = transform.position; 
        }
        else
        {
            Destroy(gameObject); 
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isCaptured) return; 

        if (other.CompareTag("ServerPlayer") || other.CompareTag("ClientPlayer"))
        {
            if (other.GetComponent<PlayerController>().isLocalPlayer)
            {
                isCaptured = true; 
                
                gameObject.SetActive(false); 

                NetworkManagerTCP.Instance.SendFlagCaptured();
            }
        }
    }

    public void ResetFlagPosition()
    {
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
        }
        
        transform.position = initialPosition;
        gameObject.SetActive(true); 
        
        isCaptured = true; 

        resetCoroutine = StartCoroutine(ResetCapturedStatus());
        Debug.Log("Flag reset to position and waiting for capture status reset.");
    }

    IEnumerator ResetCapturedStatus()
    {
        yield return new WaitForSeconds(1f); 
        isCaptured = false; 
        resetCoroutine = null;
        Debug.Log("Flag is ready for capture (isCaptured = false).");
    }
}