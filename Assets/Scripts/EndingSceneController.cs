using UnityEngine;
using System;
using System.Collections;
using TMPro; // 支援 TextMeshPro
using UnityEngine.SceneManagement; // 支援回到主場景
using UnityEngine.InputSystem; // 支援新的 Input System
using UnityEngine.Playables; // 支援 Timeline 控制
using UnityEngine.UI; // 支援動態建立遮罩圖片

/// <summary>
/// 定義結局幻燈片單張投影片的文字與圖片內容
/// </summary>
[System.Serializable]
public class EndingSlide
{
    [TextArea(3, 5)]
    [Tooltip("該頁投影片的對話文字內容")]
    public string text;

    [Tooltip("該頁投影片更換的圖片 Sprite (需搭配 slideImageDisplay)")]
    public Sprite slideSprite;

    [Tooltip("該頁投影片專屬的圖片 GameObject (會在此頁播放時自動 Active，其餘自動隱藏)")]
    public GameObject slideImageObject;
}

/// <summary>
/// 控制結局場景 (EndingScenes) 的 UI 顯示。
/// 自動讀取 PlayerPrefs 中的結局判定，動態啟用對應圖片，並提供文字與圖片本身漸亮 (Fade-In) 顯示、富文本打字機效果。
/// </summary>
public class EndingSceneController : MonoBehaviour
{
    [Header("UI 文字組件 (請拖入 Ending_text)")]
    [Tooltip("用於展示結局對話台詞的 TextMeshPro 文字組件")]
    public TMP_Text endingTextUI;

    [Header("UI 績效統計組件 (可選，請拖入 Stats_text)")]
    [Tooltip("單獨用於展示今日績效統計數據的 TextMeshPro 文字組件")]
    public TMP_Text statsTextUI;

    [Tooltip("是否在結局中顯示今日績效統計數據 (預設為 false 關閉)")]
    public bool showStats = false;

    [Header("結局圖文漸亮設定 (Fade-In Effect)")]
    [Tooltip("結局內容的 CanvasGroup (請拖入裝有 Ending_text 與 Ending_Image 的父 Panel，以便控制它們從透明漸漸亮起顯現)")]
    public CanvasGroup endingContentCanvasGroup;

    [Tooltip("漸亮顯現的持續時間 (秒)")]
    public float fadeDuration = 1.5f;

    [Header("打字機效果設定")]
    [Tooltip("打字機每個字浮現的間隔時間 (秒)")]
    public float typingSpeed = 0.05f;

    [Tooltip("打字時播放的音效名稱 (在 AudioManager 中設定的名稱)")]
    public string typingSoundName = "Dialogue_sound_1";

    [Header("場景設定")]
    [Tooltip("主線遊戲場景的名稱")]
    public string mainSceneName = "MainScene";

    public enum EndingTestType { None, Success, Failure, Interesting, Escape }

    [Header("結局測試工具 (直接在結局場景執行時使用)")]
    [Tooltip("選擇要在結局場景直接測試播放的結局類型")]
    public EndingTestType debugEndingType = EndingTestType.None;

    [Header("結局 Timeline 播放器 (可選)")]
    [Tooltip("用於控制結局畫面切換的 Timeline 播放器 (endingTimeline)，若放入則會切換為 Timeline 訊號控制模式")]
    public PlayableDirector endingTimeline;

    [Header("結局投影片設定 (像 Intro Slides 一樣)")]
    [Tooltip("成功結局的投影片列表")]
    public EndingSlide[] successSlides;

    [Tooltip("失敗結局的投影片列表")]
    public EndingSlide[] failureSlides;

    [Tooltip("有趣結局的投影片列表")]
    public EndingSlide[] interestingSlides;

    [Tooltip("烙跑結局的投影片列表")]
    public EndingSlide[] escapeSlides;

    [Tooltip("用於在結局更換圖片的單一 Image 元件 (Sprite 模式)")]
    public UnityEngine.UI.Image slideImageDisplay;

    private Coroutine typingCoroutine;       // 打字機協程引用
    private bool isTypingFinished = false;    // 標記打字是否完成
    private int totalVisibleCharacters = 0;   // 結局文本的總字元數

    private AudioSource _audioSource;
    private Coroutine _soundCoroutine;

