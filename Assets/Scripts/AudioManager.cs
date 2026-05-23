using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 統一管理遊戲中所有音效 (SFX) 與背景音樂 (BGM) 的管理器。
/// 支援單例模式 (Singleton)，可於任何腳本中以 AudioManager.PlaySound("音效名稱") 呼叫。
/// </summary>
public class AudioManager : MonoBehaviour
{
    // 單例模式唯一實例
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class SoundClip
    {
        [Tooltip("音效的識別名稱，用於在程式中呼叫 (例如: Mom_HangUp, ButtonClick)")]
        public string name;
        [Tooltip("對應的音訊檔案 (AudioClip)")]
        public AudioClip clip;
    }

    [Header("音效與音樂清單")]
    [Tooltip("在此設定所有音效 (SFX) 的對應名稱與檔案")]
    public List<SoundClip> soundEffects = new List<SoundClip>();
    [Tooltip("在此設定所有背景音樂 (BGM) 的對應名稱與檔案")]
    public List<SoundClip> backgroundMusics = new List<SoundClip>();

    [Header("音源元件 (AudioSource)")]
    [Tooltip("用於播放背景音樂的音源元件 (選填，若無指定會自動建立)")]
    public AudioSource bgmSource;
    [Tooltip("用於播放音效的音源元件 (選填，若無指定會自動建立)")]
    public AudioSource sfxSource;

    // 用於快速搜尋音效與音樂的字典 (Dictionary)
    private Dictionary<string, AudioClip> sfxDictionary = new Dictionary<string, AudioClip>();
    private Dictionary<string, AudioClip> bgmDictionary = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        // 確保場景中只有一個 AudioManager 實例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨場景不銷毀，保持音樂不中斷
            InitializeAudioSources();
            InitializeDictionaries();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 初始化音源元件，若未手動指派則自動建立
    /// </summary>
    private void InitializeAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }
    }

    /// <summary>
    /// 將 Inspector 中的清單轉存成 Dictionary 以便快速查詢
    /// </summary>
    private void InitializeDictionaries()
    {
        sfxDictionary.Clear();
        foreach (var sfx in soundEffects)
        {
            if (!string.IsNullOrEmpty(sfx.name) && sfx.clip != null)
            {
                if (!sfxDictionary.ContainsKey(sfx.name))
                {
                    sfxDictionary.Add(sfx.name, sfx.clip);
                }
                else
                {
                    Debug.LogWarning($"[AudioManager] 重複的音效名稱設定: {sfx.name}");
                }
            }
        }

        bgmDictionary.Clear();
        foreach (var bgm in backgroundMusics)
        {
            if (!string.IsNullOrEmpty(bgm.name) && bgm.clip != null)
            {
                if (!bgmDictionary.ContainsKey(bgm.name))
                {
                    bgmDictionary.Add(bgm.name, bgm.clip);
                }
                else
                {
                    Debug.LogWarning($"[AudioManager] 重複的 BGM 名稱設定: {bgm.name}");
                }
            }
        }
    }

    /// <summary>
    /// [靜態方法] 供外部快速播放音效，無縫接軌原對話系統。
    /// 呼叫範例：AudioManager.PlaySound("Mom_HangUp");
    /// </summary>
    /// <param name="soundName">音效的識別名稱</param>
    public static void PlaySound(string soundName)
    {
        if (Instance != null)
        {
            Instance.PlaySFX(soundName);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] 無法播放音效 \"{soundName}\"，因為場景中尚未建立 AudioManager 實例！");
        }
    }

    /// <summary>
    /// 播放音效 (SFX) — 支援同時播放多個音效
    /// </summary>
    /// <param name="soundName">音效的識別名稱</param>
    public void PlaySFX(string soundName)
    {
        if (sfxDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            if (sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
                Debug.Log($"[AudioManager] 播放音效: {soundName}");
            }
        }
        else
        {
            Debug.LogWarning($"[AudioManager] 找不到音效名稱: \"{soundName}\"，請確認 Inspector 中的設定！");
        }
    }

    /// <summary>
    /// 播放循環音效 (例如來電鈴聲)
    /// </summary>
    public void PlayLoopingSFX(string soundName)
    {
        if (sfxDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            if (sfxSource != null)
            {
                sfxSource.Stop();
                sfxSource.clip = clip;
                sfxSource.loop = true;
                sfxSource.Play();
                Debug.Log($"[AudioManager] 播放循環音效: {soundName}");
            }
        }
        else
        {
            Debug.LogWarning($"[AudioManager] 找不到循環音效名稱: \"{soundName}\"，請確認 Inspector 中的設定！");
        }
    }

    /// <summary>
    /// 停止播放循環音效，並將音源重置為單次播放
    /// </summary>
    public void StopLoopingSFX()
    {
        if (sfxSource != null)
        {
            sfxSource.Stop();
            sfxSource.clip = null;
            sfxSource.loop = false;
            Debug.Log("[AudioManager] 已停止播放循環音效。");
        }
    }

    /// <summary>
    /// 播放背景音樂 (BGM) — 自動處理音樂切換與漸變 (可擴充)
    /// </summary>
    /// <param name="musicName">音樂的識別名稱</param>
    /// <param name="loop">是否循環播放</param>
    public void PlayBGM(string musicName, bool loop = true)
    {
        if (bgmDictionary.TryGetValue(musicName, out AudioClip clip))
        {
            if (bgmSource != null)
            {
                // 如果目前正在播放同一首音樂，就直接忽略
                if (bgmSource.clip == clip && bgmSource.isPlaying) return;

                bgmSource.Stop();
                bgmSource.clip = clip;
                bgmSource.loop = loop;
                bgmSource.Play();
                Debug.Log($"[AudioManager] 播放背景音樂: {musicName}");
            }
        }
        else
        {
            Debug.LogWarning($"[AudioManager] 找不到背景音樂名稱: \"{musicName}\"，請確認 Inspector 中的設定！");
        }
    }

    /// <summary>
    /// 停止播放背景音樂 (BGM)
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
            Debug.Log("[AudioManager] 背景音樂已停止。");
        }
    }

    /// <summary>
    /// 調整背景音樂的主音量 (0.0f ~ 1.0f)
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        if (bgmSource != null)
        {
            bgmSource.volume = Mathf.Clamp01(volume);
        }
    }

    /// <summary>
    /// 調整音效的主音量 (0.0f ~ 1.0f)
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = Mathf.Clamp01(volume);
        }
    }
}
