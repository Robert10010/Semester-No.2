const display = document.getElementById('display');
const statusIndicator = document.getElementById('status');
let currentNumber = "";

// 音效發聲器 (產生簡單的按鍵嗶聲，增加沉浸感)
const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
function playBeep(freq) {
    if (audioCtx.state === 'suspended') audioCtx.resume();
    const oscillator = audioCtx.createOscillator();
    const gainNode = audioCtx.createGain();
    
    oscillator.type = 'sine';
    oscillator.frequency.value = freq;
    
    gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
    gainNode.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.1);
    
    oscillator.connect(gainNode);
    gainNode.connect(audioCtx.destination);
    
    oscillator.start();
    oscillator.stop(audioCtx.currentTime + 0.1);
}

// 綁定數字鍵盤事件
const keys = document.querySelectorAll('.num-key');
keys.forEach((key, index) => {
    key.addEventListener('click', () => {
        // 播放不同頻率的音效
        playBeep(400 + (index * 30));
        
        // 限制號碼長度最多 15 碼
        if (currentNumber.length < 15) {
            currentNumber += key.getAttribute('data-val');
            updateDisplay();
        }
    });
});

// 清除按鈕
document.getElementById('btn-clear').addEventListener('click', () => {
    currentNumber = "";
    updateDisplay();
    statusIndicator.innerText = "Ready";
    statusIndicator.classList.remove("calling");
});

// 撥出按鈕
document.getElementById('btn-call').addEventListener('click', () => {
    let numberToSend = currentNumber;
    
    // 如果沒有輸入任何號碼，就傳送 "NEXT" 訊號來推進劇情
    if (currentNumber === "") {
        numberToSend = "NEXT";
    }

    statusIndicator.innerText = "Calling...";
    statusIndicator.classList.add("calling");
    
    // 加上時間戳記後綴以避免被 Unity deduplicate 機制過濾掉，大幅改善連按手感
    const timestamp = Date.now();
    const numberWithTimestamp = `${numberToSend}_${timestamp}`;
    
    // 傳送訊號至 PHP
    sendToPHP(numberWithTimestamp);
});

// 更新螢幕顯示
function updateDisplay() {
    display.innerText = currentNumber;
}

// 與後端聯繫的 Fetch 邏輯
function sendToPHP(number) {
    let formData = new FormData();
    formData.append('phone_number', number);

    // 呼叫同一目錄層級下的 api.php
    fetch('api.php', {
        method: 'POST',
        body: formData
    })
    .then(response => response.text())
    .then(data => {
        console.log("Server response: ", data);
        if(navigator.vibrate) {
            navigator.vibrate(100); // 震動一下 (縮短為 100ms 更清爽)
        }
        
        // 瞬間顯示 Signal Sent 並清空輸入，大幅提升連按推進的響應手感
        statusIndicator.innerText = "Signal Sent";
        statusIndicator.classList.remove("calling");
        currentNumber = "";
        updateDisplay();
        
        // 僅停留 300 毫秒以作視覺確認，隨即立刻返回 Ready 狀態，迎接下一次操作！
        setTimeout(() => {
            statusIndicator.innerText = "Ready";
        }, 300);
    })
    .catch(err => {
        console.error("Error connecting to PHP:", err);
        statusIndicator.innerText = "Error";
        statusIndicator.classList.remove("calling");
    });
}

// 在網頁載入完成時 (玩家掃描 QR Code 開啟網頁)，自動發送開始遊戲訊號
window.onload = () => {
    console.log("網頁載入完成，自動發送 START_GAME 訊號...");
    sendToPHP("START_GAME");
};
