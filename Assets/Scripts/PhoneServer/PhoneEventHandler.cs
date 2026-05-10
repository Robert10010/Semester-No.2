using UnityEngine;

/// <summary>
/// 處理從 PhoneConnectionManager 接來的號碼，並執行對應的遊戲控制邏輯。
/// 你可以在這裡設定你的那「三組電話號碼」。
/// </summary>
public class PhoneEventHandler : MonoBehaviour
{
    [Header("目標控制物件 (範例)")]
    public GameObject targetObjectToControl;
    public Light targetLightToControl;

    [Header("設定的三組電話號碼 (需完全相符才會觸發)")]
    public string targetNumberA = "0911111111";
    public string targetNumberB = "0922222222";
    public string targetNumberC = "0933333333";
    public string targetNumberD = "0919026106";

    void OnEnable()
    {
        // 腳本啟用時，訂閱接收電話號碼的事件
        PhoneConnectionManager.OnPhoneNumberReceived += HandleIncomingCall;
    }

    void OnDisable()
    {
        // 腳本停用時，取消訂閱（好習慣，避免記憶體洩漏）
        PhoneConnectionManager.OnPhoneNumberReceived -= HandleIncomingCall;
    }

    /// <summary>
    /// 當 Server 收到底層傳來的電話號碼時，會呼叫這個函式
    /// </summary>
    /// <param name="receivedNumber">接收到的字串號碼</param>
    private void HandleIncomingCall(string receivedNumber)
    {
        Debug.Log($"[PhoneEventHandler] 開始分析號碼: {receivedNumber}");

        // 比較字串是否相符，並執行不同結果
        if (receivedNumber == targetNumberA)
        {
            ActionForNumberA();
        }
        else if (receivedNumber == targetNumberB)
        {
            ActionForNumberB();
        }
        else if (receivedNumber == targetNumberC)
        {
            ActionForNumberC();
        }
        else if (receivedNumber == targetNumberD)
        {
            ActionForNumberD();
        }
        else
        {
            Debug.LogWarning($"[PhoneEventHandler] 收到的號碼 {receivedNumber} 不在預設清單中，無法進行連動。");
            // 可在此處播放撥號錯誤的語音或音效
        }
    }

    // ==========================================
    // 下面請自由替換成您的控制邏輯 (已達成控制物件的目的)
    // ==========================================

    private void ActionForNumberA()
    {
        Debug.Log($"[PhoneEventHandler] 觸發 號碼A [{targetNumberA}] 的對應事件！");
        
        // 範例：讓綁定的目標物件消失
        if (targetObjectToControl != null)
        {
            Debug.Log("撥打A電話");
        }
    }

    private void ActionForNumberB()
    {
        Debug.Log($"[PhoneEventHandler] 觸發 號碼B [{targetNumberB}] 的對應事件！");
        
        // 範例：讓綁定的燈光變紅色
        if (targetLightToControl != null)
        {
            targetLightToControl.color = Color.red;
            Debug.Log("燈光變成紅色了！");
        }
    }

    private void ActionForNumberC()
    {
        Debug.Log($"[PhoneEventHandler] 觸發 號碼C [{targetNumberC}] 的對應事件！");
        
        // 範例：讓物件旋轉
        if (targetObjectToControl != null)
        {
            targetObjectToControl.transform.Rotate(0, 90, 0);
            Debug.Log("目標物件旋轉了 90 度！");
        }
    }
    private void ActionForNumberD()
    {
        Debug.Log($"[PhoneEventHandler] 觸發 號碼D [{targetNumberD}] 的對應事件！");
    }
}
