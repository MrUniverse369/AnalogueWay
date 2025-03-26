using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class FadeIn : MonoBehaviour
{
    [SerializeField] private float fadeInTime;
    [SerializeField] private Image fadeScreen;
    public static float fadeInval = 0f;
    // Start is called before the first frame update
    void Start()
    {
        fadeScreen = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        fadeScreen.CrossFadeAlpha(0f, fadeInTime, false);
    }

    /*   void fadeInControl()
    {
        Color imgColor = fadeScreen.color;
        imgColor.a = 1f;
        fadeScreen.color = imgColor;

        for (int counter = 100; counter > 0; --counter)
        {
            if (!changeWorld.lWorld) fadeScreen.CrossFadeAlpha(0f, fadeInTime, false);

        }

        //    if (changeWorld.lWorld) fadeScreen.CrossFadeAlpha(1f, fadeInTime, false);

    }  
    */
}
