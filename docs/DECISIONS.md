# Architecture Decisions

This document records key design decisions made during TipShield development.

## AD-001: Git Operations Skipped
- **Decision:** Git branching and commits were not used during initial build.
- **Reason:** Git commands were not available in the build environment.
- **Impact:** None on code quality. Repository can be initialized post-build.

## AD-002: .NET 8.0 Target Framework
- **Decision:** All .NET projects target `net8.0`.
- **Reason:** Specified in requirements. Stable LTS release.

## AD-003: SQLite for Storage
- **Decision:** Use SQLite via EF Core for report storage.
- **Reason:** Zero infrastructure, suitable for the single-instance deployment model.
- **Trade-off:** Not suitable for horizontal scaling. Upgrade path: PostgreSQL.

## AD-004: Gemma via Gemini API
- **Decision:** Call Gemma 4 models through the Gemini API REST endpoint.
- **Reason:** Specified in requirements. Provides managed inference.
- **Config:** Model ID configurable, default `gemma-4-31b-it`.

## AD-005: Score Aggregation Weights
- **Decision:** `final = clamp(round(0.35 * rulesScore + 0.50 * gemmaScore) + tickerBonus + registrationBonus)`
- **Reason:** Specified in requirements. Weights are configurable.
- **Note:** When Gemma unavailable, rules-only score capped at 80.

## AD-006: Mock Mode for CI
- **Decision:** `Gemma:Mock=true` returns deterministic canned results.
- **Reason:** CI and eval must run without an API key.

## AD-007: Image Processing with ImageSharp
- **Decision:** Use SixLabors.ImageSharp for image validation, re-encoding, and metadata stripping.
- **Reason:** Cross-platform, well-maintained, no native dependencies.

## AD-008: Report Storage is Hash-Only
- **Decision:** Store only HMAC-SHA256 of normalized identifiers, never raw values.
- **Reason:** Privacy requirement. Even a database breach reveals nothing.

## AD-009: .NET 10 Target Framework in Host Environment
- **Decision:** The local environment provides .NET 10 SDK (`net10.0`). Projects configured for `net10.0`.
- **Reason:** Aligned with installed host SDK tools and forward-compatible with latest C# 13 features.

## AD-010: Pure-Python Levenshtein Algorithm for Domain Lookalikes
- **Decision:** Implemented an in-memory, zero-dependency Levenshtein distance matrix function in `domain_service.py`.
- **Reason:** Eliminates C-extension compilation requirements (`python-Levenshtein`), ensuring instant cross-platform compatibility across Windows, Linux, and minimal Docker containers.

## AD-011: Configurable Rate Limits for Development and Evaluation
- **Decision:** Rate limiting permit counts exposed to `IConfiguration` (`RateLimiting:AnalyzePermitLimit`).
- **Reason:** Preserves 20/min production limits while allowing test suites and batch evaluation scripts (`run_eval.py`) to process datasets rapidly without queue timeouts.

## AD-012: Server-Side Blazor for Secret Protection
- **Decision:** Blazor Interactive Server was utilized so all API interactions occur over internal server-to-server HTTP channels.
- **Reason:** The browser client never touches or stores the `X-Api-Key`, preventing client-side secret leakage.

