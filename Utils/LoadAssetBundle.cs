using System.IO;
using System.Reflection;
using UnityEngine;

namespace GorillaInfo.LAB
{
    public class Assets
    {
        public static AssetBundle assetBundle;
        public static AssetBundle networkingAssetBundle;

        public static void LoadAssetBundle()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GorillaInfo.Resources.gorillainfo");
            if (stream != null)
                assetBundle = AssetBundle.LoadFromStream(stream);

            LoadNetworkingAssetBundle();
        }

        public static void LoadNetworkingAssetBundle()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GorillaInfo.Resources.networkingprefab");
            if (stream != null)
                networkingAssetBundle = AssetBundle.LoadFromStream(stream);
        }

        public static T LoadObject<T>(string assetName) where T : Object
        {
            if (assetBundle == null) LoadAssetBundle();
            return Object.Instantiate(assetBundle.LoadAsset<T>(assetName));
        }

        public static T LoadAsset<T>(string assetName) where T : Object
        {
            if (assetBundle == null) LoadAssetBundle();
            return assetBundle.LoadAsset(assetName) as T;
        }

        public static T LoadNetworkingAsset<T>(string assetName) where T : Object
        {
            if (networkingAssetBundle == null) LoadNetworkingAssetBundle();
            return networkingAssetBundle.LoadAsset(assetName) as T;
        }

        public static T LoadNetworkingObject<T>(string assetName) where T : Object
        {
            if (networkingAssetBundle == null) LoadNetworkingAssetBundle();
            return Object.Instantiate(networkingAssetBundle.LoadAsset<T>(assetName));
        }
    }
}
