
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    // mouvement et saut
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jetpackForce = 8f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float wallJumpForce = 16f;
    [SerializeField] private float wallJumpHorizontalForce = 8f;
    [SerializeField] private float wallSlideSpeed = 2f;

    // rotation du joueur
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float normalizeRotationSpeed = 3f;
    [SerializeField] private float maxTiltAngle = 20f;

    // carburant du jetpack
    [SerializeField] private float fuel = 100f;
    [SerializeField] private float fuelBurnRate = 18f;
    [SerializeField] private float fuelRefillRate = 20f;
    [SerializeField] private float normalGravityScale = 3f;
    [SerializeField] private float softDescentGravityScale = 0f;

    // détection sol et murs
    [SerializeField] private float boxLength = 1f;
    [SerializeField] private float boxHeight = 0.2f;
    [SerializeField] private float wallCheckDistance = 0.6f;
    [SerializeField] private Transform groundPosition;
    [SerializeField] private Transform wallCheckPosition;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private Slider fuelSlider;

    // contrôle perso et jump
    private float moveInput;
    private bool isFlying;
    private bool jumpPressed;
    private bool jumpBuffered;
    private float jumpBufferTimer;
    private float jumpBufferTime = 0.15f;
    private float coyoteTimer;
    private float coyoteTime = 0.15f;

    private bool canDoubleJump;
    private bool grounded;
    private bool touchingWall;
    private bool isWallSliding;
    private bool isWallJumping;
    private float wallJumpingDirection;
    private float wallJumpingTime = 0.2f;
    private float wallJumpingCounter;
    private float wallJumpingDuration = 0.4f;

    private Collider2D[] groundColliders = new Collider2D[1];
    private Rigidbody2D rb;
    private float currentFuel;
    private bool isFacingRight = true;

    private void Awake()
    {
        
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        currentFuel = fuel;
    }

    private void Update()
    {
        // déplacement et saut
        moveInput = Input.GetAxisRaw("Horizontal");
        isFlying = Input.GetKey(KeyCode.E) && currentFuel > 0f;
        jumpPressed = Input.GetButtonDown("Jump");
        fuelSlider.value = currentFuel / fuel;

        
        if (jumpPressed)
        {
            jumpBuffered = true;
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
            if (jumpBufferTimer <= 0f) jumpBuffered = false;
        }

        WallSlide();
        WallJump();

        if (!isWallJumping)
        {
            Flip();
        }
    }

    private void FixedUpdate()
    {
        // vérification sol
        grounded = Physics2D.OverlapBoxNonAlloc(
            groundPosition.position,
            new Vector2(boxLength, boxHeight),
            0f,
            groundColliders,
            groundLayer
        ) > 0;

        
        if (grounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }

        // detection mur
        touchingWall = Physics2D.Raycast(transform.position, Vector2.right * (isFacingRight ? 1 : -1), wallCheckDistance, wallLayer);

        // Mvmt joueur
        if (!isWallJumping)
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
        }

        HandleJump();

        // utilisation du jetpack pour planer
        if (isFlying)
        {
            rb.AddForce(Vector2.up * jetpackForce, ForceMode2D.Force);
            currentFuel -= fuelBurnRate * Time.fixedDeltaTime;
            currentFuel = Mathf.Clamp(currentFuel, 0f, fuel);
        }
        // descente douce après vol
        else if (!grounded && !touchingWall && rb.linearVelocity.y < 0f && !Input.GetKey(KeyCode.Space))
        {
            rb.gravityScale = softDescentGravityScale;
        }
        else
        {
            rb.gravityScale = normalGravityScale;
        }

        // recharge carburant au sol
        if (grounded)
        {
            RefillFuel();
        }

        // inclinaison visuelle du perso selon la direction
        float targetAngle = Mathf.Clamp(-moveInput * maxTiltAngle, -maxTiltAngle, maxTiltAngle);
        float smoothAngle = Mathf.LerpAngle(rb.rotation, targetAngle, rotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(smoothAngle);

        // retour à la position verticale au sol
        if (grounded && Mathf.Approximately(moveInput, 0f))
        {
            float uprightAngle = Mathf.LerpAngle(rb.rotation, 0f, normalizeRotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(uprightAngle);
        }
    }

    private void HandleJump()
    {
        // saut normal
        if (jumpBuffered && coyoteTimer > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            canDoubleJump = true;
            isWallJumping = false;
            jumpBuffered = false;
        }
        // double saut
        else if (jumpBuffered && canDoubleJump && !grounded && !touchingWall)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            canDoubleJump = false;
            jumpBuffered = false;
        }
    }

    private void WallSlide()
    {

        if (touchingWall && !grounded && moveInput != 0)
        {
            isWallSliding = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Clamp(rb.linearVelocity.y, -wallSlideSpeed, float.MaxValue));
        }
        else
        {
            isWallSliding = false;
        }
    }

    private void WallJump()
    {
        // détection début du WallJump
        if (isWallSliding)
        {
            isWallJumping = false;
            wallJumpingDirection = -transform.localScale.x;
            wallJumpingCounter = wallJumpingTime;

            CancelInvoke(nameof(StopWallJumping));
        }
        else
        {
            wallJumpingCounter -= Time.deltaTime;
        }

        // exécution Wall Jump
        if (jumpPressed && wallJumpingCounter > 0f)
        {
            isWallJumping = true;
            rb.linearVelocity = new Vector2(wallJumpingDirection * wallJumpHorizontalForce, wallJumpForce);
            wallJumpingCounter = 0f;
            canDoubleJump = true;

            if (transform.localScale.x != wallJumpingDirection)
            {
                isFacingRight = !isFacingRight;
                Vector3 localScale = transform.localScale;
                localScale.x *= -1f;
                transform.localScale = localScale;
            }

            Invoke(nameof(StopWallJumping), wallJumpingDuration);
        }
    }

    private void StopWallJumping()
    {
        isWallJumping = false;
    }

    private void Flip()
    {
        // flip le sprite du joueur 
        if ((isFacingRight && moveInput < 0f) || (!isFacingRight && moveInput > 0f))
        {
            isFacingRight = !isFacingRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }

    private void RefillFuel()
    {
        // recharge du carburant
        if (currentFuel < fuel)
        {
            currentFuel += fuelRefillRate * Time.fixedDeltaTime;
            currentFuel = Mathf.Clamp(currentFuel, 0f, fuel);
        }
    }
}
