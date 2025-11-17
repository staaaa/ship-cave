using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterController : MonoBehaviour
{
    [SerializeField] private GroundChecker groundChecker;
    [SerializeField] private WaterChecker waterChecker;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float jumpForce = 2f;
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private float maxVerticalAngle = 80f;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform holdingPoint;

    [Header("Smoothing")]
    [SerializeField] private float yawSmooth = 15f;     
    [SerializeField] private float cameraSmooth = 10f; 
    
    [Header("Water")]
    [SerializeField] private float waterMovementSpeed = 3f;
    private Vector3 _currentWaterVelocity = Vector3.zero;

    private InputAction _moveAction;
    private Rigidbody _rb;
    private InputAction _jumpAction;
    private InputAction _lookAction;

    private GlobalState _globalState;

    private Vector2 _moveVector;
    private Vector2 _lookVector;

    private float _targetYaw = 0f;   
    private float _targetPitch = 0f; 
    private float _bodyPitch = 0f;

    private bool _submerged = false;

    private bool IsGrounded => groundChecker != null && groundChecker.IsGrounded;
    private bool IsInWater => waterChecker != null && waterChecker.IsInWater;

    private Stats _playerStats;

    private void Awake()
    {
        _playerStats = GetComponent<Stats>();
        if (_playerStats.isLocalPlayer == false)
        {
            this.enabled = false;
        }
        _globalState = FindFirstObjectByType<GlobalState>();
    }

    void Start()
    {
        _rb = GetComponent<Rigidbody>();

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        inputActions.FindActionMap("Player").Enable();

        _moveAction = inputActions.FindAction("Move");
        _lookAction = inputActions.FindAction("Look");
        _jumpAction = inputActions.FindAction("Jump");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform == null)
            Debug.LogError("Camera Transform nie jest przypisany!");
        
        _targetYaw = transform.eulerAngles.y;
        if (cameraTransform != null)
            _targetPitch = cameraTransform.localEulerAngles.x > 180f ? cameraTransform.localEulerAngles.x - 360f : cameraTransform.localEulerAngles.x;
        _bodyPitch = transform.eulerAngles.x > 180f ? transform.eulerAngles.x - 360f : transform.eulerAngles.x;
    }

    void Update()
    {
        CollectInput();
        ApplyJumpForce();
    }

    private void OnEnable()
    {
        _globalState.OnDriverChanged += OnDriverChanged;
    }

    private void OnDisable()
    {
        _globalState.OnDriverChanged -= OnDriverChanged;
    }

    private void OnDriverChanged(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            _rb = GetComponent<Rigidbody>();
        }
    }

    private void LateUpdate()
    {
        ApplyCameraMovement();
    }

    void ApplyJumpForce()
    {
        if (_jumpAction != null && _jumpAction.ReadValue<float>() > 0f && IsGrounded)
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void ApplyCameraMovement()
    {
        if (_lookAction != null)
        {
            _lookVector = _lookAction.ReadValue<Vector2>();
        
            float mouseX = _lookVector.x * mouseSensitivity * Time.fixedDeltaTime;
            float mouseY = _lookVector.y * mouseSensitivity * Time.fixedDeltaTime;

            _targetYaw += mouseX;
            _targetPitch -= mouseY;

            if (IsInWater)
            {
                // if pitch goes beyond limit, transfer excess to body pitch
                if (_targetPitch > maxVerticalAngle)
                {
                    float excess = _targetPitch - maxVerticalAngle;
                    _targetPitch = maxVerticalAngle;
                    _bodyPitch += excess;
                }
                else if (_targetPitch < minVerticalAngle)
                {
                    float excess = _targetPitch - minVerticalAngle; // negative
                    _targetPitch = minVerticalAngle;
                    _bodyPitch += excess;
                }

                _bodyPitch = Mathf.Clamp(_bodyPitch, -89f, 89f);
            }
            else
            {
                _targetPitch = Mathf.Clamp(_targetPitch, minVerticalAngle, maxVerticalAngle);
            }
        }
    
        if (cameraTransform != null)
        {
            float currentPitch = cameraTransform.localEulerAngles.x;
            currentPitch = currentPitch > 180f ? currentPitch - 360f : currentPitch;

            float smoothedPitch = Mathf.Lerp(currentPitch, _targetPitch, Time.deltaTime * cameraSmooth);
            cameraTransform.localRotation = Quaternion.Euler(smoothedPitch, 0f, 0f);

            if (holdingPoint != null)
            {
                float distanceFromCamera = 1.5f;
                Vector3 targetPos = cameraTransform.position + cameraTransform.forward * distanceFromCamera;
                holdingPoint.position = targetPos;
                holdingPoint.rotation = cameraTransform.rotation;
            }
        }
    }

    void FixedUpdate()
    {
        if (_playerStats.IsSteeringBoat())
        {
            return;
        }
        if (IsInWater)
        {
            _submerged = true;
            ApplyMovementInWater();
            if (_jumpAction != null && _jumpAction.ReadValue<float>() > 0f && IsInWater)
            {
                _rb.AddForce(Vector3.up * waterMovementSpeed, ForceMode.Force);
            }
        }
        else
        {
            if (_submerged)
            {
                ResetRotation();
                _submerged = false;
            }
            ApplyMovementOnLand();
        }
        RotatePlayer();
    }

    private void RotatePlayer()
    {
        Quaternion currentRot = _rb.rotation;
        Quaternion targetRot = Quaternion.Euler(_bodyPitch, _targetYaw, 0f);
        Quaternion smoothedRot = Quaternion.Slerp(currentRot, targetRot, Mathf.Clamp01(yawSmooth * Time.fixedDeltaTime));
        _rb.MoveRotation(smoothedRot);
    }
    

    private void CollectInput()
    {
        if (_moveAction != null) _moveVector = _moveAction.ReadValue<Vector2>();
        if (_lookAction != null) _lookVector = _lookAction.ReadValue<Vector2>();
    }

    private void ApplyMovementOnLand()
    {
        Vector3 moveDirection = (transform.forward * _moveVector.y + transform.right * _moveVector.x);
        if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

        Vector3 targetPosition = _rb.position + moveDirection * movementSpeed * Time.fixedDeltaTime;
        _rb.MovePosition(targetPosition);
    }

    private void ApplyMovementInWater()
    {
        Vector3 moveDirection = (cameraTransform.forward * _moveVector.y + cameraTransform.right * _moveVector.x).normalized;

        if (moveDirection.sqrMagnitude < 0.01f)
            return;

        if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, 0.6f))
        {
            moveDirection = Vector3.ProjectOnPlane(moveDirection, hit.normal);
        }

        _rb.AddForce(moveDirection * waterMovementSpeed, ForceMode.Force);
    }
    
    private void ResetRotation()
    {
        Vector3 currentRotation = _rb.rotation.eulerAngles;
        Quaternion newRotation = Quaternion.Euler(0f, currentRotation.y, 0f);
        this.transform.rotation = newRotation;
    }
}
