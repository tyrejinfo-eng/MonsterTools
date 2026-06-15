import * as vscode from 'vscode';
import * as WebSocket from 'ws';

export class MonsterSidebarProvider implements vscode.WebviewViewProvider {
    private _view?: vscode.WebviewView;
    private _wss?: WebSocket.Server;
    private _activeClientSocket?: WebSocket;

    constructor(private readonly _extensionUri: vscode.Uri) {
        this.initializeNativeBridge();
    }

    private initializeNativeBridge() {
        // High-speed local loopback connection channel for standard JSON-RPC data packets
        this._wss = new WebSocket.Server({ port: 8095 });
        this._wss.on('connection', (ws) => {
            this._activeClientSocket = ws;
            
            ws.on('message', (message: string) => {
                if (this._view) {
                    try {
                        const jsonPayload = JSON.parse(message);
                        // Send the telemetry payload directly to the frontend HTML View panel
                        this._view.webview.postMessage({ command: 'engineUpdate', data: jsonPayload });
                    } catch (e) {
                        console.error("Payload corruption error tracking C# pipeline data:", e);
                    }
                }
            });
        });
    }

    public resolveWebviewView(webviewView: vscode.WebviewView, context: vscode.WebviewViewResolveContext, _token: vscode.CancellationToken) {
        this._view = webviewView;

        webviewView.webview.options = {
            enableScripts: true,
            localResourceRoots: [this._extensionUri]
        };

        webviewView.webview.html = this._generateHtmlLayout(webviewView.webview);

        // Catch intent buttons triggered from inside the sidebar panel markup
        webviewView.webview.onDidReceiveMessage(message => {
            if (this._activeClientSocket && this._activeClientSocket.readyState === WebSocket.OPEN) {
                switch (message.command) {
                    case 'sendPrompt':
                        this._activeClientSocket.send(JSON.stringify({ event: 'userPrompt', text: message.value }));
                        break;
                    case 'triggerRollback':
                        this._activeClientSocket.send(JSON.stringify({ event: 'systemRollback' }));
                        break;
                }
            }
        });
    }

    private _generateHtmlLayout(webview: vscode.WebviewView['webview']) {
        const cssUri = webview.asWebviewResourceUri(vscode.Uri.joinPath(this._extensionUri, 'media', 'interface.css'));
        const jsUri = webview.asWebviewResourceUri(vscode.Uri.joinPath(this._extensionUri, 'media', 'interface.js'));

        return `<!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <link href="${cssUri}" rel="stylesheet">
        </head>
        <body>
            <div class="matrix-layout">
                <h3>MONSTERTOOLS SYSTEM MATRIX</h3>
                
                <!-- Section 1: Active Execution State -->
                <div class="matrix-card">
                    <div class="card-header">NATIVE WORKER DISPATCHER</div>
                    <div class="badge-status" id="active-worker">IDLE</div>
                    <div class="subtext-status" id="active-action">Awaiting local task loop...</div>
                </div>

                <!-- Section 2: Real-time Telemetry Data -->
                <div class="matrix-card">
                    <div class="card-header">INFERENCE ENGINE DIAGNOSTICS</div>
                    <div class="data-row"><span>Tokens/Sec:</span><span class="val-highlight" id="telemetry-tps">0.0 t/s</span></div>
                    <div class="data-row"><span>Context Map:</span><span class="val-highlight" id="telemetry-tokens">0 / 0</span></div>
                    <div class="meter-track"><div class="meter-fill" id="context-fill" style="width: 0%"></div></div>
                </div>

                <!-- Section 3: Live Parse/Write Terminal Stream -->
                <div class="matrix-card">
                    <div class="card-header">ORCHESTRATION EVENT TRAILING LOG</div>
                    <div class="terminal-view" id="terminal-view">
                        <div class="line sys">[CORE] Pipeline initialized. Gateway to workspace locked.</div>
                    </div>
                </div>

                <!-- Section 4: Workspace Control Input console -->
                <div class="console-box">
                    <textarea id="console-input" placeholder="Inject local reasoning instruction..."></textarea>
                    <button class="action-btn execution" id="run-btn">Execute Local Run</button>
                    <button class="action-btn emergency" id="undo-btn">Git Hard Rollback</button>
                </div>
            </div>
            <script src="${jsUri}"></script>
        </body>
        </html>`;
    }
}
