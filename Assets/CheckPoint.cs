using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    private SpriteRenderer sRender;
    [SerializeField] private Sprite checkPointOpen;
    [SerializeField] private Sprite checkPointClosed;
    // Start is called before the first frame update
    void Start()
    {
        sRender = GetComponent<SpriteRenderer>();
        sRender.sprite = checkPointClosed;
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("PlayerDetectionArea"))
        {
            sRender.sprite = checkPointOpen;
        }
    }
}
