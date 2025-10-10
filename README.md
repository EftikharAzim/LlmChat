# LlmChat

Provider-agnostic chat client and simple agent in C#/.NET.

- .NET 9 solution: Abstractions | Providers | Agent | Tools | Memory | UI (Console, WPF)
- Works today with Google Gemini (free tier) and a Google Calendar search tool
- Clean DI-driven composition; add new providers by implementing `IChatClient`

---

## Projects

- `LlmChat.Abstractions` – Core contracts (`IChatClient`, chat messages/requests)
- `LlmChat.Providers.Gemini` – REST client for Google Gemini API (complete + streaming)
- `LlmChat.Agent` – Simple planning + tool execution + memory wiring
- `LlmChat.Tools` – Tool abstractions and registry
- `LlmChat.Tools.Google` – Google Calendar service and search tool
- `LlmChat.Memory` – In-memory store for transcript and facts
- `LlmChat.Console` – Console chat app
- `LlmChat.Wpf` – WPF chat app with markdown rendering and typing indicator

---

## Requirements

- .NET 9 SDK
- Gemini API key (free) – https://ai.google.dev
- Optional: Google Calendar API (OAuth desktop credentials) – https://console.cloud.google.com/apis/credentials

---

## Setup

1) Restore and build

```bash
 dotnet build
```

2) Configure Gemini API key (choose one)

- Environment variable
  - macOS/Linux: `export GEMINI_API_KEY="your_api_key_here"`
  - Windows PowerShell: `setx GEMINI_API_KEY "your_api_key_here"` (restart shell)

- User Secrets (recommended in dev)
```bash
 dotnet user-secrets init --project LlmChat.Console
 dotnet user-secrets set "Providers:Gemini:ApiKey" "your_api_key_here" --project LlmChat.Console

 dotnet user-secrets init --project LlmChat.Wpf
 dotnet user-secrets set "Providers:Gemini:ApiKey" "your_api_key_here" --project LlmChat.Wpf
```

- Local appsettings (do not commit secrets)
  - Copy `LlmChat.Console/appsettings.example.json` to `appsettings.json` and fill values
  - Copy `LlmChat.Wpf/appsettings.example.json` to `appsettings.json` and fill values

3) (Optional) Configure Google Calendar

- Create OAuth desktop credentials in Google Cloud Console
- Download your OAuth client JSON as `credentials.json` and place it next to the executable (bin output) or working dir
- Or store client id/secret via user-secrets:
```bash
 dotnet user-secrets set "Google:ClientId" "your-client-id" --project LlmChat.Console
 dotnet user-secrets set "Google:ClientSecret" "your-client-secret" --project LlmChat.Console
``` 
- On first run, a browser window will open to authorize. Tokens are cached under `LlmChat.GoogleAuth/` (ignored by git)

---

## Run

Console
```bash
 dotnet run --project LlmChat.Console
```

WPF
```bash
 dotnet run --project LlmChat.Wpf
```

Console features
- Natural language chat
- Google Calendar integration (if configured)
- Clean output (warnings+ by default). Set `Logging:LogLevel:Default=Debug` to see agent internals.

WPF features
- Markdown rendering for assistant replies
- Typing indicator while the bot is preparing a response
- Hidden scrollbars with smooth mouse/touch scrolling
- Selectable, copy-friendly text

---

## Architecture & Extensibility

- Providers: implement `IChatClient` and register in DI
- Tools: implement `ITool`, register in `IToolRegistry`, and describe `InputSchemaJson`
- Agent: `IIntentRouter` plans when to call tools; `AgentRuntime` composes context, tools, and memory
- Memory: `IMemoryStore` stores transcript and facts; `InMemoryStore` is default

Add a provider
- Create a project `LlmChat.Providers.YourProvider`
- Implement `IChatClient` (CompleteAsync, StreamAsync)
- Register in Console/WPF composition root and bind options from config

Add a tool
- Implement `ITool` and return `ToolResult`
- Add to DI; `ToolRegistry` discovers all tools

---

## Configuration

`appsettings.json` or user-secrets keys of interest:

- `LlmProvider`: gemini (default)
- `Providers:Gemini:ApiKey`: your key
- `Providers:Gemini:Model`: model name (default: gemini-2.0-flash)
- `Providers:Gemini:SystemPrompt`: optional system instruction
- `Google:ClientId`, `Google:ClientSecret`: OAuth creds for Calendar

---

## Security

- Never commit API keys or OAuth client secrets
- `.gitignore` excludes `appsettings.json`, `credentials.json`, and token caches
- Prefer user-secrets for local dev

---

## Roadmap

Short-term
- Pluggable model switching in UI (`IChatClient` registry + SelectedModel)
- SQLite-backed `IMemoryStore` with conversation summaries
- Tool result formatting helpers and schema hints for planner

Long-term
- Additional providers (OpenAI-compatible, local/Ollama)
- Streaming tool calls and partial response synthesis
- MAUI sample app

---

## Contributing

PRs welcome. Please:
- Keep changes modular and DI-friendly
- Include meaningful commit messages
- Avoid logging secrets and large payloads

---

## License

MIT

