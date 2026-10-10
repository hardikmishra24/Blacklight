# Security Documentation

## Threat Model

### Assets
1. **User-submitted content** (text, images) — processed transiently, never stored
2. **API keys and secrets** — Gemini API key, internal service keys, report hash pepper
3. **Report database** — contains only hashes, timestamps, and types
4. **Service availability** — the application itself

### Actors
1. **Legitimate users** — submit messages for analysis
2. **Attackers via input** — attempt prompt injection, SSRF, XSS, or DoS
3. **Attackers via network** — attempt to access internal services or exfiltrate data
4. **Supply chain** — compromised dependencies

### Abuse Cases
1. Prompt injection to manipulate risk scores
2. SSRF via ticker symbols, domain names, or image URLs
3. Enumeration of reported phone numbers via the lookup endpoint
4. DoS via large payloads, many requests, or regex bombs
5. XSS via model output rendered in UI
6. Secret exfiltration from logs or error messages

## Security Checklist

| # | Requirement | Implementation | Location |
|---|-------------|---------------|----------|
| 1 | No secrets in repo/images | `.env.example` has placeholders only, `.gitignore` covers secrets | `.gitignore`, `.env.example` |
| 2 | Gitleaks in CI | `.gitleaks.toml` config, CI step | `.gitleaks.toml`, `.github/workflows/ci.yml` |
| 3 | Input size limits | Text 5000 chars, image 4MB, body 6MB | `TipShield.Api/Program.cs`, `TipShield.Core/Validation/` |
| 4 | Image safety | Magic byte check, re-encode with ImageSharp, dimension cap | `TipShield.Core/Imaging/ImageValidator.cs` |
| 5 | Text normalization | Unicode NFKC, strip zero-width, collapse whitespace | `TipShield.Core/TextProcessing/TextNormalizer.cs` |
| 6 | Strict identifier validation | Regex allowlists for tickers, hostnames | `TipShield.Core/Validation/` |
| 7 | Prompt injection defense | Delimiters, instruction in system prompt, anti-override | `TipShield.Gemma/Prompts/` |
| 8 | Verbatim quote verification | Post-processing drops unverifiable quotes | `TipShield.Gemma/PostProcessing/` |
| 9 | Deterministic verdict | Rules floor at 70 → suspicious minimum | `TipShield.Core/Scoring/ScoreAggregator.cs` |
| 10 | No URL fetching | Domain check is WHOIS-only, no page fetch | `market-service/app/routes/domain.py` |
| 11 | Internal-only market service | Not published in compose, requires X-Internal-Key | `docker-compose.yml`, `market-service/app/middleware/` |
| 12 | API key auth | X-Api-Key with constant-time comparison | `TipShield.Api/Auth/` |
| 13 | Rate limiting | Per-IP rate limits on analyze and lookup | `TipShield.Api/Program.cs` |
| 14 | Report hashing | HMAC-SHA256 with pepper, normalized inputs | `TipShield.Api/Services/ReportService.cs` |
| 15 | Security headers | CSP, X-Content-Type-Options, etc. | `TipShield.Api/Program.cs`, `TipShield.Web/Program.cs` |
| 16 | CORS disabled on API | No wildcard, server-to-server only | `TipShield.Api/Program.cs` |
| 17 | Secure cookies | HttpOnly, Secure, SameSite=Strict | `TipShield.Web/Program.cs` |
| 18 | RFC 7807 errors | Global exception handler, no stack traces | `TipShield.Api/Middleware/` |
| 19 | Log redaction | No message text, images, or secrets in logs | `TipShield.Core/Logging/` |
| 20 | PII stripping | Phone numbers, emails stripped from stored/logged data | `TipShield.Core/TextProcessing/PiiStripper.cs` |
| 21 | Privacy documentation | `docs/PRIVACY.md` | `docs/PRIVACY.md` |
| 22 | Container hardening | Non-root, minimal images, cap_drop, no-new-privileges | `Dockerfile.*`, `docker-compose.yml` |
| 23 | Pinned dependencies | Exact versions in requirements.txt and NuGet | `requirements.txt`, `*.csproj` |
| 24 | CI security scans | Vulnerability checks, gitleaks, pip-audit | `.github/workflows/ci.yml` |
| 25 | Security policy | `SECURITY.md` at repo root | `SECURITY.md` |
