using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions; // 用於 CSV 解析
using TMPro; // 使用 TextMeshPro
using InteractiveNovelGames.Typography.TextControl; // 載入你的 TextControl 命名空間
using System;
using UnityEngine.InputSystem;
using UnityEngine.Playables; // 支援 Timeline/Playable 控制
using UnityEngine.SceneManagement; // 支援結局場景跳轉
using UnityEngine.Video; // 支援影片播放器控制

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
    public GameObject dialogueBox;              // 對話框背景物件 (NPC 用)
    public GameObject dialogueBoxUser;          // 玩家專用對話框背景物件 (玩家用)
    public TMP_Text nameText;                   // 人物名稱 UI (改為 TMP_Text)
    public TextControl dialogueTextControl;     // 對話內容 UI (改為你自訂的 TextControl)
    public TextControl dialogueTextControlUser; // 玩家對話內容 UI (改為你自訂的 TextControl)

    [Header("玩家角色設定")]
    [Tooltip("劇本 CSV 中代表玩家的名字 (例如: 玩家)")]
    public string playerCharacterName = "玩家"; // 【修復】預設直接改為 "玩家"，完美對齊劇本

    [Header("選項設定 (UI)")]
    public GameObject choicePanel;              // 裝載選項文字的父物件 (平時隱藏)
    public TMP_Text[] choiceTexts;              // 三個選項的文字元件 (索引 0~2 對應 1~3)

    // 儲存所有劇本的字典 (Dictionary)，用 NodeID 來快速尋找對話
    private Dictionary<string, DialogueNode> dialogueDatabase = new Dictionary<string, DialogueNode>();
    
    private DialogueNode currentNode;

    private bool isNewCallJustAnswered = false; // 標記是否為剛接通的第一句話，以強制播放漸顯 Timeline
    private Coroutine reversePlayCoroutine;     // 控制漸隱（倒放）的協程
    private bool isHangingUp = false;           // 標記目前是否正處於掛斷對話播放流程中，用以禁止顯示 NPC 2D 角色

    [Header("遊戲時間限制設定")]
    [Tooltip("遊戲限時時間 (秒)，預設 180 秒 (3 分鐘)")]
    public float maxGameDuration = 180f;
    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    private bool isTimeUp = false;
    private bool isFirstFadeOut = true; // 標記是否為遊戲開始的第一次 Fadeout
    private bool isInitialBookOpening = false; // 標記是否為遊戲剛開場的教學手冊播放，以在手冊關閉後啟動遊戲

    [Header("電視影片設定")]
    [Tooltip("播電視影片的 VideoPlayer 組件")]
    public VideoPlayer tvVideoPlayer;
    [Tooltip("用於控制電視影片漸暗的 CanvasGroup 組件")]
    public CanvasGroup tvVideoCanvasGroup;
    [Tooltip("用於 2D 角色遮罩電視影片的 GameObject (對應 TV_Video_mask)")]
    public GameObject tvVideoMaskObject;
    [Tooltip("電視影片漸暗消失的時間 (秒)")]
    public float tvFadeDuration = 0.5f;
    private Coroutine tvFadeCoroutine; // 電視漸暗協程

    [Header("教學手冊設定")]
    [Tooltip("教學手冊的 PlayableDirector (tech_bookTimeline)")]
    public PlayableDirector techBookDirector;
    [Tooltip("教學手冊 UI 物件 (例如 playcanvas 中的 book image 或 UI tech_book)，若未指定會自動搜尋")]
    public GameObject techBookUIObject;
    [Tooltip("第一個停止點的幀數 (預設 150)")]
    public float bookStopFrame1 = 150f;
    [Tooltip("第二個停止點/總幀數的幀數 (預設 300)")]
    public float bookStopFrame2 = 300f;
    [Tooltip("Timeline 的幀率 (預設 60fps)")]
    public float bookTimelineFps = 60f;

    private int bookPlayState = 0; // 0 = 關閉/閒置, 1 = 正在播放前150幀, 2 = 暫停在150幀, 3 = 正在播放後續150幀

    private TextControl ActiveTextControl
    {
        get
        {
            bool isPlayer = (currentNode != null && currentNode.Character == playerCharacterName);
            return (isPlayer && dialogueTextControlUser != null) ? dialogueTextControlUser : dialogueTextControl;
        }
    }

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

        // 自動配置打字音效保底機制
        if (dialogueTextControl != null && string.IsNullOrEmpty(dialogueTextControl.TypingSoundName))
        {
            dialogueTextControl.TypingSoundName = "Dialogue_sound_1";
        }
        if (dialogueTextControlUser != null && string.IsNullOrEmpty(dialogueTextControlUser.TypingSoundName))
        {
            dialogueTextControlUser.TypingSoundName = "Dialogue_sound_2";
        }

        // 自動保底搜尋教學手冊 UI 物件
        if (techBookUIObject == null && techBookDirector != null)
        {
            GameObject foundBook = GameObject.Find("tech_book");
            if (foundBook != null && foundBook != techBookDirector.gameObject)
            {
                techBookUIObject = foundBook;
            }
            else
            {
                foundBook = GameObject.Find("book image");
                if (foundBook != null) techBookUIObject = foundBook;
            }
        }
    }

    // 提供給 GameFlowController 呼叫的方法
    public void StartGame()
    {
        currentCallerIndex = 0;
        
        // 重設限時與計時器狀態
        gameTimer = 0f;
        isTimerRunning = false;
        isTimeUp = false;
        isFirstFadeOut = true;
        isInitialBookOpening = false; // 重設開場手冊標記
        
        // 遊戲一開始，開啟電視噪聲影片的播放並將透明度重設為 1
        if (tvFadeCoroutine != null)
        {
            StopCoroutine(tvFadeCoroutine);
            tvFadeCoroutine = null;
        }
        if (tvVideoPlayer != null)
        {
            tvVideoPlayer.gameObject.SetActive(true);
            tvVideoPlayer.isLooping = true;
            tvVideoPlayer.Play();
        }
        if (tvVideoCanvasGroup != null)
        {
            tvVideoCanvasGroup.gameObject.SetActive(true);
            tvVideoCanvasGroup.alpha = 1f;
        }
        if (tvVideoMaskObject != null)
        {
            tvVideoMaskObject.SetActive(true);
            CanvasGroup cg = tvVideoMaskObject.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
        
        // 遊戲一開始，確保鈴聲是停止的
        if (AudioManager.Instance != null) AudioManager.Instance.StopLoopingSFX();

        // 遊戲一開始，隱藏所有角色物件
        HideAllCharacters();
        
        // 遊戲一開，確保電話狀態正確 (放下狀態)
        UpdatePhoneVisuals(false);
        
        // 隱藏對話框與文字
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (dialogueBoxUser != null) dialogueBoxUser.SetActive(false);
        if (dialogueTextControl != null) dialogueTextControl.gameObject.SetActive(false);
        if (dialogueTextControlUser != null) dialogueTextControlUser.gameObject.SetActive(false);
        
        // 【開場手冊優化】移除原先直接的 TriggerNextCall()，改由開場手冊關閉後觸發！
    }

    private void StartGameOfficially()
    {
        isTimerRunning = true;
        gameTimer = 0f;
        isTimeUp = false;
        Debug.Log("[DialogueManager] 開場教學手冊關閉，正式開始遊戲，啟動計時器並響起第一通電話！");
        TriggerNextCall();
    }

    private void ToggleTechBook(bool active)
    {
        if (techBookDirector == null) return;

        techBookDirector.gameObject.SetActive(active);

        // 1. 自動控制 Timeline 綁定的所有 UI/動畫物件的啟用狀態 (只在開啟時強制設為 true，關閉時絕對不設為 false，使其永久保持可見)
        if (active && techBookDirector.playableAsset != null)
        {
            foreach (var binding in techBookDirector.playableAsset.outputs)
            {
                var boundObject = techBookDirector.GetGenericBinding(binding.sourceObject);
                if (boundObject != null)
                {
                    if (boundObject is Animator animator) animator.gameObject.SetActive(true);
                    else if (boundObject is GameObject go) go.SetActive(true);
                    else if (boundObject is Component comp) comp.gameObject.SetActive(true);
                }
            }
        }

        // 2. 控制手動/自動搜尋指定的 UI 物件 (只在開啟時強制設為 true，關閉時絕對不設為 false，使其永久保持可見)
        if (active && techBookUIObject != null)
        {
            techBookUIObject.SetActive(true);
        }

        // 3. 切換播放狀態
        if (active)
        {
            techBookDirector.time = 0;
            techBookDirector.Evaluate();
            techBookDirector.Play();
            bookPlayState = 1;
        }
        else
        {
            techBookDirector.Pause();
            bookPlayState = 0;
        }
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
        // 宣告滑鼠與空白鍵狀態於 Update 頂部，完美解決重複宣告 CS0136 編譯錯誤！
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        // 監控教學手冊 Timeline 播放狀態與幀數
        if (techBookDirector != null && techBookDirector.gameObject.activeInHierarchy)
        {
            double currentFrame = techBookDirector.time * bookTimelineFps;

            if (bookPlayState == 1)
            {
                if (currentFrame >= bookStopFrame1)
                {
                    techBookDirector.Pause();
                    techBookDirector.time = bookStopFrame1 / bookTimelineFps;
                    techBookDirector.Evaluate();
                    bookPlayState = 2;
                    Debug.Log($"[DialogueManager] 教學手冊已到達第 {bookStopFrame1} 幀，暫停播放，等待玩家按下 CALL/滑鼠/空白鍵。");
                }
            }
            else if (bookPlayState == 3)
            {
                bool isFinished = currentFrame >= bookStopFrame2 || 
                                   techBookDirector.time >= techBookDirector.duration ||
                                   techBookDirector.state != PlayState.Playing;

                if (isFinished)
                {
                    ToggleTechBook(false); // 關閉手冊
                    Debug.Log("[DialogueManager] 教學手冊播放完畢，關閉手冊。");

                    // 如果是開場教學手冊，玩家關閉手冊後才正式開始遊戲與計時！
                    if (isInitialBookOpening)
                    {
                        isInitialBookOpening = false;
                        StartGameOfficially();
                    }
                }
            }
        }

        // 在任何對話狀態下，都允許鍵盤 0 鍵開啟教學手冊
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame)
            {
                OnPhoneInput("0");
                return;
            }
        }

        // 在任何對話狀態下，如果教學手冊處於暫停狀態 (bookPlayState == 2)，攔截滑鼠與空白鍵以推進手冊，而不是推進劇本對話
        if (bookPlayState == 2)
        {
            if (clicked || spacePressed)
            {
                if (techBookDirector != null)
                {
                    techBookDirector.Play();
                    bookPlayState = 3;
                    Debug.Log("[DialogueManager] [全狀態攔截] 玩家在手冊暫停時按下滑鼠/空白鍵，繼續播放教學手冊後續幀數。");
                }
                return; // 攔截，絕對不執行後續對話劇情邏輯
            }
        }

        // 累加遊戲計時
        if (isTimerRunning && !isTimeUp)
        {
            gameTimer += Time.deltaTime;
            if (gameTimer >= maxGameDuration)
            {
                isTimeUp = true;
                isTimerRunning = false;
                Debug.Log("[DialogueManager] 限制時間已到！當前累計時間: " + gameTimer);
            }
        }

        // 當計時結束，且當前不在通話中（沒有在 Talking），立刻跳轉結局 EndingScenes
        if (isTimeUp && currentPhoneState != PhoneState.Talking)
        {
            isTimeUp = false; // 防重複觸發
            Debug.Log("[DialogueManager] 限時已到且當前無通話，正在跳轉至結局 EndingScenes...");
            SceneManager.LoadScene("EndingScenes");
            return;
        }

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
        if (ActiveTextControl == null || !ActiveTextControl.IsTyping)
        {
            // 如果是剛接通的第一句話 (isInitialCallAction)，不顯示 UI 選項，單純等待玩家用手機輸入 1, 2, 3
            if (!isInitialCallAction && currentNode.IsChoice == 1 && choicePanel != null && !choicePanel.activeSelf)
            {
                ShowChoices();
            }
        }

        // 電腦端鍵盤測試映射：按鍵盤 1, 2, 3 模擬手機發送的按鍵信號
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                OnPhoneInput("1");
                return;
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                OnPhoneInput("2");
                return;
            }
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                OnPhoneInput("3");
                return;
            }
        }

        // 點擊滑鼠左鍵 或 按下空白鍵 推進劇情
        if (clicked || spacePressed)
        {
            if (ActiveTextControl != null && ActiveTextControl.IsTyping)
            {
                // 如果還在打字，瞬間顯示全部
                ActiveTextControl.SkipTypewriter();
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

    // 判斷某個 Tag 的所有載入節點中，是否包含 any 以 _Decision 結尾的節點
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

        // 如果當前是選項節點 (IsChoice == 1)，我們只展示選項面板，不切換對話框與清空文字！
        if (currentNode.IsChoice == 1)
        {
            // 執行可能的效果
            ExecuteEffect(currentNode.EffectType, currentNode.EffectTarget);
            
            // 立即顯示選項 (不用等待打字，因為這不是台詞，是選項)
            ShowChoices();
            return; // 直接返回，不切換對話框，不清空前一個 NPC 的對話文字！
        }

        // 1. 更新 UI
        nameText.text = currentNode.Character;
        
        // 依據說話者角色自動切換對話框背景 (NPC / 玩家)
        UpdateDialogueBoxVisibility(currentNode.Character);

        // 動態開啟角色物件：當說話的角色不是玩家且不為空白時，開啟該電話 Tag 或角色名字對應的物件
        if (!string.IsNullOrEmpty(currentNode.Character) && currentNode.Character != playerCharacterName)
        {
            if (currentCallerIndex < callerTags.Count && !isHangingUp)
            {
                string currentTag = callerTags[currentCallerIndex];
                ShowCharacterByTag(currentTag, currentNode.Character);
            }
        }

        // TODO: 在這裡依照 currentNode.EmotionTag 和 Position 切換立繪圖片
        // UpdateCharacterImage(currentNode.EmotionTag, currentNode.Position);

        // 2. 執行效果
        ExecuteEffect(currentNode.EffectType, currentNode.EffectTarget);

        // 3. 開始打字機效果 (交給對應的 TextControl 處理)
        bool isPlayer = (currentNode.Character == playerCharacterName);
        TextControl targetTextControl = (isPlayer && dialogueTextControlUser != null) ? dialogueTextControlUser : dialogueTextControl;
        TextControl otherTextControl = (targetTextControl == dialogueTextControlUser) ? dialogueTextControl : dialogueTextControlUser;

        if (otherTextControl != null)
        {
            otherTextControl.ClearText();
            otherTextControl.gameObject.SetActive(false);
        }

        if (targetTextControl != null)
        {
            targetTextControl.gameObject.SetActive(true);
            targetTextControl.SetText(currentNode.TextContent);
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

    // --- 對話框背景顯示控制 ---
    private void UpdateDialogueBoxVisibility(string characterName)
    {
        bool isPlayer = (characterName == playerCharacterName);

        if (dialogueBox != null) dialogueBox.SetActive(!isPlayer);
        if (dialogueBoxUser != null) dialogueBoxUser.SetActive(isPlayer);
    }

    // --- 角色顯示控制 ---
    private void HideAllCharacters()
    {
        if (charactersParent == null) return;

        // 如果目前正在跑倒放協程，先停止它
        if (reversePlayCoroutine != null)
        {
            StopCoroutine(reversePlayCoroutine);
            reversePlayCoroutine = null;
        }

        // 如果有 Timeline 且在啟用狀態，執行倒放漸隱
        if (characterDirector != null && characterDirector.gameObject.activeInHierarchy)
        {
            reversePlayCoroutine = StartCoroutine(ReversePlayRoutine());
        }
        else
        {
            // 若沒有 Timeline，直接立即隱藏
            DisableAllCharacterObjects();
        }
    }

    private IEnumerator ReversePlayRoutine()
    {
        if (characterDirector != null)
        {
            characterDirector.Pause(); // 暫停預設的正向播放
            
            // 如果當前時間已經在起點或小於等於0.001秒，則從終點開始倒放
            float elapsed = (float)characterDirector.time;
            if (elapsed <= 0.001f)
            {
                elapsed = (float)characterDirector.duration;
            }

            while (elapsed > 0)
            {
                elapsed -= Time.deltaTime;
                characterDirector.time = Mathf.Max(0, elapsed);
                characterDirector.Evaluate(); // 強制更新該時間點的屬性（如透明度）
                yield return null;
            }
        }

        reversePlayCoroutine = null;
        DisableAllCharacterObjects();
    }

    private void DisableAllCharacterObjects()
    {
        if (charactersParent == null) return;

        // 在隱藏所有角色前，先把電視遮罩影片 UI 物件移回父空物件 charactersParent，防止它因為跟著角色 inactive 而消失！
        if (tvVideoMaskObject != null)
        {
            tvVideoMaskObject.transform.SetParent(charactersParent, false);
            tvVideoMaskObject.SetActive(true);
        }

        foreach (Transform child in charactersParent)
        {
            // 排除電視遮罩影片 UI 本身，不要把電視遮罩也關閉了
            if (tvVideoMaskObject != null && child == tvVideoMaskObject.transform) continue;

            child.gameObject.SetActive(false);
        }

        // 如果是遊戲開始的第一次 Fadeout timeline 播放完畢，立即自動開啟開場教學手冊！
        if (isFirstFadeOut)
        {
            isFirstFadeOut = false;
            if (techBookDirector != null)
            {
                ToggleTechBook(true);
                isInitialBookOpening = true;
                Debug.Log("[DialogueManager] 開場 Fadeout 播放完畢，立即自動開啟教學手冊，播放前 150 幀。");
            }
            else
            {
                // 若無手冊，直接開始遊戲
                StartGameOfficially();
            }
        }
    }

    private void ShowCharacterByTag(string tag, string characterName)
    {
        if (charactersParent == null) return;

        // 確保父物件本身是啟用的
        charactersParent.gameObject.SetActive(true);

        // 當要顯示新角色時，如果正在執行漸隱協程，必須立即停止它
        if (reversePlayCoroutine != null)
        {
            StopCoroutine(reversePlayCoroutine);
            reversePlayCoroutine = null;
        }

        bool needFadeIn = false;
        int childCount = charactersParent.childCount;
        Transform matchedChild = null; // 用於延遲記錄匹配成功的角色

        for (int i = 0; i < childCount; i++)
        {
            Transform child = charactersParent.GetChild(i);
            
            // 排除電視遮罩影片本身，不要在遍歷時將它關閉了！
            if (tvVideoMaskObject != null && child == tvVideoMaskObject.transform) continue;
            
            // 匹配邏輯：
            // 1. 如果子物件只有一個，或者扣除電視遮罩後只有一個，直接匹配成功！
            // 2. 否則進行名字雙重模糊匹配
            bool isMatch = false;
            
            // 計算當前真正代表角色的子物件數量
            int characterChildCount = childCount;
            if (tvVideoMaskObject != null && charactersParent.Find(tvVideoMaskObject.name) != null)
            {
                characterChildCount--;
            }

            if (characterChildCount == 1)
            {
                isMatch = true;
            }
            else
            {
                string childNameLower = child.name.ToLower();
                string tagLower = tag.ToLower();
                string charNameLower = string.IsNullOrEmpty(characterName) ? "" : characterName.ToLower();

                isMatch = childNameLower == tagLower || 
                          childNameLower.Contains(tagLower) || 
                          tagLower.Contains(childNameLower) ||
                          (!string.IsNullOrEmpty(charNameLower) && 
                           (childNameLower == charNameLower || 
                            childNameLower.Contains(charNameLower) || 
                            charNameLower.Contains(childNameLower)));
            }

            if (isMatch)
            {
                // 如果角色原本是隱藏的，或者是剛接通新電話，代表需要觸發漸顯
                if (!child.gameObject.activeSelf || isNewCallJustAnswered)
                {
                    child.gameObject.SetActive(true);
                    needFadeIn = true;
                }
                matchedChild = child; // 延遲記錄匹配成功的角色，防越界 Bug
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }

        // 【延遲 Reparent】在 for 循環完全結束後，再執行 SetParent，100% 避免 child out of bounds 越界 Bug！
        if (matchedChild != null && tvVideoMaskObject != null)
        {
            tvVideoMaskObject.transform.SetParent(matchedChild, false);
            tvVideoMaskObject.transform.localScale = Vector3.one; // 強制重置 Scale 防止黑洞/縮小 Bug
            
            // 重設 RectTransform，使其在 2D 角色物件內完全拉滿填滿大小
            RectTransform rect = tvVideoMaskObject.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        // 執行 Timeline 正向漸顯
        if (needFadeIn && characterDirector != null)
        {
            characterDirector.gameObject.SetActive(true); // 確保 Timeline 播放器物件是啟用的
            characterDirector.time = 0; // 回到最起點
            characterDirector.Play();
        }

        // 重置新接通標記
        isNewCallJustAnswered = false;
    }

    // --- 通話結束與換人處理 ---
    private void EndCurrentCallAndTriggerNext()
    {
        if (currentPhoneState == PhoneState.Idle) return; // 避免重複觸發

        currentPhoneState = PhoneState.Idle;
        isHangingUp = false; // 重置掛斷狀態
        
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
        if (dialogueTextControlUser != null) dialogueTextControlUser.ClearText();
        if (nameText != null) nameText.text = "";
        if (choicePanel != null) choicePanel.SetActive(false);
        
        // 電話掛斷，隱藏對話框與文字
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (dialogueBoxUser != null) dialogueBoxUser.SetActive(false);
        if (dialogueTextControl != null) dialogueTextControl.gameObject.SetActive(false);
        if (dialogueTextControlUser != null) dialogueTextControlUser.gameObject.SetActive(false);
        
        // 電話掛斷，隱藏畫面上的角色
        HideAllCharacters();
        
        // 檢查限時是否已到。若已到，立即跳轉結局場景，不再排程下一通電話！
        if (isTimeUp)
        {
            isTimeUp = false; // 防重複觸發
            Debug.Log("[DialogueManager] 當下通話已結束且時間已到，正在載入結局場景 EndingScenes...");
            SceneManager.LoadScene("EndingScenes");
            return;
        }
        
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
        if (string.IsNullOrEmpty(number)) return;
        string trimmedNumber = number.Trim();

        // === 處理特別指令：輸入 "0" 開啟教學手冊 (最優先，無論在任何遊戲階段或對話狀態) ===
        if (trimmedNumber == "0")
        {
            ToggleTechBook(true);
            Debug.Log("[DialogueManager] 玩家輸入 0，開啟教學手冊，播放前 150 幀。");
            return;
        }

        // === 處理真實電話介面傳來的接聽與掛斷訊號 ===
        if (trimmedNumber == "ANSWER" && currentPhoneState == PhoneState.Ringing)
        {
            lastAnswerTime = Time.time;

            // 接聽電話，停止響鈴音效 (透過中央音效管理器)
            if (AudioManager.Instance != null) AudioManager.Instance.StopLoopingSFX();

            currentPhoneState = PhoneState.Talking;
            isInitialCallAction = false; // 預設為 false，由 PlayNode 動態判斷是否開啟決策
            isNewCallJustAnswered = true; // 標記為剛接通的電話
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
            if (ActiveTextControl != null) ActiveTextControl.gameObject.SetActive(true);

            PlayNode(callerStartNodes[currentCallerIndex]);
            return;
        }
        else if (trimmedNumber == "HANGUP" && currentPhoneState == PhoneState.Talking)
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
            isHangingUp = true;          // 標記處於掛斷流程中
            HideAllCharacters();         // 立即隱藏/漸隱角色
            
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
        if (trimmedNumber == "000" && currentPhoneState == PhoneState.Talking)
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
        if (trimmedNumber == "NEXT")
        {
            // 如果教學手冊正處於第 150 幀的暫停狀態，CALL 鍵將繼續播放手冊，並攔截對話推進
            if (bookPlayState == 2)
            {
                if (techBookDirector != null)
                {
                    techBookDirector.Play();
                    bookPlayState = 3;
                    Debug.Log("[DialogueManager] 玩家按下 CALL 鍵，繼續播放教學手冊後續幀數。");
                }
                return; // 攔截，不前進主線對話
            }

            if (ActiveTextControl != null && ActiveTextControl.IsTyping)
            {
                ActiveTextControl.SkipTypewriter();
            }
            else
            {
                if (isInitialCallAction) return; // 初始狀態不允許跳過
                if (currentNode != null && currentNode.IsChoice == 1) return;
                GoToNextNode();
            }
            return;
        }

        // === 處理初始選擇控制 (剛接通聽完第一句話時) ===
        if (isInitialCallAction)
        {
            // 確保打字結束後才允許玩家輸入選項，避免玩家提早按
            if (ActiveTextControl != null && ActiveTextControl.IsTyping) return;
            
            // 用戶希望原先的數字 1, 2, 3 功能先移除，並改為按下手機 "1" 時直接進行「繼續收聽」
            if (trimmedNumber == "1")
            {
                isInitialCallAction = false; // 接收到指令，解除等待狀態
                
                // 繼續收聽：跳轉到對應的繼續台詞 NodeID (例如 Tag 是 "Mom"，則尋找 "Mom_Continue" 或 "Mom_continue")
                string continueNodeId = callerTags[currentCallerIndex] + "_Continue";
                if (!dialogueDatabase.ContainsKey(continueNodeId))
                {
                    continueNodeId = callerTags[currentCallerIndex] + "_continue";
                }
                PlayNode(continueNodeId);
            }
            return;
        }

        // 以下為處理分支選項的邏輯 (傳來 1, 2, 3...)
        // 只有在目前是選項節點，且選項面板已經顯示時，才接受輸入
        if (currentNode == null || currentNode.IsChoice != 1) return;
        if (choicePanel != null && !choicePanel.activeSelf) return;

        string nextNodeId = "";

        if (trimmedNumber == "1" && !string.IsNullOrEmpty(currentNode.Choice1Next))
        {
            nextNodeId = currentNode.Choice1Next;
        }
        else if (trimmedNumber == "2" && !string.IsNullOrEmpty(currentNode.Choice2Next))
        {
            nextNodeId = currentNode.Choice2Next;
        }
        else if (trimmedNumber == "3" && !string.IsNullOrEmpty(currentNode.Choice3Next))
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
                AudioManager.PlaySound(target);
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

    // --- 電視噪聲控制 ---
    private void FadeOutTVNoise()
    {
        // 只有在電視播放器正在播放且啟用時，才觸發漸暗
        if (tvVideoPlayer == null || !tvVideoPlayer.gameObject.activeInHierarchy) return;

        if (tvFadeCoroutine != null) StopCoroutine(tvFadeCoroutine);
        tvFadeCoroutine = StartCoroutine(FadeOutTVNoiseRoutine());
    }

    private IEnumerator FadeOutTVNoiseRoutine()
    {
        if (tvVideoCanvasGroup != null)
        {
            float startAlpha = tvVideoCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < tvFadeDuration)
            {
                elapsed += Time.deltaTime;
                tvVideoCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / tvFadeDuration);
                yield return null;
            }

            tvVideoCanvasGroup.alpha = 0f;
        }

        if (tvVideoPlayer != null)
        {
            tvVideoPlayer.Stop();
            tvVideoPlayer.gameObject.SetActive(false);
        }

        tvFadeCoroutine = null;
    }
}
