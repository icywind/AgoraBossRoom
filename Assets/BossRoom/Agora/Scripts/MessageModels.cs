using System;

namespace agora_game_model
{
    /// <summary>
    ///   CommonSignal is the base class of all signal messages.
    ///     Valid types are basically the name of the class without "Model"
    /// </summary>
    [Serializable]
    public class CommonSignalModel
    {
        public string Type { get; set; }
        public CommonSignalModel()
        {
            Type = "Common";
        }

        public override string ToString()
        {
            return "Type:" + Type;
        }
    }

    /// <summary>
    ///  Mapping of PhotonId and Agora UID
    /// </summary>
    [Serializable]
    public class UserInfoModel : CommonSignalModel
    {
        public uint UID { get; set; }
        public ulong ClientID { get; set; }
        public string Name { get; set; }
        public UserInfoModel()
        {
            Type = "UserInfo";
        }

        public override string ToString()
        {
            return base.ToString() + "\n" + $"Name:{Name}\n ClientId:{ClientID}\n UID:{UID}";
        }
    }

}
