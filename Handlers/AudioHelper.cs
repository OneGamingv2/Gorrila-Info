using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class AudioHelper : MonoBehaviour
{
    private static AudioHelper _instance;
    private AudioSource _audioSource;
    private static readonly Dictionary<string, AudioClip> _clipCache = new(4);

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;
    }

    private static void EnsureInstance()
    {
        if (_instance != null) return;
        GameObject obj = new GameObject("GorillaInfo_AudioHelper");
        _instance = obj.AddComponent<AudioHelper>();
    }

    public static void PlaySound(string fileName)
    {
        EnsureInstance();
        if (_clipCache.TryGetValue(fileName, out AudioClip cached))
        {
            _instance._audioSource.PlayOneShot(cached);
            GorillaTagger.Instance?.StartVibration(false, 0.3f, 0.15f);
            return;
        }
        _instance.StartCoroutine(_instance.LoadAndPlay(fileName));
    }

    private IEnumerator LoadAndPlay(string fileName)
    {
        string resourcePath = $"GorillaInfo.Resources.{fileName}";
        Assembly assembly = Assembly.GetExecutingAssembly();

        using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
        {
            if (stream == null) yield break;

            string tempPath = Path.Combine(Application.persistentDataPath, fileName);
            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                stream.CopyTo(fs);

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.WAV))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    if (clip != null)
                    {
                        _clipCache[fileName] = clip;
                        _audioSource.PlayOneShot(clip);
                        GorillaTagger.Instance?.StartVibration(false, 0.3f, 0.15f);
                    }
                }
                try { File.Delete(tempPath); } catch { }
            }
        }
    }
}
