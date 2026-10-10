# 🛡️ TipShield

**AI-powered scam and pump-and-dump detector for stock tips.**
Forward any "sure-shot" tip. Get a verdict, the evidence, and what to do next.

![License: MIT](https://img.shields.io/badge/license-MIT-green)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![Python](https://img.shields.io/badge/Python-3.11+-3776AB)
![Gemma 4](https://img.shields.io/badge/AI-Gemma%204-4285F4)

> Built for **Hacktoberfest 2026 × MUJ** · Challenge: *Best Use of Gemma 4*
>
> ⚠️ Educational tool. It gives risk *signals*, not proof of fraud or financial advice. A clean result does not mean a tip is safe.

## 📌 Problem

Students and older family members get "guaranteed return" stock tips on WhatsApp and Telegram that lead to paid "VIP groups" and pump-and-dump schemes. Most victims have no quick way to get a second opinion.

## 💡 Solution

Paste a message, upload a screenshot, or forward a tip to our Telegram bot. TipShield returns:

- A **risk score (0-100)** and verdict: *Likely Scam / Suspicious / No Obvious Red Flags*
- The **exact suspicious phrases**, each with a plain-language reason
- A **market check** on any mentioned stock (volume spike, market cap, price move)
- A **next-steps checklist** and a shareable "Forward to parents" warning card (Hindi/English)

## 🧠 How It Works

A hybrid pipeline, so results don't rely on an LLM alone:

1. **Rule engine (C#):** weighted regex checks for patterns like "guaranteed", "double in 7 days", "insider", "join VIP group".
2. **Gemma 4 (Gemini API):** reads text and screenshots, extracts claims and tickers, identifies manipulation tactics, and returns structured JSON.
3. **Market service (Python):** pulls real data for mentioned tickers and flags unusual volume spikes.
4. **Score aggregator (C#):** blends all three into one score, with weights tuned on our eval set.

If Gemma is unavailable, TipShield falls back to rules only.

```
[Blazor UI]   [Telegram Bot]
       \          /
     ASP.NET Core API (C#)
        |-- RuleEngine
        |-- GemmaClient --> Gemini API
        |-- ScoreAggregator
        '-- HTTP --> market-service (Python FastAPI, yfinance)
```

## 🧰 Tech Stack

C# / ASP.NET Core 8 · Blazor · Telegram.Bot · Python / FastAPI · yfinance · Gemma 4 via Gemini API · SQLite · Docker Compose · GitHub Actions

## 🚀 Getting Started

**Prerequisites:** .NET 8 SDK, Python 3.11+, Docker, and a [Gemini API key](https://aistudio.google.com/).

```bash
git clone https://github.com/<your-org>/tipshield.git
cd tipshield
cp .env.example .env     # add your keys
docker compose up --build
```

`.env`:

```env
GEMMA__APIKEY=your_gemini_api_key
GEMMA__MODEL=gemma-4-31b-it        # or gemma-4-26b-a4b-it
MARKET_SERVICE_URL=http://market-service:8000
TELEGRAM_BOT_TOKEN=optional
```

Never commit `.env` or API keys.

## 🔌 API Example

```http
POST /analyze
{ "text": "100% sure shot! XYZ will double in 5 din. Join VIP group abhi!" }
```

```json
{
  "score": 91,
  "verdict": "likely_scam",
  "ruleHits": ["guaranteed_returns", "unrealistic_gain", "paid_group"],
  "gemma": {
    "red_flags": [{ "quote": "100% sure shot", "why": "No legitimate advisor can guarantee returns." }],
    "tickers": ["XYZ"],
    "next_steps": ["Do not pay or join the group", "Verify the advisor on SEBI's site"]
  },
  "suspicious": true
}
```

## 📊 Evaluation

Run `python eval/run_eval.py` on a labeled set of synthetic messages in `eval/data/`.

| Method | Accuracy | False-positive rate |
|---|---|---|
| Rules only | TBD | TBD |
| Gemma only | TBD | TBD |
| **Hybrid** | TBD | TBD |

## 🔒 Responsible AI

- Output is a risk assessment, never an accusation against a named person or channel.
- Raw messages are not stored by default; phone numbers are stripped before logging.
- Community reports (stretch) store hashed identifiers only.

## 🤝 Contributing

Branch per issue (`feat/<name>-<issue#>`), small commits (`feat: add ticker check (#8)`), open a draft PR early with `Closes #<issue>`, and get a teammate review before merging. No direct pushes to `main`.

## 🗺️ Roadmap

- [x] Text + screenshot analysis, rule engine, scoring
- [ ] Ticker pump-and-dump check
- [ ] Hinglish / Hindi support
- [ ] Telegram bot
- [ ] "Forward to parents" card
- [ ] Domain check, SEBI verify link, community reports



## 📄 License

[MIT](LICENSE)
