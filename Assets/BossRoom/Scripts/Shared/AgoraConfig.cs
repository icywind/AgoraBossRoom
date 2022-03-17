using UnityEngine;
using System.IO;
using Newtonsoft.Json;

namespace agora_game_model
{

    public class AgoraConfigModel
    {
        public string Region { get; set; }
        public string RoomName { get; set; }
    }

    public class AgoraConfig : MonoBehaviour
    {
        static string ConfigFile
        {
            get
            {
                return Application.persistentDataPath + "/agora_config.json";
            }
        }

        public static AgoraConfigModel AgoraGameConfig { get; private set; }

        void Awake()
        {
            Debug.Log("AgoraConfig, persistentDataPath = " + Application.persistentDataPath);
            var config = ReadData();
            if (config == null)
            {
                config = new AgoraConfigModel() { Region = "USW", RoomName = "AGORA" };
                WriteModel(config);
            }
            AgoraGameConfig = config;
        }

        public static AgoraConfigModel ReadData()
        {
            //AgoraConfigModel config = new AgoraConfigModel { Region = "USW", RoomName = "AGORA" };
            AgoraConfigModel config = null;

            //Read the text from directly from the test.txt file
            try
            {
                StreamReader reader = new StreamReader(ConfigFile);
                string json = reader.ReadToEnd();
                Debug.Log("CONFIG JSON:" + json);

                reader.Close();
                config = JsonConvert.DeserializeObject<AgoraConfigModel>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e.ToString());
            }
            return config;
        }


        public static void WriteModel(AgoraConfigModel model)
        {
            string json = JsonConvert.SerializeObject(model);
            try
            {
                StreamWriter writer = new StreamWriter(ConfigFile);
                writer.Write(json);
                writer.Flush();
                AgoraGameConfig = model;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e.ToString());
            }
        }
    }

}
