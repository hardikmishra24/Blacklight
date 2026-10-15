# TipShield 
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/)
[![Python](https://img.shields.io/badge/Python-3.11+-yellow.svg)](https://www.python.org/)
[![FastAPI](https://img.shields.io/badge/FastAPI-0.110+-009688.svg)](https://fastapi.tiangolo.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Interactive%20Server-512BD4.svg)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg)](https://www.docker.com/)
> [!IMPORTANT]
> **Educational risk signals only. Not financial advice. A clean result does not mean a tip is safe.**  
> TipShield is an automated educational tool designed to detect scam and pump-and-dump patterns in unverified stock tips. It never predicts stock prices, never gives buy/sell advice, and never accuses named individuals.
---
## 🎥 Video Demo & Walkthrough
 
https://github.com/user-attachments/assets/a4625dfa-ab7a-4d50-b08b-6828cb1d844d


---
##  Overview
Retail investors in India face rampant financial cyber-fraud, particularly pump-and-dump schemes, unregistered advisory rackets, and fraudulent group funnels proliferating on **WhatsApp** and **Telegram**.
These fraudulent tips commonly use:
- **Guaranteed returns:** *"Double your money in 7 days guaranteed!"*
- **Fake authority & insider claims:** *"Operator action in YESBANK tonight"*, forged SEBI certificates.
- **Urgency & FOMO:** *"Only 50 seats left! Join VIP group now for ₹5,000."*
- **Profit screenshots:** Digitally fabricated Demat statements to lure beginners.
**TipShield** provides an open-source, privacy-first, automated multi-layered risk analysis engine that screens text, screenshots, or both across **English**, **Hindi (Devanagari)**, and **Hinglish (romanized Hindi)**.
---
##  Key Features
- ** Transparent Risk Score (0–100) & Verdict:**
  - `likely_scam` (65–100) — High risk with multiple scam signals.
  - `suspicious` (35–64) — Warning indicators present; requires extreme caution.
  - `no_obvious_red_flags` (0–34) — Educational/neutral content without blatant manipulation markers.
- ** Verifiable Red Flags:** Quotes exact suspicious phrases verbatim from the message along with plain-language explanations.
- ** Real-Time Market Reality Check:** Connects to live exchange feeds (NSE/BSE via Yahoo Finance) to detect abnormal volume spikes ($>3\times$), market cap reality checks ($<\text{₹}500\text{ Cr}$ small-cap pumps), and recent price swings. *Unlisted or invalid symbols are automatically filtered out.*
- ** Lookalike Broker & Domain Verification:** Checks sender URLs against homoglyph / typo-squatting lists (e.g., lookalikes of Zerodha, Groww, Upstox, SEBI) with WHOIS registration age checks.
- ** "Forward to Parents" Safety Card:** Generates easy-to-read, WhatsApp-shareable warning summaries in English and Hindi.
- ** Next-Steps Checklist:** Direct guidance with links to the official SEBI intermediary search portal and India's Cyber Crime Helpline (`1930`).
- ** Privacy-Preserving Report Vault:** Phone numbers, handles, and URLs submitted for crowdsourced fraud reporting are stored only as one-way HMAC-SHA256 hashes with server-side pepper. Raw PII is never persisted.
---
##  Architecture & System Design
               ┌─────────────────────────┐
                 │   User (Browser / App)   │
                 └────────────┬────────────┘
                              │
             ┌────────────────┴────────────────┐
             ▼                                 ▼
  [ TipShield.Web (Blazor) ]       [ TipShield.Bot (Telegram) ]
    (Interactive Server UI)             (Long Polling Worker)
             │                                 │
             └────────────────┬────────────────┘
                              │  (Authenticated via X-Api-Key)
                              ▼
            [ TipShield.Api (ASP.NET Minimal API) ]
                              │
     ┌────────────────────────┼────────────────────────┐
     ▼                        ▼                        ▼

[ Rule Engine ]          [ Gemma AI Client ]      [ Market Service ]
(TipShield.Core)         (TipShield.Gemma)        (Python / FastAPI)
• Precompiled Regexes    • Gemini API REST        • yfinance (NSE/BSE)
• 100ms ReDoS cap        • gemma-4-31b-it / mock  • Live Volume Spikes
• NFKC text normalize    • Anti-injection sandbox • WHOIS & Homoglyphs
• rules.json (extensible)• Circuit breaker Polly  • Internal Docker Net
│                        │                        │
└────────────────────────┼────────────────────────┘
▼
[ Score Aggregator ]
(Weights: 40% Rules, 60% AI, + Market Bonus)
│
▼
[ SQLite Report Store ]
(HMAC-SHA256 Peppered Hashes)

---
## 🛠️ Tech Stack
| Component | Technology | Description |
| :--- | :--- | :--- |
| **API** | ASP.NET Core Minimal API | High-throughput backend with rate limiting & RFC 7807 error handling |
| **Core Engine** | C# Class Library (`TipShield.Core`) | Weighted regex evaluation, text normalizer, PII scrubber, scoring |
| **AI Classifier** | C# Class Library (`TipShield.Gemma`) | Google Gemma 4 client via Gemini REST API with mock fallback mode |
| **Web UI** | Blazor Web App (Interactive Server) | Modern, responsive dashboard with drag-and-drop screenshot uploads |
| **Telegram Bot** | C# Worker Service (`TipShield.Bot`) | Telegram bot daemon supporting text tips and photo submissions |
| **Market Service** | Python 3.11+ / FastAPI | Live stock quotes, volume spike calculations, WHOIS domain auditing |
| **Database** | SQLite via EF Core | Secure report database storing only peppered hashes |
| **Packaging** | Docker & Docker Compose | Multi-stage builds, non-root containers, internal network isolation |
---
##  Quickstart & Local Setup
### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download) (or .NET 10.0 SDK)
* [Python 3.11+](https://www.python.org/) with `pip`
* [Docker & Docker Compose](https://www.docker.com/) *(optional for containerized deployment)*
---
### Method 1: Running Locally (Development Mode)
#### 1. Clone the Repository
```bash
git clone https://github.com/your-username/Tipshield.git
cd Tipshield
2. Start the Python Market Service
bash
cd market-service
python -m venv .venv
# Windows:
.venv\Scripts\activate
# Linux/macOS:
# source .venv/bin/activate
pip install -r requirements.txt
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000

The market service will be available at http://127.0.0.1:8000.

3. Start the ASP.NET Core Backend API

Open a new terminal at the project root:

bash
dotnet run --project src/TipShield.Api --urls http://127.0.0.1:5000

The API will be listening on http://127.0.0.1:5000.

4. Start the Blazor Web Interface

Open a new terminal at the project root:

bash
dotnet run --project src/TipShield.Web --urls http://127.0.0.1:5001

Open http://127.0.0.1:5001
 in your browser.

Method 2: Running with Docker Compose

Launch the entire stack with a single command:

bash
docker compose up --build

Services exposed:


  
Web UI: http://localhost:5001

  
API: http://localhost:5000

  
Market Service: Kept private inside the internal Docker network.

  
 Configuration & Environment Variables

Create a .env file in the root directory (refer to .env.example):

ini
# API Security
API_KEY=your-secure-api-key-here
INTERNAL_API_KEY=internal-service-secret-key
REPORT_HASH_PEPPER=minimum-32-character-secret-pepper-phrase!
# Gemma AI Configuration
GEMMA_API_KEY=your-gemini-api-key-here
GEMMA_MODEL_ID=gemma-4-31b-it
GEMMA_MOCK=false           # Set to true to run in offline mock mode without an API key
GEMMA_THINKING=false
# Telegram Bot (Optional)
TELEGRAM_BOT_TOKEN=your-telegram-bot-token-here
# Service Endpoints
MARKET_SERVICE_URL=http://127.0.0.1:8000
DATABASE_PATH=tipshield.db
 Testing

TipShield includes unit, integration, and security tests across both .NET and Python components:

bash
# Run all .NET unit and integration tests
dotnet test
# Run Python market service test suite
cd market-service
pytest

Test coverage includes:


  
Rule Engine: Positive and negative test cases for all 10 scam tactic categories.

  
ReDoS Protection: Regex evaluation capped at 100ms timeout.

  
Mock Gemma Client: Deterministic classification for automated CI runs without external API dependencies.

  
API Authentication: Fixed-time constant equality checks (CryptographicOperations.FixedTimeEquals).

  
Privacy Hashing: Validating HMAC-SHA256 peppered hashing for fraud reports.

  
 Security & Privacy Guardrails

  
No Raw PII Storage: Submitted phone numbers, handles, and URLs are normalized and stored as one-way HMAC hashes. Raw identifiers never touch disk.

  
Defensive AI Prompt Sandboxing: User messages and OCR results are enclosed in <message> boundaries and treated as untrusted text to prevent prompt injection and system prompt extraction.

  
ReDoS Immunity: All rule engine regexes are compiled with strict 100ms evaluation timeouts.

  
Internal Network Isolation: The Python market service binds only to the internal network and requires X-Internal-Key validation.

  
No Speculation: The application strictly enforces rules preventing stock price prediction, buy/sell recommendations, and naming specific individuals.

  
 Disclaimer

Educational risk signals only. Not financial advice. A clean result does not mean a tip is safe.

  TipShield is built solely for educational and fraud-awareness purposes. It is not registered with SEBI as an investment advisor or research analyst. Always verify financial intermediaries independently on the official SEBI Portal
 before committing funds. For cyber fraud assistance in India, contact the National Cyber Crime Reporting Portal at cybercrime.gov.in
 or dial helpline 1930.

 License

This project is licensed under the MIT License.
