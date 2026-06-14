using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 用於控制與同步 BGM 和 SFX 靜音/音量滑桿 (Slider) 的 UI 輔助元件。
/// 將此腳本掛載在設定面板 (Settings Panel) 上，並在 Inspector 中指派對應的 Slider 即可。
/// </summary>
public class AudioSettingsUI : MonoBehaviour
{
    [Header("音量滑桿設定 (Sliders)")]
    [Tooltip("背景音樂 (BGM) 的音量滑桿")]
    public Slider bgmSlider;
    
    [Tooltip("音效 (SFX) 的音量滑桿")]
    public Slider sfxSlider;

    private void Start()
    {
        // 確保 AudioManager 實例存在，並初始化 Slider 的數值
        if (AudioManager.Instance != null)
        {
            if (bgmSlider != null)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.value = AudioManager.Instance.BGMVolume;
                bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
                Debug.Log($"[AudioSettingsUI] 初始化 BGM 音量滑桿數值為: {bgmSlider.value}");
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.value = AudioManager.Instance.SFXVolume;
                sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
                Debug.Log($"[AudioSettingsUI] 初始化 SFX 音量滑桿數值為: {sfxSlider.value}");
            }
        }
        else
        {
            Debug.LogWarning("[AudioSettingsUI] 找不到 AudioManager 實例，無法自動初始化 Slider！");
        }
    }

    /// <summary>
    /// 當 BGM 滑桿數值變更時呼叫
    /// </summary>
    private void OnBGMVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(value);
        }
    }

    /// <summary>
    /// 當 SFX 滑桿數值變更時呼叫
    /// </summary>
    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }

    private void OnDestroy()
    {
        // 當此 UI 被銷毀時，移除事件監聽以防止記憶體洩漏
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        }
    }
}
