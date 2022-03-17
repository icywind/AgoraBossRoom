using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using Newtonsoft.Json;

using agora_gaming_rtc;
using agora_game_model;
using agora_utilities;
using Unity.Multiplayer.Samples.BossRoom.Client;
using Unity.Multiplayer.Samples.BossRoom.Visual;

namespace agora_game_control
{
    public class BossRoomController : MonoBehaviour
    {
        IRtcEngine mRtcEngine;
        Transform RemoteUserSpawnParent { get; set; }

        [SerializeField]
        GameObject VideoViewPrefab;

        [SerializeField]
        ClientPlayerAvatarRuntimeCollection m_PlayerAvatars;

        Dictionary<uint, GameObject> UserViews = new Dictionary<uint, GameObject>();
        Dictionary<ulong, uint> ClientUIDMap = new Dictionary<ulong, uint>();
        Dictionary<uint, UserInfoModel> UserInfoDict = new Dictionary<uint, UserInfoModel>();
        Dictionary<ulong, ClientPlayerAvatar> ClientAvatarMap = new Dictionary<ulong, ClientPlayerAvatar>();

        public uint AgoraUID { get; private set; }

        public bool IsHost => GameNetPortal.Instance.NetManager.IsHost;
        public bool IsServer => GameNetPortal.Instance.NetManager.IsServer;

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

            var portalName = GameNetPortal.Instance.ChannelInfo;
            string playerName = GameNetPortal.Instance.PlayerName;

            Debug.Log("Joining channel, portalName is " + portalName + $" playerName:{playerName}  IsHost:{IsHost} ");

            var chanOptions = AgoraAVOptionController.GetAVOptions();
            mRtcEngine.JoinChannel("", "unity4d", "", 0, chanOptions);// TODO: Use the portal room name
        }

        private void OnDisable()
        {
            EndSession();
        }

        private void SetUpHandlers()
        {
            m_PlayerAvatars.ItemAdded += PlayerAvatarAdded;

            // set callbacks
            mRtcEngine.OnJoinChannelSuccess += onJoinChannelSuccess;
            mRtcEngine.OnLeaveChannel += OnLeaveChannelHandler;
            mRtcEngine.OnUserJoined += onUserJoined;
            mRtcEngine.OnUserOffline += onUserOffline;
            mRtcEngine.OnWarning += HandlerWarnings;
            mRtcEngine.OnError += HandleError;
            mRtcEngine.OnStreamMessage += OnStreamMessageHandler;

            mRtcEngine.OnAudioPublishStateChanged += OnAudioPublishStateChangedHandler;
            mRtcEngine.OnAudioSubscribeStateChanged += OnAudioSubscribeStateChangedHandler;
            mRtcEngine.OnVideoPublishStateChanged += OnVideoPublishStateChangedHandler;
            mRtcEngine.OnVideoSubscribeStateChanged += OnVideoSubscribeStateChangedHandler;
            mRtcEngine.OnVideoSizeChanged += OnVideoSizeChanged;
        }

        private void UnsetHandlers()
        {
            m_PlayerAvatars.ItemAdded -= PlayerAvatarAdded;

            mRtcEngine.OnJoinChannelSuccess -= onJoinChannelSuccess;
            mRtcEngine.OnUserJoined -= onUserJoined;
            mRtcEngine.OnUserOffline -= onUserOffline;
            mRtcEngine.OnWarning -= HandlerWarnings;
            mRtcEngine.OnError -= HandleError;
            mRtcEngine.OnStreamMessage -= OnStreamMessageHandler;

            mRtcEngine.OnAudioPublishStateChanged -= OnAudioPublishStateChangedHandler;
            mRtcEngine.OnAudioSubscribeStateChanged -= OnAudioSubscribeStateChangedHandler;
            mRtcEngine.OnVideoPublishStateChanged -= OnVideoPublishStateChangedHandler;
            mRtcEngine.OnVideoSubscribeStateChanged -= OnVideoSubscribeStateChangedHandler;
            mRtcEngine.OnVideoSizeChanged -= OnVideoSizeChanged;

        }

        public void EndSession()
        {
            if (mRtcEngine != null)
            {
                mRtcEngine.DisableVideoObserver();
                mRtcEngine.DisableVideo();
                mRtcEngine.LeaveChannel();
                UnsetHandlers();
                mRtcEngine = null;
            }
        }

        private void CleanupViews()
        {
            foreach (var uid in UserViews.Keys)
            {
                Destroy(UserViews[uid]);
            }
            UserViews.Clear();
        }

        void PlayerAvatarAdded(ClientPlayerAvatar clientPlayerAvatar)
        {
            // AssignUserToAvatar(clientPlayerAvatar.gameObject, clientPlayerAvatar.OwnerClientId);
            ClientAvatarMap[clientPlayerAvatar.OwnerClientId] = clientPlayerAvatar;
        }

