using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions; // 用於 CSV 解析
using TMPro; // 使用 TextMeshPro
using InteractiveNovelGames.Typography.TextControl; // 載入你的 TextControl 命名空間
using System;
using UnityEngine.InputSystem;
using UnityEngine.Playables; // 支援 Timeline/Playable 控制

public class DialogueManager : MonoBehaviour
{
    [Header("劇本設定")]
    public TextAsset csvFile;         // 拖入你的 CSV 檔案
    
    [Header("系統自動抓取 (無需手動填寫)")]
    [Tooltip("程式會自動讀取 CSV 中有填寫 Tag 的節點，作為每通電話的起點")]
    public List<string> callerStartNodes = new List<string>();
    private List<string> callerTags = new List<string>(); // 儲存對應的 Tag 標籤，用來尋找 _HangUp / _Transfer / _continue
    private int currentCallerIndex = 0;

    public enum PhoneState { Idle, Ringing, Talking }
    public PhoneState currentPhoneState = PhoneState.Idle;

    private bool isInitialCallAction = false; // 判斷是否處於剛接通的第一句話，需顯示三大選項

    [Header("電話來電音效設定")]
    [Tooltip("對應 AudioManager 中您自訂的來電鈴聲音效名稱")]
    public string ringtoneSFXName = "Ringtone";

    [Header("電話拿起與放下音效設定")]
    [Tooltip("對應 AudioManager 中您自訂的電話拿起音效名稱 (例如: Phone_pick)")]
    public string phonePickUpSFXName = "Phone_pick";
    [Tooltip("對應 AudioManager 中您自訂的電話放下音效名稱 (例如: Phone_put)")]
    public string phonePutDownSFXName = "Phone_put";

    [Header("角色 3D 物件設定")]
    [Tooltip("請放入 PlayCanvas 中的 Character 父物件，裡面的子物件名稱必須與 Tag 相同")]
    public Transform charactersParent;

    [Header("角色 Timeline 演出設定")]
    [Tooltip("掛載在 charactersParent 上的 PlayableDirector (用於統一控制漸顯與漸隱)")]
    public PlayableDirector characterDirector;

    [Header("電話話筒狀態設定")]
    [Tooltip("電話接通時開啟的子項目 (話筒拿起)")]
    public GameObject phonePickUp;
    [Tooltip("掛斷或未接通時開啟的子項目 (話筒放下)")]
    public GameObject phoneDown;

    [Header("UI 參考")]
    public GameObject dialogueBox;              // 對話框背景物件
    public TMP_Text nameText;                   // 人物名稱 UI (改為 TMP_Text)
    public TextControl dialogueTextControl;     // 對話內容 UI (改為你自訂的 TextControl)

    [Header("玩家角色設定")]
    [Tooltip("劇本 CSV 中代表玩家的名字 (例如: 我)")]
    public string playerCharacterName = "我";

    [Header("選項設定 (UI)")]
    public GameObject choicePanel;              // 裝載選項文字的父物件 (平時隱藏)
    public TMP_Text[] choiceTexts;              // 三個選項的文字元件 (索引 0~2 對應 1~3)

    // 儲存所有劇本的字典 (Dictionary)，用 NodeID 來快速尋找對話
    private Dictionary<string, DialogueNode> dialogueDatabase = new Dictionary<string, DialogueNode>();
    
    private DialogueNode currentNode;

