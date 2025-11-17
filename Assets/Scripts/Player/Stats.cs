using System;
using UnityEngine;
using Water;

public class Stats : MonoBehaviour, IBuoyancy
{
    [Header("Physical properties")]
    [SerializeField] private float mass = 160f;
    [SerializeField] private float density = 1030f;
    [SerializeField] private float volume = 0.08f;
    
    [Header("Player Identity")]
    [SerializeField] private string playerId;
    
    //STATE
    public bool isLocalPlayer = false;
    private bool _isSteeringBoat = false;
    private GlobalState _globalState;
    
    //COMPONENTS
    private Rigidbody _rb;

    private void Awake()
    {
        _globalState = FindFirstObjectByType<GlobalState>();
    }

    void Start()
    {
        // === DEV ===
        //for further development change it
        isLocalPlayer = true;
        
        _rb = this.GetComponent<Rigidbody>();
        _rb.mass = this.mass;
    }

    public string GetPlayerId()
    {
        return playerId;
    }
    
    private void OnEnable()
    {
        _globalState.OnDriverChanged += OnDriverChanged;
    }
    
    private void OnDisable()
    {
        _globalState.OnDriverChanged -= OnDriverChanged;
    }

    private void OnDriverChanged(string driverId)
    {
        SetIsSteeringBoat(driverId == playerId);
    }

    public void SetIsSteeringBoat(bool isSteeringBoat)
    {
        _isSteeringBoat = isSteeringBoat;
    }

    public bool IsSteeringBoat()
    {
        return _isSteeringBoat;
    }

    public float GetDensity()
    {
        return density;
    }

    public float GetMass()
    {
        return mass;
    }

    public float GetVolume()
    {
        return volume;
    }
}
