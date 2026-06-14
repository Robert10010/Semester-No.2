using System.Collections;
using UnityEngine;
using TMPro; // 引用 TextMeshPro
using UnityEngine.Playables;
using UnityEngine.InputSystem;
using UnityEngine.UI; // 引用 UnityEngine.UI 支援 Image 元件
using InteractiveNovelGames.Typography.TextControl;

/// <summary>
/// 用於定義在遊戲流程中（如轉場、黑屏）顯示的獨立文字與圖片片段的設定。
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

    [Header("圖片設定 (選填)")]
    [Tooltip("此張幻燈片要更換的圖片 Sprite (需搭配 GameFlowController 的 Slide Image Display)")]
    public Sprite slideSprite;
    [Tooltip("此張幻燈片專屬的圖片 GameObject (若使用此欄位，會在此張投影片播放時自動 Active)")]
    public GameObject slideImageObject;
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

    [Header("淡出轉場文字 (舊版單一文字，向下相容)")]
    public InterludeTextSettings fadeOutTextSettings;

    [Header("開場介紹幻燈片設定")]
    [Tooltip("每張幻燈片的文字設定 (按順序播放)，在 Inspector 中設定每張幻燈片的文字內容和對應的 TextControl")]
    public InterludeTextSettings[] introSlides;

    [Header("開場介紹圖片總顯示器 (Sprite 更換模式)")]
    [Tooltip("用於在轉場時顯示幻燈片圖片的單一 Image 元件，會自動根據每張投影片的 slideSprite 更換圖片")]
    public Image slideImageDisplay;

    [Tooltip("FadeOut Timeline 的 PlayableDirector (用於暫停/恢復播放)。若未指定，會自動從 TimelineManager 中尋找 'FadeOut'")]
    public PlayableDirector fadeOutDirector;

    [Header("教學手冊 (Tutorial UI)")]
    [Tooltip("教學手冊的 GameObject (若在 Timeline 播放中啟用/關閉)")]
    public GameObject tutorialUIObject;

    // 幻燈片與教學狀態追蹤
    private int _currentTextIndex = 0;             // 目前文字播到第幾張
    private int _currentImageIndex = 0;            // 目前圖片更換到第幾張
    private bool _isWaitingForInput = false;       // 是否正在等待玩家按鍵
    private bool _isWaitingForTutorialInput = false; // 是否正在等待教學手冊輸入
    private InterludeTextSettings _activeTextSlide;  // 目前正在顯示文字的投影片
    private InterludeTextSettings _activeImageSlide; // 目前正在顯示圖片的投影片

    // 將 "START_GAME" 定義為常數，避免魔法字串
    private const string StartGameSignal = "START_GAME";

    // ================================================================
    // Timeline Signal 可呼叫的公開方法
    // ================================================================

    /// <summary>
    /// [供 Timeline Signal 呼叫] 顯示下一張幻燈片的打字機文字。
    /// 每次被 Signal 呼叫時，自動推進到下一張。
    /// </summary>
    public void TriggerNextSlideText()
    {
        if (introSlides == null || introSlides.Length == 0)
        {
            Debug.LogWarning("[GameFlowController] 沒有設定任何幻燈片文字！");
            return;
        }

        if (_currentTextIndex >= introSlides.Length)
        {
            Debug.LogWarning($"[GameFlowController] 幻燈片文字已全部播完 (共 {introSlides.Length} 張)，沒有更多文字了。");
            return;
        }

        // 清除上一張投影片的文字 (如果有的話)
        if (_activeTextSlide != null)
        {
            HideSlideTextOnly(_activeTextSlide);
        }

        // 顯示當前投影片文字 (打字機效果)
        _activeTextSlide = introSlides[_currentTextIndex];
        ShowSlideTextOnly(_activeTextSlide);
        Debug.Log($"[GameFlowController] 顯示幻燈片文字 [{_currentTextIndex + 1}/{introSlides.Length}]: {_activeTextSlide.text}");

        _currentTextIndex++;
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 更換為下一張投影片的圖片。
    /// 配合 Timeline 動畫時間拉動，達到精確換圖點的目的。
    /// </summary>
    public void TriggerNextSlideImage()
    {
        if (introSlides == null || introSlides.Length == 0)
        {
            Debug.LogWarning("[GameFlowController] 沒有設定任何幻燈片圖片！");
            return;
        }

        if (_currentImageIndex >= introSlides.Length)
        {
            Debug.LogWarning($"[GameFlowController] 幻燈片圖片已全部播完 (共 {introSlides.Length} 張)，沒有更多圖片了。");
            return;
        }

        // 隱藏上一張的圖片
        if (_activeImageSlide != null)
        {
            HideSlideImageOnly(_activeImageSlide);
        }

        // 顯示當前投影片圖片
        _activeImageSlide = introSlides[_currentImageIndex];
        ShowSlideImageOnly(_activeImageSlide);
        Debug.Log($"[GameFlowController] 更換幻燈片圖片 [{_currentImageIndex + 1}/{introSlides.Length}]");

        _currentImageIndex++;
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 清除並隱藏當前投影片的文字。
    /// 可以放在 Timeline 當畫面完全變黑或遮罩擋住時，精確清除文字。
    /// </summary>
    public void ClearActiveSlideText()
    {
        if (_activeTextSlide != null)
        {
            HideSlideTextOnly(_activeTextSlide);
            _activeTextSlide = null;
            Debug.Log("[GameFlowController] 已透過 Signal 清除當前幻燈片文字。");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 清除並隱藏當前投影片的圖片。
    /// 可以放在 Timeline 當畫面完全變黑或需要隱藏圖片時，精確清除圖片。
    /// </summary>
    public void ClearActiveSlideImage()
    {
        if (_activeImageSlide != null)
        {
            HideSlideImageOnly(_activeImageSlide);
            _activeImageSlide = null;
            Debug.Log("[GameFlowController] 已透過 Signal 清除當前幻燈片圖片。");
        }
    }


    /// <summary>
    /// [供 Timeline Signal 呼叫] 暫停 Timeline 並開啟教學手冊 UI，等待玩家按下手機 CALL 鍵或電腦空白鍵繼續。
    /// (UI 會自動開啟 [透過 ShowTutorialUI]，但翻書音效需在 Timeline 中由開發者放置 Signal 獨立觸發以精確對齊動畫)
    /// </summary>
    public void PauseForTutorial()
    {
        PlayableDirector director = GetFadeOutDirector();
        if (director != null)
        {
            director.Pause();
            _isWaitingForTutorialInput = true;

            // 自動顯示教學手冊 UI
            ShowTutorialUI();

            Debug.Log("[GameFlowController] Timeline 已暫停，已自動顯示手冊 UI，等待玩家按下手機 CALL 或電腦空白鍵解鎖...");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫 / 內部呼叫] 顯示教學手冊 UI。
    /// 可在 Timeline 中書本剛開始升起或打開的影格上放置此訊號。
    /// </summary>
    public void ShowTutorialUI()
    {
        if (tutorialUIObject != null)
        {
            tutorialUIObject.SetActive(true);
            Debug.Log("[GameFlowController] 顯示教學手冊 UI。");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫 / 內部呼叫] 隱藏教學手冊 UI。
    /// 可在 Timeline 中書本關閉完畢或完全收回的影格上放置此訊號。
    /// </summary>
    public void HideTutorialUI()
    {
        if (tutorialUIObject != null)
        {
            tutorialUIObject.SetActive(false);
            Debug.Log("[GameFlowController] 隱藏教學手冊 UI。");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 播放教學手冊打開/拿起音效。
    /// 可在 Timeline 中書本剛開始升起或打開的影格上放置此訊號。
    /// </summary>
    public void PlayTutorialOpenSFX()
    {
        if (AudioManager.Instance != null && dialogueManager != null)
        {
            string sfxName = dialogueManager.techBookPickUpSFXName;
            if (!string.IsNullOrEmpty(sfxName))
            {
                AudioManager.PlaySound(sfxName);
                Debug.Log($"[GameFlowController] 播放開書音效: {sfxName}");
            }
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 播放教學手冊關閉/放下音效。
    /// 可在 Timeline 中書本剛開始收合或降下的影格上放置此訊號。
    /// </summary>
    public void PlayTutorialCloseSFX()
    {
        if (AudioManager.Instance != null && dialogueManager != null)
        {
            string sfxName = dialogueManager.techBookPutDownSFXName;
            if (!string.IsNullOrEmpty(sfxName))
            {
                AudioManager.PlaySound(sfxName);
                Debug.Log($"[GameFlowController] 播放關書音效: {sfxName}");
            }
        }
    }

    /// <summary>
    /// [可供 Timeline Signal 呼叫 / 內部呼叫] 恢復 Timeline 播放以繼續教學手冊的後續動畫。
    /// (此處不再自動隱藏 UI，開發者需在 Timeline 的關閉/退場動畫結束處手動放置 HideTutorialUI 的 Signal 訊號)
    /// </summary>
    public void ResumeFromTutorial()
    {
        _isWaitingForTutorialInput = false;

        // 恢復 Timeline 播放
        PlayableDirector director = GetFadeOutDirector();
        if (director != null)
        {
            director.Resume();
            Debug.Log("[GameFlowController] 玩家解鎖成功，Timeline 繼續播放後續動畫...");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 暫停 Timeline 並等待玩家按下任意鍵繼續。
    /// </summary>
    public void PauseForPlayerInput()
    {
        // 取得當前正在播放的 FadeOut Director
        PlayableDirector director = GetFadeOutDirector();
        if (director != null)
        {
            director.Pause();
            _isWaitingForInput = true;
            Debug.Log("[GameFlowController] Timeline 已暫停，等待玩家按鍵繼續...");
        }
        else
        {
            Debug.LogWarning("[GameFlowController] 找不到 FadeOut Director，無法暫停！");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 觸發淡出轉場文字的打字機效果 (舊版，向下相容)。
    /// </summary>
    public void TriggerFadeOutText()
    {
        ShowSlideTextOnly(fadeOutTextSettings);
        _activeTextSlide = fadeOutTextSettings;
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 清除並隱藏淡出轉場文字 (舊版，向下相容)。
    /// </summary>
    public void ClearFadeOutText()
    {
        HideSlideTextOnly(fadeOutTextSettings);
        if (_activeTextSlide == fadeOutTextSettings) _activeTextSlide = null;
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 立即關閉開始畫布，避免與 Timeline 狀態衝突。
    /// </summary>
    public void CloseStartCanvas()
    {
        if (startCanvas != null) 
        {
            startCanvas.SetActive(false);
            Debug.Log("[GameFlowController] 已透過 Signal 關閉 StartCanvas。");
        }
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 立即開啟正式遊玩畫布，並確保其 CanvasGroup 透明度正常。
    /// </summary>
    public void OpenPlayCanvas()
    {
        if (playCanvas != null)
        {
            playCanvas.SetActive(true);
            
            // 如果 PlayCanvas 上面有 CanvasGroup，確保它是完全不透明的
            CanvasGroup pg = playCanvas.GetComponent<CanvasGroup>();
            if (pg != null) pg.alpha = 1f;
            
            Debug.Log("[GameFlowController] 已透過 Signal 開啟 PlayCanvas。");
        }
    }

    // ================================================================
    // Unity 生命週期
    // ================================================================

    private bool isTransitioning = false;

    void Awake()
    {
        Debug.Log("[GameFlowController] Awake 執行成功！物件已載入。");
    }

    void OnEnable()
    {
        Debug.Log("[GameFlowController] OnEnable 執行成功，開始訂閱 OnPhoneNumberReceived 事件！");
        // 訂閱手機訊號
        PhoneConnectionManager.OnPhoneNumberReceived += OnPhoneInput;
    }

    void OnDisable()
    {
        Debug.Log("[GameFlowController] OnDisable 執行，取消訂閱事件！");
        // 取消訂閱
        PhoneConnectionManager.OnPhoneNumberReceived -= OnPhoneInput;
    }

    void Start()
    {
        // 遊戲一開始，確保只顯示 StartCanvas
        if (startCanvas != null) startCanvas.SetActive(true);
        if (playCanvas != null) playCanvas.SetActive(false);
        if (fadeImageObject != null) fadeImageObject.SetActive(false); // 確保轉場圖片預設是關閉的

        // 播放開場背景音樂 BG_1
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM("BG_1");
        }
    }

    void Update()
    {
        // 1. 處理教學手冊輸入
        if (_isWaitingForTutorialInput)
        {
            bool inputDetected = false;

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                inputDetected = true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                inputDetected = true;
            }

            if (inputDetected)
            {
                ResumeFromTutorial();
                return;
            }
        }

        // 2. 處理一般幻燈片等待輸入
        if (_isWaitingForInput)
        {
            bool inputDetected = false;

            // 滑鼠左鍵
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                inputDetected = true;

            // 鍵盤：空白鍵、Enter、Backspace
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                    Keyboard.current.backspaceKey.wasPressedThisFrame)
                {
                    inputDetected = true;
                }
            }

            if (inputDetected)
            {
                ResumeTimeline();
            }
        }
    }

    // ================================================================
    // 內部邏輯
    // ================================================================

    private void ResumeTimeline()
    {
        _isWaitingForInput = false;

        // 恢復 Timeline 播放
        PlayableDirector director = GetFadeOutDirector();
        if (director != null)
        {
            director.Resume();
            Debug.Log("[GameFlowController] 玩家按鍵，Timeline 繼續播放。");
        }
    }

    /// <summary>
    /// 取得 FadeOut Timeline 的 PlayableDirector。
    /// 優先使用 Inspector 手動指定的，其次從 TimelineManager 自動搜尋。
    /// </summary>
    private PlayableDirector GetFadeOutDirector()
    {
        // 優先使用 TimelineManager 當前正在播放的 active director
        if (TimelineManager.Instance != null && TimelineManager.Instance.ActiveDirector != null)
        {
            return TimelineManager.Instance.ActiveDirector;
        }

        if (fadeOutDirector != null) return fadeOutDirector;

        // 嘗試從 TimelineManager 自動取得
        if (TimelineManager.Instance != null)
        {
            Transform tmTransform = TimelineManager.Instance.transform;
            Transform fadeOutChild = tmTransform.Find("FadeOut");
            if (fadeOutChild != null)
            {
                fadeOutDirector = fadeOutChild.GetComponent<PlayableDirector>();
                return fadeOutDirector;
            }
        }

        return null;
    }

    private void OnPhoneInput(string receivedNumber)
    {
        Debug.Log($"[GameFlowController] OnPhoneInput 收到訊號: '{receivedNumber}'");
        if (string.IsNullOrEmpty(receivedNumber)) return;
        string trimmedNumber = receivedNumber.Trim();

        // 處理帶有時間戳記的信號 (如 NEXT_1716543452)，僅在底線後方為 Unix 時間戳記(純數字且長度>=9)時進行裁切
        int lastUnderscore = trimmedNumber.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore < trimmedNumber.Length - 1)
        {
            string potentialTimestamp = trimmedNumber.Substring(lastUnderscore + 1);
            bool isNumeric = true;
            foreach (char c in potentialTimestamp)
            {
                if (!char.IsDigit(c))
                {
                    isNumeric = false;
                    break;
                }
            }
            if (isNumeric && potentialTimestamp.Length >= 9)
            {
                trimmedNumber = trimmedNumber.Substring(0, lastUnderscore);
            }
        }

        bool isMatch = string.Equals(trimmedNumber, StartGameSignal, System.StringComparison.OrdinalIgnoreCase);
        Debug.Log($"[GameFlowController] 訊號比較: '{trimmedNumber}' (長度={trimmedNumber.Length}) 與 '{StartGameSignal}' (長度={StartGameSignal.Length}), 是否相同: {isMatch}, startCanvas為空: {startCanvas == null}, isTransitioning: {isTransitioning}");

        // 如果收到的是網頁載入時送出的 START_GAME 訊號
        if (isMatch)
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
                Debug.Log($"[GameFlowController] 忽略 START_GAME 訊號，原因: startCanvas是否啟用={((startCanvas != null) ? startCanvas.activeSelf : false)}, isTransitioning={isTransitioning}");
            }
        }

        // 處理手機 CALL 鍵 (NEXT) 訊號
        if (trimmedNumber == "NEXT")
        {
            if (_isWaitingForTutorialInput)
            {
                ResumeFromTutorial();
            }
            else if (_isWaitingForInput)
            {
                ResumeTimeline();
            }
        }
    }

    private IEnumerator FadeTransition()
    {
        isTransitioning = true;

        // 漸變切換為常駐背景音樂 BG_2 (淡出舊音樂淡入新音樂，歷時 2 秒)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.TransitionToBGM("BG_2", 2f);
        }

        // 重置所有幻燈片與教學狀態，確保每次開始遊戲都從第一張幻燈片開始
        _currentTextIndex = 0;
        _currentImageIndex = 0;
        _activeTextSlide = null;
        _activeImageSlide = null;
        _isWaitingForInput = false;
        _isWaitingForTutorialInput = false;

        // 手動啟用轉場圖片，準備播放動畫
        if (fadeImageObject != null) fadeImageObject.SetActive(true);

        // ====== 1. 播放漸暗動畫 (包含幻燈片介紹) ======
        // 文字與圖片的顯示、暫停、恢復，全部由 FadeOut Timeline 內部的 Signal 觸發
        yield return StartCoroutine(TimelineManager.Instance.PlayAndWait("FadeOut"));

        // FadeOut 播放完畢，將背景音樂 BG_2 的音量比例漸變至 0.3 (歷時 2 秒)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeBGMTo(0.3f, 2f);
        }

        // ====== 2. 確保所有幻燈片文字與圖片已清除 ======
        if (_activeTextSlide != null)
        {
            HideSlideTextOnly(_activeTextSlide);
            _activeTextSlide = null;
        }
        if (_activeImageSlide != null)
        {
            HideSlideImageOnly(_activeImageSlide);
            _activeImageSlide = null;
        }

        // ====== 3. 在全黑的狀態下，切換背後的畫布 ======
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

        // ====== 4.5 播放開場書本 Timeline ======
        yield return StartCoroutine(TimelineManager.Instance.PlayAndWait("book_first"));

        // ====== 5. 轉場與書本播放結束，正式開始遊戲對話 ======
        if (dialogueManager != null)
        {
            dialogueManager.StartGame();
        }

        // 轉場完全結束，關閉轉場圖片
        if (fadeImageObject != null) fadeImageObject.SetActive(false);

        isTransitioning = false;
    }

    /// <summary>
    /// 顯示投影片的打字機文字。
    /// </summary>
    private void ShowSlideTextOnly(InterludeTextSettings settings)
    {
        if (settings == null) return;

        if (settings.textControl != null && !string.IsNullOrEmpty(settings.text))
        {
            // 套用自訂字型 (如果有的話)
            if (settings.font != null)
            {
                TMP_Text tmp = settings.textControl.GetComponent<TMP_Text>();
                if (tmp != null) tmp.font = settings.font;
            }

            settings.textControl.gameObject.SetActive(true);
            settings.textControl.SetText(settings.text);
        }
    }

    /// <summary>
    /// 清除並隱藏投影片的文字。
    /// </summary>
    private void HideSlideTextOnly(InterludeTextSettings settings)
    {
        if (settings == null) return;

        if (settings.textControl != null)
        {
            settings.textControl.ClearText();
            settings.textControl.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 顯示投影片對應的圖片。
    /// </summary>
    private void ShowSlideImageOnly(InterludeTextSettings settings)
    {
        if (settings == null) return;

        // 更換圖片 (Sprite 更換模式 - 共用單一 UI Image 顯示器)
        if (settings.slideSprite != null && slideImageDisplay != null)
        {
            slideImageDisplay.gameObject.SetActive(true);
            slideImageDisplay.sprite = settings.slideSprite;
        }

        // 顯示專屬圖片物件 (獨立 GameObject 模式)
        if (settings.slideImageObject != null)
        {
            settings.slideImageObject.SetActive(true);

            // 如果同時有指定 slideSprite，且該專屬物件有 Image 元件，就自動更換其 Sprite
            if (settings.slideSprite != null)
            {
                Image img = settings.slideImageObject.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = settings.slideSprite;
                }
            }
        }
    }

    /// <summary>
    /// 清除並隱藏投影片對應的圖片。
    /// </summary>
    private void HideSlideImageOnly(InterludeTextSettings settings)
    {
        if (settings == null) return;

        // 隱藏共用圖片顯示器
        if (slideImageDisplay != null)
        {
            slideImageDisplay.gameObject.SetActive(false);
            slideImageDisplay.sprite = null;
        }

        // 隱藏專屬圖片物件
        if (settings.slideImageObject != null)
        {
            settings.slideImageObject.SetActive(false);

            // 清除 Sprite 避免殘留
            if (settings.slideSprite != null)
            {
                Image img = settings.slideImageObject.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = null;
                }
            }
        }
    }
}