        #region -- Engine Callback Events --
        // implement engine callbacks
        private void onJoinChannelSuccess(string channelName, uint uid, int elapsed)
        {
            AgoraUID = uid;
            var clientId = NetworkManager.Singleton.LocalClientId;
            Debug.Log("JoinChannelSuccessHandler: uid = " + uid);
            GameObject go = GameObject.Find("Hero HUD");
            GameObject view = MakeImageSurface(0, go.transform, VideoViewPrefab, true);
            UserViews[0] = view;
            ClientUIDMap[clientId] = 0;

            RectTransform rt = view.GetComponent<RectTransform>();
            rt.anchorMin = go.transform.GetComponent<RectTransform>().anchorMin;
            rt.anchorMax = go.transform.GetComponent<RectTransform>().anchorMax;
            view.transform.localPosition = new Vector3(-60, -90, 0);

            UIHUDButton button = view.GetComponent<UIHUDButton>();
            button.OnPointerUpEvent = delegate
            {
                HandleAvatorButton(clientID: clientId);
                MarkAction(0);
            };
        }

        private void OnLeaveChannelHandler(RtcStats stats)
        {
            Debug.Log("Leaving Channel and cleaning up views");
            CleanupViews();
            this.enabled = false;
        }

        // When a remote user joined, this delegate will be called. Typically
        // create a GameObject to render video on it
        private void onUserJoined(uint uid, int elapsed)
        {
            Debug.Log("onUserJoined: uid = " + uid + " elapsed = " + elapsed);
            // this is called in main thread
            // Send data at this poin since everyone has set up the callback
            SendMyInfoData();

            // find a game object to render video stream from 'uid'
            GameObject go = GameObject.Find(uid.ToString());
            if (go != null)
            {
                return; // reuse
            }

            // create a GameObject and assign to this new user
            go = MakeImageSurface(uid, RemoteUserSpawnParent, VideoViewPrefab);
            UserViews[uid] = go;

            StartCoroutine(CoBindUserJoinInvocation(uid, HandleAvatorButton));
        }

        void HandleAvatorButton(ulong clientID)
        {
            if (ClientAvatarMap.ContainsKey(clientID))
            {
                GameObject aObj = ClientAvatarMap[clientID].gameObject;
                ViewTarget view = aObj.GetComponent<ViewTarget>();

                if (view)
                {
                    var targetObj = view.ViewTargetImage;
                    if (null == targetObj.GetComponent<VideoSurface>())
                    {
                        targetObj.SetActive(true);
                        AssignUserToAvatar(target: targetObj, clientId: clientID);
                    }
                    else
                    {
                        // is it showing video
                        if (targetObj.activeInHierarchy)
                        {
                            targetObj.SetActive(false);
                        }
                        else
                        {
                            targetObj.SetActive(true);
                        }
                    }
                }

                // Target the avatar
                MarkAction(clientID);
            }
        }

        void MarkAction(ulong clientID)
        {
            if (ClientAvatarMap.ContainsKey(clientID))
            {
                ulong id = ClientAvatarMap[clientID].NetworkObjectId;
                var senderExist = ClientAvatarMap[clientID].TryGetComponent(out ClientInputSender clientSender);
                if (senderExist)
                {
                    clientSender.RequestAction(ActionType.GeneralTarget, ClientInputSender.SkillTriggerStyle.UI, id);
                }
            }
        }

        IEnumerator CoBindUserJoinInvocation(uint uid, System.Action<ulong> action)
        {
            yield return new WaitUntil(() => UserInfoDict.ContainsKey(uid));
            ulong clientID = UserInfoDict[uid].ClientID;
            UIHUDButton button = UserViews[uid].GetComponent<UIHUDButton>();
            if (button)
            {
                button.OnPointerUpEvent = delegate
                {
                    action(clientID);
                };
            }
            else
            {
                Debug.LogError($"GameObject: {UserViews[uid].name} does not have UIHUDButton");
            }
        }

        /// <summary>
        ///   handling data stream message from remote users. Messsage is in form
        /// of JSON string which should map to UserInfoModel
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="streamId"></param>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        void OnStreamMessageHandler(uint userId, int streamId, byte[] buffer, int length)
        {
            string json = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
            Debug.Log("Receive JSON:" + json);

            try
            {
                UserInfoModel userInfo = JsonConvert.DeserializeObject<UserInfoModel>(json);
                Debug.Log("Received user info:" + userInfo.ToString());
                ClientUIDMap[userInfo.ClientID] = userInfo.UID;
                UserInfoDict[userInfo.UID] = userInfo;
            }
            catch
            {
                Debug.LogError("Invalid json:" + json);
            }
        }

        void OnVideoPublishStateChangedHandler(string channel, STREAM_PUBLISH_STATE oldState, STREAM_PUBLISH_STATE newState, int elapseSinceLastState)
        {
            Debug.LogWarning("OnVideoPublishStateChanged: " + newState);
            if (newState == STREAM_PUBLISH_STATE.PUB_STATE_NO_PUBLISHED)
            {
                mRtcEngine.EnableLocalVideo(false);
                ToggleVideoSurface(0, false);
            }
            else if (newState == STREAM_PUBLISH_STATE.PUB_STATE_PUBLISHING)
            {
                // The following is handled by the AgoraAVOptionController
                // mRtcEngine.EnableLocalVideo(true);
                ToggleVideoSurface(0, true);
            }
        }

