using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Note: Raycasts could potentially be deloaded when not immediately needed to save resources
// Find something that isn't velocity.movetowards

public class Wanderer2 : MonoBehaviour
{
    // Initializations --------------------

    // Unity components
    private SpriteRenderer wSR;
    private Animator wAnim;

    // State booleans
    [SerializeField]
    private bool grounded = false;
    private bool againstLeftWall = false;
    private bool againstRightWall = false;
    [SerializeField]
    private bool ejectingFromGround;
    private bool onSlope;
    private bool changingDirection;
    private bool activeCoroutine;

    // Physics constants
    private float Accel = .02f;      // Ground acceleration
    private float AccelAir = .04f;   // Air acceleration
    private float Decel = .2f;      // Deceleration
    private float Fric = .2f;       // Friction between player and ground
    private float Grav = -7f;       // Gravity
    private float JumpForce = 6.5f; // Factor that affects jump strength
    private float SlopeMod = .3f;    // Factor by which slopes affect momentum
    private float Brakes = .09f;    // Factor x and y speed are multipled by to make acceleration meaningful

    private float JogV = 20f;  // The lower speed bound of the jog state
    private float RunV = 40f;  // The lower speed bound of the run state
    private float MaxV = 60f;  // The speed cap (unless I add overdrive...)

    // Control variables
    private float xDirection = 0;       // -1 is left, 0 is idle, 1 is right
    private float vCurrentGround = 0f;  // Ground speed that gets translated into x and y speed; makes handling slopes easier
    [SerializeField]
    private float xSpeed;   // Horizontal speed
    [SerializeField]
    private float ySpeed;   // Vertical speed
    private float slopeSpeedFactor;
    [SerializeField]
    private Vector2 velocity;
    float newY;

    // Collision detection constants
    private Vector2 ColliderSize = new Vector2(.11f, .36f); // For use in placing raycasts on the Wanderer .11, .36 // Wrong but causes issues to change
    private float FloorCheckDistance = 3f;    // TEST THESE MORE TO OPTIMIZE
    private float WallCheckDistance = .7f;
    private float SlopeCheckDistance = 3f;
    [SerializeField]
    private LayerMask layerMask;   // Helps optimize raycast    // Is there a way to just do this in code
    [SerializeField]
    float distance;

    // Collision detection variables
    private float wallAngle;
    private float slopeDownAngle;
    private float slopeDownAngleOld;
    private float slopeSideAngle;
    private Vector2 slopeNormalPerp;
    [SerializeField]
    private float playerAngle;

    // Animation constants
    private float WalkAnimModifier = 25f;   // Divisiors used for variable animation speed calculations
    private float JogAnimModifier = 45f;
    private float RunAnimModifier = 60f;

    // Animation tags
    private string AnimGroundSpeed = "groundSpeed";
    private string AnimGroundFramerate = "groundSpeedMod";
    private string AnimXSpeed = "xSpeed";
    private string AnimYSpeed = "ySpeed";

    private string AnimGrounded = "grounded";
    private string AnimCoroutine = "coroutine";


    //private string aMovement = "isMoving_A";
    //private string aGroundRunTurn = "isTurningFast_A";
    //private string aChangeDirection = "isChangingDirection_A";
    



    // Start and Updates --------------------

    // Start is called before the first frame update
    void Start()
    {
        ComponentRetrieval();
        Application.targetFrameRate = 60;
    }

    // Update is called once per frame
    void Update()
    {
        CollisionChecking();
        BasicMovement();
        AnimateWanderer();
    }

    // Player Behavior Methods  --------------------

    void BasicMovement()
    {
        XMovement();
        YMovement();
        transform.Translate(velocity * Time.deltaTime);
    }