    private bool isWaitingForEndingInput = false; // 是否正在等待玩家點擊或空白鍵繼續 Timeline
    private int currentEndingTextIndex = 0;       // 當前播放的結局文字行索引
    private int currentEndingImageIndex = 0;      // 當前播放的結局圖片索引 (用於雙圖片結局)

    void Start()
    {
        // 播放結局背景音樂 BG_3 (從遊玩音樂 BG_2 平滑切換過來，歷時 1.5 秒)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TransitionToBGM("BG_3", 1.5f);
        }

        // 安全保底：如果 Inspector 中沒有設定結局打字音效，自動保底設為 "Dialogue_sound_1"
        if (string.IsNullOrEmpty(typingSoundName))
        {
            typingSoundName = "Dialogue_sound_1";
        }

        // 1. 自動保底搜尋結局圖文的 CanvasGroup 組件
        if (endingContentCanvasGroup == null)
        {
            endingContentCanvasGroup = GetComponent<CanvasGroup>();
            if (endingContentCanvasGroup == null)
            {
                GameObject contentObj = GameObject.Find("EndingContent");
                if (contentObj == null) contentObj = GameObject.Find("Panel");
                if (contentObj != null)
                {
                    endingContentCanvasGroup = contentObj.GetComponent<CanvasGroup>();
                    if (endingContentCanvasGroup == null)
                    {
                        endingContentCanvasGroup = contentObj.AddComponent<CanvasGroup>();
                    }
                }
            }
        }

        // 2. 啟動結局文字與圖片本身的漸亮 (Fade-In) 協程 (若有 Timeline 控制則跳過，由 Timeline 處理)
        if (endingContentCanvasGroup != null && endingTimeline == null)
        {
            StartCoroutine(FadeInRoutine());
        }

        // 3. 自動保底搜尋今日績效統計文字組件 statsTextUI (僅在開啟顯示統計且未綁定時尋找)
        if (showStats && statsTextUI == null)
        {
            GameObject foundStats = GameObject.Find("Stats_text");
            if (foundStats == null) foundStats = GameObject.Find("StatsText");
            if (foundStats != null)
            {
                statsTextUI = foundStats.GetComponent<TMP_Text>();
            }
        }

        // 4. 讀取先前 DialogueManager 存儲的結局對話與今日統計數據
        string endingType = GetEndingType();
        int completedCount = PlayerPrefs.GetInt("CompletedPeopleCount", 0);
        int violationsCount = PlayerPrefs.GetInt("RuleViolationsCount", 0);

        Debug.Log($"[EndingSceneController] 結局場景載入！類型: {endingType}, 完成人數: {completedCount}, 違反指南: {violationsCount}");

        currentEndingTextIndex = 0;
        currentEndingImageIndex = 0;

