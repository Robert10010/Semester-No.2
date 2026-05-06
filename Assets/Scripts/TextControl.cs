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
        private WaitForSeconds _skipdelay;

        [Header("每秒出現字數")]
        [SerializeField] private float charactersPerSecond = 20f;
        [SerializeField] private float interpunctuationDelay = 0.5f;
        [Header("跳過設定")]
        [SerializeField] private bool Skip;
        [SerializeField] [Min(1)] private int skipSpeedUp = 5;

        void Awake()
        {
            _textBox = GetComponent<TMP_Text>();

            _simpleDeleay = new WaitForSeconds(1/charactersPerSecond);
            _interpunctuationDelay = new WaitForSeconds(interpunctuationDelay);
            _skipdelay = new WaitForSeconds(1/charactersPerSecond * skipSpeedUp);
        }
        // 移除了 Start()，因為現在由 DialogueManager 主動呼叫 SetText
        private void Update()
        {
            bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if(Skip && spacePressed)
            {
                if(_typingCoroutine != null)
                {
                    StopCoroutine(_typingCoroutine);
                    _textBox.maxVisibleCharacters = _textBox.textInfo.characterCount;
                }
            }
        }
        public void SetText(string Text)
        {
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
        private IEnumerator TyperCoroutine()
        {
            TMP_TextInfo textInfo = _textBox.textInfo;
            
            while(_currentVisibleCharacterIndex < textInfo.characterCount)
            {
                char currentChar = textInfo.characterInfo[_currentVisibleCharacterIndex].character;
                _textBox.maxVisibleCharacters++;
                _currentVisibleCharacterIndex++; // 加上這行，否則會變成無窮迴圈！
                yield return _simpleDeleay;
            }
            
            // 打字結束，將協程設為 null
            _typingCoroutine = null;
        }
    }
}