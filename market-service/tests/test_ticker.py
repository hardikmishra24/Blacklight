from app.services.ticker_service import validate_symbol, get_ticker_info
from app.models import TickerResponse
import yfinance as yf
from unittest.mock import MagicMock
import pandas as pd

def test_validate_symbol():
    assert validate_symbol("RELIANCE")
    assert validate_symbol("TCS-EQ")
    assert not validate_symbol("INVALID!@#")
    assert not validate_symbol("THISISWAYTOOLONGFORATICKERSYMBOL")

def test_ticker_service(mock_yf_ticker):
    resp = get_ticker_info("RELIANCE")
    assert resp.found
    assert resp.last_close == 100.0
    assert not resp.suspicious

def test_yfinance_failure(monkeypatch):
    def _mock(*args, **kwargs):
        raise Exception("Connection error")
    monkeypatch.setattr(yf, "Ticker", _mock)
    
    resp = get_ticker_info("FAIL")
    assert not resp.found
    assert resp.error == "upstream_unavailable"

def test_suspicious_heuristic(monkeypatch):
    def _mock(*args, **kwargs):
        m = MagicMock()
        m.fast_info.market_cap = 10000000.0  # 1 Cr (very small)
        dates = pd.date_range("2023-01-01", periods=61)
        df = pd.DataFrame({
            "Close": [100.0] * 60 + [130.0],
            "Volume": [5000.0] * 60 + [25000.0]
        }, index=dates)
        m.history.return_value = df
        return m
    monkeypatch.setattr(yf, "Ticker", _mock)
    
    resp = get_ticker_info("SUSP")
    assert resp.found
    assert resp.suspicious
    assert len(resp.reasons) >= 1
