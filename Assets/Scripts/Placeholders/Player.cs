using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{

    // Initializations ------------------------------------------------------------------------------------------

    //[SerializeField]    // Used to let you modify private variables in Unity's inspector window
    //private float moveForce = 10f;
    [SerializeField]
    private float jumpForce = 11f;

    //private float initialVelocity = 0f; // The bottom range of our velocity
    private float maxVelocity = 500f;    // Our velocity cap
    [SerializeField]
    private float groundVelocity = 0f; // Our velocity right now, in this moment
    private float accelFactor = 2f;      // How fast we accelerate / the velocity we add 
    private float decelFactor = 4f;        // How fast we decelerate
    private float fricFactor = 3f;         // How fast friction with the earth will slow us down

    private float movementX;

    // Component declarations
    private Rigidbody2D myBody; 
    private BoxCollider2D myCollider;
    private Animator anim;
    private AudioSource playerAudio;
    private SpriteRenderer sr;

    // Animation tags
    private string WalkBool = "isWalking";
    private string GroundBool = "isGrounded";
    private string FallBool = "isFalling";
    private string TalkBool = "isTalking";
    private string CrouchBool = "isCrouching";  // This one is used specifically to trigger the transition animation
    private string CrouchedBool = "isCrouched";
    private string UncrouchBool = "isUncrouching";  // This one too

    //Misc tags
    private string GroundTag = "Ground";

    //In-code booleans
    private bool isGrounded = true;
    private bool isTalking = false;
    private bool isCrouching = false;
    private bool crouchEnabled = true;

    private bool activeCoroutine = false;

    // Current bugs:
    // The slight gap that occurs while crouching as a result of the sudden collider change, which I think boils down to frame-intensive alterations of the collider
    // Crouch animation doesn't transition from walking

    // Start and Update ------------------------------------------------------------------------------

    // Start is called before the first frame update
    void Start()
    {
        ComponentRetrieval();
    }

    // Update is called once per frame
    void Update()
    {
        PlayerTalk();
        //PlayerMoveKeyboard();
        PlayerJump();
        PlayerCrouch();
        AnimatePlayer();
    }

    private void FixedUpdate()
    {
        PlayerMoveKeyboard();
    }

    // Player Behavior Functions --------------------------------------------------------------------------------------------

    // Ok, note to future me when programming the Wanderer: There are better ways to do this than this if statement gobbleygook. Brackeys' video outlines them.

    void AnimatePlayer()    // Handles all player animaton
    {
        
        if (isGrounded) // If the player is on the ground
        {
            anim.SetBool(GroundBool, true); // Lets the animator know we're on the ground
            anim.SetBool(FallBool, false);

            if (isTalking)  // Special animation sequences should always override standard movement
            {
                anim.SetBool(TalkBool, true);
            }
            else    // If we aren't in a special animation sequence
            {
                anim.SetBool(TalkBool, false);

                // Movement logic, which is determined simultaneously with crouching since you can move and crouch
                if (movementX != 0)   // Moving 
                {
                    anim.SetBool(WalkBool, true);
                }
                else if (movementX == 0)    // Not moving
                {
                    anim.SetBool(WalkBool, false);
                }

                // Crouch logic
                if (Input.GetKeyDown("s") && !activeCoroutine)
                    {
                        anim.SetBool(CrouchBool, true);
                        StartCoroutine(CrouchDownCoroutine());

                    }
                 if (Input.GetKeyUp("s") && anim.GetBool(CrouchedBool) && !activeCoroutine)
                    {
                        anim.SetBool(CrouchedBool, false);
                        anim.SetBool(UncrouchBool, true);
                        StartCoroutine(UncrouchCoroutine());
                    }
                 if (!Input.GetKey("s"))
                    {
                        //anim.SetBool(CrouchBool, false);
                        anim.SetBool(CrouchedBool, false);
                        //anim.SetBool(UncrouchBool, false);
                    }
            }
        }
        else // If the player is in the air
        {
            anim.SetBool(GroundBool, false);

            if (myBody.velocity.y >= 0f)    // Determines if the player is rising or falling
            {
                anim.SetBool(FallBool, false);
            }
            else
            {
                anim.SetBool(FallBool, true);
            }
        }
    }

    void PlayerMoveKeyboard()   // Moves the player by use of the keyboard
    {
        if (!isTalking)
        {
            // GetAxisRaw is ONLY 0, 1, -1; GetAxis actually takes turnaround time and changes the value
            movementX = Input.GetAxisRaw("Horizontal"); // Gets input from L/R arrows, A, and D by default; 1, 0, or -1

            if(movementX == 1)  // The player wants to go right
            {
                if(groundVelocity < 0) // If the player is moving to the left
                {
                    groundVelocity += decelFactor;  // Slow them down
                    if(groundVelocity >= 0) // This is a decelaration quirk from Sonic the Hedgehog - I think I owe the blue blur an easter egg, so here it is
                    {
                        groundVelocity = 0.5f;
                    }
                }
                else if(groundVelocity < maxVelocity)  // If the player is moving right
                {
                    groundVelocity += accelFactor;
                    if(groundVelocity >= maxVelocity)   // Makes sure you don't exceed top velocity
                    {
                        groundVelocity = maxVelocity;
                    }
                }
            }

            else if(movementX == -1)    // The player wants to go left
            {
                if(groundVelocity > 0)  // The player is currently moving right
                {
                    groundVelocity -= decelFactor; // Slow down
                    if(groundVelocity <= 0) // Decelartion quirk again
                    {
                        groundVelocity = -0.5f;
                    }
                }
                else if(groundVelocity > -maxVelocity)  // The player is currently moving left (speed between 0 and left max
                {
                    groundVelocity -= accelFactor;  // FASTER
                    if(groundVelocity <= -maxVelocity)  // Speed cap
                    {
                        groundVelocity = -maxVelocity;
                    }
                }
            }
            else    // The player isn't touching left or right
            {
                groundVelocity -= Mathf.Min(Mathf.Abs(groundVelocity), fricFactor) * Mathf.Sign(groundVelocity);    // Passive deceleration
            }
                                                     
            myBody.position += new Vector2(groundVelocity, 0f) * Time.deltaTime * .1f; // Done this way to avoid working with Vector3 * moveForce
        }
        
    }

    void PlayerCrouch() 
    {
        if (isGrounded && !isTalking)
        {
            if (Input.GetKeyDown("s"))
            {
                crouchEnabled = true;
            }

            if (Input.GetKey("s") && crouchEnabled)
            {
                //Box2D gets smol
                myCollider.size = new Vector2(myCollider.size.x, .1f);
                jumpForce = 5f;
                //moveForce = 5f;
                isCrouching = true;
            }

            else if (!Input.GetKey("s") && isCrouching) // The boolean prevents this information from updating every frame
            {
                myCollider.size = new Vector2(myCollider.size.x, .1376f);
                jumpForce = 11f;
                //moveForce = 10f;
                isCrouching = false;
                crouchEnabled = false;
            }
        }
        
        
    }

    void PlayerJump()   // Only for when pressed; no nuance to conditions based on time held
    {
        if (Input.GetButtonDown("Jump") && isGrounded && !isTalking)    // This is a predefined button reader - in this case, on PC, this refers to spacebar, but it works across platforms
        {
            myBody.AddForce(new Vector2(0f, jumpForce), ForceMode2D.Impulse);   // Adding vertical force; impulse is instant application
            isGrounded = false;
        }
    }

    void PlayerTalk()
    {
        if (Input.GetKeyDown("e") && isGrounded && movementX == 0 && !isCrouching && !activeCoroutine)    // Need a condition here for not crouching
        {
            isTalking = true;
            StartCoroutine(TalkingCoroutine());
            
        }
    }

    // Coroutines ----------------------------------------------------------------------------------
    IEnumerator TalkingCoroutine()
    {
        activeCoroutine = true;
        playerAudio.Play();
        yield return new WaitForSeconds(4.05f);
        isTalking = false;
        activeCoroutine = false;
    }

    IEnumerator CrouchDownCoroutine()
    {
        activeCoroutine = true;
        yield return new WaitForSeconds(.1f);
        anim.SetBool(CrouchBool, false);
        anim.SetBool(CrouchedBool, true);
        activeCoroutine = false;
    }
    IEnumerator UncrouchCoroutine()
    {
        activeCoroutine = true;
        yield return new WaitForSeconds(.1f);
        anim.SetBool(UncrouchBool, false);
        activeCoroutine = false;
    }

    // Misc. Functions --------------------------------------------------------------------

    private void OnCollisionEnter2D(Collision2D collision)  // Allows you to detect collisions between game objects
    {
        if (collision.gameObject.CompareTag(GroundTag))
        {
            isGrounded = true;
        }
    }   // Somehow I don't ever have to call this myself? Sure

    private void ComponentRetrieval()   // Instantiates all of those components we need
    {
        myBody = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<BoxCollider2D>();
        anim = GetComponent<Animator>();
        playerAudio = GetComponent<AudioSource>();

        sr = GetComponent<SpriteRenderer>();
    }

    // FixedUpdate is only for physics stuff

}
