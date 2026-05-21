using UnityEngine;
using System;
using System.Collections;
using UnityEngine;
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
        
        // 新增：讓外部判斷是否還在打字
        public bool IsTyping => _typingCoroutine != null;

        private WaitForSeconds _simpleDeleay;
        private WaitForSeconds _interpunctuationDelay;

        [Header("每秒出現字數")]
        [SerializeField] private float charactersPerSecond = 20f;
        [SerializeField] private float interpunctuationDelay = 0.5f;

        // 標點符號集合，用於判斷停頓
        private readonly System.Collections.Generic.HashSet<char> _punctuationChars = new System.Collections.Generic.HashSet<char> { '.', ',', '!', '?', '…', ':', ';' };

        void Awake()
        {
            _textBox = GetComponent<TMP_Text>();
            _simpleDeleay = new WaitForSeconds(1/charactersPerSecond);
            _interpunctuationDelay = new WaitForSeconds(interpunctuationDelay);
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

            _textBox.text = Text;
            _textBox.ForceMeshUpdate(); // 強制更新 TextMeshPro 資訊，否則 characterCount 會抓到上一句的長度
            _textBox.maxVisibleCharacters = 0;

            _currentVisibleCharacterIndex = 0;
            
            _typingCoroutine = StartCoroutine(TyperCoroutine());
        }

        // 新增：讓外部呼叫跳過打字機
        public void SkipTypewriter()
        {
            if(_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
                _textBox.maxVisibleCharacters = _textBox.textInfo.characterCount;
            }
        }

        /// <summary>
        /// 停止打字並清除文字內容
        /// </summary>
        public void ClearText()
        {
            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
            }

            _textBox.text = string.Empty;
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
                    yield return _interpunctuationDelay;
                else
                    yield return _simpleDeleay;
            }
            
            // 打字結束，將協程設為 null
            _typingCoroutine = null;
        }
    }
}