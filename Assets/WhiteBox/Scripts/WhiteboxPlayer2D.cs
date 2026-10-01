using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Unity 6 whitebox controller. Attach to Test_Player, not its camera.
// World units: with your current Grid, 1 unit = 1 tile.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class WhiteboxPlayer2D : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] public float moveSpeed = 5f;
    [Tooltip("Approximate rise of the player's feet in world units.")]
    [Min(0.1f)] public float jumpHeight = 3f;
    [Min(0.1f)] public float gravityScale = 2f;

    [Header("Double Jump")]
    [Tooltip("Allow one extra jump while airborne. Resets on landing.")]
    public bool enableDoubleJump = false;
    [Tooltip("Rise from the position where the second jump begins, in world units.")]
    [Min(0.1f)] public float doubleJumpHeight = 3f;

    [Header("Dash")]
    public bool enableDash = true;
    public bool allowAirDash = true;
    [Min(0.1f)] public float dashDistance = 3f;
    [Min(0.02f)] public float dashDuration = 0.15f;
    [Min(0f)] public float dashCooldown = 0.35f;

    [Header("Ground and Jump Assistance")]
    [Tooltip("Leave as Everything for the current whitebox. Triggers and this player's colliders are ignored.")]
    public LayerMask groundLayers = ~0;
    [Min(0.001f)] public float groundCheckDistance = 0.04f;
    [Tooltip("Small grace period after walking off a ledge. Set to 0 for strict jumps.")]
    [Min(0f)] public float coyoteTime = 0.08f;
    [Tooltip("Remember a jump pressed shortly before landing.")]
    [Min(0.02f)] public float jumpBufferTime = 0.12f;

    private Rigidbody2D body;
    private BoxCollider2D playerCollider;
    private PhysicsMaterial2D originalMaterial;
    private PhysicsMaterial2D testMaterial;
    private float originalGravityScale;
    private Vector2 startPosition;
    private float startRotation;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
    private float moveInput;
    private float facing = 1f;
    private float jumpBuffer;
    private float coyoteRemaining;
    private float groundLock;
    private float dashRemaining;
    private float dashSpeed;
    private float dashDirection;
    private float cooldownRemaining;
    private bool dashRequested;
    private bool resetRequested;
    private bool dashing;
    private bool airDashUsed;
    private bool doubleJumpUsed;
    private float knockbackRemaining;
    private float spikeProtectionRemaining;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<BoxCollider2D>();
        startPosition = body.position;
        startRotation = body.rotation;
        originalGravityScale = body.gravityScale;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.freezeRotation = true;
        body.linearDamping = 0f;
        body.gravityScale = gravityScale;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        playerCollider.isTrigger = false;

        // A temporary zero-friction material prevents sticking to vertical walls.
        originalMaterial = playerCollider.sharedMaterial;
        testMaterial = new PhysicsMaterial2D("Whitebox Player - Runtime")
        {
            friction = 0f,
            bounciness = 0f,
            hideFlags = HideFlags.HideAndDontSave
        };
        playerCollider.sharedMaterial = testMaterial;
    }

    private void Update()
    {
        bool left = false, right = false, jump = false, dash = false, reset = false;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            jump = keyboard.spaceKey.wasPressedThisFrame;
            dash = keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame;
            reset = keyboard.rKey.wasPressedThisFrame;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        jump = Input.GetKeyDown(KeyCode.Space);
        dash = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        reset = Input.GetKeyDown(KeyCode.R);
#endif
        moveInput = (right ? 1f : 0f) - (left ? 1f : 0f);
        if (moveInput != 0f) facing = Mathf.Sign(moveInput);
        if (jump) jumpBuffer = jumpBufferTime;
        dashRequested |= dash;
        resetRequested |= reset;
    }

    private void FixedUpdate()
    {
        if (resetRequested)
        {
            ResetToStart();
            return;
        }

        float dt = Time.fixedDeltaTime;
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
        groundLock = Mathf.Max(0f, groundLock - dt);
        spikeProtectionRemaining = Mathf.Max(0f, spikeProtectionRemaining - dt);
        if (knockbackRemaining > 0f)
        {
            knockbackRemaining = Mathf.Max(0f, knockbackRemaining - dt);
            body.gravityScale = gravityScale;
            jumpBuffer = 0f;
            dashRequested = false;
            return;
        }
        bool requestDash = dashRequested;
        dashRequested = false;

        if (dashing && (dashRemaining <= 0f || !enableDash))
        {
            dashing = false;
            body.linearVelocity = Vector2.zero;
        }

        bool grounded = groundLock <= 0f && body.linearVelocity.y <= 0.05f && CheckGround();
        if (!dashing && grounded)
        {
            coyoteRemaining = coyoteTime;
            airDashUsed = false;
            doubleJumpUsed = false;
        }
        else coyoteRemaining = Mathf.Max(0f, coyoteRemaining - dt);

        if (!dashing)
        {
            body.gravityScale = gravityScale;
            Vector2 velocity = body.linearVelocity;
            velocity.x = moveInput * moveSpeed;
            bool canGroundJump = grounded || coyoteRemaining > 0f;
            bool canDoubleJump = enableDoubleJump && !doubleJumpUsed;
            if (jumpBuffer > 0f && (canGroundJump || canDoubleJump))
            {
                bool isDoubleJump = !canGroundJump;
                float height = isDoubleJump ? doubleJumpHeight : jumpHeight;
                float gravity = Mathf.Abs(Physics2D.gravity.y * gravityScale);
                // Compensate approximately for Unity's discrete gravity step.
                velocity.y = Mathf.Sqrt(2f * gravity * Mathf.Max(0.1f, height)) + gravity * dt * 0.5f;
                if (isDoubleJump) doubleJumpUsed = true;
                jumpBuffer = 0f;
                coyoteRemaining = 0f;
                groundLock = 0.1f;
                grounded = false;
            }
            body.linearVelocity = velocity;

            if (requestDash && enableDash && cooldownRemaining <= 0f &&
                (grounded || (allowAirDash && !airDashUsed)))
            {
                dashing = true;
                airDashUsed = true;
                dashRemaining = Mathf.Max(0.02f, dashDuration);
                dashSpeed = dashDistance / dashRemaining;
                dashDirection = facing;
                cooldownRemaining = dashRemaining + dashCooldown;
                coyoteRemaining = 0f;
            }
        }

        if (dashing)
        {
            // Horizontal dash pauses gravity and cancels vertical velocity.
            // Partial last step keeps unobstructed distance near dashDistance.
            float step = Mathf.Min(dt, dashRemaining);
            body.gravityScale = 0f;
            body.linearVelocity = new Vector2(dashDirection * dashSpeed * step / dt, 0f);
            dashRemaining = Mathf.Max(0f, dashRemaining - step);
        }
        jumpBuffer = Mathf.Max(0f, jumpBuffer - dt);
    }

    // Called by hazards; movement must not overwrite knockback during this lock.
    public bool TrySpikeKnockback(Vector2 velocity, float controlLock, float protectionTime)
    {
        if (!isActiveAndEnabled || body == null || spikeProtectionRemaining > 0f)
            return false;
        dashing = false;
        dashRemaining = jumpBuffer = coyoteRemaining = 0f;
        dashRequested = false;
        groundLock = Mathf.Max(0.1f, controlLock);
        knockbackRemaining = Mathf.Max(Time.fixedDeltaTime, controlLock);
        spikeProtectionRemaining = Mathf.Max(knockbackRemaining, protectionTime);
        body.gravityScale = gravityScale;
        body.linearVelocity = velocity;
        body.WakeUp();
        return true;
    }

    private bool CheckGround()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;
        int count = body.Cast(Vector2.down, filter, groundHits, groundCheckDistance);
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].normal.y >= 0.65f) return true;
        }
        return false;
    }

    private void ResetToStart()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.position = startPosition;
        body.rotation = startRotation;
        body.gravityScale = gravityScale;
        jumpBuffer = coyoteRemaining = groundLock = dashRemaining = cooldownRemaining = 0f;
        dashing = airDashUsed = doubleJumpUsed = dashRequested = resetRequested = false;
        knockbackRemaining = spikeProtectionRemaining = 0f;
        body.WakeUp();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) return;
        moveInput = 0f;
        jumpBuffer = 0f;
        dashRequested = resetRequested = false;
    }

    private void OnDisable()
    {
        if (body == null) return;
        body.gravityScale = originalGravityScale;
        body.linearVelocity = Vector2.zero;
        dashing = false;
        dashRemaining = jumpBuffer = coyoteRemaining = groundLock = cooldownRemaining = 0f;
        dashRequested = resetRequested = airDashUsed = doubleJumpUsed = false;
        knockbackRemaining = spikeProtectionRemaining = 0f;
        moveInput = 0f;
    }

    private void OnDestroy()
    {
        if (playerCollider != null && playerCollider.sharedMaterial == testMaterial)
            playerCollider.sharedMaterial = originalMaterial;
        if (testMaterial != null) Destroy(testMaterial);
    }
}