    private float lastAnswerTime = -999f;

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
        UpdatePhoneVisuals(false); // 預設關閉 PickUp，開啟 down
        // 移除了這裡的 PlayNode(startNodeID);，改為等 GameFlowController 呼叫 StartGame() 時才播放
    }

    // 提供給 GameFlowController 呼叫的方法
    public void StartGame()
    {
        currentCallerIndex = 0;
        
        // 遊戲一開始，確保鈴聲是停止的
        if (AudioManager.Instance != null) AudioManager.Instance.StopLoopingSFX();

        // 遊戲一開始，隱藏所有角色物件
        HideAllCharacters();
        
        // 遊戲一開，確保電話狀態正確 (放下狀態)
        UpdatePhoneVisuals(false);
        
        // 隱藏對話框與文字
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (dialogueTextControl != null) dialogueTextControl.gameObject.SetActive(false);
        
        TriggerNextCall();
    }

    private void TriggerNextCall()
    {
        if (currentCallerIndex < callerStartNodes.Count)
        {
            currentPhoneState = PhoneState.Ringing;
            Debug.Log($"[DialogueManager] 電話響起！等待玩家接聽... (第 {currentCallerIndex + 1} 通)");
            
            // 播放電話響鈴音效 (透過中央音效管理器)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLoopingSFX(ringtoneSFXName);
            }

            // 發送響鈴訊號給手機，讓手機端也播放鈴聲
            if (PhoneConnectionManager.Instance != null)
            {
                PhoneConnectionManager.Instance.SendSignalToPhone("RING");
            }
        }
        else
        {
            currentPhoneState = PhoneState.Idle;
            Debug.Log($"[DialogueManager] {callerStartNodes.Count}通電話皆已播畢！");
        }
    }

    void Update()
    {
        // 強制安全機制：確保非響鈴狀態時，鈴聲絕對不會繼續播放
        if (currentPhoneState != PhoneState.Ringing && AudioManager.Instance != null && AudioManager.Instance.sfxSource.isPlaying && AudioManager.Instance.sfxSource.loop)
        {
            AudioManager.Instance.StopLoopingSFX();
        }

        // 如果遊戲還沒開始 (currentNode 為 null)，就不要處理任何點擊或按鍵
        if (currentNode == null) return;
        
        // 若處於尚未接聽狀態，亦不處理對話推進
        if (currentPhoneState != PhoneState.Talking) return;

        // 檢查是否打字完畢需要顯示選項
        if (dialogueTextControl == null || !dialogueTextControl.IsTyping)
        {
            // 如果是剛接通的第一句話 (isInitialCallAction)，不顯示 UI 選項，單純等待玩家用手機輸入 1, 2, 3
            if (!isInitialCallAction && currentNode.IsChoice == 1 && choicePanel != null && !choicePanel.activeSelf)
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
                // 如果在等待三大選項，或該節點是一般選項，封鎖滑鼠與空白鍵推進
                if (isInitialCallAction) return;
                if (currentNode != null && currentNode.IsChoice == 1) return;
                
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
        
        // 清空舊清單，準備重新從 CSV 自動抓取
        callerStartNodes.Clear();
        callerTags.Clear();

        // 使用更強大的自訂 CSV 解析器，支援單一儲存格內包含「真實換行」與「逗號」
        List<string[]> parsedLines = ParseCSV(csvFile.text);

        // 第一行通常是標題列 (Header)，所以從 i=1 開始讀取
        for (int i = 1; i < parsedLines.Count; i++)
        {
            string[] values = parsedLines[i];

            if (values.Length >= 9) // 確保基本欄位數量正確 (現在包含了 Tag，所以是 9 個基本欄位)
            {
                DialogueNode node = new DialogueNode();
                node.Tag = values[0];
                node.NodeID = values[1];
                node.Character = values[2];
                node.EmotionTag = values[3];
                node.Position = values[4];
                
                // 自訂解析器已經幫忙去除外層雙引號了，這裡只需替換字串中寫死的 \n 為真實換行
                node.TextContent = values[5].Replace("\\n", "\n"); 
                
                node.NextID = values[6];
                node.EffectType = values[7];
                node.EffectTarget = values[8];

                // 解析新加入的分支選項欄位 (如果有填寫的話)
                if (values.Length > 9) int.TryParse(values[9], out node.IsChoice);
                if (values.Length > 10) node.Choice1Text = values[10];
                if (values.Length > 11) node.Choice1Next = values[11];
                if (values.Length > 12) node.Choice2Text = values[12];
                if (values.Length > 13) node.Choice2Next = values[13];
                if (values.Length > 14) node.Choice3Text = values[14];
                if (values.Length > 15) node.Choice3Next = values[15];

                // 自動抓取：如果 Tag 欄位有填寫內容，就視為新的一通電話起點
                // 【修復】不再強制 NodeID 必須包含 START，而是檢查這個 Tag 是否已經加入過
                if (!string.IsNullOrEmpty(node.Tag))
                {
                    string trimmedTag = node.Tag;
                    if (!callerTags.Contains(trimmedTag))
                    {
                        callerStartNodes.Add(node.NodeID);
                        callerTags.Add(trimmedTag);
                    }
                    else
                    {
                        // 強化機制：如果這個 Tag 已經加過，但這行的 NodeID 明確標示了 START，就強制將它替換為真正的起點
                        if (node.NodeID.ToUpper().Contains("START") || node.NodeID.ToUpper().Contains("SRART"))
                        {
                            int index = callerTags.IndexOf(trimmedTag);
                            callerStartNodes[index] = node.NodeID;
                        }
                    }
                }   

                // 加入字典中
                dialogueDatabase.Add(node.NodeID, node);
            }
        }
        Debug.Log($"成功讀取劇本！共載入 {dialogueDatabase.Count} 句對話。");
    }

    // --- 新增：安全的 CSV 解析器 (解決 Excel 多行文本或特殊符號問題) ---
    private List<string[]> ParseCSV(string text)
    {
        List<string[]> rows = new List<string[]>();
        bool inQuotes = false;
        List<string> currentRow = new List<string>();
        string currentValue = "";

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            if (c == '\"')
            {
                // 處理雙引號跳脫 (Excel 會把文內的 " 變成 "")
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '\"')
                {
                    currentValue += '\"';
                    i++; // 跳過下一個引號
                }
                else
                {
                    inQuotes = !inQuotes; // 切換引號狀態
                }
            }
            else if (c == ',' && !inQuotes)
            {
                currentRow.Add(currentValue.Trim());
                currentValue = "";
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                // 碰到沒有被雙引號包住的換行，才代表這是換到下一行
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                
                currentRow.Add(currentValue.Trim());
                
                // 防呆：忽略空行
                if (currentRow.Count > 1 || (currentRow.Count == 1 && !string.IsNullOrWhiteSpace(currentRow[0])))
                {
                    rows.Add(currentRow.ToArray());
                }
                
                currentRow = new List<string>();
                currentValue = "";
            }
            else
            {
                currentValue += c;
            }
        }
        
        // 加入最後一行結尾
        if (currentRow.Count > 0 || !string.IsNullOrWhiteSpace(currentValue))
        {
            currentRow.Add(currentValue.Trim());
            rows.Add(currentRow.ToArray());
        }
        
        return rows;
    }

    // 判斷某個 Tag 的所有載入節點中，是否包含任何以 _Decision 結尾的節點
    private bool HasDecisionNode(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        foreach (var key in dialogueDatabase.Keys)
        {
            if (key.StartsWith(tag, StringComparison.OrdinalIgnoreCase) && 
                key.EndsWith("_Decision", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    // --- 核心功能 2：播放特定節點 ---
    void PlayNode(string nodeId)
    {
        if (choicePanel != null) choicePanel.SetActive(false); // 確保每次播放新句子時先隱藏選項

        if (string.IsNullOrEmpty(nodeId) || nodeId.ToLower() == "end" || !dialogueDatabase.ContainsKey(nodeId))
        {
            Debug.Log("[DialogueManager] 劇情節點結束，準備切換下一通電話！");
            EndCurrentCallAndTriggerNext();
            return;
        }

        currentNode = dialogueDatabase[nodeId];

        // 判斷是否為決策控制節點：
        // 1. NodeID 明確以 _Decision 結尾 (新邏輯，如 C1_Decision)
        // 2. 或者是該通話的起點節點，且該劇本 Tag 中「沒有任何以 _Decision 結尾的節點」(向下相容舊邏輯)
        bool isDecisionNode = nodeId.EndsWith("_Decision", StringComparison.OrdinalIgnoreCase);
        if (!isDecisionNode && currentCallerIndex < callerTags.Count && currentCallerIndex < callerStartNodes.Count)
        {
            string currentTag = callerTags[currentCallerIndex];
            bool isStartNode = (nodeId == callerStartNodes[currentCallerIndex]);
            if (isStartNode && !HasDecisionNode(currentTag))
            {
                isDecisionNode = true;
            }
        }

        if (isDecisionNode)
        {
            isInitialCallAction = true;
            Debug.Log($"[DialogueManager] 觸發電話選擇控制 (NodeID: {nodeId})，等待玩家輸入手機按鍵 1 (掛斷), 2 (轉接) 或 3 (繼續)");
        }

        // 1. 更新 UI
        nameText.text = currentNode.Character;
        
        // 確保對話框開啟
        if (dialogueBox != null) dialogueBox.SetActive(true);

        // 動態開啟角色 3D 物件：當說話的角色不是玩家且不為空白時，才開啟該電話 Tag 對應的角色物件
        if (!string.IsNullOrEmpty(currentNode.Character) && currentNode.Character != playerCharacterName)
        {
            if (currentCallerIndex < callerTags.Count)
            {
                string currentTag = callerTags[currentCallerIndex];
                ShowCharacterByTag(currentTag);
            }
        }

        // TODO: 在這裡依照 currentNode.EmotionTag 和 Position 切換立繪圖片
        // UpdateCharacterImage(currentNode.EmotionTag, currentNode.Position);

        // 2. 執行效果
        ExecuteEffect(currentNode.EffectType, currentNode.EffectTarget);

        // 3. 開始打字機效果 (交給你的 TextControl 處理)
        if (dialogueTextControl != null)
        {
            dialogueTextControl.gameObject.SetActive(true);
            dialogueTextControl.SetText(currentNode.TextContent);
        }
    }

    void GoToNextNode()
    {
        PlayNode(currentNode.NextID);
    }

    // --- 電話視覺狀態控制 ---
    private void UpdatePhoneVisuals(bool isPickedUp)
    {
        if (phonePickUp != null) phonePickUp.SetActive(isPickedUp);
        if (phoneDown != null) phoneDown.SetActive(!isPickedUp);
    }

    // --- 角色顯示控制 ---
    private void HideAllCharacters()
    {
        if (charactersParent == null) return;

        // 如果目前正在跑延遲隱藏，先取消它
        CancelInvoke(nameof(DisableAllCharacterObjects));

        // 如果有 Timeline 且在啟用狀態，執行倒放漸隱
        if (characterDirector != null && characterDirector.gameObject.activeInHierarchy)
        {
            characterDirector.time = characterDirector.duration; // 定位至最尾端
            if (characterDirector.playableGraph.IsValid())
            {
                var rootPlayable = characterDirector.playableGraph.GetRootPlayable(0);
                if (rootPlayable.IsValid())
                {
                    rootPlayable.SetSpeed(-1.0); // 倒放速度
                }
            }
            characterDirector.Play();

            // 延遲關閉子物件，等漸隱動畫播完後真正 SetActive(false)
            Invoke(nameof(DisableAllCharacterObjects), (float)characterDirector.duration);
        }
        else
        {
            // 若沒有 Timeline，直接立即隱藏
            DisableAllCharacterObjects();
        }
    }

    private void DisableAllCharacterObjects()
    {
        if (charactersParent == null) return;
        foreach (Transform child in charactersParent)
        {
            child.gameObject.SetActive(false);
        }
    }

    private void ShowCharacterByTag(string tag)
    {
        if (charactersParent == null) return;

        // 當要顯示新角色時，如果前一個角色正在執行「漸隱延遲隱藏」，必須立即取消它
        CancelInvoke(nameof(DisableAllCharacterObjects));

        bool needFadeIn = false;

        foreach (Transform child in charactersParent)
        {
            if (child.name == tag)
            {
                // 如果角色原本是隱藏的，代表需要觸發漸顯
                if (!child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                    needFadeIn = true;
                }
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }

        // 執行 Timeline 正向漸顯
        if (needFadeIn && characterDirector != null)
        {
            characterDirector.time = 0; // 回到最起點
            if (characterDirector.playableGraph.IsValid())
            {
                var rootPlayable = characterDirector.playableGraph.GetRootPlayable(0);
                if (rootPlayable.IsValid())
                {
                    rootPlayable.SetSpeed(1.0); // 確保是正向播放速度
                }
            }
            characterDirector.Play();
        }
    }

    // --- 通話結束與換人處理 ---
    private void EndCurrentCallAndTriggerNext()
    {
        if (currentPhoneState == PhoneState.Idle) return; // 避免重複觸發

        currentPhoneState = PhoneState.Idle;
        
        // 確保掛斷時狀態為放下
        UpdatePhoneVisuals(false);

        // 播放電話放下音效
        if (AudioManager.Instance != null && !string.IsNullOrEmpty(phonePutDownSFXName))
        {
            AudioManager.PlaySound(phonePutDownSFXName);
        }

        // 通知手機端停止播放並掛斷
        if (PhoneConnectionManager.Instance != null)
        {
            PhoneConnectionManager.Instance.SendSignalToPhone("HANGUP");
        }

        currentNode = null;
        if (dialogueTextControl != null) dialogueTextControl.ClearText();
        if (nameText != null) nameText.text = "";
        if (choicePanel != null) choicePanel.SetActive(false);
        
        // 電話掛斷，隱藏對話框與文字
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (dialogueTextControl != null) dialogueTextControl.gameObject.SetActive(false);
        
        // 電話掛斷，隱藏畫面上的角色
        HideAllCharacters();
        
        CancelInvoke(nameof(TriggerNextCall)); // 防呆：確保不會重複排程下一通電話
        currentCallerIndex++;
        Invoke(nameof(TriggerNextCall), 2.0f); // 延遲 2 秒後撥打下一通
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
        // === 處理真實電話介面傳來的接聽與掛斷訊號 ===
        if (number == "ANSWER" && currentPhoneState == PhoneState.Ringing)
        {
            lastAnswerTime = Time.time;

            // 接聽電話，停止響鈴音效 (透過中央音效管理器)
            if (AudioManager.Instance != null) AudioManager.Instance.StopLoopingSFX();

            currentPhoneState = PhoneState.Talking;
            isInitialCallAction = false; // 預設為 false，由 PlayNode 動態判斷是否開啟決策
            Debug.Log("[DialogueManager] 玩家已接聽電話！");

            // 播放電話拿起音效
            if (AudioManager.Instance != null && !string.IsNullOrEmpty(phonePickUpSFXName))
            {
                AudioManager.PlaySound(phonePickUpSFXName);
            }
            
            // 接通時：開啟 PickUp，關閉 down
            UpdatePhoneVisuals(true);

            // 接聽時：通知手機端停止播放鈴聲
            if (PhoneConnectionManager.Instance != null)
            {
                PhoneConnectionManager.Instance.SendSignalToPhone("STOP_RING");
            }
            
            // 開啟對話框與文字 (PlayNode 會自動開啟對應的，此處做防呆開啟)
            if (dialogueTextControl != null) dialogueTextControl.gameObject.SetActive(true);

            PlayNode(callerStartNodes[currentCallerIndex]);
            return;
        }
        else if (number == "HANGUP" && currentPhoneState == PhoneState.Talking)
        {
            // 防呆：如果剛接聽不到 1 秒就收到掛斷訊號，可能是硬體開關彈跳，忽略它
            if (Time.time - lastAnswerTime < 1.0f)
            {
                Debug.LogWarning("[DialogueManager] 忽略過快的掛斷訊號 (可能是硬體開關彈跳)。");
                return;
            }

            // 掛斷時：關閉 PickUp，開啟 down
            UpdatePhoneVisuals(false);

            string tag = callerTags[currentCallerIndex];
            string targetNodeId = tag + "_HangUp";
            if (!dialogueDatabase.ContainsKey(targetNodeId))
            {
                targetNodeId = tag + "_HangUp"; // 支援兩種拼法
            }
            
            // 如果已經在播放掛斷台詞了，就不重複觸發，避免玩家狂按掛斷鍵
            if (currentNode != null && currentNode.NodeID == targetNodeId) return;

            Debug.Log("[DialogueManager] 玩家按下實體掛斷鍵，播放掛斷反應台詞！");
            isInitialCallAction = false; // 解除等待選項狀態
            
            // 檢查 CSV 中是否有這句掛斷台詞，有的話就播放，沒有就直接結束
            if (dialogueDatabase.ContainsKey(targetNodeId))
            {
                PlayNode(targetNodeId);
            }
            else
            {
                EndCurrentCallAndTriggerNext();
            }
            return;
        }

        // === 處理特別指令：輸入 "000" 進行轉接 ===
        if (number == "000" && currentPhoneState == PhoneState.Talking)
        {
            isInitialCallAction = false; // 解除等待選項狀態

            string transferNodeId = callerTags[currentCallerIndex] + "_Transfer";

            // 如果 CSV 劇本中已經包含轉接起始節點，直接播放它 (避免重複播放)
            if (dialogueDatabase.ContainsKey(transferNodeId))
            {
                PlayNode(transferNodeId);
            }
            else
            {
                // 如果 CSV 中未設定，才動態生成臨時節點作為過度
                DialogueNode playerNode = new DialogueNode();
                playerNode.Character = playerCharacterName;
                playerNode.TextContent = "我先幫你轉接到心理醫師。";
                playerNode.NextID = transferNodeId;

                if (!dialogueDatabase.ContainsKey("TEMP_TRANSFER")) dialogueDatabase.Add("TEMP_TRANSFER", playerNode);
                else dialogueDatabase["TEMP_TRANSFER"] = playerNode;

                PlayNode("TEMP_TRANSFER");
            }
            return;
        }

        // 處理手機直接按 Call (傳來 NEXT 訊號)，當作按空白鍵推進劇情
        if (number == "NEXT")
        {
            if (dialogueTextControl != null && dialogueTextControl.IsTyping)
            {
                dialogueTextControl.SkipTypewriter();
            }
            else
            {
                if (isInitialCallAction) return; // 初始狀態不允許跳過
                if (currentNode != null && currentNode.IsChoice == 1) return;
                GoToNextNode();
            }
            return;
        }

        // === 處理三大初始選項 (剛接通聽完第一句話時) ===
        if (isInitialCallAction)
        {
            // 確保打字結束後才允許玩家輸入選項，避免玩家提早按
            if (dialogueTextControl != null && dialogueTextControl.IsTyping) return;
            
            if (number == "1" || number == "2" || number == "3")
            {
                isInitialCallAction = false; // 接收到正確指令，解除初始等待狀態
                
                if (number == "1") // 1. 掛斷
                {
                    // 使用 Tag 來尋找對應的被掛斷台詞 NodeID (例如 Tag 是 "Mom"，則尋找 "Mom_HangUp")
                    string tag = callerTags[currentCallerIndex];
                    string targetNodeId = tag + "_HangUp";
                    if (!dialogueDatabase.ContainsKey(targetNodeId))
                    {
                        targetNodeId = tag + "_HangUp";
                    }
                    
                    // 掛斷時：關閉 PickUp，開啟 down
                    UpdatePhoneVisuals(false);

                    // 通知手機端掛斷
                    if (PhoneConnectionManager.Instance != null)
                    {
                        PhoneConnectionManager.Instance.SendSignalToPhone("HANGUP");
                    }
                    
                    PlayNode(targetNodeId);
                }
                else if (number == "2") // 2. 轉接
                {
                    string transferNodeId = callerTags[currentCallerIndex] + "_Transfer";
                    
                    // 如果 CSV 劇本中已經包含轉接起始節點，直接播放它 (避免重複播放)
                    if (dialogueDatabase.ContainsKey(transferNodeId))
                    {
                        PlayNode(transferNodeId);
                    }
                    else
                    {
                        // 創建玩家轉接台詞的臨時節點
                        DialogueNode playerNode = new DialogueNode();
                        playerNode.Character = playerCharacterName;
                        playerNode.TextContent = "我先幫你轉接到心理醫師。";
                        playerNode.NextID = transferNodeId;
                        
                        if (!dialogueDatabase.ContainsKey("TEMP_TRANSFER")) dialogueDatabase.Add("TEMP_TRANSFER", playerNode);
                        else dialogueDatabase["TEMP_TRANSFER"] = playerNode;
                            
                        PlayNode("TEMP_TRANSFER");
                    }
                }
                else if (number == "3") // 3. 繼續收聽
                {
                    // 繼續收聽：跳轉到對應的繼續台詞 NodeID (例如 Tag 是 "Mom"，則尋找 "Mom_Continue" 或 "Mom_continue")
                    string continueNodeId = callerTags[currentCallerIndex] + "_Continue";
                    if (!dialogueDatabase.ContainsKey(continueNodeId))
                    {
                        continueNodeId = callerTags[currentCallerIndex] + "_continue";
                    }
                    PlayNode(continueNodeId);
                }
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
