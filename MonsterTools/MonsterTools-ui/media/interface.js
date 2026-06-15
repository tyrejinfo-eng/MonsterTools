(function () {
    const vscode = acquireVsCodeApi();
    const activeWorker = document.getElementById('active-worker');
    const activeAction = document.getElementById('active-action');
    const telemetryTps = document.getElementById('telemetry-tps');
    const telemetryTokens = document.getElementById('telemetry-tokens');
    const contextFill = document.getElementById('context-fill');
    const terminalView = document.getElementById('terminal-view');
    const consoleInput = document.getElementById('console-input');

    document.getElementById('run-btn').addEventListener('click', () => {
        const text = consoleInput.value;
        if (text) {
            pushLogLine('llm', `[PROMPT] -> ${text}`);
            vscode.postMessage({ command: 'sendPrompt', value: text });
            consoleInput.value = '';
        }
    });

    document.getElementById('undo-btn').addEventListener('click', () => {
        vscode.postMessage({ command: 'triggerRollback' });
    });

    window.addEventListener('message', event => {
        const packet = event.data;
        if (packet.command === 'engineUpdate') {
            const serverState = packet.data;
            if (serverState.ActiveWorker) activeWorker.innerText = serverState.ActiveWorker.toUpperCase();
            if (serverState.CurrentAction) activeAction.innerText = serverState.CurrentAction;
            if (serverState.TokensPerSecond) telemetryTps.innerText = `${serverState.TokensPerSecond.toFixed(1)} t/s`;
            
            if (serverState.ContextUsed && serverState.ContextMax) {
                telemetryTokens.innerText = `${serverState.ContextUsed} / ${serverState.ContextMax}`;
                contextFill.style.width = `${(serverState.ContextUsed / serverState.ContextMax) * 100}%`;
            }
            if (serverState.RawStreamChunk) {
                pushLogLine(serverState.LogType || 'worker', serverState.RawStreamChunk);
            }
        }
    });

    function pushLogLine(styleType, contentString) {
        const element = document.createElement('div');
        element.className = `line ${styleType}`;
        element.innerText = contentString;
        terminalView.appendChild(element);
        terminalView.scrollTop = terminalView.scrollHeight;
    }
}());
