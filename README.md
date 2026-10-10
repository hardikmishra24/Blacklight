# TipShield 🛡️

[![Build Status](https://img.shields.io/github/actions/workflow/status/tipshield/tipshield/ci.yml)](https://github.com/tipshield/tipshield/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Python Version](https://img.shields.io/badge/Python-3.11+-yellow.svg)](https://www.python.org/)

> **Disclaimer:** *Educational risk signals only. Not financial advice. A clean result does not mean a tip is safe.* TipShield is an educational risk assessment tool designed to help retail investors identify suspicious patterns in stock tips. It never predicts stock prices, never makes buy/sell recommendations, and never accuses named individuals.

---

## 1. Problem Statement

Retail investors across India face widespread financial cyber-fraud, particularly pump-and-dump schemes, illegal advisory operations, and fraudulent group funnels proliferating on **WhatsApp** and **Telegram**. These schemes often lure victims with:
- Guaranteed or fixed daily returns
- Fabricated insider/operator connections
- Unrealistic claims (e.g. 2x to 10x gains in days)
- Forged SEBI registration certificates and fake profit screenshots
- Urgent countdown pressure pushing victims into paid VIP channels or unverified Demat handling services

TipShield provides an open-source, privacy-first, automated risk assessment pipeline to analyze suspicious messages and screenshots in **English**, **Hindi (Devanagari)**, and **Hinglish (romanized Hindi)**.

---

## 2. Architecture & Design

TipShield implements a multi-tiered defense-in-depth architecture:

```
[ User (Web Browser) ]         [ User (Telegram App) ]
         │                               │
         ▼                               ▼
[ TipShield.Web (Blazor) ]    [ TipShield.Bot (Worker) ]
   (Server-Side Interactive)     (Long Polling, Rate-Limited)
         │                               │
         └───────────────┬───────────────┘
                         │ (X-Api-Key Auth)
                         ▼
             [ TipShield.Api (ASP.NET Minimal API) ]
                         │
      ┌──────────────────┼──────────────────┐
      ▼                  ▼                  ▼
[ Rule Engine ]   [ Gemma Client ]   [ Market Service ]
(TipShield.Core)  (TipShield.Gemma)  (Python / FastAPI)
- 100ms ReDoS cap - Gemini API REST  - yfinance (NSE/BSE)
- NFKC Unicode    - gemma-4-31b-it   - WHOIS lookups
- Weighted rules  - Mock mode for CI - Edit distance homoglyphs
- rules.json      - Circuit breaker  - Internal network only
      │                  │                  │
      └──────────────────┼──────────────────┘
                         ▼
              [ Score Aggregator ]
              (0-100 Risk Score + Verdict)
                         │
                         ▼
              [ SQLite Report Store ]
              (HMAC-SHA256 Peppered Hash)
```

### Components

| Component | Technology | Role |
|---|---|---|
| **`TipShield.Core`** | C# Class Library | Text normalization, regex rule engine, image sanitization, PII stripping, score aggregation, log redaction |
| **`TipShield.Gemma`** | C# Class Library | Google Gemini API client for Gemma 4 models (`gemma-4-31b-it`), structured JSON extraction, circuit breaker, anti-injection prompt |
| **`TipShield.Api`** | ASP.NET Core Minimal API | Secure REST API (`/api/analyze`, `/api/family-card`, `/api/reports`, `/api/health`), HMAC reporting, rate limiting |
| **`TipShield.Web`** | Blazor Web App | Interactive web frontend with real-time text highlighter, screenshot upload, family warning card generator, and Scam Academy quiz |
| **`TipShield.Bot`** | .NET Worker Service | Telegram bot using `Telegram.Bot`, per-chat rate limiting (5/min), MarkdownV2 formatting, photo analysis |
| **`market-service`** | Python 3.11+ / FastAPI | Upstream market data (`yfinance`), volume spike heuristics, domain age/lookalike checking (`python-whois`) |

---

## 3. Quick Start

### Option A: Local Development

#### Prerequisites
- .NET 10 SDK (or .NET 8)
- Python 3.11+
- Git

#### 1. Configure Environment
```bash
cp .env.example .env
# Edit .env with your GEMINI_API_KEY (or leave GEMMA_MOCK=true for testing without keys)
```

#### 2. Start the Market Service
```bash
cd market-service
python -m pip install -r requirements.txt
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

#### 3. Start the API
```bash
dotnet run --project src/TipShield.Api --urls http://localhost:5000
```

#### 4. Start the Web UI
```bash
dotnet run --project src/TipShield.Web --urls http://localhost:5001
```
Open [http://localhost:5001](http://localhost:5001) in your browser.

---

### Option B: Docker Compose

TipShield includes production-ready multi-stage Docker builds:

```bash
# Build and start all services (API, Web, Bot, Market Service)
docker compose up -d

# View service health
docker compose ps
```
The Web UI will be available at [http://localhost:5001](http://localhost:5001). The market service is isolated on an internal network and cannot be accessed from outside the Docker cluster.

---

## 4. API Reference

All endpoints except `/api/health` require the `X-Api-Key` header.

### `POST /api/analyze?mode=hybrid|rules|gemma`
Analyzes text, screenshots, or both.

**Request:**
```bash
curl -X POST http://localhost:5000/api/analyze \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: default-dev-api-key" \
  -d '{
    "text": "🚀 GUARANTEED 200% return in 7 days! Operator friend confirmed RELIANCE will hit 5000! Join VIP group: pay ₹5000.",
    "outputLanguage": "en"
  }'
```

**Response:**
```json
{
  "score": 82,
  "verdict": "likely_scam",
  "aiAvailable": true,
  "tactics": [
    "guaranteed_returns",
    "urgency",
    "insider_claim",
    "paid_group_funnel"
  ],
  "redFlags": [
    {
      "quote": "GUARANTEED 200% return in 7 days",
      "why": "Claims of guaranteed returns are a hallmark of financial scams",
      "verified": true
    }
  ],
  "nextSteps": [
    "Do not make any payment",
    "Verify the advisor on SEBI's official intermediary search page",
    "Report at cybercrime.gov.in",
    "Call 1930 for financial cyber-fraud helpline"
  ],
  "disclaimer": "Educational risk signals only. Not financial advice. A clean result does not mean a tip is safe.",
  "requestId": "948ba583babd"
}
```

### `POST /api/family-card`
Generates a short, plain-language warning card in English or Hindi designed to be forwarded to parents or elderly relatives via WhatsApp.

### `POST /api/reports`
Submits a phone number, channel handle, or domain name for anonymous community tracking. Stores only an **HMAC-SHA256** hash using a server-side secret pepper. Raw identifiers are never stored.

---

## 5. Model Evaluation Results

Tested on a benchmark dataset of 60 synthetic messages (25 scams, 4 suspicious tips, 31 legitimate research notes and news items) across English, Hinglish, and Hindi:

| Metric | Rules Mode | Gemma Mode (Mock) | Hybrid Mode |
|---|:---:|:---:|:---:|
| **Accuracy** | 65.0% | 83.3% | **83.3%** |
| **Precision** | 100.0% | 100.0% | **100.0%** |
| **Recall** | 27.6% | 65.5% | **65.5%** |
| **F1 Score** | 0.432 | 0.792 | **0.792** |
| **False Positive Rate** | **0.0%** | **0.0%** | **0.0%** |
| **Avg Latency** | **22.1 ms** | 379.8 ms | 364.4 ms |

### Key Findings
1. **Zero False Positives:** Legitimate institutional equity research notes (e.g. ICICI Direct, Motilal Oswal) with price targets and disclosures are never falsely flagged as scams.
2. **Offline Fail-Safe:** When Gemma is unavailable (`aiAvailable: false`), the deterministic rule engine operates at sub-25ms latency with 100% precision.
3. Full evaluation report: [`eval/results.md`](eval/results.md).

---

## 6. Security & Privacy Guarantees

TipShield implements strict security controls documented in [`docs/SECURITY.md`](docs/SECURITY.md):
- **Prompt Injection Defense:** User messages are isolated inside `<message>` boundary tags. The system prompt instructs Gemma to treat enclosed content as untrusted data and flag prompt overrides as an explicit red flag (`other`).
- **Verbatim Quote Verification:** Post-processing rejects red flags whose quoted text does not exist as a substring in the user's message.
- **ReDoS Protection:** All regular expressions enforce a strict 100ms match timeout.
- **Zero Raw Storage:** Community reports are hashed with HMAC-SHA256 using `REPORT_HASH_PEPPER`. Phone numbers and handles are never stored in plaintext.
- **SSRF Prevention:** The market service accepts only validated hostnames and stock tickers. It never fetches arbitrary URLs.
- **Log Redaction:** `LogRedactor` strips user message text, base64 images, and API keys before logging.
- **Constant-Time Comparison:** All authentication comparisons utilize `CryptographicOperations.FixedTimeEquals` or `hmac.compare_digest`.

---

## 7. Running Tests

```bash
# Run all .NET unit and integration tests (Core, Gemma, Api)
dotnet test

# Run Python market service test suite
python -m pytest market-service/tests/ -v

# Run evaluation script
python eval/run_eval.py --mode all
```

---

## 8. License

This project is licensed under the MIT License. See [`LICENSE`](LICENSE) for details.
