using UnityEngine;
using UnityEngine.UI;

public class UISpriteFrameAnimator : MonoBehaviour
{
    public Image targetImage;
    public Sprite[] frames;
    public float framesPerSecond = 8f;
    public bool playOnEnable = true;
    public bool useBounce = true;
    public float bounceAmplitude = 8f;
    public float bounceSpeed = 6f;

    private RectTransform _rectTransform;
    private Vector2 _startAnchoredPosition;
    private float _timer;
    private int _frameIndex;
    private bool _isPlaying;

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        _rectTransform = targetImage != null ? targetImage.rectTransform : null;

        if (_rectTransform != null)
        {
            _startAnchoredPosition = _rectTransform.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        _timer = 0f;
        _frameIndex = 0;
        _isPlaying = playOnEnable;

        if (targetImage != null && frames != null && frames.Length > 0)
        {
            targetImage.sprite = frames[0];
        }

        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = _startAnchoredPosition;
        }
    }

    private void Update()
    {
        if (!_isPlaying || targetImage == null || frames == null || frames.Length == 0)
        {
            return;
        }

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        _timer += Time.unscaledDeltaTime;

        while (_timer >= frameDuration)
        {
            _timer -= frameDuration;
            _frameIndex++;
            if (_frameIndex >= frames.Length)
            {
                _frameIndex = 0;
            }

            targetImage.sprite = frames[_frameIndex];
        }

        if (useBounce && _rectTransform != null)
        {
            float offsetY = Mathf.Sin(Time.unscaledTime * bounceSpeed) * bounceAmplitude;
            _rectTransform.anchoredPosition = _startAnchoredPosition + new Vector2(0f, offsetY);
        }
    }

    public void Play()
    {
        _isPlaying = true;
    }

    public void Stop()
    {
        _isPlaying = false;

        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = _startAnchoredPosition;
        }
    }
}