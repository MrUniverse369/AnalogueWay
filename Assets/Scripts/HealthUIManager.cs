using UnityEngine;
using UnityEngine.UI;

public class HealthUIManager : MonoBehaviour
{
    [SerializeField] private Image playerHealthUI;
    [SerializeField] private Sprite healthOne;
    [SerializeField] private Sprite healthTwo;
    [SerializeField] private Sprite healthThree;
    [SerializeField] private Sprite healthFour;


    [SerializeField] private Image playerManaUI;
    [SerializeField] private Sprite manaOne;
    [SerializeField] private Sprite manaTwo;
    [SerializeField] private Sprite manaThree;
    [SerializeField] private Sprite manaFour;

    [Header("CHI: each tier with 3, 2 and 1 units lit (Tools/UI/chi_meter.py). A spent unit shows the next colour.")]
    [Tooltip("CHI: blue with the spent units green. Empty = manaOne-manaThree (spent units dark).")]
    [SerializeField] private Sprite[] blueChi = new Sprite[3];
    [SerializeField] private Sprite[] greenChi = new Sprite[3];
    [SerializeField] private Sprite[] redChi = new Sprite[3];
    [Tooltip("CHI: how long the meter pops when it changes colour.")]
    [SerializeField] private float tierPopTime = 0.3f;
    [Tooltip("CHI: how long the meter shakes when a talisman is pressed with no chi left.")]
    [SerializeField] private float denyShakeTime = 0.3f;

    private Chi.Tier shownTier;
    private RectTransform meter;
    private Vector3 meterScale;
    private Vector2 meterPos;
    private float popLeft, shakeLeft;

    private void Start()
    {
        shownTier = Chi.CurrentTier; // CHI: no pop for the tier the level starts on
        if (playerManaUI == null) return;
        meter = playerManaUI.rectTransform;
        meterScale = meter.localScale;
        meterPos = meter.anchoredPosition;
    }

    private void OnEnable() => Chi.Denied += OnChiDenied; // CHI:

    private void OnDisable() => Chi.Denied -= OnChiDenied;

    private void OnChiDenied() => shakeLeft = denyShakeTime;

    // Update is called once per frame
    void Update()
    {
        updatePlayerHealthUI();
        updatePlayerManaUI();
    }

    private void updatePlayerHealthUI()
    {
        if (HurtPlayer.pLivesCount >= 3) { playerHealthUI.sprite = healthOne; }
        if (HurtPlayer.pLivesCount == 2) { playerHealthUI.sprite = healthTwo; }
        if (HurtPlayer.pLivesCount == 1) { playerHealthUI.sprite = healthThree; }
        if (HurtPlayer.pLivesCount <= 0) { playerHealthUI.sprite = healthFour; }
    }

    // CHI: nine units in three tiers (see Chi). The meter shows the tier being spent, blue, then green, then red, and
    // counts its three units down top to bottom as the old blue meter did. A spent unit shows the colour that comes
    // next (green under blue, red under green); in the red tier it goes dark. manaFour (all dark) is empty.
    private void updatePlayerManaUI()
    {
        Chi.Tier tier = Chi.CurrentTier;
        playerManaUI.sprite = TierSprite(tier, Chi.UnitsLeftInTier);
        if (tier != shownTier)
        {
            if (tier != Chi.Tier.Empty) popLeft = tierPopTime; // the next colour fills in (or a refill moves back up)
            shownTier = tier;
        }
        AnimateMeter();
    }

    private Sprite TierSprite(Chi.Tier tier, int lit)
    {
        if (tier == Chi.Tier.Empty || lit <= 0) return manaFour;
        Sprite[] set = tier == Chi.Tier.Blue ? blueChi : tier == Chi.Tier.Green ? greenChi : redChi;
        int i = Chi.UnitsPerTier - Mathf.Min(lit, Chi.UnitsPerTier); // 3 lit = [0], 2 = [1], 1 = [2]
        if (set != null && i < set.Length && set[i] != null) return set[i];
        return i == 0 ? manaOne : i == 1 ? manaTwo : manaThree; // the original blue frames (a tier with no art set)
    }

    // CHI: a quick pop when the colour changes, a one-art-pixel side-to-side shake when a talisman is refused.
    // Only touches the meter while animating, and puts it back exactly when done.
    private void AnimateMeter()
    {
        if (meter == null || (popLeft <= 0f && shakeLeft <= 0f)) return;
        float dt = Time.unscaledDeltaTime;
        popLeft = Mathf.Max(0f, popLeft - dt);
        shakeLeft = Mathf.Max(0f, shakeLeft - dt);
        float pop = tierPopTime > 0f ? popLeft / tierPopTime : 0f;
        meter.localScale = meterScale * (1f + 0.35f * pop * pop);
        float artPixel = playerManaUI.sprite != null ? meter.rect.width / playerManaUI.sprite.rect.width : 1f;
        float shake = shakeLeft > 0f ? (Mathf.Repeat(shakeLeft * 20f, 1f) < 0.5f ? artPixel : -artPixel) : 0f;
        meter.anchoredPosition = meterPos + new Vector2(shake, 0f);
    }
}
