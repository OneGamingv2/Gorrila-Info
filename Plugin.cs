using BepInEx;
using PlayFab.ClientModels;
using UnityEngine;

namespace GorillaInfo
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            GameObject loader = new GameObject("gorillainfopad");
            DontDestroyOnLoad(loader);
        }
    }
}
