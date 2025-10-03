# LlmChat

A provider-agnostic chat client built in C#/.NET.  
- Works today with **Google Gemini API (free tier)**.  
- Designed so you can add other providers later without touching the UI.  
- Current UI: simple **.NET Console App**. Replaceable with **WPF, MAUI, or any other UI**.

---

## ✨ Features
- `IChatClient` abstraction → UI is backend-agnostic.  
- Gemini provider using REST API (`/v1/models/{model}:generateContent`).  
- Supports **system, user, assistant roles**.  
- Clean architecture: `Abstractions` | `Providers` | `UI`.

---

## 🚀 Getting Started

### 1. Clone & Build
```bash
git clone https://github.com/yourusername/LlmChat.git
cd LlmChat
dotnet build
```

### 2. Get an API Key
- Go to [Google AI Studio](https://studio.google.ai/).
- Sign in with a Google account.
- Create a free Gemini API key (you’ll stay in free tier unless you attach billing).

### 3. Set Environment Variable

**macOS / Linux**

```bash
export GEMINI_API_KEY="your_api_key_here"
```

To make it permanent, add the line above to your `~/.zshrc` or `~/.bashrc`.

**Windows (PowerShell)**

```powershell
setx GEMINI_API_KEY "your_api_key_here"
```

Restart your shell so it takes effect.

**Verify:**

- On macOS/Linux:
```bash
echo $GEMINI_API_KEY
```

- On Windows PowerShell:
```powershell
echo $Env:GEMINI_API_KEY
```

### 4. Run the Console App

```bash
dotnet run --project LlmChat.Console
```

You’ll see:

```
LLM Chat (Gemini). Type '/exit' to quit.

You > Hello
Bot > Hi there! 👋
```

Alternative: appsettings or user-secrets

You can also put the key in `LlmChat.Console/appsettings.json` under `Providers:Gemini:ApiKey` for local development, or use `dotnet user-secrets`:

```bash
dotnet user-secrets init --project LlmChat.Console
dotnet user-secrets set "Providers:Gemini:ApiKey" "your_api_key_here" --project LlmChat.Console
```

---

## ⚠️ Cost & Free Tier

- The Gemini API free tier is safe to use without incurring charges.
- Free tier limits include quotas on tokens per day and requests per minute.
- If you exceed these limits or enable billing, charges may apply.
- You can monitor your usage and billing status in the Google AI Studio dashboard.
- **Rule of thumb:** If you never enable billing, you cannot be charged.

---

## 🔄 Swapping Providers

This project is designed to support multiple providers:

- `GeminiChatClient` → uses Google Gemini.
- You can add additional providers by implementing the `IChatClient` interface.
- To add a new provider, implement `IChatClient` methods for sending and receiving messages according to your provider's API.
- The UI remains unchanged — only the factory or service registration that builds the client needs to be updated.

---

## 📦 Roadmap
- Streaming responses (SSE).
- (Optional) Additional providers such as OpenAI or local models can be added in future by following the provider pattern.
- Local ONNX provider.
- WPF / MAUI front-ends.

---

## 🤝 Contributing
- Fork the repo.
- Add your provider or UI layer.
- PRs welcome!

---

## 🔑 Security

Never commit your API key to the repo.  
- Keep it in environment variables.  
- Add any `appsettings.*.json` or `.env` files to `.gitignore`.  
- Each user must provide their own key.

**Why?** Committing API keys publicly can lead to unauthorized usage and potential charges or data breaches. Keeping keys out of source control is a best practice for security.

---

## 📜 License

MIT (or whichever you prefer)

---

This way:  
- Your **own repo is clean** (no secrets).  
- Any **3rd-party user** can just clone, set their own `GEMINI_API_KEY`, and run.  
- If you want additional providers later, you can plug them in by implementing `IChatClient` and updating the factory/DI registration.

