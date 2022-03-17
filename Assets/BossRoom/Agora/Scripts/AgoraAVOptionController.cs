using UnityEngine;
using UnityEngine.UI;

using agora_gaming_rtc;

public class AgoraAVOptionController : MonoBehaviour
{
    [SerializeField] Toggle toggleSubAudio;
    [SerializeField] Toggle toggleSubVideo;
    [SerializeField] Toggle togglePubAudio;
    [SerializeField] Toggle togglePubVideo;

    public const string TOGGLE_SUB_AUDIO = "TOGGLE_SUB_AUDIO";
    public const string TOGGLE_SUB_VIDEO = "TOGGLE_SUB_VIDEO";
    public const string TOGGLE_PUB_AUDIO = "TOGGLE_PUB_AUDIO";
    public const string TOGGLE_PUB_VIDEO = "TOGGLE_PUB_VIDEO";


    // Start is called before the first frame update
    void Start()
    {
        toggleSubAudio.onValueChanged.AddListener(HandleSubAudioToggle);
        toggleSubVideo.onValueChanged.AddListener(HandleSubVideoToggle);
        togglePubAudio.onValueChanged.AddListener(HandlePubAudioToggle);
        togglePubVideo.onValueChanged.AddListener(HandlePubVideoToggle);
    }

    private void OnDisable()
    {
        Debug.LogWarning(name + " disabled");

        PlayerPrefs.SetInt(TOGGLE_SUB_AUDIO, toggleSubAudio.isOn ? 1 : 0);
        PlayerPrefs.SetInt(TOGGLE_SUB_VIDEO, toggleSubVideo.isOn ? 1 : 0);
        PlayerPrefs.SetInt(TOGGLE_PUB_AUDIO, togglePubAudio.isOn ? 1 : 0);
        PlayerPrefs.SetInt(TOGGLE_PUB_VIDEO, togglePubVideo.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static ChannelMediaOptions GetAVOptions()
    {
        bool sa = PlayerPrefs.GetInt(TOGGLE_SUB_AUDIO, 1) == 1;
        bool sv = PlayerPrefs.GetInt(TOGGLE_SUB_VIDEO, 1) == 1;
        bool pa = PlayerPrefs.GetInt(TOGGLE_PUB_AUDIO, 1) == 1;
        bool pv = PlayerPrefs.GetInt(TOGGLE_PUB_VIDEO, 1) == 1;

        return new ChannelMediaOptions(sa, sv, pa, pv);
    }

    void HandleSubAudioToggle(bool isOn)
    {
        IRtcEngine engine = IRtcEngine.QueryEngine();
        engine?.MuteAllRemoteAudioStreams(!isOn);
    }

    void HandleSubVideoToggle(bool isOn)
    {
        IRtcEngine engine = IRtcEngine.QueryEngine();
        engine?.MuteAllRemoteVideoStreams(!isOn);
    }

    void HandlePubAudioToggle(bool isOn)
    {
        IRtcEngine engine = IRtcEngine.QueryEngine();
        engine?.MuteLocalAudioStream(!isOn);
    }
    void HandlePubVideoToggle(bool isOn)
    {
        IRtcEngine engine = IRtcEngine.QueryEngine();
        if (isOn)
        {
            engine.EnableVideo();
            engine.EnableVideoObserver();
        }
        engine?.MuteLocalVideoStream(!isOn);
    }

    private void OnDestroy()
    {
        PlayerPrefs.DeleteAll();
    }
}
