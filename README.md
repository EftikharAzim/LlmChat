# LlmChat

A provider-agnostic chat client built in C#/.NET.
- Works today with Google Gemini API (free tier).
- Clean separation: `Abstractions` | `Providers` | `UI (Console, WPF)`.
- Easy to extend with new LLM providers via `IChatClient`.

---

## Features
- `IChatClient` abstraction keeps UIs backend-agnostic.
- Gemini provider using REST API (`/v1/models/{model}:generateContent`).
- Supports system, user, assistant roles.
- Console and WPF front-ends.

---

## Getting Started

### 1) Build
```bash
dotnet build
```

### 2) Get an API key
- Visit Google AI Studio and create a free Gemini API key.

### 3) Configure the key (choose one)

- Environment variable
  - macOS/Linux: `export GEMINI_API_KEY="your_api_key_here"`
  - Windows PowerShell: `setx GEMINI_API_KEY "your_api_key_here"` (restart shell)

- User Secrets (recommended in development)
```bash
dotnet user-secrets init --project LlmChat.Console
dotnet user-secrets set "Providers:Gemini:ApiKey" "your_api_key_here" --project LlmChat.Console

# For WPF (set from the WPF project):
dotnet user-secrets init --project LlmChat.Wpf
dotnet user-secrets set "Providers:Gemini:ApiKey" "your_api_key_here" --project LlmChat.Wpf
```

- Local config file (do not commit secrets)
  - Console example: `LlmChat.Console/appsettings.example.json`
  - WPF example: `LlmChat.Wpf/appsettings.example.json`
  - Copy to `appsettings.json` and fill values locally.

### 4) Run

- Console
```bash
dotnet run --project LlmChat.Console
```

- WPF
```bash
dotnet run --project LlmChat.Wpf
```

---

## Security
- Do not commit API keys.
- Prefer environment variables or user-secrets for development.
- Example config files are provided; copy locally and never push secrets.

---

## Extending Providers
- Implement `IChatClient` for a new provider.
- Register it in the DI container (Console/WPF) and wire up config.

---

## Roadmap
- Streaming responses.
- Additional providers (OpenAI, local models) via the provider pattern.
- More UI polish in WPF; MAUI sample.

---

## Contributing
- Fork and open a PR. Providers and UIs welcome!

