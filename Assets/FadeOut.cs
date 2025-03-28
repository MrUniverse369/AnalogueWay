using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeOut : MonoBehaviour
{
    [SerializeField] private float fadeInTime;  // Time to fade in (seconds)
    [SerializeField] private Image fadeScreen;  // The image to fade in

    private bool isFadedIn = false;  // Flag to track if the fade-in has already occurred

    // Start is called before the first frame update
    void Start()
    {
        fadeScreen = GetComponent<Image>();  // Get the Image component on this GameObject

        // Ensure the alpha is set to 0 (fully transparent) at the start
        //  fadeScreen.color = new Color(fadeScreen.color.r, fadeScreen.color.g, fadeScreen.color.b, 0f);
    }

    // Update is called once per frame
    void Update()
    {
        // Start fading in only if it hasn't been faded in already
        if (!isFadedIn)
        {
            // Fade the image in to full opacity (alpha = 1) over the fadeOutTime duration
            fadeScreen.CrossFadeAlpha(1f, fadeInTime, false);

            // Mark the fade as completed so we don't keep fading in
            isFadedIn = true;
        }
    }
}
