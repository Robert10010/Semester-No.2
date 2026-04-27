<?php
// ================================================================
// setup_db.php — 一次性資料庫初始化腳本
//
// 使用方式：
//   上傳此檔案到主機後，用瀏覽器開啟一次。
//   看到「資料表建立成功」後，即可刪除此檔案。
// ================================================================

// ★ 請填入與 api.php 相同的資料庫連線資訊
$db_host = "sql100.infinityfree.com"; 
$db_name = "if0_41757817_phonedb";
$db_user = "if0_41757817";      
$db_pass = "robert10010"; 

header("Content-Type: text/html; charset=UTF-8");
echo "<h2>🛠️ 資料庫初始化</h2>";

try {
    $pdo = new PDO(
        "mysql:host={$db_host};dbname={$db_name};charset=utf8",
        $db_user,
        $db_pass,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );

    // 建立 signals 資料表（若已存在則不重建）
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS signals (
            id           INT AUTO_INCREMENT PRIMARY KEY,
            phone_number VARCHAR(20) NOT NULL,
            created_at   TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8;
    ");

    echo "<p style='color:green;font-size:18px;'>✅ 資料表 <b>signals</b> 建立成功（或已存在）！</p>";
    echo "<p>你現在可以：</p>";
    echo "<ol>";
    echo "<li>刪除此 <code>setup_db.php</code> 檔案（已不需要）</li>";
    echo "<li>開啟 <code>api.php</code> 確認 API 正常運作</li>";
    echo "<li>在 Unity Inspector 填入 API 網址，開始測試</li>";
    echo "</ol>";

} catch (PDOException $e) {
    echo "<p style='color:red;font-size:18px;'>❌ 連線失敗：" . htmlspecialchars($e->getMessage()) . "</p>";
    echo "<p>請確認 api.php / setup_db.php 內的資料庫連線資訊是否正確。</p>";
}
?>
