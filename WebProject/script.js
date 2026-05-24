document.addEventListener('DOMContentLoaded', () => {
    const display = document.getElementById('display');
    const status = document.getElementById('status');
    const numKeys = document.querySelectorAll('.num-key');
    const btnClear = document.getElementById('btn-clear');
    const btnCall = document.getElementById('btn-call');

    let currentNumber = "";
    let isRinging = false;
    let ringInterval = null;

    // 建立語音響鈴音效保底 (利用 Web Audio API 合成音效，避免缺少音訊檔播放失敗)
    let audioCtx = null;
    function playBuzzer(frequency, duration) {
        try {
            if (!audioCtx) {
                audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            }
            if (audioCtx.state === 'suspended') {
                audioCtx.resume();
            }
            const oscillator = audioCtx.createOscillator();
            const gainNode = audioCtx.createGain();
            oscillator.type = 'sine';
            oscillator.frequency.value = frequency;
            gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + duration);
            oscillator.connect(gainNode);
            gainNode.connect(audioCtx.destination);
            oscillator.start();
            oscillator.stop(audioCtx.currentTime + duration);
        } catch (e) {
            console.log("AudioContext blocked or not supported:", e);
        }
    }

    // 1. 數字按鈕點擊事件
    numKeys.forEach(key => {
        key.addEventListener('click', () => {
            const val = key.getAttribute('data-val');
            playBuzzer(600, 0.08); // 按鍵按下去的嗶聲
            
            // 限制最大撥號長度 (例如 10 碼)
            if (currentNumber.length < 10) {
                currentNumber += val;
                updateDisplay();
            }
        });
    });

    // 2. 清除按鈕點擊事件
    btnClear.addEventListener('click', () => {
        playBuzzer(400, 0.1);
        currentNumber = "";
        updateDisplay();
    });

    // 3. 發送 (Call) 按鈕點擊事件
    btnCall.addEventListener('click', () => {
        playBuzzer(800, 0.15);
        
        // 若輸入為空，預設發送 "NEXT" 用於推進劇情；否則發送撥打的數字
        const numberToSend = currentNumber.trim() === "" ? "NEXT" : currentNumber.trim();
        
        status.innerText = "Sending...";
        
        // 發送 POST 請求給相對路徑的 api.php (100% 避免網域跨域或域名不對的問題)
        fetch('./api.php', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `phone_number=${encodeURIComponent(numberToSend)}`
        })
        .then(response => response.text())
        .then(data => {
            console.log("Success:", data);
            status.innerText = "Success";
            setTimeout(() => {
                status.innerText = isRinging ? "Ringing..." : "Ready";
            }, 1000);
            
            // 發送成功後清空螢幕
            currentNumber = "";
            updateDisplay();
        })
        .catch((error) => {
            console.error("Error sending number:", error);
            status.innerText = "Failed";
            setTimeout(() => {
                status.innerText = isRinging ? "Ringing..." : "Ready";
            }, 1500);
        });
    });

    function updateDisplay() {
        display.innerText = currentNumber;
    }

    // 4. 定時輪詢接收來自 Unity 的指令 (每 1 秒一次)
    setInterval(() => {
        fetch('./api.php?action=read_to_phone')
        .then(response => response.text())
        .then(signal => {
            const trimmedSignal = signal.trim();
            if (trimmedSignal === "RING") {
                if (!isRinging) {
                    isRinging = true;
                    status.innerText = "Ringing...";
                    status.classList.add('ringing-active');
                    startPhoneRingSFX();
                }
            } else if (trimmedSignal === "HANGUP" || trimmedSignal === "STOP_RING") {
                isRinging = false;
                status.innerText = "Ready";
                status.classList.remove('ringing-active');
                stopPhoneRingSFX();
            }
        })
        .catch(err => console.log("Error polling Unity signals:", err));
    }, 1000);

    // 模擬手機來電鈴聲音效
    function startPhoneRingSFX() {
        if (ringInterval) clearInterval(ringInterval);
        
        // 模擬傳統電話鈴聲：每 2 秒響兩聲
        ringInterval = setInterval(() => {
            if (!isRinging) return;
            playBuzzer(440, 0.4);
            setTimeout(() => {
                if (isRinging) playBuzzer(440, 0.4);
            }, 500);
        }, 2000);
    }

    function stopPhoneRingSFX() {
        if (ringInterval) {
            clearInterval(ringInterval);
            ringInterval = null;
        }
    }
});
