using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ParallaxBackGround : MonoBehaviour
{
    [SerializeField] private float startPos;
    [SerializeField] private float lenght;
    [SerializeField] private bool smallBackGround;
    [SerializeField] private float movement;
    [SerializeField] private GameObject cam;
    [SerializeField] private float parallaxEffect;
    [SerializeField] private bool isPlayerInBackGroundView;
    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position.x;
        lenght = GetComponent<SpriteRenderer>().bounds.size.x;
        smallBackGround = false;

    }

    // Update is called once per frame
    void FixedUpdate()
    {  
        float dist = cam.transform.position.x * parallaxEffect;
        movement = cam.transform.position.x * (1- parallaxEffect);
        
        if (isPlayerInBackGroundView && smallBackGround)
        {
        transform.position = new Vector3(startPos+dist,transform.position.y,transform.position.z);
        Debug.Log("Activeted script for abckGround");
        }
        else
        {
            transform.position = new Vector3(startPos+dist,transform.position.y,transform.position.z);
            
            if (movement > startPos + lenght)
            {
                startPos += lenght;
            }
            else if(movement < startPos - lenght)
            {
                startPos -= lenght;
            }
        }
        }
    
    

    private void OnTriggerStay2D(Collider2D other)
    {
        
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("colliding with player");
            isPlayerInBackGroundView = true;
        }
    
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        isPlayerInBackGroundView = false;
    }
}


