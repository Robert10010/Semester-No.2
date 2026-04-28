using UnityEngine;
using System;
using System.Collections;
using UnityEngine;
using TMPro;

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

        private WaitForSeconds _simpleDeleay;
        private WaitForSeconds _interpunctuationDelay;

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
        }
        void Start()
        {
            SetText(Text);
        }
        public void SetText(string Text)
        {
            if(_typingCoroutine != null)
                StopCoroutine(_typingCoroutine);

            _textBox.text = Text;
            _textBox.maxVisibleCharacters = 0;

            _currentVisibleCharacterIndex = 0;
            
            _typingCoroutine = StartCoroutine(TyperCoroutine());
        }
        private IEnumerator TyperCoroutine()
        {
            TMP_TextInfo textInfo = _textBox.textInfo;
            
            while(_currentVisibleCharacterIndex < textInfo.characterCount + 1)
            {
                char currentChar = textInfo.characterInfo[_currentVisibleCharacterIndex].character;
                _textBox.maxVisibleCharacters++;
                yield return _simpleDeleay;
            }
        }
    }
}