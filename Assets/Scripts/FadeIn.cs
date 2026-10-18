using UnityEngine;
using UnityEngine.UI;


public class FadeIn : MonoBehaviour
{
    [SerializeField] private float fadeInTime;
    [SerializeField] private Image fadeScreen;
    private bool iris;    // FX: opening as a cat-head iris instead of a plain fade
    private float closed; // FX: 1 = covered, 0 = open (the same ease-out the alpha fade used)
    // Start is called before the first frame update
    void Start()
    {
        fadeScreen = GetComponent<Image>();
        closed = fadeScreen.canvasRenderer.GetAlpha();
        iris = IrisWipe.Attach(fadeScreen); // FX:
        if (iris)
        {
            fadeScreen.canvasRenderer.SetAlpha(1f);
            IrisWipe.Set(fadeScreen, IrisWipe.SizeFor(1f - closed));
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (iris) // FX: the iris opens around Harvey with the fade's timing
        {
            closed = fadeInTime > 0f ? closed * Mathf.Max(0f, 1f - Time.deltaTime / fadeInTime) : 0f;
            if (closed <= 0.01f)
            {
                fadeScreen.canvasRenderer.SetAlpha(0f);
                enabled = false;
                return;
            }
            IrisWipe.Set(fadeScreen, IrisWipe.SizeFor(1f - closed));
            return;
        }
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
