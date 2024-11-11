using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    private Transform player;
    private Vector3 tempPos;

    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindWithTag("Player").transform;
    }

    // Update is called once per frame
    void LateUpdate()   // LateUpdate is called every frame AFTER every routine in Update is complete
    {
        tempPos = transform.position;   // Storing the current position of the camera
        tempPos.x = player.position.x;  // Change the temp's x position to the player's current x position
        tempPos.y = player.position.y;

        transform.position = tempPos;   // Assign the new value to the camera of the position

        // Oh, there is SO much you can do to make this stylish

    }
}
