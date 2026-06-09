
A deterministic execution layer that augments
small local coding models by converting
high-cost reasoning tasks into reliable tool operations.

(User uses VS Code Gituhub Copilot Interface - LM Studio(ibm/granite-4-h-tiny), LM Studio LLM is set up as a custom endpoint in VS Code Github Copilot)
MCP Service Needs to be compleated and needs to be correctly wired to LM studio so that it is functional.
Will upgrade Workers once i have working pipeline.


\MonsterTools 
  Program.cs
  MonsterTools.csproj
  MonsterMcpServer.cs
\Core
  AgentLoop.cs
  IToolWorker.cs
  ToolCall.cs
  ToolExecutor.cs
  ToolRequest.cs
  ToolResult.cs
  ToolRouter.cs
  ToolSchemas.cs
  ToolWorkerBase.cs
  ToolArgumentNormalizer.cs
  ToolValidator.cs
\Services
 LlmClient.cs
 LMStudioService.cs
 WorkerDispatcher.cs
\Workers
 BuildWorker.cs
 FileSystemWorkers.cs
 FileWorkers.cs
 Searchworkers.cs
 ValidationWorkers.c
 WorkspaceWorker.cs
\obj
  \Debug
  MonsterTools.csproj.nuget.dgspec.json
  MonsterTools.csproj.nuget.g.props
  MonsterTools.csproj.nuget.g.targets
  project.assets.json
  project.nuget.cache



Program.cs
   ↓
McpServer.Run()
   ↓
Deserialize MCP request
   ↓
WorkerDispatcher
   ↓
ToolExecutor
   ↓
SearchWorker
   ↓
JSON response back




 Search Tab:Qwen2.5-Coder-7B-Instruct-GGUF: Best overall balance for multi-language logic and structural explanations.CodeGemma-7B-GGUF: Created by Google, excellent for strict Java and Python tasks.DeepSeek-Coder-6.7B-Instruct-GGUF: Highly optimized for low-resource autocomplete capabilities.Are you looking to use this setup for inline ghost-text completions as you type, or primarily for side-panel chat debugging? I can tailor the configuration file rules for either requirement.





ibm/granite-4-h-tiny model is an incredibly fast, lightweight LLM, making it an excellent choice for immediate inline autocomplete inside VS Code. However, because GitHub Copilot natively formats prompts assuming a massive, cloud-based GPT model, routing a tiny local model like Granite through it often leads to broken or erratic completions.The most common reasons for these issues—and how an open-source proxy clone fixes them—explain how you can expand on this setup.Why Local Models Fail Natively in CopilotWhen VS Code's GitHub Copilot extension requests a code completion, it sends a highly complex JSON payload containing hidden instruction structures, strict formatting tokens, and multi-file code fragments.Prompt Bloat: Cloud models easily process thousands of prompt tokens, but a tiny local model like granite-4-h-tiny can quickly run out of context length, causing it to truncate mid-response.Token Deserialization: Tiny models struggle with the nested Markdown system messages that Copilot automatically appends, often resulting in them outputting conversational filler (e.g., "Sure, here is your code:") instead of pure code.The Solution: An Open-Source Proxy MiddlemanInstead of routing Copilot directly to LM Studio, you can place a lightweight, open-source custom server between them. This proxy acts as a translation layer: it intercepts Copilot's heavy payloads, cleans out cloud-specific bloat, formats the code snippet into the exact prompt structure your model needs, and hands it off to LM Studio.An excellent, highly customizable repository to build upon is the open-source ⁠hankchiutw/copilot-proxy on GitHub. While it was originally written to intercept Copilot cloud tokens for external apps, its underlying HTTP request-handling structure makes it the perfect codebase to fork and modify for a local-first system.[ VS Code Copilot ] ──(Complex Cloud Payload)──> [ Your Forked Proxy Server ]
                                                           │
                                                (Regex / Prompt Cleanup)
                                                           ▼
[ LM Studio Server ] <──(Clean Granite Prompt)─────────────┘
