using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMovement : MonoBehaviour
{
    [SerializeField] private float acceleration;
    [SerializeField] private float maximumVelocity;
    [SerializeField] private float frictionCoefficient;
    [SerializeField] private float gravity;
    [SerializeField] private float jumpForce;
    [SerializeField] private float mouseX;
    [SerializeField] private float mouseY;
    [SerializeField] private Transform weaponHolder;
    
    private PlayerInput _playerInput;
    private CharacterController _characterController;
    private Vector3 _velocity;
    private Vector3 _acceleration;

    private void Start()
    {
        _playerInput = GetComponent<PlayerInput>();
        _characterController = GetComponent<CharacterController>();
        _playerInput.actions.FindAction("Movement").performed += SetAcceleration;
        _playerInput.actions.FindAction("Movement").canceled += RemoveAcceleration;
        _playerInput.actions.FindAction("Jump").performed += Jump;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetAcceleration(InputAction.CallbackContext pCallback)
    {
        Vector2 inputVector = pCallback.ReadValue<Vector2>();
        _acceleration = new Vector3(inputVector.x, 0, inputVector.y);
    }

    private void RemoveAcceleration(InputAction.CallbackContext pCallback)
    {
        _acceleration = Vector3.zero;
    }

    private void Accelerate()
    {
        _velocity += new Vector3(_acceleration.x, 0, _acceleration.z) * acceleration;
    }

    private void Move()
    {
        if (new Vector3(_velocity.x, 0, _velocity.z).magnitude > maximumVelocity)
        {
            Vector3 constrainedVelocityXZ = new Vector3(_velocity.x, 0, _velocity.z).normalized * maximumVelocity;
            _velocity = new Vector3(constrainedVelocityXZ.x, _velocity.y, constrainedVelocityXZ.z);
        }
        _characterController.Move(transform.rotation * (_velocity * Time.deltaTime));
    }

    private void Friction()
    {
        _velocity *= frictionCoefficient;
    }

    private void Jump(InputAction.CallbackContext pCallback) 
    {
        if (_characterController.isGrounded)
            _velocity.y = jumpForce;
    }

    private void Gravity()
    {
        if (!_characterController.isGrounded)
            _velocity.y += gravity;
    }
    
    private void Rotate()
    {
        Vector2 mouseDelta = _playerInput.actions.FindAction("Mouse").ReadValue<Vector2>();
        transform.Rotate(new Vector3(0,mouseDelta.x,0) * mouseX);
        if (weaponHolder.localRotation.x < -0.65f && mouseDelta.y > 0
            || weaponHolder.localRotation.x > 0.5f && mouseDelta.y < 0)
            return;
        weaponHolder.Rotate(new Vector3(-mouseDelta.y,0,0) * mouseY);
    }

    private void Update()
    {
        Rotate();
        Friction();
        Gravity();
        Accelerate();
        Move();
    }
}