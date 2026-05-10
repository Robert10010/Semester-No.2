using UnityEngine;

public class GameFlowController : MonoBehaviour
{
    [Header("UI 畫布設定")]
    [Tooltip("遊戲一開始顯示的主視覺畫面")]
    public GameObject startCanvas;
    
    [Tooltip("正式遊玩的劇情畫面")]
    public GameObject playCanvas;

    [Header("系統參考")]
    public DialogueManager dialogueManager;

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
            // 防呆機制：只有在 StartCanvas 開啟著的時候（代表還沒開始遊戲），才允許開始
            if (startCanvas != null && startCanvas.activeSelf)
            {
                Debug.Log("[GameFlowController] 收到 QR 掃描啟動訊號，開始遊戲！");
                
                // 關閉主視覺，開啟遊玩畫面
                startCanvas.SetActive(false);
                if (playCanvas != null) playCanvas.SetActive(true);

                // 呼叫劇情管理器開始跑第一句對話
                if (dialogueManager != null)
                {
                    dialogueManager.StartGame();
                }
            }
            else
            {
                // 如果 startCanvas 已經關閉（代表遊戲正在進行中）
                // 收到 START_GAME 就直接忽略，防止別人重新整理網頁干擾現有玩家
                Debug.Log("[GameFlowController] 忽略 START_GAME 訊號，因為遊戲已經在進行中。");
            }
        }
    }
}
