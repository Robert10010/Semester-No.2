using UnityEngine;
using System.Collections;
using TMPro; // 支援 TextMeshPro
using UnityEngine.SceneManagement; // 支援回到主場景

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

    [Header("結局圖片物件 (Ending_Image 中的三個子圖片物件)")]
    [Tooltip("上司生氣結局圖片物件 (請拖入 Ending_Image 下的 angry)")]
    public GameObject angryImageObject;
    
    [Tooltip("辭退結局圖片物件 (請拖入 Ending_Image 下的 Fired)")]
    public GameObject firedImageObject;
    
    [Tooltip("優秀升職結局圖片物件 (請拖入 Ending_Image 下的 increacet)")]
    public GameObject increacetImageObject;

    private Coroutine typingCoroutine;       // 打字機協程引用
    private bool isTypingFinished = false;    // 標記打字是否完成
    private int totalVisibleCharacters = 0;   // 結局文本的總字元數
    private bool isReturnRoutineStarted = false; // 標記是否已啟動回到主場景的計時

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

        // 2. 啟動結局文字與圖片本身的漸亮 (Fade-In) 顯現協程
        if (endingContentCanvasGroup != null)
        {
            StartCoroutine(FadeInRoutine());
        }

        // 3. 自動保底搜尋今日績效統計文字組件 statsTextUI
        if (statsTextUI == null)
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
        string line1 = PlayerPrefs.GetString("EndingLine1", "");
        string line2 = PlayerPrefs.GetString("EndingLine2", "");
        string line3 = PlayerPrefs.GetString("EndingLine3", "");

        int completedCount = PlayerPrefs.GetInt("CompletedPeopleCount", 0);
        int violationsCount = PlayerPrefs.GetInt("RuleViolationsCount", 0);

        Debug.Log($"[EndingSceneController] 結局場景載入！類型: {endingType}, 完成人數: {completedCount}, 違反指南: {violationsCount}");

        // 5. 準備結局文本內容與統計內容
        string fullText = line1;
        if (!string.IsNullOrEmpty(line2)) fullText += "\n" + line2;
        if (!string.IsNullOrEmpty(line3)) fullText += "\n" + line3;

        string statsText = $"【今日績效統計】\n完成對話人數: {completedCount} 人\n違反教學指南: {violationsCount} 次";

        // 6. 分流展示文字：
        // A. 績效統計單獨放置於 statsTextUI 展示
        if (statsTextUI != null)
        {
            statsTextUI.text = statsText;
        }
        else
        {
            // B. 退路保底：若玩家未建立單獨 Text，則自動拼裝至 endingTextUI 後方以防數據丟失
            fullText += $"\n\n<size=75%><color=#94a3b8>{statsText}</color></size>";
            Debug.LogWarning("[EndingSceneController] 未綁定 statsTextUI (Stats_text)，已保底拼裝於結局台詞後方。");
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
        if (angryImageObject != null) angryImageObject.SetActive(false);
        if (firedImageObject != null) firedImageObject.SetActive(false);
        if (increacetImageObject != null) increacetImageObject.SetActive(false);

        switch (endingType)
        {
            case "Fired":
                if (firedImageObject != null) firedImageObject.SetActive(true);
                break;
                
            case "AngryBoss":
                if (angryImageObject != null) angryImageObject.SetActive(true);
                break;
                
            case "Increase":
                if (increacetImageObject != null) increacetImageObject.SetActive(true);
                break;

            default:
                if (angryImageObject != null) angryImageObject.SetActive(true);
                break;
        }
    }

    void Update()
    {
        // 8. 貼心操作：打字尚未結束時，若玩家按下「空白鍵」或「滑鼠左鍵」，可瞬間顯示全部文字 (Skip 功能)
        if (!isTypingFinished && typingCoroutine != null)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
                endingTextUI.maxVisibleCharacters = totalVisibleCharacters;
                isTypingFinished = true;
                Debug.Log("[EndingSceneController] 玩家跳過了打字機效果，瞬間顯示全部結局文字。");
                
                // 瞬間顯示後，立刻啟動 10 秒返回主場景倒數計時
                StartAutoReturnTimer();
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
}
