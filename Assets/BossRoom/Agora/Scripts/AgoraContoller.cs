using UnityEngine;
using UnityEngine.SceneManagement;

using agora_gaming_rtc;

namespace agora_game_control
{
    public class AgoraContoller : MonoBehaviour
    {
        // PLEASE KEEP THIS App ID IN SAFE PLACE
        // Get your own App ID at https://dashboard.agora.io/
        [SerializeField]
        private string AppID = "your_appid";
        [SerializeField]
        GameObject DebugConsole;

        public static AgoraContoller Instance { get; private set; }
        public IRtcEngine mRtcEngine { get; private set; }

        public bool IsInitialized { get; private set; }
        public int DataStreamID { get; private set; }

        BossRoomController _BossRoomController { get; set; }

        const int DAREA = 100;

        void Awake()
        {
            // keep this alive across scenes
            DontDestroyOnLoad(this.gameObject);

            IsInitialized = CheckAppId();

            SceneManager.sceneLoaded += OnLevelFinishedLoading;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // Singleton Pattern
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
            _BossRoomController = GetComponent<BossRoomController>();
        }

        private void Start()
        {
            if (DebugConsole)
            {
                DebugConsole.SetActive(false);
            }

            if (IsInitialized)
            {
                LoadEngine(AppID);
            }
        }

        void Update()
        {
            PermissionHelper.RequestCameraPermission();
            PermissionHelper.RequestMicrophontPermission();

            if (Input.touchCount == 3)
            {
                var touch = Input.GetTouch(2);
                Debug.Log(touch.position);
                if (touch.position.x < DAREA && touch.position.y < DAREA)
                {
                    DebugConsole.SetActive(!DebugConsole.activeInHierarchy);
                }
            }
        }

        void OnGUI()
        {
            Event e = Event.current;
            if (e.isMouse)
            {
                if (e.clickCount > 2)
                {
                    // Debug.Log("Mouse pos = " + e.mousePosition);
                    // toggle gameobject when 3x tap at top-left corner
                    if (e.mousePosition.x < DAREA && e.mousePosition.y < DAREA)
                    {
                        DebugConsole.SetActive(!DebugConsole.activeInHierarchy);
                    }
                }
            }
        }

        private bool CheckAppId()
        {
            Debug.Assert(AppID.Length > 10, "Please fill in your AppId first on Game Controller object.");
            return AppID.Length > 10;
        }

        void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            switch (scene.name)
            {
                case "MainMenu":
                    break;
                case "CharSelect":
                    ShowPreview();
                    break;
                case "BossRoom":
                    _BossRoomController.enabled = true;
                    break;
                case "PostGame":
                    break;
                case "StartUp":
                    break;
                default:
                    break;
            }
        }

        void OnSceneUnloaded(Scene scene)
        {
            switch (scene.name)
            {
                case "MainMenu":
                    break;
                case "CharSelect":
                    StopPreview();
                    break;
                case "BossRoom":
                    _BossRoomController.EndSession();
                    break;
                case "PostGame":
                    break;
                case "StartUp":
                    break;
                default:
                    break;
            }

        }

        #region Engine Code
        // load agora engine
        void LoadEngine(string appId)
        {
            // start sdk
            Debug.Log("initializeEngine");

            if (mRtcEngine != null)
            {
                Debug.Log("Engine exists. Please unload it first!");
                return;
            }

            // init engine
            mRtcEngine = IRtcEngine.GetEngine(appId);

            // mRtcEngine.SetLogFile("agora.log");
            // enable log
            mRtcEngine.SetLogFilter(LOG_FILTER.DEBUG | LOG_FILTER.INFO | LOG_FILTER.WARNING | LOG_FILTER.ERROR | LOG_FILTER.CRITICAL);

            // create a datastream
            DataStreamID = mRtcEngine.CreateDataStream(true, true);
        }


        private void OnApplicationQuit()
        {
            // delete
            if (mRtcEngine != null)
            {
                IRtcEngine.Destroy();  // Place this call in ApplicationQuit
                mRtcEngine = null;
            }
        }
        #endregion

        #region CharSelect
        bool _isPreviewing = false;
        void ShowPreview()
        {
            GameObject go = GameObject.Find("SelfVideoView");
            if (go != null)
            {
                // configure videoSurface
                ViewTarget target = go.GetComponentInChildren<ViewTarget>();
                target.ViewTargetImage.gameObject.AddComponent<VideoSurface>();
            }
            mRtcEngine.EnableVideo();
            mRtcEngine.EnableVideoObserver();
            mRtcEngine.StartPreview();
            _isPreviewing = true;
        }

        void StopPreview()
        {
            if (_isPreviewing)
            {
                mRtcEngine.StopPreview();
                mRtcEngine.DisableVideoObserver();
                mRtcEngine.DisableVideo();
            }
        }
        #endregion
    }
}
