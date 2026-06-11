using UnityEngine;
using System;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem;

namespace InteractiveNovelGames.Typography.TextControl
{

    [RequireComponent(typeof(TMP_Text))]
    public class TextControl : MonoBehaviour
    {
        private TMP_Text _textBox;
        [Header("字串")]
        [SerializeField] private string Text;
        
        private int _currentVisibleCharacterIndex;
        private Coroutine _typingCoroutine;  
        
        private AudioSource _audioSource;
        private AudioClip _typingClip;
        private Coroutine _soundCoroutine;

        // 新增：讓外部判斷是否還在打字
        public bool IsTyping => _typingCoroutine != null;

        [Header("打字音效")]
        [Tooltip("打字時播放的音效名稱 (在 AudioManager 中設定的名稱)")]
        [SerializeField] private string typingSoundName = "";

        public string TypingSoundName
        {
            get => typingSoundName;
            set => typingSoundName = value;
        }

        private WaitForSeconds _simpleDeleay;
        private WaitForSeconds _interpunctuationDelay;

        [Header("每秒出現字數")]
        [SerializeField] private float charactersPerSecond = 20f;
        [SerializeField] private float interpunctuationDelay = 0.5f;

        // 標點符號集合，用於判斷停頓
        private readonly System.Collections.Generic.HashSet<char> _punctuationChars = new System.Collections.Generic.HashSet<char> { '.', ',', '!', '?', '…', ':', ';' };

        [Header("啟用時自動打字")]
        [Tooltip("如果開啟此選項，當此 UI 物件被啟用 (Active) 時，會自動將目前的文字內容套用打字機效果。適合 Timeline 的 Activation Track 啟用時使用。")]
        [SerializeField] private bool typeOnEnable = false;

        private bool _isAwakeDone = false;

        void Awake()
        {
            _textBox = GetComponent<TMP_Text>();
            _simpleDeleay = new WaitForSeconds(1/charactersPerSecond);
            _interpunctuationDelay = new WaitForSeconds(interpunctuationDelay);
            _isAwakeDone = true;
        }

        void OnEnable()
        {
            if (typeOnEnable)
            {
                // 如果 Awake 還沒跑過，先手動呼叫 Awake 初始化元件參考
                if (!_isAwakeDone)
                {
                    Awake();
                }
                
                if (_textBox != null && !string.IsNullOrEmpty(_textBox.text))
                {
                    // 呼叫 SetText 開始打字機效果
                    SetText(_textBox.text);
                }
            }
        }
        
        public void SetText(string Text)
        {
            // 防護：如果 Awake() 還沒跑過（物件之前是停用的），先手動初始化
            if (_textBox == null)
            {
                _textBox = GetComponent<TMP_Text>();
                _simpleDeleay = new WaitForSeconds(1 / charactersPerSecond);
                _interpunctuationDelay = new WaitForSeconds(interpunctuationDelay);
            }

            if(_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);

            if (_soundCoroutine != null)
            {
                StopCoroutine(_soundCoroutine);
                _soundCoroutine = null;
            }

            if (_audioSource != null)
            {
                _audioSource.Stop();
            }

            _textBox.richText = true; // 強制開啟富文本支援，確保 <color> 等標籤能正確渲染
            _textBox.text = Text;
            _textBox.ForceMeshUpdate(); // 強制更新 TextMeshPro 資訊，否則 characterCount 會抓到上一句的長度
            _textBox.maxVisibleCharacters = 0;

            _currentVisibleCharacterIndex = 0;
            
            _typingCoroutine = StartCoroutine(TyperCoroutine());

            // 啟動音效播放協程
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }
            if (!string.IsNullOrEmpty(typingSoundName) && AudioManager.Instance != null)
            {
                _typingClip = AudioManager.Instance.GetSFXClip(typingSoundName);
                if (_typingClip != null)
                {
                    _soundCoroutine = StartCoroutine(PlayTypingSoundsRoutine());
                }
            }
        }

        // 新增：讓外部呼叫跳過打字機
        public void SkipTypewriter()
        {
            if (_textBox == null)
            {
                _textBox = GetComponent<TMP_Text>();
            }

            if(_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
                if (_textBox != null)
                {
                    _textBox.maxVisibleCharacters = _textBox.textInfo.characterCount;
                }
            }

            if (_soundCoroutine != null)
            {
                StopCoroutine(_soundCoroutine);
                _soundCoroutine = null;
            }
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
        }

        /// <summary>
        /// 停止打字並清除文字內容
        /// </summary>
        public void ClearText()
        {
            if (_textBox == null)
            {
                _textBox = GetComponent<TMP_Text>();
            }

            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
            }

            if (_soundCoroutine != null)
            {
                StopCoroutine(_soundCoroutine);
                _soundCoroutine = null;
            }
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }

            if (_textBox != null)
            {
                _textBox.text = string.Empty;
            }
        }

        private IEnumerator TyperCoroutine()
        {
            TMP_TextInfo textInfo = _textBox.textInfo;
            
            while(_currentVisibleCharacterIndex < textInfo.characterCount)
            {
                // 取得當前字元，注意 characterInfo 只包含可見字元
                char currentChar = textInfo.characterInfo[_currentVisibleCharacterIndex].character; 
                
                // 顯示下一個字
                _textBox.maxVisibleCharacters++;
                _currentVisibleCharacterIndex++;

                // 根據字元是否為標點符號，決定等待時間
                if (_punctuationChars.Contains(currentChar))
                {
                    // 檢查下一個字元是否也是標點（連續標點如 ...）
                    // 如果是連續標點，只用普通速度顯示，在最後一個標點才停頓
                    bool nextIsPunctuation = false;
                    if (_currentVisibleCharacterIndex < textInfo.characterCount)
                    {
                        char nextChar = textInfo.characterInfo[_currentVisibleCharacterIndex].character;
                        nextIsPunctuation = _punctuationChars.Contains(nextChar);
                    }

                    if (nextIsPunctuation)
                        yield return _simpleDeleay;     // 連續標點中間：用普通速度
                    else
                        yield return _interpunctuationDelay; // 最後一個標點：正常停頓
                }
                else
                {
                    yield return _simpleDeleay;
                }
            }
            
            // 打字結束，將協程設為 null
            _typingCoroutine = null;
        }

        private IEnumerator PlayTypingSoundsRoutine()
        {
            if (_audioSource == null || _typingClip == null) yield break;

            _audioSource.clip = _typingClip;
            _audioSource.loop = false; // 一個一個播放

            while (IsTyping)
            {
                if (!_audioSource.isPlaying)
                {
                    _audioSource.Play();
                }
                yield return null;
            }

            _audioSource.Stop();
        }
    }
}