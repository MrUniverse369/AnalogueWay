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
        fadeScreen.CrossFadeAlpha(0f, fadeInTime, false);
    }
}
