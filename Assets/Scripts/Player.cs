using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{

    public bool isGrounded;
    public bool isSprinting;

    private Transform cam;
    private World world;

    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float jumpForce = 5f;
    public float gravity = -9.8f;

    public float playerWidth = 0.15f;

    private float horizontal;
    private float vertical;
    private float mouseHorizontal;
    private float mouseVertical;
    private Vector3 velocity;
    private float verticalMomentum = 0;
    private bool jumpRequest;

    void Start()
    {
        cam = GameObject.Find("Main Camera").transform;
        world = GameObject.Find("World").GetComponent<World>();
    }

    void FixedUpdate()
    {
        CalculateVelocity();
        if (jumpRequest)
            Jump();

        transform.Rotate(Vector3.up * mouseHorizontal);
        cam.Rotate(Vector3.right * -mouseVertical);
        transform.Translate(velocity, Space.World);
    }

    void Update()
    {
        GetPlayerInputs();
    }

    void CalculateVelocity()
    {
        // Affect vertical momentum with gravity
        if (verticalMomentum > gravity)
            verticalMomentum += Time.fixedDeltaTime * gravity;

        // if we're sprinting, use the sprint multiplier
        if (isSprinting)
            velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * sprintSpeed;
        else
            velocity = ((transform.forward * vertical) + (transform.right * horizontal)) * Time.fixedDeltaTime * walkSpeed;

        // Apply vertical momentum (falling/jumping)
        velocity += Vector3.up * verticalMomentum * Time.fixedDeltaTime;

        if ((velocity.z > 0 && front) || (velocity.z < 0 && back))
            velocity.z = 0;
        if ((velocity.x > 0 && right) || (velocity.x < 0 && left))
            velocity.x = 0;
        if (velocity.y < 0)
            velocity.y = checkDownSpeed(velocity.y);
        else if (velocity.y > 0)
            velocity.y = checkUpSpeed(velocity.y);
    }

    void Jump()
    {
        verticalMomentum = jumpForce;
        isGrounded = false;
        jumpRequest = false;
    }

    void GetPlayerInputs()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");
        mouseHorizontal = Input.GetAxis("Mouse X");
        mouseVertical = Input.GetAxis("Mouse Y");

        if (Input.GetButtonDown("Sprint"))
            isSprinting = true;
        if (Input.GetButtonUp("Sprint"))
            isSprinting = false;

        if (isGrounded && Input.GetButtonDown("Jump"))
            jumpRequest = true;
    }

    float checkDownSpeed(float downSpeed)
    {
        if (
            world.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth) ||
            world.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z - playerWidth) ||
            world.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth) ||
            world.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + downSpeed, transform.position.z + playerWidth)
            )
        {
            isGrounded = true;
            return 0;
        }
        else
        {
            isGrounded = false;
            return downSpeed;
        }
    }

    float checkUpSpeed(float upSpeed)
    {
        if (
            world.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth) ||
            world.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z - playerWidth) ||
            world.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth) ||
            world.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + 2f + upSpeed, transform.position.z + playerWidth)
            )
        {
            return 0;
        }
        else
        {
            return upSpeed;
        }
    }

    public bool front
    {
        get
        {
            return
                world.CheckForVoxel(transform.position.x, transform.position.y, transform.position.z + playerWidth)
                || world.CheckForVoxel(transform.position.x, transform.position.y + 1f, transform.position.z + playerWidth);
        }
    }

    public bool back
    {
        get
        {
            return
                world.CheckForVoxel(transform.position.x, transform.position.y, transform.position.z - playerWidth)
                || world.CheckForVoxel(transform.position.x, transform.position.y + 1f, transform.position.z - playerWidth);
        }
    }

    public bool left
    {
        get
        {
            return
                world.CheckForVoxel(transform.position.x - playerWidth, transform.position.y, transform.position.z)
                || world.CheckForVoxel(transform.position.x - playerWidth, transform.position.y + 1f, transform.position.z);
        }
    }

    public bool right
    {
        get
        {
            return
                world.CheckForVoxel(transform.position.x + playerWidth, transform.position.y, transform.position.z)
                || world.CheckForVoxel(transform.position.x + playerWidth, transform.position.y + 1f, transform.position.z);
        }
    }
}
