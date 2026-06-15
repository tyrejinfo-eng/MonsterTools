import * as vscode from 'vscode';
import { execFile, ChildProcess } from 'child_process';
import * as path from 'path';
import { MonsterSidebarProvider } from './MonsterSidebarProvider';

let nativeEngineProcess: ChildProcess | null = null;

export function activate(context: vscode.ExtensionContext) {
    const sidebarProvider = new MonsterSidebarProvider(context.extensionUri);

    context.subscriptions.push(
        vscode.window.registerWebviewViewProvider(
            "monstertools.sidebarView",
            sidebarProvider
        )
    );

    // Dynamic execution targeting your compiled C# executable build directory
    const engineBinary = path.join(context.extensionPath, '..', 'MonsterTools', 'bin', 'Debug', 'net8.0', 'MonsterTools.exe');
    const activeWorkspace = vscode.workspace.workspaceFolders?.?.uri.fsPath || "";

    if (activeWorkspace) {
        try {
            nativeEngineProcess = execFile(engineBinary, [`--workspace=${activeWorkspace}`], (error) => {
                if (error) console.error(`C# Engine Process Exit Signal: ${error}`);
            });
            console.log(`MonsterTools C# Core Engine bound to workspace: ${activeWorkspace}`);
        } catch (err) {
            console.error("Failed to spin up native C# background daemon:", err);
        }
    }
}

export function deactivate() {
    if (nativeEngineProcess) {
        nativeEngineProcess.kill();
    }
}
