using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 處理與 PHP 伺服器的通訊 (取得最新撥打號碼、發送清除指令)。
/// 單一職責：不負責遊戲具體控制邏輯，只負責收發與廣播訊號。
/// </summary>
public class PhoneConnectionManager : MonoBehaviour
{
    public static PhoneConnectionManager Instance { get; private set; }

    [Header("伺服器設定")]
    [Tooltip("請輸入你的 PHP 伺服器網址 (例如: http://192.168.1.100/WebProject/api.php)")]
    public string apiEndpoint = "https://visualnovel.gamer.gd/WebProject/api.php";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    
    [Tooltip("多久向 PHP 詢問一次是否有新號碼 (單位: 秒)")]
    public float pollInterval = 1.0f;

    // 建立一個事件，當收到號碼時廣播出去
    public static event Action<string> OnPhoneNumberReceived;

    // 防重複觸發：記錄上一次處理的號碼與時間
    private string  _lastProcessedNumber = "";
    private float   _lastProcessedTime   = -999f;

    [Tooltip("同一個號碼在幾秒內不會重複觸發（防止資料庫殘留導致連續觸發）")]
    public float deduplicateSeconds = 3f;

    void Start()
    {
        // 啟動定時輪詢協程
        StartCoroutine(PollServerCoroutine());
    }

    /// <summary>
    /// 定時向伺服器發送請求的協程
    /// </summary>
    IEnumerator PollServerCoroutine()
    {
        while (true)
        {
            yield return StartCoroutine(ReadSignalFromServer());
            yield return new WaitForSeconds(pollInterval);
        }
    }

    /// <summary>
    /// 向 PHP 讀取訊號 (GET)
    /// </summary>
    IEnumerator ReadSignalFromServer()
    {
        // 加上 action=read 參數呼叫 api.php
        string requestUrl = apiEndpoint + "?action=read";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
        {
            // 模擬瀏覽器標頭（避免部分主機攔截）
            webRequest.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");
            // ngrok 免費版會插入警告頁（ERR_NGROK_6024），加此標頭可跳過
            webRequest.SetRequestHeader("ngrok-skip-browser-warning", "1");
            
            // 發送請求並等待回應
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                // 可以將此 Debug 註解掉以免沒連線時控制台洗版
                Debug.LogWarning($"[PhoneConnectionManager] 連線失敗: {webRequest.error}");
            }
            else
            {
                // 成功取得資料
                string downloadedText = webRequest.downloadHandler.text.Trim();

                // 如果讀到的不是空字串，代表有人撥打電話了
                if (!string.IsNullOrEmpty(downloadedText))
                {
                    // 防重複：同一號碼在 deduplicateSeconds 秒內只觸發一次
                    bool isDuplicate = (downloadedText == _lastProcessedNumber) &&
                                       (Time.time - _lastProcessedTime < deduplicateSeconds);

                    if (isDuplicate)
                    {
                        Debug.LogWarning($"[PhoneConnectionManager] 偵測到重複號碼 {downloadedText}，跳過觸發並清除。");
                        yield return StartCoroutine(ClearSignalOnServer());
                    }
                    else
                    {
                        Debug.Log($"[PhoneConnectionManager] 從手機端收到號碼: {downloadedText}");
                        _lastProcessedNumber = downloadedText;
                        _lastProcessedTime   = Time.time;

                        // 1. 先清除伺服器紀錄（必須在事件觸發前，避免事件把此物件 SetActive(false) 導致 Coroutine 失敗）
                        yield return StartCoroutine(ClearSignalOnServer());

                        // 2. 廣播號碼給所有訂閱此事件的腳本（PhoneEventHandler 等）
                        OnPhoneNumberReceived?.Invoke(downloadedText);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 向 PHP 發送清除訊號 (POST)
    /// </summary>
    IEnumerator ClearSignalOnServer()
    {
        // 準備 POST 要傳遞的表單資料 (action = clear)
        WWWForm form = new WWWForm();
        form.AddField("action", "clear");

        using (UnityWebRequest webRequest = UnityWebRequest.Post(apiEndpoint, form))
        {
            webRequest.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");
            webRequest.SetRequestHeader("ngrok-skip-browser-warning", "1");
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[PhoneConnectionManager] 已通知 PHP 清空號碼紀錄。");
            }
        }
    }

    /// <summary>
    /// 向手機端發送訊號指令 (例如 RING, HANGUP_SFX 等)
    /// </summary>
    public void SendSignalToPhone(string signalName)
    {
        StartCoroutine(SendSignalToPhoneCoroutine(signalName));
    }

    private IEnumerator SendSignalToPhoneCoroutine(string signalName)
    {
        WWWForm form = new WWWForm();
        form.AddField("action", "send_to_phone");
        form.AddField("signal", signalName);

        using (UnityWebRequest webRequest = UnityWebRequest.Post(apiEndpoint, form))
        {
            webRequest.SetRequestHeader("User-Agent", "Mozilla/5.0");
            webRequest.SetRequestHeader("ngrok-skip-browser-warning", "1");
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[PhoneConnectionManager] 成功傳送訊號給手機: {signalName}");
            }
            else
            {
                Debug.LogWarning($"[PhoneConnectionManager] 傳送訊號給手機失敗: {webRequest.error}");
            }
        }
    }
}
