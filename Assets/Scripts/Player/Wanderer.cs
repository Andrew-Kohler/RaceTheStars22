using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// To Do:
// Add popup animation if you decide not to slow down while running

// Fix clipping into the wall at high speeds
// Fix the wall pressing animation
// Add jumping
// Maybe do crouching and crawling today too so I can spend tomorrow refactoring my whole physics engine

// I mean, a lot of other things, but right now those are "list items" and not "active dev"
// This is more for when I'm doing something, and that means I need to do other things, and those other things lead to...

public class Wanderer : MonoBehaviour
{

    // Initializations ----------------------------------

    // Unity components
    private Rigidbody2D wBody;
    private BoxCollider2D wCollider;
    private Animator wAnim;
    private AudioSource wAudio;
    private SpriteRenderer wSR;

    // Physics variables          
    [SerializeField]
    private float vCurrentGround = 0f;
    private float vCurrentAir = 0f;

    private float acModifier = .3f;      // Acceleration and deceleration
    private float acModifierAir = .6f;
    private float dcModifier = .7f;

    private float fModifier = .5f;       // Friction between player and ground
    private float gModifier = .3f;        // Gravity constant
    private float slopeModifier = 1.2f;     // Factor by which slopes affect momentum
    private float brakes = .09f;    // Factor by which vCurrentGround is altered to actually make acceleration a process

    private float jumpForce = 6.5f;

    private float vJog = 100f;   // Lower cap of the "jog" state
    private float vRun = 200f;  // Lower cap of the "run" state
    private float vMax = 300f;  // Normal speed cap
    private float vOverdrive = 400f;    // Overdrive speed cap

    private float walkAnimModifier = 70f;    // Divisors for animation manipulation
    private float jogAnimModifier = 150f;
    private float runAnimModifier = 300f;   // 70, 150, 300 // 100, 200, 300

    // Control variables
    private float xDirection = 0;
    [SerializeField]
    private float xSpeed;
    [SerializeField]
    private float ySpeed;
    [SerializeField]
    private float slopeSpeedFactor;

    // Slope check variables
    private Vector2 colliderSize;
    [SerializeField]
    private float slopeCheckDistance = 3f;
    [SerializeField]
    private LayerMask layerMask;   // Helps optimize raycast

    [SerializeField]
    private float slopeDownAngle;
    private float slopeDownAngleOld;
    [SerializeField]
    private float playerAngle;
    private float playerAngleOld;
    [SerializeField]
    private Vector2 slopeNormalPerp;

    private float slopeSideAngle;

    // Wall check variables
    private float wallCheckDistance = .8f;
    [SerializeField]
    private float wallAngle;

    // State booleans
    [SerializeField]
    private bool isGrounded = true;
    private bool isOnSlope;
    [SerializeField]
    private bool isAgainstWall;
    private bool activeCoroutine;
    private bool changingDirection = false;

    // Unity GameObject tags
    private string GroundTag = "Ground";

    // Animation variables
    private string aVCurrentGround = "vCurrentGround_A";
    private string aGroundPlaybackSpeed = "groundSpeedMod_A";
    private string aXSpeed = "xSpeed_A";
    private string aYSpeed = "ySpeed_A";

    private string aMovement = "isMoving_A";
    private string aGroundRunTurn = "isTurningFast_A";
    private string aChangeDirection = "isChangingDirection_A";
    private string aGrounded = "isGrounded_A";
    private string aActiveCoroutine = "activeCoroutine_A";

    // Start and Updates --------------------------------

    // Start is called before the first frame update
    void Start()
    {
        ComponentRetrieval();
    }

    // Update is called once per frame
    void Update()
    {
        SlopeCheck();
        LRMovement();
        JumpMovement();
        WallCheck();
        AnimateWanderer();
    }

    void LateUpdate()
    {
        WallCheck();
    }

    // Player Behavior ----------------------------------

