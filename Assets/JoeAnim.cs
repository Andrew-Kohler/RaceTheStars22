using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoeAnim : MonoBehaviour
{
    private Animation anim;
    // Start is called before the first frame update
    void Start()
    {
        anim = gameObject.GetComponent<Animation>();
    }

    // Update is called once per frame
    void Update()
    {
        anim.Play("Joe");
    }
}
