using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class BoatController : MonoBehaviour
{
    [Header("Steering position")] 
    private readonly Vector3 _steeringPosition = new Vector3(-5.5f, 3f, -0.02f);
    private readonly float _steeringRotation = -90f;
    [SerializeField] private Collider trigger;

    [Header("Ship Control")] 
    [SerializeField] private Rigidbody shipRb;
    [SerializeField] private float forwardForce = 1000f;
    [SerializeField] private float backwardsForce = 300f;
    [SerializeField] private float torqueForce = 400f;

    [Header("Input handling")] 
    [SerializeField] private InputActionAsset inputActions;

    private InputAction _startSteering;
    private InputAction _stopSteering;
    private InputAction _forward;
    private InputAction _backward;
    private InputAction _left;
    private InputAction _right;

    private float _w = 0f;
    private float _s = 0f;
    private float _a = 0f;
    private float _d = 0f;
    
    //LOCAL PLAYER REFERENCES
    private Transform _localPlayerTransform;
    private Stats _localPlayerState;
    private GameObject _localGameObject;
    private Rigidbody _localRB;
    
    //GLOBAL STATE
    private GlobalState _globalState;

    private bool _canStartSteering = false;
    private bool _isSteering = false;

    private RigidbodyContainer _savedRb = new RigidbodyContainer();

    private struct RigidbodyContainer
    {
        public float Mass;
        public float LinearDamping;
        public float AngularDamping;
        public bool UseGravity;
        public RigidbodyConstraints SavedConstraints;
        public CollisionDetectionMode SavedCollisionDetection;
        public RigidbodyInterpolation SavedInterpolation;
    }

    private void Awake()
    {
        _globalState = FindFirstObjectByType<GlobalState>();
        shipRb = this.GetComponent<Rigidbody>();
        
        inputActions.FindActionMap("Boat").Enable();
        _forward = inputActions.FindAction("Forward");
        _startSteering = inputActions.FindAction("Start Steering");
        _stopSteering = inputActions.FindAction("Stop Steering");
        _backward = inputActions.FindAction("Backward");
        _left = inputActions.FindAction("Left");
        _right = inputActions.FindAction("Right");
    }

    //CHECK IF LOCAL PLAYER ENTERED TRIGGER IF SO ENABLE
    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<Stats>(out var stats) || !stats.isLocalPlayer) return;
        _canStartSteering = true;
        _localPlayerState = other.gameObject.GetComponent<Stats>();
        _localPlayerTransform = other.transform;
        _localGameObject = other.gameObject;
        _localRB =  _localGameObject.GetComponent<Rigidbody>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent<Stats>(out var stats) || !stats.isLocalPlayer) return;
        _canStartSteering = false;
        _localPlayerState = null;
        _localPlayerTransform = null;
        _localGameObject = null;
        _localRB = null;
    }

    private void OnEnable()
    {
        _globalState.OnDriverChanged += OnDriverChanged;
    }

    private void OnDisable()
    {
        _globalState.OnDriverChanged -= OnDriverChanged;
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(_globalState.currentShipDriver))
        {
            WaitForStartSteering();
        }

        if (_localPlayerState != null && _globalState.IsPlayerDriving(_localPlayerState.GetPlayerId()))
        {
            WaitForStopSteering();
        }

        //IF LOCAL PLAYER IS DRIVING COLLECT INPUT
        if (_localPlayerState == null) return;
        if (_globalState.IsPlayerDriving(_localPlayerState.GetPlayerId()))
        {
            CollectInput();
        }
    }

    private void FixedUpdate()
    {
        if (_globalState.currentShipDriver != null)
        {
            HandleBoatSteering();
        }
    }

    private void CollectInput()
    {
        _w = _forward.ReadValue<float>();
        _s = _backward.ReadValue<float>();
        _a = _left.ReadValue<float>();
        _d = _right.ReadValue<float>();
    }
    
    private void WaitForStartSteering()
    {
        //If local player can start steering and pressed E set new driver
        if (_canStartSteering && _startSteering.ReadValue<float>() > 0f)
        {
            _globalState.TrySetDriver(_localPlayerState.GetPlayerId());
        }
    }

    private void WaitForStopSteering()
    {
        if (_isSteering && _stopSteering.ReadValue<float>() > 0f)
        {
            _globalState.ReleaseDriver(_localPlayerState.GetPlayerId());
        }
    }
    
    private void HandleBoatSteering()
    {
        shipRb.AddForce(-transform.right * (forwardForce * _w), ForceMode.Force);
        shipRb.AddForce(transform.right * (backwardsForce * _s), ForceMode.Force);
        shipRb.AddTorque(-transform.up * (torqueForce * _a), ForceMode.Force);
        shipRb.AddTorque(transform.up * (torqueForce * _d), ForceMode.Force);
    }

    private void SaveRigidbodyState()
    {
        if (_localRB != null)
        {
            _savedRb.Mass = _localRB.mass;
            _savedRb.LinearDamping = _localRB.linearDamping;
            _savedRb.AngularDamping = _localRB.angularDamping;
            _savedRb.UseGravity = _localRB.useGravity;
            _savedRb.SavedConstraints = _localRB.constraints;
            _savedRb.SavedCollisionDetection = _localRB.collisionDetectionMode;
            _savedRb.SavedInterpolation = _localRB.interpolation;
        }
    }

    private void RestoreRigidbodyState()
    {
        _localGameObject.AddComponent<Rigidbody>();
        _localRB = _localGameObject.GetComponent<Rigidbody>();
        _localRB.mass = _savedRb.Mass;
        _localRB.linearDamping = _savedRb.LinearDamping;
        _localRB.angularDamping = _savedRb.AngularDamping;
        _localRB.useGravity = _savedRb.UseGravity;
        _localRB.constraints = _savedRb.SavedConstraints;
        _localRB.collisionDetectionMode = _savedRb.SavedCollisionDetection;
        _localRB.interpolation = _savedRb.SavedInterpolation;
    }

    private void OnDriverChanged(string playerId)
    {
        if (_localPlayerState == null) return;
        if (playerId == _localPlayerState.GetPlayerId())
        {
            SaveRigidbodyState();
            Destroy(_localRB);
            _localGameObject.transform.SetParent(this.transform);
            _isSteering = true;
            _localPlayerTransform.localPosition = _steeringPosition;
            _localPlayerTransform.localRotation = Quaternion.Euler(0f, _steeringRotation, 0f);
        }
        else if(string.IsNullOrEmpty(playerId))
        {
            _isSteering = false;
            _localGameObject.transform.SetParent(null);
            RestoreRigidbodyState();
        }
    }
}
