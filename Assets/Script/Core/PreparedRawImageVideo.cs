using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
[RequireComponent(typeof(RawImage))]
public class PreparedRawImageVideo : MonoBehaviour
{
    [SerializeField] VideoAspectRatio aspectRatio = VideoAspectRatio.FitInside;
    [SerializeField] bool playOnEnable = true;
    [SerializeField] bool restartWhenEnabled = true;

    VideoPlayer _vp;
    RawImage _rawImage;

    void Awake()
    {
        _vp = GetComponent<VideoPlayer>();
        _rawImage = GetComponent<RawImage>();

        _vp.playOnAwake = false;
        _vp.waitForFirstFrame = true;
        _vp.aspectRatio = aspectRatio;

        _vp.prepareCompleted += OnPrepared;
    }

    void OnEnable()
    {
        if (!playOnEnable || _vp == null)
            return;

        _vp.aspectRatio = aspectRatio;

        if (restartWhenEnabled)
            _vp.Stop();

        _rawImage.enabled = false; // ÁØºñ Àü Ã¹ ÇÁ·¹ÀÓ/Å©±â Æ¦ ¼û±è

        _vp.Prepare();
    }

    void OnPrepared(VideoPlayer vp)
    {
        _rawImage.enabled = true;
        vp.Play();
    }

    void OnDisable()
    {
        if (_vp != null)
            _vp.Stop();

        if (_rawImage != null)
            _rawImage.enabled = false;
    }

    void OnDestroy()
    {
        if (_vp != null)
            _vp.prepareCompleted -= OnPrepared;
    }
}