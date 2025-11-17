using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GlobalState : MonoBehaviour
{
    public static GlobalState Instance {get; private set;}

    public string currentShipDriver = null;
    public event Action<string> OnDriverChanged;
    

    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool TrySetDriver(string playerId)
    {
        if (!string.IsNullOrEmpty(currentShipDriver)) return false;
        currentShipDriver = playerId;
        OnDriverChanged?.Invoke(playerId);
        return true;
    }

    public void ReleaseDriver(string playerId)
    {
        if (currentShipDriver == playerId)
        {
            currentShipDriver = null;
            OnDriverChanged?.Invoke(null);
        }
    }

    public bool IsPlayerDriving(string playerId)
    {
        return currentShipDriver == playerId;
    }
}
