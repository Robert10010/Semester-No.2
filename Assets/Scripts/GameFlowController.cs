using System.Collections;
using UnityEngine;
using TMPro; // 引用 TextMeshPro
using UnityEngine.Playables;
using InteractiveNovelGames.Typography.TextControl;

/// <summary>
/// 用於定義在遊戲流程中（如轉場、黑屏）顯示的獨立文字片段的設定。
/// 這樣可以將相關設定（文字內容、UI元件、字型）打包在一起，方便管理。
/// </summary>
[System.Serializable]
public class InterludeTextSettings
{
    [Tooltip("要顯示的文字內容")]
    [TextArea(2, 5)]
    public string text = "";
    [Tooltip("用於顯示文字的 TextControl 元件")]
    public TextControl textControl;
    [Tooltip("指定文字的字型，若不指定則使用預設字型")]
    public TMP_FontAsset font;
}

public class GameFlowController : MonoBehaviour
{
    [Header("UI 畫布設定")]
    [Tooltip("遊戲一開始顯示的主視覺畫面")]
    public GameObject startCanvas;
    
    [Tooltip("正式遊玩的劇情畫面")]
    public GameObject playCanvas;

    [Header("轉場效果")]
    [Tooltip("用於淡入淡出的 Image 或其父物件")]
    public GameObject fadeImageObject;

    [Header("系統參考")]
    public DialogueManager dialogueManager;

    [Header("淡出轉場文字")]
    public InterludeTextSettings fadeOutTextSettings;

    // 將 "START_GAME" 定義為常數，避免魔法字串
    private const string StartGameSignal = "START_GAME";

    /// <summary>
    /// [供 Timeline Signal 呼叫] 觸發淡出轉場文字的打字機效果。
    /// </summary>
    public void TriggerFadeOutText()
    {
        ShowInterludeText(fadeOutTextSettings);
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 清除並隱藏淡出轉場文字。
    /// </summary>
    public void ClearFadeOutText()
    {
        HideInterludeText(fadeOutTextSettings);
    }
    private bool isTransitioning = false;

    void OnEnable()
    {
        // 訂閱手機訊號
        PhoneConnectionManager.OnPhoneNumberReceived += OnPhoneInput;
    }

    void OnDisable()
    {
        // 取消訂閱
        PhoneConnectionManager.OnPhoneNumberReceived -= OnPhoneInput;
    }

    void Start()
    {
        // 遊戲一開始，確保只顯示 StartCanvas
        if (startCanvas != null) startCanvas.SetActive(true);
        if (playCanvas != null) playCanvas.SetActive(false);
        if (fadeImageObject != null) fadeImageObject.SetActive(false); // 確保轉場圖片預設是關閉的
    }

    private void OnPhoneInput(string receivedNumber)
    {
        // 如果收到的是網頁載入時送出的 START_GAME 訊號
        if (receivedNumber == StartGameSignal)
        {
            // 防呆機制：只有在 StartCanvas 開啟著的時候（代表還沒開始遊戲），且不是正在轉場中，才允許開始
            if (startCanvas != null && startCanvas.activeSelf && !isTransitioning)
            {
                Debug.Log("[GameFlowController] 收到 QR 掃描啟動訊號，開始轉場！");
                StartCoroutine(FadeTransition());
            }
            else
            {
                // 如果 startCanvas 已經關閉（代表遊戲正在進行中）
                // 收到 START_GAME 就直接忽略，防止別人重新整理網頁干擾現有玩家
                Debug.Log("[GameFlowController] 忽略 START_GAME 訊號，因為遊戲已經在進行或正在轉場中。");
            }
        }
    }

    private IEnumerator FadeTransition()
    {
        isTransitioning = true;

        // 手動啟用轉場圖片，準備播放動畫
        if (fadeImageObject != null) fadeImageObject.SetActive(true);

        // ====== 1. 播放漸暗動畫 ======
        // 文字的顯示與清除，現在已交由 FadeOut Timeline 內部的 Signal 觸發
        yield return StartCoroutine(TimelineManager.Instance.PlayAndWait("FadeOut"));

        // ====== 2. 在全黑的狀態下，切換背後的畫布 ======
        if (startCanvas != null) startCanvas.SetActive(false);
        if (playCanvas != null)
        {
            playCanvas.SetActive(true);
            // 如果 PlayCanvas 上面有 CanvasGroup，確保它是完全不透明的 (避免之前的設定殘留)
            CanvasGroup pg = playCanvas.GetComponent<CanvasGroup>();
            if (pg != null) pg.alpha = 1f;
        }

        // ====== 4. 播放漸亮動畫 ======
        yield return StartCoroutine(TimelineManager.Instance.PlayAndWait("FadeIn"));

        // ====== 5. 轉場結束，正式開始遊戲對話 ======
        if (dialogueManager != null)
        {
            dialogueManager.StartGame();
        }

        // 轉場完全結束，關閉轉場圖片
        if (fadeImageObject != null) fadeImageObject.SetActive(false);

        isTransitioning = false;
    }

    /// <summary>
    /// 根據提供的設定來顯示一段插曲文字。
    /// </summary>
    /// <param name="settings">包含文字內容、UI 元件和字型的設定。</param>
    private void ShowInterludeText(InterludeTextSettings settings)
    {
        if (settings == null || settings.textControl == null || string.IsNullOrEmpty(settings.text)) return;

        // 套用自訂字型 (如果有的話)
        if (settings.font != null)
        {
            TMP_Text tmp = settings.textControl.GetComponent<TMP_Text>();
            if (tmp != null) tmp.font = settings.font;
        }

        settings.textControl.gameObject.SetActive(true);
        settings.textControl.SetText(settings.text);
    }

    /// <summary>
    /// 根據提供的設定來清除並隱藏一段插曲文字。
    /// </summary>
    private void HideInterludeText(InterludeTextSettings settings)
    {
        if (settings == null || settings.textControl == null) return;

        settings.textControl.ClearText();
        settings.textControl.gameObject.SetActive(false);
    }
}