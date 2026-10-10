# Privacy Policy

## What TipShield Processes

When you submit a message or screenshot for analysis, TipShield processes:
- The text content you provide
- Any image you upload (screenshots)

## What Is Sent to External Services

### Google Gemini API
Your message text and/or image content is sent to Google's Gemini API for AI-powered analysis using the Gemma model. This is necessary for the scam detection functionality. Google's API usage policies apply: https://ai.google.dev/terms

### Market Data (yfinance)
If stock tickers are detected in your message, basic market data (price, volume) is fetched from Yahoo Finance for those specific tickers only. No user data is sent.

## What Is Stored

### Analysis Results
Message text, images, and analysis results are **NOT stored** by default. They exist only in memory during processing.

### Reports
When you report a suspicious sender (phone number, channel handle, or domain):
- Only a **one-way cryptographic hash** (HMAC-SHA256) of the identifier is stored
- The original phone number, handle, or domain **cannot be recovered** from the hash
- A timestamp and report type are stored alongside the hash

### Logs
Application logs contain:
- Request IDs and timestamps
- Content lengths (not content)
- Verdicts and scores
- Error categories

Logs **never** contain: message text, image data, API keys, phone numbers, or any personally identifiable information.

## What Is NOT Collected

- No cookies for tracking
- No analytics or third-party trackers
- No user accounts or profiles
- No IP addresses in stored data
- No message content persistence

## Your Rights

Since we don't store personal data or message content, there is generally nothing to delete. Report hashes cannot be reversed to identify the reported entity.

## Contact

For privacy questions, open an issue on our GitHub repository.