    void LRMovement()
    {
        // GetAxisRaw is ONLY 0, 1, -1; GetAxis actually takes turnaround time and changes the value
        xDirection = getXDirection(); // Gets input from L/R arrows, A, and D by default; 1, 0, or -1

        if (isGrounded)
        {
            slopeSpeedFactor = (slopeModifier * Mathf.Sin((playerAngle * Mathf.PI) / 180)); // Code responsible for speed changes on slopes
            if (vCurrentGround < 301 && vCurrentGround > -301)
                vCurrentGround -= slopeSpeedFactor;

            if (xDirection == 1)  // The player wants to go right
            {
                if (vCurrentGround < 0) // If the player is moving to the left
                {
                    vCurrentGround += dcModifier;  // Slow them down
                    changingDirection = true;
                    if (vCurrentGround >= 0) // This is a decelaration quirk from Sonic the Hedgehog - I think I owe the blue blur an easter egg, so here it is
                    {
                        vCurrentGround = 0.5f;
                    }
                }
                else if (vCurrentGround < vMax)  // If the player is moving right
                {
                    vCurrentGround += acModifier;
                    changingDirection = false;
                    if (vCurrentGround >= vMax)   // Makes sure you don't exceed top velocity
                    {
                        vCurrentGround = vMax;
                    }
                }
            }

            else if (xDirection == -1)    // The player wants to go left
            {
                if (vCurrentGround > 0)  // The player is currently moving right
                {
                    vCurrentGround -= dcModifier; // Slow down
                    changingDirection = true;
                    if (vCurrentGround <= 0) // Decelartion quirk again
                    {
                        vCurrentGround = -0.5f;
                    }
                }
                else if (vCurrentGround > -vMax)  // The player is currently moving left (speed between 0 and left max)
                {
                    vCurrentGround -= acModifier;  // FASTER
                    changingDirection = false;
                    if (vCurrentGround <= -vMax)  // Speed cap
                    {
                        vCurrentGround = -vMax;
                    }
                }
            }
            else    // The player isn't touching left or right
            {
                vCurrentGround -= Mathf.Min(Mathf.Abs(vCurrentGround), fModifier) * Mathf.Sign(vCurrentGround);    // Passive deceleration
                changingDirection = false;
            }

            xSpeed = vCurrentGround * Mathf.Cos((playerAngle * Mathf.PI) / 180);
            ySpeed = vCurrentGround * Mathf.Sin((playerAngle * Mathf.PI) / 180);

            wBody.position += new Vector2(xSpeed * brakes, ySpeed * brakes) * Time.deltaTime;
            //wBody.velocity.Set(xSpeed * brakes, ySpeed * brakes);
            //wBody.velocity += new Vector2(xSpeed * brakes, ySpeed * brakes);
            //wBody.AddForce(new Vector2(xSpeed * brakes, ySpeed * brakes), ForceMode2D.Impulse);
        }
        else    // If airborne
        {
            wBody.position += new Vector2(xSpeed * brakes, ySpeed * brakes) * Time.deltaTime;
        }
        
    }

