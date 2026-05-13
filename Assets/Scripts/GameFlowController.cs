using System.Collections;
using UnityEngine;
using InteractiveNovelGames.Typography.TextControl;

public class GameFlowController : MonoBehaviour
{
    [Header("UI 畫布設定")]
    [Tooltip("遊戲一開始顯示的主視覺畫面")]
    public GameObject startCanvas;
    
    [Tooltip("正式遊玩的劇情畫面")]
    public GameObject playCanvas;

    [Header("系統參考")]
    public DialogueManager dialogueManager;

    [Header("轉場設定")]
    [Tooltip("漸變動畫的時間(秒)")]
    public float fadeDuration = 1.0f;

    [Tooltip("全黑畫面停留的時間(秒)")]
    public float blackScreenHoldDuration = 3.0f;

    [Header("黑屏文字設定")]
    [Tooltip("全黑畫面上顯示的文字內容")]
    [TextArea(2, 5)]
    public string blackScreenText = "故事即將開始...";

    [Tooltip("黑屏上的打字機文字元件 (場景中預先建好的 TextControl)")]
    public TextControl blackScreenTextControl;

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
    }

    private void OnPhoneInput(string receivedNumber)
    {
        // 如果收到的是網頁載入時送出的 START_GAME 訊號
        if (receivedNumber == "START_GAME")
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

        // 1. 動態建立一個全螢幕的黑色遮罩畫布
        GameObject fadeObj = new GameObject("BlackFadeScreen");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 999; // 確保蓋在所有 UI 的最上層

        UnityEngine.UI.Image fadeImage = fadeObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = Color.black;

        CanvasGroup fadeGroup = fadeObj.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = true; // 轉場期間阻擋玩家點擊

        float halfDuration = fadeDuration / 2f;
        float elapsedTime = 0f;

        // 2. 漸暗至全黑 (Fade to Black)
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / halfDuration);
            yield return null;
        }
        fadeGroup.alpha = 1f;

        // 3. 在全黑的狀態下，切換背後的畫布
        if (startCanvas != null) startCanvas.SetActive(false);
        if (playCanvas != null) playCanvas.SetActive(true);

        // 如果 PlayCanvas 上面有 CanvasGroup，確保它是完全不透明的 (避免之前的設定殘留)
        if (playCanvas != null)
        {
            CanvasGroup pg = playCanvas.GetComponent<CanvasGroup>();
            if (pg != null) pg.alpha = 1f;
        }

        // ---- 黑屏期間：用 TextControl 的打字機效果顯示文字 ----
        if (blackScreenTextControl != null && !string.IsNullOrEmpty(blackScreenText))
        {
            // 記住文字物件原本的父物件，等等要搬回去
            Transform originalParent = blackScreenTextControl.transform.parent;

            // 把文字物件暫時搬到黑色遮罩畫布底下，這樣只有文字會顯示在黑幕上方
            blackScreenTextControl.transform.SetParent(fadeObj.transform, false);
            blackScreenTextControl.gameObject.SetActive(true);

            // 設定 RectTransform 讓文字在黑幕上置中顯示
            RectTransform rt = blackScreenTextControl.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 觸發打字機效果
            blackScreenTextControl.SetText(blackScreenText);

            // 等待打字機跑完
            while (blackScreenTextControl.IsTyping)
            {
                yield return null;
            }

            // 打字完畢後，再額外停留一段時間讓玩家閱讀
            if (blackScreenHoldDuration > 0f)
            {
                yield return new WaitForSeconds(blackScreenHoldDuration);
            }

            // 搬回原本的父物件、清空文字、隱藏
            blackScreenTextControl.SetText("");
            blackScreenTextControl.transform.SetParent(originalParent, false);
            blackScreenTextControl.gameObject.SetActive(false);
        }
        else
        {
            // 沒有設定文字的話，就單純停留全黑畫面
            if (blackScreenHoldDuration > 0f)
            {
                yield return new WaitForSeconds(blackScreenHoldDuration);
            }
        }

        // 4. 從全黑漸亮 (Fade from Black)
        elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / halfDuration);
            yield return null;
        }

        // 5. 轉場結束，銷毀黑色遮罩並開始遊戲
        Destroy(fadeObj);

        if (dialogueManager != null)
        {
            dialogueManager.StartGame();
        }

        isTransitioning = false;
    }
}