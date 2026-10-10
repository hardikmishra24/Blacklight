from pydantic import BaseModel, Field

class TickerResponse(BaseModel):
    """Response for ticker lookup."""
    symbol: str
    found: bool
    last_close: float | None = None
    volume_spike_x: float | None = None
    market_cap: float | None = None
    week_change_pct: float | None = None
    month_change_pct: float | None = None
    liquidity_flag: str | None = None
    suspicious: bool = False
    reasons: list[str] = Field(default_factory=list)
    error: str | None = None

class DomainResponse(BaseModel):
    """Response for domain lookup."""
    hostname: str
    creation_date: str | None = None
    age_days: int | None = None
    registrar: str | None = None
    lookalike_of: str | None = None
    suspicious: bool = False
    reasons: list[str] = Field(default_factory=list)

class HealthResponse(BaseModel):
    """Health check response."""
    status: str = "ok"
    service: str = "tipshield-market-service"
