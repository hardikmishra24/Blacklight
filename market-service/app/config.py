from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    """Application settings loaded from environment variables."""
    internal_api_key: str = "change-me-in-production"
    cache_ttl_seconds: int = 600  # 10 minutes
    cache_max_size: int = 1000
    yfinance_timeout: int = 10
    whois_timeout: int = 10
    max_request_size: int = 1_048_576  # 1 MB
    
    # Suspicious heuristic thresholds (illustrative, not proof of manipulation)
    volume_spike_threshold: float = 3.0
    small_cap_crore: float = 500.0  # ₹500 crore
    weekly_move_threshold: float = 20.0  # percent
    low_volume_threshold: int = 10_000  # average daily volume
    
    # Rate limiting
    rate_limit_per_minute: int = 60

    model_config = {"env_prefix": "MARKET_", "env_file": ".env"}

def get_settings() -> Settings:
    return Settings()
