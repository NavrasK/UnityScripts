using UnityEngine;
using UnityEngine.Events;

// TODO Fix air control
// TODO Variable jump height
// TODO Hitbox pinching (https://www.youtube.com/watch?v=HCnZhs-92j0)
// TODO Improve slope handling / raycasting
// TODO One way platforms / fall through (only if grounded, and if held before becoming grounded, wait 0.2 sec)
// TODO Moving platforms
// TODO Determinate physics (https://www.youtube.com/watch?v=hG9SzQxaCm8)

[RequireComponent(typeof(Rigidbody2D))]
public class PlatformerCharacterController2D : MonoBehaviour {
	// Required user inputs
	[Header("Required Values")]
	[SerializeField] [Tooltip("Parent an empty to the bottom character and reference it here")]
	private Transform _playerBottom;
	[SerializeField] [Tooltip("Put ground objects in a layer and reference it here")]
	private LayerMask _groundLayer;
	[SerializeField]
	private float _moveSpeeed = 400f;
	[SerializeField]
	private float _jumpForce = 800f;
	
	// User tweakable values
	[Header("Tweaks")] [Space]
	[SerializeField] [Tooltip("If a player can change their trajectory in mid-air")]
	private bool _airControl = true;
	[Range(0, 0.3f)] [SerializeField] [Tooltip("How long a player has to jump after falling off a platform")]
	private float _coyoteTime = 0.1f;
	[Range(0, 0.3f)] [SerializeField] [Tooltip("How early a player can input a jump before they hit the ground")]
	private float _jumpBuffer = 0.2f;
	[Range(0, 0.3f)] [SerializeField]
	private float _movementSmoothing = 0.05f;
	[SerializeField] [Tooltip("True if base sprite is facing right")]
	private bool _spriteFacingRight = true;
	
	// Event system
	[Header("Events")] [Space]
	public UnityEvent OnLandEvent;
	
	// Movement variables
	private bool _hasJumped = false;
	private float _coyoteBuffer = 0f;
	private float _jumpInputBuffer = 0f;
	private float _horz = 0f;
	private float _vert = 0f;
	private bool _jump = false;
	
	// Core variables
	const float _groundedRadius = 0.2f;
	private bool _isGrounded;
	private Rigidbody2D _rb;
	private Vector3 _velocity = Vector3.zero;
	
	private void Awake() {
		_rb = GetComponent<Rigidbody2D>();
		if (_rb == null){
			Debug.LogError("PlatformerCharacterController2D: Rigidbody2D not found");
		}
		if (OnLandEvent == null) {
			OnLandEvent = new UnityEvent();
		}
	}
	
	private void Update() {
		_horz = Input.GetAxisRaw("Horizontal");
		_vert = Input.GetAxisRaw("Vertical");
		if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) {
			_jump = true;
		}
		if (_jumpInputBuffer > 0) {
			_jumpInputBuffer -= Time.deltaTime;
		} else {
			_jumpInputBuffer = 0;
		}
		
		if (_coyoteBuffer > 0) {
			_coyoteBuffer -= Time.deltaTime;
		} else {
			_coyoteBuffer = 0;
		}
	}
	
	private void FixedUpdate() {
		CheckGround();
		Move(_horz);
		if (_jump) {
			Jump();
			_jump = false;
		}
	}
	
	private void CheckGround() {
		bool wasGrounded = _isGrounded;
		Collider2D[] colliders = Physics2D.OverlapCircleAll(_playerBottom.position, _groundedRadius, _groundLayer);
		bool groundDetected = false;
		// Check if it detected any collisions aside from itself
		for (int i = 0; i < colliders.Length; i++) {
			if (colliders[i].gameObject != this.gameObject) {
				groundDetected = true;
			}
		}
		// If it did, player is grounded
		if (groundDetected) {
			_isGrounded = true;
			if (!wasGrounded) {
				OnLandEvent.Invoke();
				if (_jumpInputBuffer > 0) { 
					Jump();
				}
			}
			_hasJumped = false;
		} else {
			_isGrounded = false;
			if (wasGrounded && !_hasJumped && _coyoteTime > 0) {
				_coyoteBuffer = _coyoteTime;
			}
		}
	}
	
	public void Move(float dir) {
		if (_isGrounded || _airControl) {
			Vector3 targetVelocity = new Vector2(dir * _moveSpeeed * Time.deltaTime, _rb.velocity.y);
			_rb.velocity = Vector3.SmoothDamp(_rb.velocity, targetVelocity, ref _velocity, _movementSmoothing);
		}
		
		if ((dir > 0 && !_spriteFacingRight) || (dir < 0 && _spriteFacingRight)) {
			FlipSprite();
		}
	}
	
	public void Jump() {
		if (_isGrounded || _coyoteBuffer > 0) {
			_isGrounded = false;
			_rb.AddForce(new Vector2(0f, _jumpForce));
		} else {
			_jumpInputBuffer = _jumpBuffer;
		}
	}
	
	private void FlipSprite() {
		_spriteFacingRight = !_spriteFacingRight;
		Vector3 newScale = transform.localScale;
		newScale.x *= -1;
		transform.localScale = newScale;
	}
}