        void OnAudioPublishStateChangedHandler(string channel, STREAM_PUBLISH_STATE oldState, STREAM_PUBLISH_STATE newState, int elapseSinceLastState)
        {
            Debug.LogWarning("OnAudioPublishStateChanged: " + newState);
        }

        void OnAudioSubscribeStateChangedHandler(string channel, uint uid, STREAM_SUBSCRIBE_STATE oldState, STREAM_SUBSCRIBE_STATE newState, int elapseSinceLastState)
        {
            Debug.LogWarning("OnAudioStateSubChanged: " + newState);
        }

        void OnVideoSubscribeStateChangedHandler(string channel, uint uid, STREAM_SUBSCRIBE_STATE oldState, STREAM_SUBSCRIBE_STATE newState, int elapseSinceLastState)
        {
            Debug.LogWarning("OnVideoStateSubChanged: " + newState + " uid:" + uid);
            if (newState == STREAM_SUBSCRIBE_STATE.SUB_STATE_SUBSCRIBED)
            {
                ToggleVideoSurface(uid, true);
            }
            else if (newState == STREAM_SUBSCRIBE_STATE.SUB_STATE_NO_SUBSCRIBED)
            {
                ToggleVideoSurface(uid, false);
            }
        }
        void OnVideoSizeChanged(uint uid, int width, int height, int rotation)
        {
            if (UserViews.ContainsKey(uid))
            {
                var go = UserViews[uid];
                var target = go.GetComponentInChildren<ViewTarget>();
                var video = target.ViewTargetImage.GetComponent<VideoSurface>();

                RawImage image = video.GetComponent<RawImage>();
                Vector2 v2 = AgoraUIUtils.GetScaledDimension(width, height, 160f);
                image.rectTransform.sizeDelta = v2;
            }
        }
        #endregion

        void SendMyInfoData()
        {
            var clientId = NetworkManager.Singleton.LocalClientId;
            string playerName = GameNetPortal.Instance.PlayerName;

            SendUserInfo(AgoraUID, clientId, playerName);
        }

        void ToggleVideoSurface(uint uid, bool onOff)
        {
            if (UserViews.ContainsKey(uid))
            {
                var go = UserViews[uid];
                var target = go.GetComponentInChildren<ViewTarget>();
                var video = target.ViewTargetImage.GetComponent<VideoSurface>();
                video.SetEnable(onOff);
            }
        }

        GameObject MakeImageSurface(uint uid, Transform parentTrans, GameObject prefab, bool mirror = false)
        {
            GameObject go = Instantiate(prefab);

            if (go == null)
            {
                return null;
            }

            go.name = uid == 0 ? "LocalView" : uid.ToString();
            go.transform.SetParent(parentTrans);

            // set up transform
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            // configure videoSurface
            ViewTarget target = go.GetComponentInChildren<ViewTarget>();
            // ViewTarget contains a reference to the RawImage used for rendering the video
            VideoSurface videoSurface = target.ViewTargetImage.gameObject.AddComponent<VideoSurface>();
            if (!ReferenceEquals(videoSurface, null))
            {
                // configure videoSurface
                videoSurface.SetForUser(uid);
                videoSurface.SetEnable(true);
            }
            videoSurface.transform.localScale = new Vector3(mirror ? -1 : 1, -1, 1);

            return go;
        }

        // When remote user is offline, this delegate will be called. Typically
        // delete the GameObject for this user
        private void onUserOffline(uint uid, USER_OFFLINE_REASON reason)
        {
            // remove video stream
            Debug.Log("onUserOffline: uid = " + uid + " reason = " + reason);
            if (UserViews.ContainsKey(uid))
            {
                // this is called in main thread
                GameObject go = UserViews[uid];
                if (!ReferenceEquals(go, null))
                {
                    Destroy(go);
                }
                UserViews.Remove(uid);
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

        public void SendUserInfo(uint uid, ulong clientId, string name)
        {
            UserInfoModel userInfoModel = new UserInfoModel() { ClientID = clientId, UID = uid, Name = name };
            var json = JsonConvert.SerializeObject(userInfoModel);
            Debug.Log("Sending JSON:" + json);
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
            mRtcEngine.SendStreamMessage(AgoraContoller.Instance.DataStreamID, data);
        }

        public void AssignUserToAvatar(GameObject target, ulong clientId)
        {
            // configure videoSurface
            // ViewTarget contains a reference to the RawImage used for rendering the video
            VideoSurface videoSurface = target.AddComponent<VideoSurface>();
            if (!ReferenceEquals(videoSurface, null))
            {
                //// configure videoSurface
                videoSurface.SetForUser(ClientUIDMap[clientId]);
                videoSurface.SetEnable(true);
            }
        }
    }
}
