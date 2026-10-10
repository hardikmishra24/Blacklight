# Suggested GitHub Issues

Create these issues manually on GitHub when ready.

## Core

### Issue 1: Add support for more regional languages
- **Labels:** enhancement, good-first-issue
- **Acceptance:** Rule patterns added for Gujarati and Tamil scam phrases. Tests pass.

### Issue 2: Rule engine performance benchmarks
- **Labels:** performance
- **Acceptance:** BenchmarkDotNet suite for rule evaluation. Baseline numbers documented.

### Issue 3: Configurable scoring weights via admin UI
- **Labels:** enhancement
- **Acceptance:** Admin page to adjust score aggregation weights. Persisted to config.

## Gemma Integration

### Issue 4: Support Gemma vision for direct image analysis
- **Labels:** enhancement
- **Acceptance:** Send images directly to Gemma instead of extracting text first.

### Issue 5: Prompt A/B testing framework
- **Labels:** enhancement
- **Acceptance:** Multiple prompt versions, random selection, eval comparison.

## Market Service

### Issue 6: Add BSE ticker support
- **Labels:** enhancement
- **Acceptance:** .BO suffix tried first for BSE-only tickers.

### Issue 7: Historical scam ticker database
- **Labels:** enhancement, data
- **Acceptance:** Curated list of past pump-and-dump tickers with date ranges.

## Web UI

### Issue 8: Progressive Web App (PWA) support
- **Labels:** enhancement
- **Acceptance:** Installable PWA with offline cached quiz.

### Issue 9: WhatsApp Web integration guide
- **Labels:** documentation
- **Acceptance:** Guide for forwarding messages to TipShield from WhatsApp Web.

### Issue 10: Dark mode
- **Labels:** enhancement, good-first-issue
- **Acceptance:** CSS dark mode toggle. Respects prefers-color-scheme.

## Security

### Issue 11: Add Content Security Policy reporting endpoint
- **Labels:** security
- **Acceptance:** CSP report-uri configured, reports logged.

### Issue 12: Rate limiting per API key
- **Labels:** security, enhancement
- **Acceptance:** Per-key rate limits instead of per-IP only.

## DevOps

### Issue 13: Kubernetes deployment manifests
- **Labels:** enhancement, devops
- **Acceptance:** Helm chart or kustomize manifests for k8s deployment.

### Issue 14: Monitoring and alerting
- **Labels:** enhancement, devops
- **Acceptance:** Prometheus metrics endpoint, Grafana dashboard template.

### Issue 15: Load testing suite
- **Labels:** performance, testing
- **Acceptance:** k6 or Artillery load test scripts with baseline results.