        if (endingTimeline != null)
        {
            // Timeline 模式：重置所有文字與圖片，交由 Timeline 訊號控制
            if (endingTextUI != null) endingTextUI.text = "";
            if (statsTextUI != null) statsTextUI.gameObject.SetActive(false);
            if (slideImageDisplay != null) slideImageDisplay.gameObject.SetActive(false);
            
            // 隱藏所有投影片的 GameObject
            HideAllEndingSlideObjects();

            endingTimeline.played += OnEndingTimelinePlayed;
            endingTimeline.time = 0;
            endingTimeline.Evaluate();
            endingTimeline.Play();
            Debug.Log("[EndingSceneController] 檢測到 endingTimeline，切換為 Timeline 訊號控制結局播放模式。");
        }
        else
        {
            // 舊的自動播放邏輯 (向下相容)
            // 5. 準備結局文本內容與統計內容
            string fullText = "";
            EndingSlide[] activeSlides = GetActiveSlides(endingType);
            
            if (activeSlides != null && activeSlides.Length > 0)
            {
                for (int i = 0; i < activeSlides.Length; i++)
                {
                    if (activeSlides[i] != null && !string.IsNullOrEmpty(activeSlides[i].text))
                    {
                        if (fullText != "") fullText += "\n";
                        fullText += activeSlides[i].text;
                    }
                }
                
                // 顯示第一張投影片的圖片
                if (activeSlides[0] != null)
                {
                    DisplaySlideImage(activeSlides[0], activeSlides);
                }
            }
            else
            {
                // 保底相容舊格式 (從 PlayerPrefs 讀取)
                int totalLines = PlayerPrefs.GetInt("EndingLineCount", 0);
                if (totalLines > 0)
                {
                    for (int i = 0; i < totalLines; i++)
                    {
                        string line = PlayerPrefs.GetString($"EndingLine_{i}", "");
                        if (!string.IsNullOrEmpty(line))
                        {
                            if (fullText != "") fullText += "\n";
                            fullText += line;
                        }
                    }
                }
                else
                {
                    string legacyLine1 = PlayerPrefs.GetString("EndingLine1", "這是測試結局對話的段落 (請從主場景執行以套用正式文本)");
                    string legacyLine2 = PlayerPrefs.GetString("EndingLine2", "");
                    string legacyLine3 = PlayerPrefs.GetString("EndingLine3", "");
                    
                    fullText = legacyLine1;
                    if (!string.IsNullOrEmpty(legacyLine2)) fullText += "\n" + legacyLine2;
                    if (!string.IsNullOrEmpty(legacyLine3)) fullText += "\n" + legacyLine3;
                }
            }

            if (showStats)
            {
                string statsText = $"【今日績效統計】\n完成對話人數: {completedCount} 人\n違反教學指南: {violationsCount} 次";

                // 6. 分流展示文字：
                // A. 績效統計單獨放置於 statsTextUI 展示
                if (statsTextUI != null)
                {
                    statsTextUI.gameObject.SetActive(true);
                    statsTextUI.text = statsText;
                }
                else
                {
                    // B. 退路保底：若玩家未建立單獨 Text，則自動拼裝至 endingTextUI 後方以防數據丟失
                    fullText += $"\n\n<size=75%><color=#94a3b8>{statsText}</color></size>";
                    Debug.LogWarning("[EndingSceneController] 未綁定 statsTextUI (Stats_text)，已保底拼裝於結局台詞後方。");
                }
            }
            else
            {
                if (statsTextUI != null)
                {
                    statsTextUI.gameObject.SetActive(false);
                }
            }

            // C. 啟動對結局台詞的打字機效果 (TextMeshPro 安全版)
            if (endingTextUI != null)
            {
                isTypingFinished = false;
                typingCoroutine = StartCoroutine(TypeTextPlay(fullText));
            }
            else
            {
                Debug.LogWarning("[EndingSceneController] 未綁定 endingTextUI (Ending_text)！");
            }
        }
    }

    void Update()
    {
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        // A. 處理打字機 Skip 功能 (僅在非 Timeline 模式或 Timeline 暫停等待輸入時)
        if (!isTypingFinished && typingCoroutine != null && (endingTimeline == null || isWaitingForEndingInput))
        {
            if (spacePressed || clicked)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
                if (endingTextUI != null) endingTextUI.maxVisibleCharacters = totalVisibleCharacters;
                isTypingFinished = true;

                if (_soundCoroutine != null)
                {
                    StopCoroutine(_soundCoroutine);
                    _soundCoroutine = null;
                }
                if (_audioSource != null)
                {
                    _audioSource.Stop();
                }

                Debug.Log("[EndingSceneController] 玩家跳過了打字機效果，瞬間顯示當前結局文字。");
                return; // 攔截本次點擊，避免重複推進
            }
        }

        // B. 處理 Timeline 暫停等待玩家按鍵繼續
        if (endingTimeline != null && isWaitingForEndingInput)
        {
            if (spacePressed || clicked)
            {
                ResumeEndingTimeline();
            }
        }
        // C. 非 Timeline 模式下，打字完成後，玩家點擊或按空白鍵直接回到主場景
        else if (endingTimeline == null && isTypingFinished)
        {
            if (spacePressed || clicked)
            {
                ReturnToMainScene();
            }
        }
    }

    /// <summary>
    /// 結局內容本身漸亮 (Fade-In) 協程，控制圖文父 Panel 的 CanvasGroup 透明度從 0 升到 1，將其漸漸顯示出來
    /// </summary>
    private IEnumerator FadeInRoutine()
    {
        endingContentCanvasGroup.alpha = 0f; // 強制起始完全透明 (看不見)

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            endingContentCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        endingContentCanvasGroup.alpha = 1f; // 確保完全顯示
        Debug.Log("[EndingSceneController] 結局文字與圖片漸亮 (Fade-In) 顯現完畢！");
    }

    /// <summary>
    /// 利用 TextMeshPro 的 maxVisibleCharacters 來實現打字機效果。
    /// </summary>
    private IEnumerator TypeTextPlay(string fullText)
    {
        endingTextUI.text = fullText;
        endingTextUI.maxVisibleCharacters = 0;
        
        // 強制 TextMeshPro 立即更新網格，以便精確計算出總字元數
        endingTextUI.ForceMeshUpdate();
        totalVisibleCharacters = endingTextUI.textInfo.characterCount;

        // 啟動音效播放協程
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        if (_soundCoroutine != null) StopCoroutine(_soundCoroutine);
        if (!string.IsNullOrEmpty(typingSoundName) && 
            !typingSoundName.Equals("none", StringComparison.OrdinalIgnoreCase) && 
            AudioManager.Instance != null)
        {
            _soundCoroutine = StartCoroutine(PlayTypingSoundsRoutine());
        }

        int counter = 0;
        while (counter <= totalVisibleCharacters)
        {
            endingTextUI.maxVisibleCharacters = counter;
            counter++;
            yield return new WaitForSeconds(typingSpeed);
        }

        // 打字結束，確保完全顯示
        endingTextUI.maxVisibleCharacters = totalVisibleCharacters;
        isTypingFinished = true;

        if (_soundCoroutine != null)
        {
            StopCoroutine(_soundCoroutine);
            _soundCoroutine = null;
        }
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }
    }

    // ================================================================
    // Timeline Signal 呼叫與控制公開方法
    // ================================================================

    /// <summary>
    /// [供 Timeline Signal 呼叫] 顯示下一句結局對話台詞。
    /// 每次觸發時，依序顯示對話框段落，並啟動打字機效果。
    /// </summary>
    public void TriggerNextEndingText()
    {
        string endingType = GetEndingType();
        EndingSlide[] activeSlides = GetActiveSlides(endingType);

        string nextLine = "";
        int totalLines = activeSlides != null ? activeSlides.Length : 0;

        if (totalLines > 0)
        {
            if (currentEndingTextIndex < totalLines)
            {
                nextLine = activeSlides[currentEndingTextIndex].text;
            }
            else
            {
                Debug.LogWarning($"[EndingSceneController] 結局對話索引 ({currentEndingTextIndex}) 超出總投影片頁數 ({totalLines})。結局類型: {endingType}");
            }
        }
        else
        {
            // 沒讀到 PlayerPrefs，使用預設測試段落
            string[] fallbackLines = new string[]
            {
                "這是測試結局對話的第一個對話框 (請從主場景執行以套用正式文本)",
                "這是測試結局對話的第二個對話框",
                "這是測試結局對話的第三個對話框"
            };
            if (currentEndingTextIndex < fallbackLines.Length)
            {
                nextLine = fallbackLines[currentEndingTextIndex];
            }
        }

        if (!string.IsNullOrEmpty(nextLine))
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            isTypingFinished = false;
            typingCoroutine = StartCoroutine(TypeTextPlay(nextLine));
            int displayCount = totalLines > 0 ? totalLines : 3;
            Debug.Log($"[EndingSceneController] 透過 Signal 顯示結局文字 [{currentEndingTextIndex + 1}/{displayCount}] (結局類型: {endingType}): {nextLine}");
        }
        else
        {
            Debug.LogWarning($"[EndingSceneController] 結局對話段落 [{currentEndingTextIndex + 1}] 為空。結局類型: {endingType}");
            if (endingTextUI != null) endingTextUI.text = "";
        }
        currentEndingTextIndex++;
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 顯示對應當前結局的圖片。
    /// 依據 currentEndingImageIndex 從 Slides 中尋找並顯示圖片。
    /// </summary>
    public void TriggerEndingImage()
    {
        string endingType = GetEndingType();
        EndingSlide[] activeSlides = GetActiveSlides(endingType);

        if (activeSlides != null && activeSlides.Length > 0)
        {
            if (currentEndingImageIndex < activeSlides.Length)
            {
                EndingSlide slide = activeSlides[currentEndingImageIndex];
                DisplaySlideImage(slide, activeSlides);
                Debug.Log($"[EndingSceneController] 透過 Signal 顯示結局圖片 (結局類型: {endingType}, 頁面索引: {currentEndingImageIndex}, Sprite: {(slide.slideSprite != null ? slide.slideSprite.name : "無")}, GameObject: {(slide.slideImageObject != null ? slide.slideImageObject.name : "無")})");
            }
            else
            {
                Debug.LogWarning($"[EndingSceneController] 結局圖片索引 ({currentEndingImageIndex}) 超出總投影片頁數 ({activeSlides.Length})。結局類型: {endingType}");
            }
        }
        else
        {
            Debug.LogWarning($"[EndingSceneController] 未在 EndingSceneController 設定對應的結局投影片 (Slides)！結局類型: {endingType}");
        }
        currentEndingImageIndex++;
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 暫停 Timeline 並等待玩家按下點擊或空白鍵以繼續。
    /// </summary>
    public void PauseForEndingInput()
    {
        if (endingTimeline != null)
        {
            endingTimeline.Pause();
            isWaitingForEndingInput = true;
            Debug.Log("[EndingSceneController] Timeline 已暫停，等待玩家按鍵繼續...");
        }
    }

    /// <summary>
    /// [內部/Signal 呼叫] 恢復 Timeline 播放。
    /// </summary>
    public void ResumeEndingTimeline()
    {
        isWaitingForEndingInput = false;

        string endingType = GetEndingType();
        EndingSlide[] activeSlides = GetActiveSlides(endingType);
        int totalLines = activeSlides != null ? activeSlides.Length : 0;
        int maxLines = totalLines > 0 ? totalLines : 3; // 3 為預設測試行數

        // 當前播放對話的索引如果已經大於等於總對話行數，代表沒有後續文本，按下去直接回到主畫面
        if (currentEndingTextIndex >= maxLines)
        {
            Debug.Log("[EndingSceneController] 已無後續文本，玩家按鍵觸發回到主場景。");
            ReturnToMainScene();
            return;
        }

        if (endingTimeline != null)
        {
            endingTimeline.Play();
            Debug.Log("[EndingSceneController] 玩家按鍵解鎖，Timeline 繼續播放...");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 清除結局文字。
    /// </summary>
    public void HideEndingText()
    {
        if (endingTextUI != null)
        {
            endingTextUI.text = "";
            Debug.Log("[EndingSceneController] 透過 Signal 隱藏結局文字。");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 隱藏結局圖片。
    /// </summary>
    public void HideEndingImage()
    {
        // 隱藏幻燈片 GameObject
        HideAllEndingSlideObjects();

        // 隱藏幻燈片 Image 貼圖
        if (slideImageDisplay != null)
        {
            slideImageDisplay.gameObject.SetActive(false);
        }

        Debug.Log("[EndingSceneController] 透過 Signal 隱藏結局圖片。");
    }

    // ================================================================
    // 內部輔助方法
    // ================================================================

    private string GetEndingType()
    {
        string endingType = PlayerPrefs.GetString("EndingType", "Interesting");
#if UNITY_EDITOR
        if (debugEndingType != EndingTestType.None)
        {
            endingType = debugEndingType.ToString();
        }
#endif
        return endingType;
    }

    private EndingSlide[] GetActiveSlides(string endingType)
    {
        switch (endingType)
        {
            case "Success": return successSlides;
            case "Failure": return failureSlides;
            case "Interesting": return interestingSlides;
            case "Escape": return escapeSlides;
            default: return interestingSlides;
        }
    }

    private void DisplaySlideImage(EndingSlide currentSlide, EndingSlide[] allSlides)
    {
        // 先隱藏本結局所有投影片的 GameObject
        if (allSlides != null)
        {
            foreach (var slide in allSlides)
            {
                if (slide != null && slide.slideImageObject != null)
                {
                    slide.slideImageObject.SetActive(false);
                }
            }
        }

        // 1. 顯示 GameObject 模式的圖片
        if (currentSlide.slideImageObject != null)
        {
            currentSlide.slideImageObject.SetActive(true);

            // 備用機制：如果沒有綁定單一的 slideImageDisplay，但投影片上有設定 Slide Sprite，
            // 則自動尋找該 GameObject 上（或其子物件）的 Image 組件來更換貼圖
            if (currentSlide.slideSprite != null && slideImageDisplay == null)
            {
                var imgComp = currentSlide.slideImageObject.GetComponent<UnityEngine.UI.Image>();
                if (imgComp == null) imgComp = currentSlide.slideImageObject.GetComponentInChildren<UnityEngine.UI.Image>();
                if (imgComp != null)
                {
                    imgComp.sprite = currentSlide.slideSprite;
                    imgComp.gameObject.SetActive(true);
                }
            }
        }

        // 2. 顯示 Sprite 模式的圖片
        if (currentSlide.slideSprite != null && slideImageDisplay != null)
        {
            slideImageDisplay.sprite = currentSlide.slideSprite;
            slideImageDisplay.gameObject.SetActive(true);
        }
    }

    private void HideAllEndingSlideObjects()
    {
        HideSlideObjects(successSlides);
        HideSlideObjects(failureSlides);
        HideSlideObjects(interestingSlides);
        HideSlideObjects(escapeSlides);
    }

    private void HideSlideObjects(EndingSlide[] slides)
    {
        if (slides == null) return;
        foreach (var slide in slides)
        {
            if (slide != null && slide.slideImageObject != null)
            {
                slide.slideImageObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 開始回到主畫面轉場 (畫面漸暗、音樂漸弱)，然後載入主場景。
    /// </summary>
    public void ReturnToMainScene()
    {
        StartCoroutine(ReturnToMainSceneRoutine());
    }

    private IEnumerator ReturnToMainSceneRoutine()
    {
        Debug.Log("[EndingSceneController] 開始回到主選單轉場 (畫面漸暗、音樂漸弱)...");

        // 1. 動態建立滿版黑色遮罩 (Fade Overlay)
        GameObject fadeOverlayObj = new GameObject("FadeToBlackOverlay");
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            fadeOverlayObj.transform.SetParent(canvas.transform, false);
        }
        
        Image fadeImage = fadeOverlayObj.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f); // 初始為完全透明
        
        RectTransform rectTransform = fadeOverlayObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        // 確保黑色遮罩位於 UI 最上層
        fadeOverlayObj.transform.SetAsLastSibling();

        // 2. 啟動背景音樂漸弱 (Fade Out) 到 0
        float duration = 1.5f; // 漸變時間 (秒)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeBGMTo(0f, duration);
        }

        // 3. 畫面漸暗 (Alpha 0 -> 1)
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (fadeImage != null)
            {
                fadeImage.color = new Color(0f, 0f, 0f, progress);
            }
            yield return null;
        }

        if (fadeImage != null)
        {
            fadeImage.color = new Color(0f, 0f, 0f, 1f);
        }

        // 等待一小段時間讓畫面完全穩定在全黑狀態
        yield return new WaitForSeconds(0.1f);

        // 4. 載入主場景
        Debug.Log($"[EndingSceneController] 正在載入主場景 {mainSceneName} 重新開始遊戲...");
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnEndingTimelinePlayed(PlayableDirector director)
    {
        if (director == endingTimeline)
        {
            // 只有當 Timeline 是從頭開始播放（時間接近 0）時，才重置索引
            // 避免在 Pause 之後呼叫 Play (Resume) 時誤觸重置
            if (director.time < 0.1f)
            {
                currentEndingTextIndex = 0;
                currentEndingImageIndex = 0;
                Debug.Log("[EndingSceneController] Timeline 從頭開始播放，重置投影片文字與圖片索引為 0。");
            }
        }
    }

    private IEnumerator PlayTypingSoundsRoutine()
    {
        if (_audioSource == null) yield break;

        AudioClip clip = null;
        float clipVolumeScale = 1f;
        if (AudioManager.Instance != null)
        {
            clip = AudioManager.Instance.GetSFXClip(typingSoundName);
            if (!string.IsNullOrEmpty(typingSoundName))
            {
                clipVolumeScale = AudioManager.Instance.GetSFXVolumeScale(typingSoundName);
            }
        }
        if (clip == null) yield break;

        _audioSource.clip = clip;
        _audioSource.loop = false; // 一個一個播放

        while (!isTypingFinished)
        {
            // 動態同步音效主音量 * 音效專屬音量比例
            if (AudioManager.Instance != null)
            {
                _audioSource.volume = AudioManager.Instance.SFXVolume * clipVolumeScale;
            }

            if (!_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
            yield return null;
        }

        _audioSource.Stop();
    }

    void OnDestroy()
    {
        if (endingTimeline != null)
        {
            endingTimeline.played -= OnEndingTimelinePlayed;
        }
        if (_soundCoroutine != null)
        {
            StopCoroutine(_soundCoroutine);
        }
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }
    }
}