    void XMovement()    // Handles all left and right movement
    {
        // GetAxisRaw is ONLY 0, 1, -1; GetAxis actually takes turnaround time and changes the value
        xDirection = getXDirection(); // Gets input from L/R arrows, A, and D by default; 1, 0, or -1

        if (grounded)
        {            
            slopeSpeedFactor = (SlopeMod * Mathf.Sin((playerAngle * Mathf.PI) / 180)); // Code responsible for speed changes on slopes
            //if (vCurrentGround < 60 && vCurrentGround > -301)
            //if(playerAngle > -4f && playerAngle < 4f)
            //{
                vCurrentGround -= slopeSpeedFactor;
            //}
            

            if (xDirection == 1)  // The player wants to go right
            {
                if (vCurrentGround < 0) // If the player is moving to the left
                {
                    vCurrentGround += Decel;  // Slow them down
                    changingDirection = true;
                    if (vCurrentGround >= 0) // This is a decelaration quirk from Sonic the Hedgehog - I think I owe the blue blur an easter egg, so here it is
                    {
                        vCurrentGround = 0.5f;
                    }
                }
                else if (vCurrentGround < MaxV)  // If the player is moving right
                {
                    vCurrentGround += Accel;
                    changingDirection = false;
                    if (vCurrentGround >= MaxV)   // Makes sure you don't exceed top velocity
                    {
                        vCurrentGround = MaxV;
                    }
                }
            }

            else if (xDirection == -1)    // The player wants to go left
            {
                if (vCurrentGround > 0)  // The player is currently moving right
                {
                    vCurrentGround -= Decel; // Slow down
                    changingDirection = true;
                    if (vCurrentGround <= 0) // Decelartion quirk again
                    {
                        vCurrentGround = -0.5f;
                    }
                }
                else if (vCurrentGround > -MaxV)  // The player is currently moving left (speed between 0 and left max)
                {
                    vCurrentGround -= Accel;  // FASTER
                    changingDirection = false;
                    if (vCurrentGround <= -MaxV)  // Speed cap
                    {
                        vCurrentGround = -MaxV;
                    }
                }
            }
            else    // The player isn't touching left or right
            {
                vCurrentGround -= Mathf.Min(Mathf.Abs(vCurrentGround), Fric) * Mathf.Sign(vCurrentGround);    // Passive deceleration
                changingDirection = false;
            }

            xSpeed = vCurrentGround * Mathf.Cos((playerAngle * Mathf.PI) / 180);
            ySpeed = vCurrentGround * Mathf.Sin((playerAngle * Mathf.PI) / 180);

            if(xSpeed > MaxV)
            {
                xSpeed = MaxV;
            }
            else if (xSpeed < -MaxV)
            {
                xSpeed = -MaxV;
            }

            velocity.x = xSpeed;//Mathf.MoveTowards(velocity.x, xSpeed, 5f * Time.deltaTime);  // Current value, value being moved towards, max ROC

            if (againstLeftWall)
            {
                if (velocity.x < 0) velocity.x = 0; //You WILL stop moving if you hit a wall.
                if (xSpeed < 0) xSpeed = 0;
            }
            else if (againstRightWall)
            {
                if (velocity.x > 0) velocity.x = 0;
                if (xSpeed > 0) xSpeed = 0;
            }
            
           //velocity.y = Mathf.MoveTowards(velocity.y, ySpeed, 5f * Time.deltaTime);
            
        }
        //else    // If we're airborne
        //{
        //    if (xDirection == 1)  // The player wants to go right
        //    {
        //        xSpeed += AccelAir;
        //    }
        //    else if (xDirection == -1)  // The player wants to go left
        //    {
        //        xSpeed -= AccelAir;
        //    }

        //    if(xSpeed > 300)
        //    {
        //        xSpeed = 300;
        //    }
        //}
    }
    
    void YMovement()    // Handles jumping and gravity
    {
        if (!grounded)    // Gravity pulls us down when we aren't on the ground
        {
            velocity.y += Grav * Time.deltaTime;
        }
        else if (grounded)
        {
            velocity.y = 0;
            if (Input.GetButtonDown("Jump"))
            {
                velocity.y += JumpForce * Mathf.Cos((playerAngle * Mathf.PI) / 180);
                velocity.x -= JumpForce * Mathf.Sin((playerAngle * Mathf.PI) / 180);    // Allows for slopes to affect jump height - you get a bigger jump at the end of a slope, for example
                
                grounded = false;
            }

        }

    }

    // Collision Methods --------------------

    private void CollisionChecking()
    {
        FloorSensor();
        WallSensors();
        SlopeCheck();
    }

    // For all of these, work in terms of the transform, not raw Vector3s

