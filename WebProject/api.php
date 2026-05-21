<?php
// ================================================================
// api.php — 手機前端與 Unity 的中繼站（MySQL PDO 版本）
// 部署至虛擬主機後，請填寫下方資料庫連線設定
// ================================================================

// ----------------------------------------------------------------
// ★ 資料庫連線設定（XAMPP 本機模式）
// ----------------------------------------------------------------
$db_host = "localhost";
$db_name = "phone_control";
$db_user = "root";
$db_pass = "";    // XAMPP 預設無密碼

// ----------------------------------------------------------------
// CORS 設定（允許 Unity 跨域讀取）
// ----------------------------------------------------------------
header("Access-Control-Allow-Origin: *");
header("Access-Control-Allow-Methods: GET, POST");
header("Content-Type: text/plain; charset=UTF-8");

// ----------------------------------------------------------------
// 建立 PDO 連線
// ----------------------------------------------------------------
try {
    $pdo = new PDO(
        "mysql:host={$db_host};dbname={$db_name};charset=utf8",
        $db_user,
        $db_pass,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
} catch (PDOException $e) {
    http_response_code(500);
    echo "DB_ERROR: " . $e->getMessage();
    exit;
}

// ================================================================
// 1. 接收來自手機前端的號碼（POST: phone_number）
// ================================================================
if (isset($_POST['phone_number'])) {
    $number = trim($_POST['phone_number']);

    if (empty($number)) {
        echo "Error: Empty number.";
        exit;
    }

    $stmt = $pdo->prepare("INSERT INTO signals (phone_number) VALUES (?)");
    $stmt->execute([$number]);
    echo "Success: Number saved.";
    exit;
}

// ================================================================
// 2. 供 Unity 讀取最新號碼（GET: ?action=read）
// ================================================================
if (isset($_GET['action']) && $_GET['action'] === 'read') {
    $stmt = $pdo->query("SELECT id, phone_number FROM signals ORDER BY id ASC LIMIT 1");
    $row = $stmt->fetch(PDO::FETCH_ASSOC);

    if ($row) {
        echo $row['phone_number'];
    } else {
        echo ""; // 無資料時回傳空字串（Unity 忽略空字串）
    }
    exit;
}

// ================================================================
// 3. 供 Unity 讀取完畢後清除紀錄（POST: action=clear）
//    只刪最舊一筆，避免同時多封訊號遺失
// ================================================================
if (isset($_POST['action']) && $_POST['action'] === 'clear') {
    $pdo->exec("DELETE FROM signals ORDER BY id ASC LIMIT 1");
    echo "Cleared";
    exit;
}

// ================================================================
// 預設說明頁（直接瀏覽器開啟時顯示）
// ================================================================
header("Content-Type: text/html; charset=UTF-8");
echo "<h2>📞 Phone API is running.</h2>";
echo "<ul>";
echo "<li><b>POST</b> <code>phone_number=XXXXXX</code> → 手機撥號</li>";
echo "<li><b>GET</b> <code>?action=read</code> → Unity 讀取號碼</li>";
echo "<li><b>POST</b> <code>action=clear</code> → Unity 讀取後清除</li>";
echo "</ul>";
?>
