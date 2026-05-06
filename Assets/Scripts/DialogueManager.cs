using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions; // 用於 CSV 解析
using TMPro; // 使用 TextMeshPro
using InteractiveNovelGames.Typography.TextControl; // 載入你的 TextControl 命名空間
using System;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    [Header("劇本設定")]
    public TextAsset csvFile;         // 拖入你的 CSV 檔案
    public string startNodeID = "START";

    [Header("UI 參考")]
    public TMP_Text nameText;                   // 人物名稱 UI (改為 TMP_Text)
    public TextControl dialogueTextControl;     // 對話內容 UI (改為你自訂的 TextControl)
    // public Image characterImage;   // 人物立繪 UI (依需求加入)

    [Header("選項設定 (UI)")]
    public GameObject choicePanel;              // 裝載選項文字的父物件 (平時隱藏)
    public TMP_Text[] choiceTexts;              // 三個選項的文字元件 (索引 0~2 對應 1~3)

    // 儲存所有劇本的字典 (Dictionary)，用 NodeID 來快速尋找對話
    private Dictionary<string, DialogueNode> dialogueDatabase = new Dictionary<string, DialogueNode>();
    
    private DialogueNode currentNode;

    void OnEnable()
    {
        // 訂閱手機連線管理器的號碼接收事件
        PhoneConnectionManager.OnPhoneNumberReceived += OnPhoneInput;
    }

    void OnDisable()
    {
        // 取消訂閱
        PhoneConnectionManager.OnPhoneNumberReceived -= OnPhoneInput;
    }

    void Start()
    {
        LoadCSV();
        PlayNode(startNodeID);
    }

    void Update()
    {
        // 檢查是否打字完畢需要顯示選項
        if (currentNode != null && currentNode.IsChoice == 1 && choicePanel != null && !choicePanel.activeSelf)
        {
            if (dialogueTextControl == null || !dialogueTextControl.IsTyping)
            {
                ShowChoices();
            }
        }

        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        // 點擊滑鼠左鍵 或 按下空白鍵 推進劇情
        if (clicked || spacePressed)
        {
            if (dialogueTextControl != null && dialogueTextControl.IsTyping)
            {
                // 如果還在打字，瞬間顯示全部
                dialogueTextControl.SkipTypewriter();
            }
            else
            {
                if (currentNode != null && currentNode.IsChoice == 1)
                {
                    // 如果是選項節點，封鎖空白鍵與點擊的推進，等待手機輸入
                    return;
                }
                
                // 如果打完了且不是選項，跳到下一句
                GoToNextNode();
            }
        }
    }

    // --- 核心功能 1：讀取並解析 CSV ---
    void LoadCSV()
    {
        if (csvFile == null)
        {
            Debug.LogError("沒有放入 CSV 檔案！");
            return;
        }

        // 讀取所有行
        string[] lines = csvFile.text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        // 處理 CSV 逗號的正規表達式 (忽略引號內的逗號)
        string pattern = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";

        // 第一行通常是標題列 (Header)，所以從 i=1 開始讀取
        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = Regex.Split(lines[i], pattern);

            if (values.Length >= 9) // 確保基本欄位數量正確 (現在包含了 Tag，所以是 9 個基本欄位)
            {
                DialogueNode node = new DialogueNode();
                node.Tag = values[0].Trim();
                node.NodeID = values[1].Trim();
                node.Character = values[2].Trim();
                node.EmotionTag = values[3].Trim();
                node.Position = values[4].Trim();
                
                // 去除可能因為有逗號而被 Excel 加上去的雙引號
                node.TextContent = values[5].Trim().Trim('"'); 
                
                node.NextID = values[6].Trim();
                node.EffectType = values[7].Trim();
                node.EffectTarget = values[8].Trim();

                // 解析新加入的分支選項欄位 (如果有填寫的話)
                if (values.Length > 9) int.TryParse(values[9].Trim(), out node.IsChoice);
                if (values.Length > 10) node.Choice1Text = values[10].Trim().Trim('"');
                if (values.Length > 11) node.Choice1Next = values[11].Trim();
                if (values.Length > 12) node.Choice2Text = values[12].Trim().Trim('"');
                if (values.Length > 13) node.Choice2Next = values[13].Trim();
                if (values.Length > 14) node.Choice3Text = values[14].Trim().Trim('"');
                if (values.Length > 15) node.Choice3Next = values[15].Trim();

                // 加入字典中
                dialogueDatabase.Add(node.NodeID, node);
            }
        }
        Debug.Log($"成功讀取劇本！共載入 {dialogueDatabase.Count} 句對話。");
    }

    // --- 核心功能 2：播放特定節點 ---
    void PlayNode(string nodeId)
    {
        if (choicePanel != null) choicePanel.SetActive(false); // 確保每次播放新句子時先隱藏選項

        if (string.IsNullOrEmpty(nodeId) || nodeId.ToLower() == "end" || !dialogueDatabase.ContainsKey(nodeId))
        {
            Debug.Log("劇情結束！");
            // 這裡可以呼叫隱藏 UI 或切換場景的邏輯
            return;
        }

        currentNode = dialogueDatabase[nodeId];

        // 1. 更新 UI
        nameText.text = currentNode.Character;
        
        // TODO: 在這裡依照 currentNode.EmotionTag 和 Position 切換立繪圖片
        // UpdateCharacterImage(currentNode.EmotionTag, currentNode.Position);

        // 2. 執行效果
        ExecuteEffect(currentNode.EffectType, currentNode.EffectTarget);

        // 3. 開始打字機效果 (交給你的 TextControl 處理)
        if (dialogueTextControl != null)
        {
            dialogueTextControl.SetText(currentNode.TextContent);
        }
    }

    void GoToNextNode()
    {
        PlayNode(currentNode.NextID);
    }

    // --- 選項處理 ---
    void ShowChoices()
    {
        if (choicePanel != null) choicePanel.SetActive(true);

        if (choiceTexts != null && choiceTexts.Length > 0 && choiceTexts[0] != null) 
            choiceTexts[0].text = string.IsNullOrEmpty(currentNode.Choice1Text) ? "" : $"1. {currentNode.Choice1Text}";
            
        if (choiceTexts != null && choiceTexts.Length > 1 && choiceTexts[1] != null) 
            choiceTexts[1].text = string.IsNullOrEmpty(currentNode.Choice2Text) ? "" : $"2. {currentNode.Choice2Text}";
            
        if (choiceTexts != null && choiceTexts.Length > 2 && choiceTexts[2] != null) 
            choiceTexts[2].text = string.IsNullOrEmpty(currentNode.Choice3Text) ? "" : $"3. {currentNode.Choice3Text}";
    }

    void OnPhoneInput(string number)
    {
        // 處理手機直接按 Call (傳來 NEXT 訊號)，當作按空白鍵推進劇情
        if (number == "NEXT")
        {
            if (dialogueTextControl != null && dialogueTextControl.IsTyping)
            {
                dialogueTextControl.SkipTypewriter();
            }
            else
            {
                if (currentNode != null && currentNode.IsChoice == 1)
                {
                    // 如果是選項節點，封鎖推進
                    return;
                }
                GoToNextNode();
            }
            return;
        }

        // 以下為處理分支選項的邏輯 (傳來 1, 2, 3...)
        // 只有在目前是選項節點，且選項面板已經顯示時，才接受輸入
        if (currentNode == null || currentNode.IsChoice != 1) return;
        if (choicePanel != null && !choicePanel.activeSelf) return;

        string nextNodeId = "";

        if (number == "1" && !string.IsNullOrEmpty(currentNode.Choice1Next))
        {
            nextNodeId = currentNode.Choice1Next;
        }
        else if (number == "2" && !string.IsNullOrEmpty(currentNode.Choice2Next))
        {
            nextNodeId = currentNode.Choice2Next;
        }
        else if (number == "3" && !string.IsNullOrEmpty(currentNode.Choice3Next))
        {
            nextNodeId = currentNode.Choice3Next;
        }

        if (!string.IsNullOrEmpty(nextNodeId))
        {
            PlayNode(nextNodeId);
        }
    }

    // --- 系統：執行特殊效果 ---
    void ExecuteEffect(string effect, string target)
    {
        if (string.IsNullOrEmpty(effect) || effect.ToLower() == "none") return;

        switch (effect)
        {
            case "PlaySound":
                Debug.Log($"播放音效: {target}");
                // AudioManager.PlaySound(target);
                break;
            case "Shake":
                Debug.Log($"震動目標: {target}");
                // CameraShake(target);
                break;
            default:
                Debug.LogWarning($"未知的效果類型: {effect}");
                break;
        }
    }
}
