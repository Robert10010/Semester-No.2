using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineManager : MonoBehaviour
{
    // 單例模式 (Singleton)，方便其他腳本直接呼叫
    public static TimelineManager Instance { get; private set; }

    // 儲存所有子物件的 PlayableDirector
    private Dictionary<string, PlayableDirector> _directorsDict = new Dictionary<string, PlayableDirector>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 自動搜尋底下所有子物件的 PlayableDirector
        foreach (Transform child in transform)
        {
            PlayableDirector director = child.GetComponent<PlayableDirector>();
            if (director != null)
            {
                // 使用子物件的名字作為 Dictionary 的 Key
                _directorsDict.Add(child.name, director);
                
                // 預設把子物件關閉，避免 Awake 時自動播放
                child.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 播放指定的 Timeline（不等待，立即返回）
    /// </summary>
    /// <param name="timelineName">子物件的名稱</param>
    public void Play(string timelineName)
    {
        if (_directorsDict.TryGetValue(timelineName, out PlayableDirector director))
        {
            // 播放前先註冊「播放完畢」的事件
            director.stopped += OnTimelineStopped;

            // 開啟物件並播放
            director.gameObject.SetActive(true);
            director.Play();
            Debug.Log($"[TimelineManager] 開始播放: {timelineName}");
        }
        else
        {
            Debug.LogWarning($"[TimelineManager] 找不到名為 {timelineName} 的 Timeline 子物件！");
        }
    }

    /// <summary>
    /// 播放指定的 Timeline，並等待播放完畢才繼續（用在 Coroutine 中）
    /// 用法：yield return StartCoroutine(TimelineManager.Instance.PlayAndWait("fadeOut"));
    /// </summary>
    /// <param name="timelineName">子物件的名稱</param>
    public IEnumerator PlayAndWait(string timelineName)
    {
        if (_directorsDict.TryGetValue(timelineName, out PlayableDirector director))
        {
            // 開啟物件並播放
            director.gameObject.SetActive(true);
            director.Play();
            Debug.Log($"[TimelineManager] 開始播放 (等待模式): {timelineName}");

            // 每幀檢查，直到 Timeline 不再處於播放狀態
            while (director.state == PlayState.Playing)
            {
                yield return null;
            }

            // 播放結束，關閉物件
            director.gameObject.SetActive(false);
            Debug.Log($"[TimelineManager] {timelineName} 播放結束。");
        }
        else
        {
            Debug.LogWarning($"[TimelineManager] 找不到名為 {timelineName} 的 Timeline 子物件！");
        }
    }

    // 當任何一個 Timeline 播完時，會自動觸發這個 function（給 Play() 用的）
    private void OnTimelineStopped(PlayableDirector director)
    {
        // 播完了，取消事件註冊
        director.stopped -= OnTimelineStopped;

        // 把該子物件隱藏起來，省效能也避免畫面殘留問題
        director.gameObject.SetActive(false);
        Debug.Log($"[TimelineManager] {director.gameObject.name} 播放結束，已自動關閉物件。");
    }
}
