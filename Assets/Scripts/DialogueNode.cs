using System.Collections.Generic;

[System.Serializable]
public class DialogueNode
{
    public string Tag;          //是否是選項
    public string NodeID;       // 節點ID (用來查找對話，例如: START, node_01)
    public string Character;    // 人物名稱 (例如: 主角, 神秘人)
    public string EmotionTag;   // 人物標籤 (例如: Smile, Angry -> 用來切換圖片)
    public string Position;     // 位置 (例如: Left, Right, Center)
    public string TextContent;  // 文本內容
    public string NextID;       // 跳轉順序 (下一句話的 NodeID)
    public string EffectType;   // 效果類型 (例如: PlaySound, Shake, FadeIn)
    public string EffectTarget; // 效果目標 (例如: BGM_01, 攝影機)

    // 分支選項相關 (新增)
    public int IsChoice;        // 是否為選項節點 (1為選項, 0為一般)
    public string Choice1Text;  // 選項1文字 (對應按鍵 1)
    public string Choice1Next;  // 選項1跳轉ID
    public string Choice2Text;  // 選項2文字 (對應按鍵 2)
    public string Choice2Next;  // 選項2跳轉ID
    public string Choice3Text;  // 選項3文字 (對應按鍵 3)
    public string Choice3Next;  // 選項3跳轉ID
}
