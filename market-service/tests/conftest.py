import pytest
from fastapi.testclient import TestClient
from app.main import app
from app.config import Settings
import yfinance as yf
from unittest.mock import MagicMock
import pandas as pd

@pytest.fixture
def client() -> TestClient:
    return TestClient(app)

@pytest.fixture
def auth_headers() -> dict[str, str]:
    settings = Settings()
    return {"X-Internal-Key": settings.internal_api_key}

@pytest.fixture
def mock_yf_ticker(monkeypatch):
    def _mock(*args, **kwargs):
        m = MagicMock()
        m.fast_info.market_cap = 10000000000.0  # 1000 Cr
        
        dates = pd.date_range("2023-01-01", periods=100)
        df = pd.DataFrame({
            "Close": [100.0] * 100,
            "Volume": [20000.0] * 100
        }, index=dates)
        
        m.history.return_value = df
        return m
    monkeypatch.setattr(yf, "Ticker", _mock)