    private void FloorSensor()  // New problem: The player is very slowly sneaking under the ground when going up or down a slope
    {
        // Ok, steps, steps, this'll be easier to do from scratch with steps.
        // 1: Establish the positions that the rays are being cast from.
        Vector2 checkPos = transform.position;// - new Vector3(0f, ColliderSize.y / 2);
        // 2: Cast the ray 
        RaycastHit2D floorHit = Physics2D.Raycast(checkPos, -transform.up, FloorCheckDistance, layerMask);  // Casts the ray

        if (floorHit)// If the raycast hits something
        {
            
            distance = floorHit.distance;
            if(distance < 2f && distance > 1.9f)
            {
                grounded = true;  // That means we're grounded!
            }
            
            float z = 1.98f - floorHit.distance;
            //if (grounded && !onSlope)
            //{
            //    if (floorHit.distance < ColliderSize.y / 2)  // If we try going into the ground, we translate ourselves back out.
            //    {
            //        transform.Translate(new Vector3(0f, ColliderSize.y / 2 - floorHit.distance));
            //    }
            //}
            //else if (grounded && onSlope)
            //{
            if (floorHit.distance < 1.98f)  // Same condition check, but there needs to be a different translation
                {
                    ejectingFromGround = true;
                    transform.Translate(new Vector3(z * Mathf.Sin((playerAngle * Mathf.PI) / 180), z * Mathf.Cos((playerAngle * Mathf.PI) / 180)));
                }
                else
                {
                    ejectingFromGround = false;
                }
            //}
        }
        else
        {
            grounded = false; // Otherwise, we aren't grounded.
        }
        Debug.DrawRay(floorHit.point, floorHit.normal, Color.red);  // Displays the ray for debug purposes
    }

    private void WallSensors()
    {
        // 1: Establish L and R positions.
        Vector2 leftCheckPos = transform.position; // - new Vector3(.6f, 0f);
        Vector2 rightCheckPos = transform.position; // + new Vector3(.6f, 0f);
        // 2: Cast
        RaycastHit2D wallHitLeft = Physics2D.Raycast(leftCheckPos, -transform.right, WallCheckDistance, layerMask);
        RaycastHit2D wallHitRight = Physics2D.Raycast(rightCheckPos, transform.right, WallCheckDistance, layerMask);
        // 3: Push the player out of the wall if they are trying to enter it
        if (wallHitLeft)
        {
            againstLeftWall = true;
            wallAngle = Vector2.Angle(wallHitLeft.normal, Vector2.right); // Gets the angle of the raycast
            if (wallAngle < 10f && wallAngle > -10f)  // Checks if the player is up against a perpendicular wall to the ground
            {
                if(wallHitLeft.distance < .1f) // We only want to shove them if they're actively moving into it
                {
                    transform.Translate(new Vector3(.1f - wallHitLeft.distance, 0f));
                }
            }
        }
        else if (wallHitRight)
        {
            againstRightWall = true;
            wallAngle = Vector2.Angle(wallHitRight.normal, Vector2.left); // Gets the angle of the raycast
            if (wallAngle < 10f && wallAngle > -10f) 
            {
                if (wallHitRight.distance < .1f)
                {
                    transform.Translate(new Vector3(-(.1f - wallHitRight.distance), 0f));
                }
            }
        }
        else
        {
            againstLeftWall = false;
            againstRightWall = false;
        }
        Debug.DrawRay(wallHitLeft.point, wallHitLeft.normal, Color.magenta);
        Debug.DrawRay(wallHitRight.point, wallHitRight.normal, Color.yellow);
    }

    private void SlopeCheck()
    {
        Vector2 checkPos = transform.position - new Vector3(0f, ColliderSize.y / 2);    // Establishes the position that the raycast originates from

        SlopeCheckHorizontal(checkPos); // Together, these account for the horizontal and vertical components of a slope
        SlopeCheckVertical(checkPos);

    }

    private void SlopeCheckHorizontal(Vector2 checkPos)
    {
        RaycastHit2D slopeHitFront = Physics2D.Raycast(checkPos, transform.right, SlopeCheckDistance, layerMask);
        RaycastHit2D slopeHitBack = Physics2D.Raycast(checkPos, -transform.right, SlopeCheckDistance, layerMask);

        if (slopeHitFront)
        {
            onSlope = true;
            slopeSideAngle = Vector2.Angle(slopeHitFront.normal, Vector2.up);
        }
        else if (slopeHitBack)
        {
            onSlope = true;
            slopeSideAngle = Vector2.Angle(slopeHitBack.normal, Vector2.up);
        }
        else
        {
            slopeSideAngle = 0.0f;
            onSlope = false;
        }
    }