    void JumpMovement()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            wBody.AddForce(new Vector2(0f, 10f), ForceMode2D.Impulse);
            xSpeed -= jumpForce * Mathf.Sin((playerAngle * Mathf.PI) / 180);    // Allows for slopes to affect jump height - you get a bigger jump at the end of a slope, for example
            ySpeed -= jumpForce * Mathf.Cos((playerAngle * Mathf.PI) / 180);
            isGrounded = false;
        }
    }

    // Player Animation + Animation Coroutines ----------
    float calculateGroundModifier() // Changes animation speed of walking, jogging, and running
    {
        float mod = 1;
        float speed = Mathf.Abs(vCurrentGround);
        if (speed < vJog) // Walking
        {
            mod = speed / walkAnimModifier;
        }
        else if(speed < vRun)    // Jogging  
        {
            mod = speed / jogAnimModifier;
        }
        else if(speed <= vMax)   // Running
        {
            mod = speed / runAnimModifier;
        }

        return mod;
    }

    void AnimateWanderer()
    {
        wBody.SetRotation(playerAngle);

        wAnim.SetFloat(aVCurrentGround, Mathf.Abs(vCurrentGround));         // Sends ground speed and animation speed to the animator
        wAnim.SetFloat(aGroundPlaybackSpeed, calculateGroundModifier());
        wAnim.SetFloat(aYSpeed, wBody.velocity.y);                                    // Sends y speed to animator
        wAnim.SetBool(aChangeDirection, changingDirection);                 // Tell the animator if we're changing directions
        wAnim.SetBool(aGrounded, isGrounded);                               // Tells the animator if we're grounded
        wAnim.SetBool(aActiveCoroutine, activeCoroutine);                   // Informs the animator of an active coroutine           

        if (isGrounded)
        {
            if (vCurrentGround != 0f)   // If we're moving on the ground
            {
                wAnim.SetBool(aMovement, true);
                if (vCurrentGround > 0f)         // This set of if statements determines the direction the player appears to be facing
                {
                    wSR.flipX = false;
                }
                else if (vCurrentGround < 0f)
                {
                    wSR.flipX = true;
                }

                if (Mathf.Abs(vCurrentGround) > vRun) // If the player is running 
                {
                    if (vCurrentGround / getXDirection() < 0)    // If the current directional input is opposite the current direction of motion
                    {
                        wAnim.SetBool(aGroundRunTurn, true);
                    }
                    else
                    {
                        wAnim.SetBool(aGroundRunTurn, false);
                    }
                }

            }
            else   // If we aren't moving on the ground
            {
                wAnim.SetBool(aMovement, false);
                wAnim.SetBool(aGroundRunTurn, false);
            }
        }
        else if(!isGrounded)   // If we're airborne
        {
            if (wBody.velocity.y < 0 && activeCoroutine == false)
            {
                StartCoroutine(TransitionJumpCR());
            }
        }

        IEnumerator TransitionJumpCR()
        {
            activeCoroutine = true;
            yield return new WaitForSeconds(.2f);
            activeCoroutine = false;
        }

    }

    // Miscellaneous Functions --------------------------
    private void ComponentRetrieval()   // Instantiates all of those components we need
    {
        wBody = GetComponent<Rigidbody2D>();
        wCollider = GetComponent<BoxCollider2D>();
        wAnim = GetComponent<Animator>();
        wAudio = GetComponent<AudioSource>();
        wSR = GetComponent<SpriteRenderer>();

        colliderSize = wCollider.size; 
    }

    private void OnCollisionEnter2D(Collision2D collision)  // Allows you to detect collisions between game objects
    {
        if (collision.gameObject.CompareTag(GroundTag))
        {
            isGrounded = true;
        }
    }

    private float getXDirection()
    {
        xDirection = Input.GetAxisRaw("Horizontal");
        return xDirection;
    }

    private void SlopeCheck()
    {
        Vector2 checkPos = transform.position - new Vector3(0f, colliderSize.y / 2);    // Establishes the position that the raycast originates from

        SlopeCheckHorizontal(checkPos); // Together, these account for the horizontal and vertical components of a slope
        SlopeCheckVertical(checkPos);

    }

    private void SlopeCheckHorizontal(Vector2 checkPos) 
    {
        RaycastHit2D slopeHitFront = Physics2D.Raycast(checkPos, transform.right, slopeCheckDistance, layerMask);
        RaycastHit2D slopeHitBack = Physics2D.Raycast(checkPos, -transform.right, slopeCheckDistance, layerMask);

        if (slopeHitFront)
        {
            isOnSlope = true;
            slopeSideAngle = Vector2.Angle(slopeHitFront.normal, Vector2.up);
        }
        else if (slopeHitBack)
        {
            isOnSlope = true;
            slopeSideAngle = Vector2.Angle(slopeHitBack.normal, Vector2.up);
        }
        else
        {
            slopeSideAngle = 0.0f;
            isOnSlope = false;
        }
    }

    private void SlopeCheckVertical(Vector2 checkPos)
    {
        RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, slopeCheckDistance, layerMask);

        if (hit)    // If the raycast hits something
        {

            slopeNormalPerp = Vector2.Perpendicular(hit.normal).normalized;    // Gets the vector perpendicular to the raycast
            slopeDownAngle = Vector2.Angle(hit.normal, Vector2.up); // Gets the angle of the raycast


            playerAngle = Vector2.Angle(slopeNormalPerp, Vector2.left);
            if(slopeNormalPerp.y > 0.0f)
            {
                playerAngle = playerAngle * -1f;
            }
            

            if(slopeDownAngle != slopeDownAngleOld)
            {
                isOnSlope = true;
            }
            slopeDownAngleOld = slopeDownAngle;

            Debug.DrawRay(hit.point, slopeNormalPerp, Color.cyan);
            Debug.DrawRay(hit.point, hit.normal, Color.blue);   // Returns the normal of the surface that's been hit
        }
    }

    private void WallCheck()
    {
        Vector2 checkPos = transform.position;    // Establishes the position that the raycast originates from

        WallCheckHorizontal(checkPos);
    }

    private void WallCheckHorizontal(Vector2 checkPos)
    {
        RaycastHit2D wallHitLeft = Physics2D.Raycast(checkPos, -transform.right, wallCheckDistance, layerMask);
        RaycastHit2D wallHitRight = Physics2D.Raycast(checkPos, transform.right, wallCheckDistance, layerMask);

        if (wallHitLeft)
        {
            isAgainstWall = true;
            wallAngle = Vector2.Angle(wallHitLeft.normal, Vector2.right); // Gets the angle of the raycast
            if(wallAngle > -3f && wallAngle < 3f)    // If it's perpendicular to the wall
            {
                if(xSpeed < 0) // Stops the player. This would need to be rewritten with the introduction of movable objects
                {
                    wBody.position -= new Vector2(xSpeed * brakes, ySpeed * brakes) * Time.deltaTime;
                    vCurrentGround = 0;
                    xSpeed = 0;
                    ySpeed = 0;

                }
            }
            Debug.DrawRay(wallHitLeft.point, wallHitLeft.normal, Color.red);
        }

        if (wallHitRight)
        {
            isAgainstWall = true;
            wallAngle = Vector2.Angle(wallHitRight.normal, Vector2.left); // Gets the angle of the raycast
            if (wallAngle > -3f && wallAngle < 3f)    // If it's perpendicular to the wall
            {
                if (xSpeed > 0) // Stops the player. This would need to be rewritten with the introduction of movable objects
                {
                    wBody.position -= new Vector2(xSpeed * brakes, ySpeed * brakes) * Time.deltaTime;
                    vCurrentGround = 0;
                    xSpeed = 0;
                    ySpeed = 0;

                }
            }
            Debug.DrawRay(wallHitRight.point, wallHitRight.normal, Color.green);
        }
        else
        {
            isAgainstWall = false;
        }
    }

} // End of code
