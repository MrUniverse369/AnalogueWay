using UnityEngine;
using UnityEngine.UI;


public class FadeIn : MonoBehaviour
{
    [SerializeField] private float fadeInTime;
    [SerializeField] private Image fadeScreen;
    // Start is called before the first frame update
    void Start()
    {
        fadeScreen = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        // PERF: CrossFadeAlpha starts a new tween each call; once the screen is clear there is nothing left to do
        if (fadeScreen.canvasRenderer.GetAlpha() <= 0.001f)
        {
            enabled = false;
            return;
        }
        fadeScreen.CrossFadeAlpha(0f, fadeInTime, false);
    }
}
