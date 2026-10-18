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
        // PERF: once the screen is clear there is nothing left to do
        float alpha = fadeScreen.canvasRenderer.GetAlpha();
        if (alpha <= 0.001f)
        {
            enabled = false;
            return;
        }
        // PERF: this used to restart CrossFadeAlpha every frame, which allocated a new tween each frame. Each restart
        // moved the alpha dt/fadeInTime of the way to 0, so doing that step directly keeps the same ease-out fade.
        fadeScreen.canvasRenderer.SetAlpha(fadeInTime > 0f ? alpha * Mathf.Max(0f, 1f - Time.deltaTime / fadeInTime) : 0f);
    }
}
