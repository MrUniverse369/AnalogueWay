using UnityEngine;
using UnityEngine.UI;

public class FadeOut : MonoBehaviour
{
    [SerializeField] private float fadeInTime;  // Time to fade in (seconds)
    [SerializeField] private Image fadeScreen;  // The image to fade in

    private bool isFadedIn = false;  // Flag to track if the fade-in has already occurred
    private float irisTime = -1f;     // FX: closing as a cat-head iris onto Harvey

    // Start is called before the first frame update
    void Start()
    {
        fadeScreen = GetComponent<Image>();  // Get the Image component on this GameObject
    }

    // Update is called once per frame
    void Update()
    {
        // Start fading in only if it hasn't been faded in already
        if (irisTime >= 0f) // FX:
        {
            irisTime += Time.unscaledDeltaTime;
            float k = fadeInTime > 0f ? irisTime / fadeInTime : 1f;
            IrisWipe.Set(fadeScreen, IrisWipe.SizeFor(1f - k));
            if (k >= 1f) irisTime = -1f;
            return;
        }
        if (!isFadedIn && IrisWipe.Attach(fadeScreen)) // FX: iris instead of the plain fade
        {
            fadeScreen.canvasRenderer.SetAlpha(1f);
            IrisWipe.Set(fadeScreen, IrisWipe.OpenSize);
            irisTime = 0f;
            isFadedIn = true;
            return;
        }
        if (!isFadedIn)
        {
            // Fade the image in to full opacity (alpha = 1) over the fadeOutTime duration
            fadeScreen.CrossFadeAlpha(1f, fadeInTime, false);

            // Mark the fade as completed so we don't keep fading in
            isFadedIn = true;
        }
    }
}
