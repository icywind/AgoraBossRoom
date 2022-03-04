using UnityEngine;
using UnityEngine.UI;
using agora_gaming_rtc;
using agora_utilities;

namespace agora_game_control {
    public class BossRoomController : MonoBehaviour
    {
        IRtcEngine mRtcEngine;
        Transform RemoteUserSpawnParent { get; set; }

        [SerializeField]
        GameObject VideoViewPrefab;

        VideoSurface HeroVideoView { get; set; }

        private void OnEnable()
        {
            // Get the engine instance, which should be created in the StartUp
            mRtcEngine = IRtcEngine.QueryEngine();
            Debug.Assert(mRtcEngine != null, "There is no RtcEngine!!!");
            if (mRtcEngine != null)
            {
                SetUpHandlers();
            }

            GameObject go = GameObject.Find("RemoteUserSpawnParent");
            if (go != null)
            {
                RemoteUserSpawnParent = go.transform;
            }

            mRtcEngine.EnableVideo();
            mRtcEngine.EnableVideoObserver();
            mRtcEngine.JoinChannel("unity3d");
        }

        private void OnDisable()
        {
            mRtcEngine.DisableVideoObserver();
            mRtcEngine.DisableVideo();
            mRtcEngine.LeaveChannel();
            UnsetHandlers();
        }

        private void SetUpHandlers()
        {
            // set callbacks
            mRtcEngine.OnJoinChannelSuccess += onJoinChannelSuccess;
            mRtcEngine.OnUserJoined += onUserJoined;
            mRtcEngine.OnUserOffline += onUserOffline;
            mRtcEngine.OnWarning += HandlerWarnings;
            mRtcEngine.OnError += HandleError;
        }

        private void UnsetHandlers()
        {
            mRtcEngine.OnJoinChannelSuccess -= onJoinChannelSuccess;
            mRtcEngine.OnUserJoined -= onUserJoined;
            mRtcEngine.OnUserOffline -= onUserOffline;
            mRtcEngine.OnWarning -= HandlerWarnings;
            mRtcEngine.OnError -= HandleError;
        }


        // implement engine callbacks
        private void onJoinChannelSuccess(string channelName, uint uid, int elapsed)
        {
            Debug.Log("JoinChannelSuccessHandler: uid = " + uid);
            GameObject go = GameObject.Find("Hero HUD");
            HeroVideoView = makeImageSurface("MyView", go.transform, VideoViewPrefab);
            // HeroVideoView = MakeImageSurface("MyView");
            HeroVideoView.SetForUser(0);
            HeroVideoView.SetEnable(true);

            var holder = HeroVideoView.transform.parent;
            RectTransform rt = holder.GetComponent<RectTransform>();
            rt.anchorMin = go.transform.GetComponent<RectTransform>().anchorMin;
            rt.anchorMax = go.transform.GetComponent<RectTransform>().anchorMax;
            holder.localPosition = new Vector3(-60, -90, 0);
        }

        // When a remote user joined, this delegate will be called. Typically
        // create a GameObject to render video on it
        private void onUserJoined(uint uid, int elapsed)
        {
            Debug.Log("onUserJoined: uid = " + uid + " elapsed = " + elapsed);
            // this is called in main thread

            // find a game object to render video stream from 'uid'
            GameObject go = GameObject.Find(uid.ToString());
            if (!ReferenceEquals(go, null))
            {
                return; // reuse
            }

            // create a GameObject and assign to this new user
            VideoSurface videoSurface = makeImageSurface(uid.ToString(), RemoteUserSpawnParent, VideoViewPrefab);
            if (!ReferenceEquals(videoSurface, null))
            {
                // configure videoSurface
                videoSurface.SetForUser(uid);
                videoSurface.SetEnable(true);
                videoSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
            }
            videoSurface.transform.localScale = new Vector3(1, -1, 1);
        }


        public VideoSurface makeImageSurface(string goName, Transform parentTrans, GameObject prefab)
        {
            GameObject go = Instantiate(prefab);

            if (go == null)
            {
                return null;
            }

            go.name = goName;
            go.transform.SetParent(parentTrans);

            // set up transform
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            // configure videoSurface
            VideoSurface videoSurface = go.GetComponentInChildren<VideoSurface>();
            return videoSurface;
        }

        // When remote user is offline, this delegate will be called. Typically
        // delete the GameObject for this user
        private void onUserOffline(uint uid, USER_OFFLINE_REASON reason)
        {
            // remove video stream
            Debug.Log("onUserOffline: uid = " + uid + " reason = " + reason);
            // this is called in main thread
            GameObject go = GameObject.Find(uid.ToString());
            if (!ReferenceEquals(go, null))
            {
                Object.Destroy(go);
            }
        }

        #region Error Handling
        private int LastError { get; set; }
        private void HandleError(int error, string msg)
        {
            if (error == LastError)
            {
                return;
            }

            if (string.IsNullOrEmpty(msg))
            {
                msg = string.Format("Error code:{0} msg:{1}", error, IRtcEngine.GetErrorDescription(error));
            }

            switch (error)
            {
                case 101:
                    msg += "\nPlease make sure your AppId is valid and it does not require a certificate for this demo.";
                    break;
            }

            Debug.LogError(msg);
            LastError = error;
        }

        void HandlerWarnings(int warn, string msg)
        {
            Debug.LogWarningFormat("Warning code:{0} msg:{1}", warn, IRtcEngine.GetErrorDescription(warn));
        }
        #endregion

    }
}