    private void SlopeCheckVertical(Vector2 checkPos)
    {
        RaycastHit2D hit = Physics2D.Raycast(checkPos, -transform.up, SlopeCheckDistance, layerMask);

        if (hit && grounded)    // If the raycast hits something
        {

            slopeNormalPerp = Vector2.Perpendicular(hit.normal).normalized;    // Gets the vector perpendicular to the raycast
            slopeDownAngle = Vector2.Angle(hit.normal, Vector2.up); // Gets the angle of the raycast

            playerAngle = Vector2.Angle(slopeNormalPerp, Vector2.left);
            if (slopeNormalPerp.y > 0.0f)
            {
                playerAngle = playerAngle * -1f;
            }

            if (slopeDownAngle != slopeDownAngleOld)
            {
                onSlope = true;
            }
            slopeDownAngleOld = slopeDownAngle;

            //Debug.DrawRay(hit.point, slopeNormalPerp, Color.cyan);
            //Debug.DrawRay(hit.point, hit.normal, Color.blue);   // Returns the normal of the surface that's been hit
        }
    }

    // State Getter Methods --------------------

    private float getXDirection()
    {
        xDirection = Input.GetAxisRaw("Horizontal");
        return xDirection;
    }

    // Animation and Coroutines --------------------

    void AnimateWanderer()
    {
        WandererRotation();
        UpdateAnimatorObservational();
        UpdateAnimatorConditional();
    }

    private void WandererRotation() // Ensures that the player is perpendicular to the slope they stand upon
    {
        if (grounded)
        {
            transform.rotation = Quaternion.Euler(new Vector3(0, 0, playerAngle)); // z, x, y
        }
        else if (!grounded)
        {
            playerAngle = Mathf.MoveTowards(playerAngle, 0f, 10f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(new Vector3(0, 0, playerAngle));
        }

    }

    private void UpdateAnimatorObservational()   // Updates animator floats and booleans that aren't tied to animator-related conditions
    {
        wAnim.SetFloat(AnimGroundSpeed, Mathf.Abs(vCurrentGround));         // Sends ground speed and animation speed to the animator
        wAnim.SetFloat(AnimGroundFramerate, calculateGroundModifier());
        wAnim.SetFloat(AnimYSpeed, velocity.y);                                    // Sends y speed to animator

        wAnim.SetBool(AnimGrounded, grounded);                               // Tells the animator if we're grounded
        wAnim.SetBool(AnimCoroutine, activeCoroutine);                   // Informs the animator of an active coroutine  

        //wAnim.SetBool(aChangeDirection, changingDirection);                 // Tell the animator if we're changing directions


    }

    private void UpdateAnimatorConditional()    // Updates the animator to trigger animations that have specific sets of conditions
    {
        if (grounded)
        {
            if (vCurrentGround != 0f)   // If we're moving on the ground
            {
                //wAnim.SetBool(aMovement, true);
                if (vCurrentGround > 0f)         // This set of if statements determines the direction the player appears to be facing
                {
                    wSR.flipX = false;
                }
                else if (vCurrentGround < 0f)
                {
                    wSR.flipX = true;
                }

                //if (Mathf.Abs(vCurrentGround) > RunV) // If the player is running 
                //{
                //    if (vCurrentGround / getXDirection() < 0)    // If the current directional input is opposite the current direction of motion
                //    {
                //        wAnim.SetBool(aGroundRunTurn, true);
                //    }
                //    else
                //    {
                //        wAnim.SetBool(aGroundRunTurn, false);
                //    }
                //}

            }
            //else   // If we aren't moving on the ground
            //{
            //    wAnim.SetBool(aMovement, false);
            //    wAnim.SetBool(aGroundRunTurn, false);
            //}
        }
        else if (!grounded)   // If we're airborne
        {
            if (velocity.y < 0 && activeCoroutine == false)
            {
                StartCoroutine(TransitionJumpCR());
            }
        }
    }
    IEnumerator TransitionJumpCR()
    {
        activeCoroutine = true;
        yield return new WaitForSeconds(.2f);
        activeCoroutine = false;
    }

    // Miscellaneous Methods --------------------

    private void ComponentRetrieval()
    {
        wSR = GetComponent<SpriteRenderer>();
        wAnim = GetComponent<Animator>();
    }

    float calculateGroundModifier() // Changes animation speed of walking, jogging, and running
    {
        float mod = .8f;
        float speed = Mathf.Abs(vCurrentGround);
        if (speed < JogV) // Walking
        {
            mod = speed / WalkAnimModifier;
        }
        else if (speed < RunV)    // Jogging  
        {
            mod = speed / JogAnimModifier;
        }
        else if (speed <= MaxV)   // Running
        {
            mod = speed / RunAnimModifier;
        }

        return mod;
    }
}
