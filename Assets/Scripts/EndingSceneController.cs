using UnityEngine;
using System.Collections;
using TMPro; // 支援 TextMeshPro
using UnityEngine.SceneManagement; // 支援回到主場景
using UnityEngine.InputSystem; // 支援新的 Input System
using UnityEngine.Playables; // 支援 Timeline 控制

/// <summary>
/// 控制結局場景 (EndingScenes) 的 UI 顯示。
/// 自動讀取 PlayerPrefs 中的結局判定，動態啟用對應圖片，並提供文字與圖片本身漸亮 (Fade-In) 顯示、富文本打字機效果與自動重置返回主場景功能。
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

    [Header("自動重置/返回主場景設定")]
    [Tooltip("結局文字完全顯示後，停留多少秒自動回到主場景 (秒)")]
    public float autoReturnDelay = 10f;
    
    [Tooltip("主線遊戲場景的名稱")]
    public string mainSceneName = "MainScene";

    [Header("結局 Timeline 播放器 (可選)")]
    [Tooltip("用於控制結局畫面切換的 Timeline 播放器 (endingTimeline)，若放入則會切換為 Timeline 訊號控制模式")]
    public PlayableDirector endingTimeline;

    [Header("結局圖片物件 (Ending_Image 中的子圖片物件)")]
    [Tooltip("成功結局 - 圖片 1")]
    public GameObject successImage1;

    [Tooltip("成功結局 - 圖片 2")]
    public GameObject successImage2;

    [Tooltip("失敗結局 - 圖片 1")]
    public GameObject failureImage1;

    [Tooltip("失敗結局 - 圖片 2")]
    public GameObject failureImage2;

    [Tooltip("有趣結局 - 圖片")]
    public GameObject interestingImage;

    [Tooltip("烙跑結局 - 圖片")]
    public GameObject escapeImage;

    // 向下相容隱藏舊變數，確保已在舊 Inspector 綁定的物件不會失效
    [HideInInspector] public GameObject angryImageObject;
    [HideInInspector] public GameObject firedImageObject;
    [HideInInspector] public GameObject firedLowEfficiencyImageObject;
    [HideInInspector] public GameObject firedViolationsImageObject;
    [HideInInspector] public GameObject increacetImageObject;

    private Coroutine typingCoroutine;       // 打字機協程引用
    private bool isTypingFinished = false;    // 標記打字是否完成
    private int totalVisibleCharacters = 0;   // 結局文本的總字元數
    private bool isReturnRoutineStarted = false; // 標記是否已啟動回到主場景的計時

    private bool isWaitingForEndingInput = false; // 是否正在等待玩家點擊或空白鍵繼續 Timeline
    private int currentEndingTextIndex = 0;       // 當前播放的結局文字行索引
    private int currentEndingImageIndex = 0;      // 當前播放的結局圖片索引 (用於雙圖片結局)

    void Start()
    {
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
        string endingType = PlayerPrefs.GetString("EndingType", "AngryBoss"); // 預設為 AngryBoss
        string line1 = PlayerPrefs.GetString("EndingLine1", "這是測試結局文字的第一行 (請從主場景執行以套用正式文本)");
        string line2 = PlayerPrefs.GetString("EndingLine2", "這是測試結局文字的第二行");
        string line3 = PlayerPrefs.GetString("EndingLine3", "這是測試結局文字的第三行");

        int completedCount = PlayerPrefs.GetInt("CompletedPeopleCount", 0);
        int violationsCount = PlayerPrefs.GetInt("RuleViolationsCount", 0);

        Debug.Log($"[EndingSceneController] 結局場景載入！類型: {endingType}, 完成人數: {completedCount}, 違反指南: {violationsCount}");

        // 保底相容舊的 Inspector 欄位 Drag-and-Drop
        if (interestingImage == null) interestingImage = angryImageObject;
        if (failureImage1 == null) failureImage1 = firedImageObject;
        if (successImage1 == null) successImage1 = increacetImageObject;

        if (endingTimeline != null)
        {
            // Timeline 模式：重置所有文字與圖片，交由 Timeline 訊號控制
            if (endingTextUI != null) endingTextUI.text = "";
            if (statsTextUI != null) statsTextUI.gameObject.SetActive(false);
            if (successImage1 != null) successImage1.SetActive(false);
            if (successImage2 != null) successImage2.SetActive(false);
            if (failureImage1 != null) failureImage1.SetActive(false);
            if (failureImage2 != null) failureImage2.SetActive(false);
            if (interestingImage != null) interestingImage.SetActive(false);
            if (escapeImage != null) escapeImage.SetActive(false);

            endingTimeline.stopped += OnEndingTimelineStopped;
            endingTimeline.time = 0;
            endingTimeline.Evaluate();
            endingTimeline.Play();
            Debug.Log("[EndingSceneController] 檢測到 endingTimeline，切換為 Timeline 訊號控制結局播放模式。");
        }
        else
        {
            // 舊的自動播放邏輯 (向下相容)
            // 5. 準備結局文本內容與統計內容
            string fullText = line1;
            if (!string.IsNullOrEmpty(line2)) fullText += "\n" + line2;
            if (!string.IsNullOrEmpty(line3)) fullText += "\n" + line3;

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
                isReturnRoutineStarted = false;
                typingCoroutine = StartCoroutine(TypeTextPlay(fullText));
            }
            else
            {
                Debug.LogWarning("[EndingSceneController] 未綁定 endingTextUI (Ending_text)！");
            }

            // 7. 根據結局類型，動態啟用對應的結局圖片，並隱藏其餘的圖片
            if (successImage1 != null) successImage1.SetActive(false);
            if (successImage2 != null) successImage2.SetActive(false);
            if (failureImage1 != null) failureImage1.SetActive(false);
            if (failureImage2 != null) failureImage2.SetActive(false);
            if (interestingImage != null) interestingImage.SetActive(false);
            if (escapeImage != null) escapeImage.SetActive(false);

            switch (endingType)
            {
                case "Success":
                    if (successImage1 != null) successImage1.SetActive(true);
                    break;
                    
                case "Failure":
                    if (failureImage1 != null) failureImage1.SetActive(true);
                    break;
                    
                case "Interesting":
                    if (interestingImage != null) interestingImage.SetActive(true);
                    break;

                case "Escape":
                    if (escapeImage != null) escapeImage.SetActive(true);
                    break;

                default:
                    if (interestingImage != null) interestingImage.SetActive(true);
                    break;
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
                Debug.Log("[EndingSceneController] 玩家跳過了打字機效果，瞬間顯示當前結局文字。");
                
                // 非 Timeline 模式下，瞬間顯示後立刻啟動返回主場景計時
                if (endingTimeline == null)
                {
                    StartAutoReturnTimer();
                }
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

        // 正常打完字後，啟動 10 秒返回主場景倒數計時
        StartAutoReturnTimer();
    }

    /// <summary>
    /// 啟動自動返回主場景倒數計時器
    /// </summary>
    private void StartAutoReturnTimer()
    {
        if (isReturnRoutineStarted) return;
        isReturnRoutineStarted = true;
        StartCoroutine(AutoReturnRoutine());
    }

    private IEnumerator AutoReturnRoutine()
    {
        Debug.Log($"[EndingSceneController] 結局已完全顯示，將在 {autoReturnDelay} 秒後自動返回主場景 {mainSceneName} 重新開始...");
        yield return new WaitForSeconds(autoReturnDelay);
        
        Debug.Log($"[EndingSceneController] {autoReturnDelay} 秒時間已到！正在載入主場景 {mainSceneName} 重新開始遊戲...");
        SceneManager.LoadScene(mainSceneName);
    }

    // ================================================================
    // Timeline Signal 呼叫與控制公開方法
    // ================================================================

    /// <summary>
    /// [供 Timeline Signal 呼叫] 顯示下一句結局對話台詞。
    /// 每次觸發時，依序顯示 Line1, Line2, Line3，並啟動打字機效果。
    /// </summary>
    public void TriggerNextEndingText()
    {
        string nextLine = "";
        if (currentEndingTextIndex == 0) nextLine = PlayerPrefs.GetString("EndingLine1", "");
        else if (currentEndingTextIndex == 1) nextLine = PlayerPrefs.GetString("EndingLine2", "");
        else if (currentEndingTextIndex == 2) nextLine = PlayerPrefs.GetString("EndingLine3", "");

        if (!string.IsNullOrEmpty(nextLine))
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            isTypingFinished = false;
            typingCoroutine = StartCoroutine(TypeTextPlay(nextLine));
            Debug.Log($"[EndingSceneController] 透過 Signal 顯示結局文字 [{currentEndingTextIndex + 1}/3]: {nextLine}");
        }
        else
        {
            Debug.LogWarning($"[EndingSceneController] 結局文字第 {currentEndingTextIndex + 1} 行為空。");
            if (endingTextUI != null) endingTextUI.text = "";
        }
        currentEndingTextIndex++;
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 顯示對應當前結局的圖片。
    /// 支援雙圖片結局（成功/失敗）隨著呼叫次數推進更換圖片。
    /// </summary>
    public void TriggerEndingImage()
    {
        string endingType = PlayerPrefs.GetString("EndingType", "Interesting");
        
        // 先隱藏所有結局圖片，準備顯示當前對應的圖片
        if (successImage1 != null) successImage1.SetActive(false);
        if (successImage2 != null) successImage2.SetActive(false);
        if (failureImage1 != null) failureImage1.SetActive(false);
        if (failureImage2 != null) failureImage2.SetActive(false);
        if (interestingImage != null) interestingImage.SetActive(false);
        if (escapeImage != null) escapeImage.SetActive(false);

        switch (endingType)
        {
            case "Success":
                if (currentEndingImageIndex == 0)
                {
                    if (successImage1 != null) successImage1.SetActive(true);
                }
                else
                {
                    if (successImage2 != null) successImage2.SetActive(true);
                    else if (successImage1 != null) successImage1.SetActive(true); // 保底
                }
                break;
                
            case "Failure":
                if (currentEndingImageIndex == 0)
                {
                    if (failureImage1 != null) failureImage1.SetActive(true);
                }
                else
                {
                    if (failureImage2 != null) failureImage2.SetActive(true);
                    else if (failureImage1 != null) failureImage1.SetActive(true); // 保底
                }
                break;
                
            case "Interesting":
                if (interestingImage != null) interestingImage.SetActive(true);
                break;
                
            case "Escape":
                if (escapeImage != null) escapeImage.SetActive(true);
                break;

            default:
                if (interestingImage != null) interestingImage.SetActive(true);
                break;
        }

        Debug.Log($"[EndingSceneController] 透過 Signal 顯示結局圖片: {endingType} (圖片索引: {currentEndingImageIndex})");
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
        if (successImage1 != null) successImage1.SetActive(false);
        if (successImage2 != null) successImage2.SetActive(false);
        if (failureImage1 != null) failureImage1.SetActive(false);
        if (failureImage2 != null) failureImage2.SetActive(false);
        if (interestingImage != null) interestingImage.SetActive(false);
        if (escapeImage != null) escapeImage.SetActive(false);
        Debug.Log("[EndingSceneController] 透過 Signal 隱藏結局圖片。");
    }

    /// <summary>
    /// [供 Timeline Signal 呼叫] 立即載入主場景重新開始遊戲。
    /// </summary>
    public void ReturnToMainScene()
    {
        if (endingTimeline != null)
        {
            endingTimeline.stopped -= OnEndingTimelineStopped; // 取消訂閱
        }
        Debug.Log($"[EndingSceneController] 正在載入主場景 {mainSceneName} 重新開始遊戲...");
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnEndingTimelineStopped(PlayableDirector director)
    {
        if (director == endingTimeline)
        {
            ReturnToMainScene();
        }
    }
}
