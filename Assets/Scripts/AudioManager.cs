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
        [Range(0f, 1f)]
        [Tooltip("此音訊的個別音量微調比例 (0.0 ~ 1.0，預設為 1.0)")]
        public float volume = 1f;
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

    [Header("主音量設定 (可在 Inspector 拖動測試)")]
    [Range(0f, 1f)]
    [Tooltip("背景音樂主音量 (0~1)")]
    public float bgmVolumeSetting = 1f;

    [Range(0f, 1f)]
    [Tooltip("音效主音量 (0~1)")]
    public float sfxVolumeSetting = 1f;

    // 用於快速搜尋音效與音樂的字典 (Dictionary)
    private Dictionary<string, SoundClip> sfxDictionary = new Dictionary<string, SoundClip>();
    private Dictionary<string, SoundClip> bgmDictionary = new Dictionary<string, SoundClip>();

    // 當前播放的音訊設定 (用於即時縮放音量)
    private SoundClip activeBGMClip;
    private SoundClip activeSFXClip; // 用於循環音效

    private Coroutine bgmTransitionCoroutine;
    private Coroutine bgmVolumeFadeCoroutine; // 新增：用於動態音量漸變的協程

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

    private void OnValidate()
    {
        // 當在 Inspector 中拖動滑桿時，即時同步音量並儲存
        if (Application.isPlaying)
        {
            BGMVolume = bgmVolumeSetting;
            SFXVolume = sfxVolumeSetting;
        }
        else
        {
            // 編輯模式下僅更新預覽，不寫入 PlayerPrefs
            if (bgmSource != null) bgmSource.volume = bgmVolumeSetting;
            if (sfxSource != null) sfxSource.volume = sfxVolumeSetting;
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

        // 初始化載入音量
        bgmVolumeSetting = PlayerPrefs.GetFloat("BGMVolume", 1f);
        sfxVolumeSetting = PlayerPrefs.GetFloat("SFXVolume", 1f);

        bgmSource.volume = BGMVolume;
        sfxSource.volume = SFXVolume;
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
                    sfxDictionary.Add(sfx.name, sfx);
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
                    bgmDictionary.Add(bgm.name, bgm);
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
    /// 供外部獲取已註冊音效的 AudioClip
    /// </summary>
    public AudioClip GetSFXClip(string soundName)
    {
        if (sfxDictionary.TryGetValue(soundName, out SoundClip sfx))
        {
            return sfx.clip;
        }
        return null;
    }

    /// <summary>
    /// 獲取特定音效的個別音量微調比例 (0.0 ~ 1.0)
    /// </summary>
    public float GetSFXVolumeScale(string soundName)
    {
        if (sfxDictionary.TryGetValue(soundName, out SoundClip sfx))
        {
            return sfx.volume;
        }
        return 1f;
    }

    /// <summary>
    /// 播放音效 (SFX) — 支援同時播放多個音效
    /// </summary>
    /// <param name="soundName">音效的識別名稱</param>
    public void PlaySFX(string soundName)
    {
        if (sfxDictionary.TryGetValue(soundName, out SoundClip sfx))
        {
            if (sfxSource != null)
            {
                sfxSource.PlayOneShot(sfx.clip, sfx.volume);
                Debug.Log($"[AudioManager] 播放音效: {soundName} (自訂個別音量比例: {sfx.volume})");
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
        if (sfxDictionary.TryGetValue(soundName, out SoundClip sfx))
        {
            if (sfxSource != null)
            {
                activeSFXClip = sfx;
                sfxSource.Stop();
                sfxSource.clip = sfx.clip;
                sfxSource.loop = true;
                sfxSource.volume = SFXVolume * sfx.volume;
                sfxSource.Play();
                Debug.Log($"[AudioManager] 播放循環音效: {soundName} (自訂個別音量比例: {sfx.volume})");
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
            activeSFXClip = null;
            Debug.Log("[AudioManager] 已停止播放循環音效。");
        }
    }

    /// <summary>
    /// 播放背景音樂 (BGM)
    /// </summary>
    /// <param name="musicName">音樂的識別名稱</param>
    /// <param name="loop">是否循環播放</param>
    public void PlayBGM(string musicName, bool loop = true)
    {
        if (bgmDictionary.TryGetValue(musicName, out SoundClip bgm))
        {
            if (bgmSource != null)
            {
                // 如果目前正在播放同一首音樂，就直接忽略
                if (bgmSource.clip == bgm.clip && bgmSource.isPlaying) return;

                // 停止可能正在進行的漸變或音量漸弱協程
                if (bgmTransitionCoroutine != null)
                {
                    StopCoroutine(bgmTransitionCoroutine);
                    bgmTransitionCoroutine = null;
                }
                if (bgmVolumeFadeCoroutine != null)
                {
                    StopCoroutine(bgmVolumeFadeCoroutine);
                    bgmVolumeFadeCoroutine = null;
                }

                bgmSource.Stop();
                activeBGMClip = bgm;
                bgmSource.clip = bgm.clip;
                bgmSource.loop = loop;
                bgmSource.volume = BGMVolume * bgm.volume;
                bgmSource.Play();
                Debug.Log($"[AudioManager] 播放背景音樂: {musicName} (自訂個別音量比例: {bgm.volume})");
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
            // 停止可能正在進行的協程
            if (bgmTransitionCoroutine != null)
            {
                StopCoroutine(bgmTransitionCoroutine);
                bgmTransitionCoroutine = null;
            }
            if (bgmVolumeFadeCoroutine != null)
            {
                StopCoroutine(bgmVolumeFadeCoroutine);
                bgmVolumeFadeCoroutine = null;
            }

            bgmSource.Stop();
            activeBGMClip = null;
            Debug.Log("[AudioManager] 背景音樂已停止。");
        }
    }

    /// <summary>
    /// 平滑切換背景音樂，包含舊音樂漸弱與新音樂漸強
    /// </summary>
    public void TransitionToBGM(string nextBGMName, float fadeDuration = 1.5f)
    {
        if (bgmTransitionCoroutine != null)
        {
            StopCoroutine(bgmTransitionCoroutine);
        }
        if (bgmVolumeFadeCoroutine != null)
        {
            StopCoroutine(bgmVolumeFadeCoroutine);
            bgmVolumeFadeCoroutine = null;
        }
        bgmTransitionCoroutine = StartCoroutine(TransitionToBGMRoutine(nextBGMName, fadeDuration));
    }

    private IEnumerator TransitionToBGMRoutine(string nextBGMName, float fadeDuration)
    {
        float startVolumeSetting = BGMVolume;
        float startVolume = bgmSource != null ? bgmSource.volume : 0f;

        // 獲取當前播放音樂的音量比例 (如果是 0，就用 1f 作為分母安全值)
        float startClipVolume = activeBGMClip != null ? activeBGMClip.volume : 1f;
        float startFraction = (startVolumeSetting > 0f && startClipVolume > 0f) ? (startVolume / (startVolumeSetting * startClipVolume)) : 1f;
        startFraction = Mathf.Clamp01(startFraction);

        // 1. 漸弱 (Fade Out)
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedProgress = Mathf.Clamp01(elapsed / fadeDuration);
            float currentFraction = Mathf.Lerp(startFraction, 0f, normalizedProgress);
            if (bgmSource != null)
            {
                bgmSource.volume = currentFraction * BGMVolume * startClipVolume; // 即時反應調整的主音量
            }
            yield return null;
        }

        if (bgmSource != null)
        {
            bgmSource.volume = 0f;
            bgmSource.Stop();
        }

        // 2. 獲取並切換音樂
        if (bgmDictionary.TryGetValue(nextBGMName, out SoundClip nextClip))
        {
            activeBGMClip = nextClip;
            if (bgmSource != null)
            {
                bgmSource.clip = nextClip.clip;
                bgmSource.loop = true;
                bgmSource.Play();
                Debug.Log($"[AudioManager] 開始播放新 BGM (漸強): {nextBGMName}");
            }
        }
        else
        {
            Debug.LogWarning($"[AudioManager] 找不到背景音樂名稱: \"{nextBGMName}\"！無法漸強播放。");
            bgmTransitionCoroutine = null;
            yield break;
        }

        float nextClipVolume = activeBGMClip != null ? activeBGMClip.volume : 1f;

        // 3. 漸強 (Fade In)
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedProgress = Mathf.Clamp01(elapsed / fadeDuration);
            float currentFraction = Mathf.Lerp(0f, 1f, normalizedProgress);
            if (bgmSource != null)
            {
                bgmSource.volume = currentFraction * BGMVolume * nextClipVolume; // 即時反應調整的主音量
            }
            yield return null;
        }

        // 最終恢復為新 BGM 的目標音量值
        if (bgmSource != null)
        {
            bgmSource.volume = BGMVolume * nextClipVolume;
        }
        bgmTransitionCoroutine = null;
    }

    /// <summary>
    /// 動態將當前 BGM 音量比例漸變到指定的比例 (targetFraction 介於 0.0 ~ 1.0)
    /// </summary>
    public void FadeBGMTo(float targetFraction, float duration)
    {
        if (bgmVolumeFadeCoroutine != null)
        {
            StopCoroutine(bgmVolumeFadeCoroutine);
        }
        // 漸變時也需要停掉其他的切換協程，避免打架
        if (bgmTransitionCoroutine != null)
        {
            StopCoroutine(bgmTransitionCoroutine);
            bgmTransitionCoroutine = null;
        }
        bgmVolumeFadeCoroutine = StartCoroutine(FadeBGMToRoutine(targetFraction, duration));
    }

    private IEnumerator FadeBGMToRoutine(float targetFraction, float duration)
    {
        float startVolumeSetting = BGMVolume;
        float startVolume = bgmSource != null ? bgmSource.volume : 0f;
        float clipVolume = activeBGMClip != null ? activeBGMClip.volume : 1f;

        // 計算目前的音量佔主音量及個別音量的比例分母
        float maxTargetVolume = startVolumeSetting * clipVolume;
        float startFraction = maxTargetVolume > 0f ? (startVolume / maxTargetVolume) : 1f;
        startFraction = Mathf.Clamp01(startFraction);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedProgress = Mathf.Clamp01(elapsed / duration);
            float currentFraction = Mathf.Lerp(startFraction, targetFraction, normalizedProgress);
            if (bgmSource != null)
            {
                bgmSource.volume = currentFraction * BGMVolume * clipVolume; // 即時適應隨時調整的 BGMVolume
            }
            yield return null;
        }

        if (bgmSource != null)
        {
            bgmSource.volume = targetFraction * BGMVolume * clipVolume;
        }
        bgmVolumeFadeCoroutine = null;
    }

    /// <summary>
    /// 背景音樂屬性，自動讀寫 PlayerPrefs 並套用至 bgmSource (結合當前音樂的個別比例)
    /// </summary>
    public float BGMVolume
    {
        get => PlayerPrefs.GetFloat("BGMVolume", 1f);
        set
        {
            float val = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("BGMVolume", val);
            bgmVolumeSetting = val; // 同步更新 Inspector 的數值
            if (bgmSource != null && bgmTransitionCoroutine == null && bgmVolumeFadeCoroutine == null)
            {
                float clipVolume = activeBGMClip != null ? activeBGMClip.volume : 1f;
                bgmSource.volume = val * clipVolume;
            }
        }
    }

    /// <summary>
    /// 音效屬性，自動讀寫 PlayerPrefs 並套用至 sfxSource (結合當前音效的個別比例)
    /// </summary>
    public float SFXVolume
    {
        get => PlayerPrefs.GetFloat("SFXVolume", 1f);
        set
        {
            float val = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("SFXVolume", val);
            sfxVolumeSetting = val; // 同步更新 Inspector 的數值
            if (sfxSource != null)
            {
                float clipVolume = activeSFXClip != null ? activeSFXClip.volume : 1f;
                sfxSource.volume = val * clipVolume;
            }
        }
    }

    /// <summary>
    /// 調整背景音樂的主音量 (0.0f ~ 1.0f) — 供 Slider 等外部調用
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        BGMVolume = volume;
    }

    /// <summary>
    /// 調整音效的主音量 (0.0f ~ 1.0f) — 供 Slider 等外部調用
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        SFXVolume = volume;
    }
}
