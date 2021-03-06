using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// TODO Determinate physics (https://www.youtube.com/watch?v=hG9SzQxaCm8)
// TODO Variable jump height

// STRETCH GOALS
// Hitbox pinching (https://www.youtube.com/watch?v=HCnZhs-92j0)
// One way platforms / fall through (only if grounded, and if held before becoming grounded, wait 0.2 sec) 
// Crouch / slide
// Wall jump / slide / ledge grab
// Moving platform support

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlatformerCharacterController2D : MonoBehaviour {
	// Required user inputs
	[Header("Required Values")]
	[SerializeField] [Tooltip("Put ground objects in a layer and reference it here")]
	private LayerMask _groundLayer;
	[SerializeField] [Tooltip("Width of the area used to determine if the character is touching the ground")]
	private float _groundedBoxWidth = 1f;
	[Range(0.1f, 2f)] [SerializeField] [Tooltip("Height of the area used to determine if the character is touching the ground")]
	private float _groundedBoxHeight = 0.25f;
	[SerializeField]
	private float _moveSpeed = 400f;
	[SerializeField]
	private float _jumpForce = 800f;
	[Min(0)] [SerializeField] [Tooltip("Number of jumps your player can execute without touching the ground by default")]
	private int _totalJumps = 1;

	// User tweakable values
	[Header("Tweaks")] [Space]
	[SerializeField] [Tooltip("If a player can change their trajectory in mid-air")]
	private bool _airControl = true;
	[Range(0, 0.3f)] [SerializeField] [Tooltip("How long a player has to jump after falling off a platform")]
	private float _coyoteTime = 0.1f;
	[Range(0, 0.3f)] [SerializeField] [Tooltip("How early a player can input a jump before they hit the ground")]
	private float _jumpBuffer = 0.2f;
	[Range(0f, 1f)] [SerializeField] [Tooltip("Fraction of your base jump height used in extra jumps")]
	private float _extraJumpHeight = 1f;
	[Range(0, 0.3f)] [SerializeField]
	private float _movementSmoothing = 0.05f;
	[Min(0)] [SerializeField] [Tooltip("How long before the player can jump again")]
	private float _jumpCooldown = 0.05f;
	[SerializeField] [Tooltip("True if base sprite is facing right")]
	private bool _spriteFacingRight = true;
	[SerializeField] [Tooltip("Show the bounding box for debugging / tweaking (make sure Gizmos are enabled)")]
	private bool _showGizmos = false;

	// Event system
	[Header("Events")] [Space]
	public UnityEvent OnLandEvent;

	// Movement variables
	private int _jumpsRemaining = 0;
	private float _coyoteBuffer = 0f;
	private float _jumpInputBuffer = 0f;
	private float _horz = 0f;
	private float _vert = 0f;
	private bool _jump = false;

	// Core variables
	private bool _isGrounded;
	private bool _canJump = true;
	private Rigidbody2D _rb;
	private SpriteRenderer _spr;
	private Vector3 _velocity = Vector3.zero;

	private void Awake() {
		_rb = GetComponent<Rigidbody2D>();
		_spr = GetComponent<SpriteRenderer>();
		if (_rb == null) {
			Debug.LogError("PlatformerCharacterController2D: Rigidbody2D component not found");
		}
		if (_spr == null) {
			Debug.LogError("PlatformerCharacterController2D: SpriteRenderer component not found");
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
		// Centers boxcast on bottom of sprite
		Vector2 boxcastCenter = new Vector2(_spr.bounds.center.x, _spr.bounds.center.y - _spr.bounds.size.y / 2);
		Vector2 boxcastSize = new Vector2(_groundedBoxWidth, _groundedBoxHeight);
		Collider2D[] colliders = Physics2D.OverlapBoxAll(boxcastCenter, boxcastSize, 0f, _groundLayer);
		if (_showGizmos) {
			ShowBoxGizmo(boxcastCenter, boxcastSize);
		}
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
			_jumpsRemaining = _totalJumps;
		} else {
			_isGrounded = false;
			if (wasGrounded && _jumpsRemaining > 0 && _coyoteTime > 0) {
				_coyoteBuffer = _coyoteTime;
			}
		}
	}

	private void ShowBoxGizmo(Vector2 center, Vector2 size) {
		// Show the ground bounding box
		Color rayColour;
		if (_isGrounded) {
			rayColour = Color.green;
		} else {
			rayColour = Color.red;
		}
		Vector3 tl = new Vector3(center.x - size.x / 2, center.y - size.y / 2, 0);
		Vector3 bl = new Vector3(center.x - size.x / 2, center.y + size.y / 2, 0);
		Vector3 tr = new Vector3(center.x + size.x / 2, center.y - size.y / 2, 0);
		Vector3 br = new Vector3(center.x + size.x / 2, center.y + size.y / 2, 0);
		Debug.DrawLine(tl, tr, rayColour, 0);
		Debug.DrawLine(bl, br, rayColour, 0);
		Debug.DrawLine(tl, bl, rayColour, 0);
		Debug.DrawLine(tr, br, rayColour, 0);
	}

	private void Move(float dir) {
		if (_isGrounded || _airControl) {
			Vector3 targetVelocity = new Vector2(dir * _moveSpeed * Time.deltaTime, _rb.velocity.y);
			_rb.velocity = Vector3.SmoothDamp(_rb.velocity, targetVelocity, ref _velocity, _movementSmoothing);
		}

		if ((dir > 0 && !_spriteFacingRight) || (dir < 0 && _spriteFacingRight)) {
			FlipSprite();
		}
	}

	private void Jump() {
		if (_canJump && (_jumpsRemaining > 0 || _coyoteBuffer > 0)) {
			_isGrounded = false;
			if (_jumpsRemaining < _totalJumps) {
				_rb.AddForce(new Vector2(0f, _jumpForce * _extraJumpHeight));
			} else {
				_rb.AddForce(new Vector2(0f, _jumpForce));
			}
			_jumpsRemaining--;
			StartCoroutine(JumpReset());
		} else {
			_jumpInputBuffer = _jumpBuffer;
		}
	}

	IEnumerator JumpReset() {
		_canJump = false;
		yield return new WaitForSeconds(_jumpCooldown);
		_canJump = true;
    }

	private void FlipSprite() {
		_spriteFacingRight = !_spriteFacingRight;
		Vector3 newScale = transform.localScale;
		newScale.x *= -1;
		transform.localScale = newScale;
	}

	public bool IsGrounded() {
		return _isGrounded;
    }

	public bool FacingRight() {
		return _spriteFacingRight;
    }

	public Vector3 GetVelocity() {
		return _velocity;
    }

	public int RemainingJumps() {
		return _jumpsRemaining;
    }

	public bool IsMoving() {
		return Mathf.Abs(_rb.velocity.x) > 0.01f;
    }
}