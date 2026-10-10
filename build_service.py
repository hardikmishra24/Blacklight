import os

base_path = r"c:\Users\hardi\OneDrive\Desktop\Tipshield\market-service"

files = {
    "requirements.txt": """fastapi==0.115.12
uvicorn[standard]==0.34.2
yfinance==0.2.54
python-whois==0.9.5
pydantic==2.11.3
pydantic-settings==2.9.1
httpx==0.28.1
""",
    "requirements-dev.txt": """-r requirements.txt
pytest==8.3.5
pytest-asyncio==0.25.3
httpx==0.28.1
ruff==0.11.12
mypy==1.15.0
pip-audit==2.9.0
""",
    "pyproject.toml": """[project]
name = "tipshield-market-service"
version = "0.1.0"
description = "TipShield Market & Domain Lookup Service"
requires-python = ">=3.11"

[tool.ruff]
target-version = "py311"
line-length = 100

[tool.ruff.lint]
select = ["E", "F", "W", "I", "N", "UP", "S", "B", "A", "C4", "SIM", "TCH"]
ignore = ["S104"]

[tool.mypy]
python_version = "3.11"
strict = true

[tool.pytest.ini_options]
asyncio_mode = "auto"
""",
    "Dockerfile": """FROM python:3.11-slim AS base

WORKDIR /app

# Create non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app/ ./app/

USER appuser

EXPOSE 8000

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \\
    CMD python -c "import httpx; httpx.get('http://localhost:8000/health').raise_for_status()"

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
""",
    "README.md": """# TipShield Market Service
Python FastAPI service for stock market insights and domain lookup.
""",
    "app/__init__.py": "",
    "app/config.py": """from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    \"\"\"Application settings loaded from environment variables.\"\"\"
    internal_api_key: str = "change-me-in-production"
    cache_ttl_seconds: int = 600  # 10 minutes
    cache_max_size: int = 1000
    yfinance_timeout: int = 10
    whois_timeout: int = 10
    max_request_size: int = 1_048_576  # 1 MB
    
    # Suspicious heuristic thresholds
    volume_spike_threshold: float = 3.0
    small_cap_crore: float = 500.0  # ₹500 crore
    weekly_move_threshold: float = 20.0  # percent
    low_volume_threshold: int = 10_000  # average daily volume
    
    # Rate limiting
    rate_limit_per_minute: int = 60

    model_config = {"env_prefix": "MARKET_", "env_file": ".env"}

def get_settings() -> Settings:
    return Settings()
""",
    "app/models.py": """from pydantic import BaseModel, Field

class TickerResponse(BaseModel):
    \"\"\"Response for ticker lookup.\"\"\"
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
    \"\"\"Response for domain lookup.\"\"\"
    hostname: str
    creation_date: str | None = None
    age_days: int | None = None
    registrar: str | None = None
    lookalike_of: str | None = None
    suspicious: bool = False
    reasons: list[str] = Field(default_factory=list)

class HealthResponse(BaseModel):
    \"\"\"Health check response.\"\"\"
    status: str = "ok"
    service: str = "tipshield-market-service"
""",
    "app/services/__init__.py": "",
    "app/services/cache.py": """import threading
import time
from typing import Any

class TTLCache:
    \"\"\"Thread-safe in-memory cache with TTL and bounded size.\"\"\"
    
    def __init__(self, ttl_seconds: int = 600, max_size: int = 1000) -> None:
        self._cache: dict[str, tuple[Any, float]] = {}
        self._lock = threading.Lock()
        self._ttl = ttl_seconds
        self._max_size = max_size
    
    def get(self, key: str) -> Any | None:
        with self._lock:
            if key in self._cache:
                value, expiry = self._cache[key]
                if time.monotonic() < expiry:
                    return value
                del self._cache[key]
        return None
    
    def set(self, key: str, value: Any) -> None:
        with self._lock:
            if len(self._cache) >= self._max_size:
                now = time.monotonic()
                expired = [k for k, (_, exp) in self._cache.items() if now >= exp]
                for k in expired:
                    del self._cache[k]
            if len(self._cache) >= self._max_size:
                oldest_key = min(self._cache, key=lambda k: self._cache[k][1])
                del self._cache[oldest_key]
            self._cache[key] = (value, time.monotonic() + self._ttl)
    
    def clear(self) -> None:
        with self._lock:
            self._cache.clear()
    
    @property
    def size(self) -> int:
        with self._lock:
            return len(self._cache)
""",
    "app/services/ticker_service.py": """import re
import yfinance as yf
from app.config import get_settings
from app.models import TickerResponse
from app.services.cache import TTLCache
import logging

logger = logging.getLogger(__name__)

ticker_cache = TTLCache()

def validate_symbol(symbol: str) -> bool:
    return bool(re.match(r"^[A-Za-z0-9&\-]{1,20}$", symbol))

def get_ticker_info(symbol: str) -> TickerResponse:
    if not validate_symbol(symbol):
        return TickerResponse(symbol=symbol, found=False, error="invalid_symbol")
        
    cached = ticker_cache.get(symbol)
    if cached:
        return cached

    settings = get_settings()
    
    try:
        t = yf.Ticker(f"{symbol}.NS")
        hist = t.history(period="3mo")
        if hist.empty:
            t = yf.Ticker(f"{symbol}.BO")
            hist = t.history(period="3mo")
            
        if hist.empty:
            resp = TickerResponse(symbol=symbol, found=False)
            ticker_cache.set(symbol, resp)
            return resp

        info = t.fast_info
        market_cap = getattr(info, "market_cap", None)
        
        last_close = float(hist["Close"].iloc[-1])
        recent_vol = float(hist["Volume"].iloc[-1])
        prior_vol_mean = float(hist["Volume"].iloc[:-1].tail(60).mean()) if len(hist) > 1 else recent_vol
        
        volume_spike = recent_vol / prior_vol_mean if prior_vol_mean > 0 else 1.0
        
        # Calculate % changes
        week_change = 0.0
        if len(hist) >= 5:
            week_change = ((last_close - float(hist["Close"].iloc[-5])) / float(hist["Close"].iloc[-5])) * 100
            
        month_change = 0.0
        if len(hist) >= 21:
            month_change = ((last_close - float(hist["Close"].iloc[-21])) / float(hist["Close"].iloc[-21])) * 100
            
        liquidity = "low" if prior_vol_mean < settings.low_volume_threshold else "normal"
        
        suspicious = False
        reasons = []
        
        if volume_spike > settings.volume_spike_threshold:
            suspicious = True
            reasons.append(f"Volume spike {volume_spike:.1f}x")
            
        if market_cap:
            mc_crore = market_cap / 10000000.0
            if mc_crore < settings.small_cap_crore and abs(week_change) > settings.weekly_move_threshold:
                suspicious = True
                reasons.append("Small cap with >20% weekly move")
                
        if prior_vol_mean < settings.low_volume_threshold:
            suspicious = True
            reasons.append("Very low average volume")
            
        resp = TickerResponse(
            symbol=symbol,
            found=True,
            last_close=last_close,
            volume_spike_x=volume_spike,
            market_cap=market_cap,
            week_change_pct=week_change,
            month_change_pct=month_change,
            liquidity_flag=liquidity,
            suspicious=suspicious,
            reasons=reasons
        )
        ticker_cache.set(symbol, resp)
        return resp
        
    except Exception as e:
        logger.error(f"Error fetching ticker {symbol}: {e}")
        return TickerResponse(symbol=symbol, found=False, error="upstream_unavailable")
""",
    "app/services/domain_service.py": """import re
import whois
from datetime import datetime, timezone
import Levenshtein
from app.config import get_settings
from app.models import DomainResponse
from app.services.cache import TTLCache
import logging

logger = logging.getLogger(__name__)

domain_cache = TTLCache()

KNOWN_DOMAINS = [
    "zerodha", "groww", "upstox", "angelone", "icicidirect", 
    "nseindia", "bseindia", "sebi.gov.in", "moneycontrol", 
    "sharekhan", "motilaloswal", "hdfcsecurities", "kotaksecurities"
]

def validate_hostname(hostname: str) -> bool:
    # No IP literals, no localhost, no ports, no paths, no credentials
    if re.search(r"[:/\\@]", hostname): return False
    if hostname == "localhost": return False
    if re.match(r"^(?:[0-9]{1,3}\\.){3}[0-9]{1,3}$", hostname): return False
    if not re.match(r"^([a-zA-Z0-9-]+\\.)+[a-zA-Z]{2,}$", hostname): return False
    
    tld = hostname.split('.')[-1].lower()
    if tld in ['local', 'internal', 'lan', 'test']: return False
    
    return True

def get_domain_info(hostname: str) -> DomainResponse:
    if not validate_hostname(hostname):
        return DomainResponse(hostname=hostname, suspicious=True, reasons=["Invalid hostname format"])
        
    cached = domain_cache.get(hostname)
    if cached: return cached
    
    settings = get_settings()
    
    creation_date = None
    age_days = None
    registrar = None
    whois_failed = False
    
    try:
        w = whois.whois(hostname)
        if w.creation_date:
            cd = w.creation_date[0] if isinstance(w.creation_date, list) else w.creation_date
            creation_date = cd.isoformat() if hasattr(cd, 'isoformat') else str(cd)
            if hasattr(cd, 'timestamp'):
                age_days = (datetime.now().timestamp() - cd.timestamp()) // 86400
        if w.registrar:
            registrar = w.registrar
    except Exception as e:
        logger.warning(f"WHOIS lookup failed for {hostname}: {e}")
        whois_failed = True

    # Lookalike detection
    lookalike_of = None
    base_name = hostname.split('.')[0].lower()
    for kd in KNOWN_DOMAINS:
        kd_base = kd.split('.')[0].lower()
        if base_name == kd_base: continue
        # Simple edit distance
        if len(base_name) > 3 and Levenshtein.distance(base_name, kd_base) <= 1:
            lookalike_of = kd
            break
        # Homoglyphs could be added here
        homoglyph_map = {'0': 'o', '1': 'l', '3': 'e', '4': 'a', '5': 's', 'l': 'i'}
        mod_base = ''.join(homoglyph_map.get(c, c) for c in base_name)
        if mod_base == kd_base:
            lookalike_of = kd
            break

    suspicious = False
    reasons = []
    
    if age_days is not None and age_days < 180:
        suspicious = True
        reasons.append(f"Domain is new (age: {age_days} days)")
        
    if lookalike_of:
        suspicious = True
        reasons.append(f"Lookalike of {lookalike_of}")
        
    if whois_failed:
        suspicious = True
        reasons.append("WHOIS lookup failed")
        
    resp = DomainResponse(
        hostname=hostname,
        creation_date=creation_date,
        age_days=age_days,
        registrar=registrar,
        lookalike_of=lookalike_of,
        suspicious=suspicious,
        reasons=reasons
    )
    
    domain_cache.set(hostname, resp)
    return resp
""",
    "app/middleware/__init__.py": "",
    "app/middleware/auth.py": """import hmac
import time
from collections import defaultdict
from fastapi import Request, Response, status
from starlette.middleware.base import BaseHTTPMiddleware
from app.config import get_settings

class AuthMiddleware(BaseHTTPMiddleware):
    def __init__(self, app):
        super().__init__(app)
        self.rate_limits = defaultdict(list)

    async def dispatch(self, request: Request, call_next) -> Response:
        if request.url.path == "/health":
            return await call_next(request)

        settings = get_settings()
        api_key = request.headers.get("X-Internal-Key")
        
        if not api_key or not hmac.compare_digest(api_key.encode(), settings.internal_api_key.encode()):
            return Response(content="Unauthorized", status_code=status.HTTP_401_UNAUTHORIZED)
            
        now = time.time()
        self.rate_limits[api_key] = [t for t in self.rate_limits[api_key] if now - t < 60]
        
        if len(self.rate_limits[api_key]) >= settings.rate_limit_per_minute:
            return Response(content="Rate limit exceeded", status_code=status.HTTP_429_TOO_MANY_REQUESTS)
            
        self.rate_limits[api_key].append(now)
        
        # Request size limit check is typically handled by server config, but can enforce here
        if request.headers.get("content-length"):
            if int(request.headers["content-length"]) > settings.max_request_size:
                return Response(content="Payload too large", status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE)
                
        return await call_next(request)
""",
    "app/routes/__init__.py": "",
    "app/routes/health.py": """from fastapi import APIRouter
from app.models import HealthResponse

router = APIRouter()

@router.get("/health", response_model=HealthResponse)
async def health_check():
    return HealthResponse()
""",
    "app/routes/ticker.py": """from fastapi import APIRouter, HTTPException, Path
from app.models import TickerResponse
from app.services.ticker_service import get_ticker_info

router = APIRouter()

@router.get("/ticker/{symbol}", response_model=TickerResponse)
async def get_ticker(symbol: str = Path(..., title="Ticker Symbol", min_length=1, max_length=20)):
    resp = get_ticker_info(symbol)
    if resp.error == "invalid_symbol":
        raise HTTPException(status_code=400, detail="Invalid ticker symbol")
    return resp
""",
    "app/routes/domain.py": """from fastapi import APIRouter, HTTPException, Query
from app.models import DomainResponse
from app.services.domain_service import get_domain_info

router = APIRouter()

@router.get("/domain", response_model=DomainResponse)
async def get_domain(host: str = Query(..., title="Hostname to check")):
    resp = get_domain_info(host)
    return resp
""",
    "app/main.py": """import logging
from fastapi import FastAPI
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from app.routes import ticker, domain, health
from app.middleware.auth import AuthMiddleware

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")

app = FastAPI(title="TipShield Market Service")

app.add_middleware(AuthMiddleware)

@app.exception_handler(Exception)
async def global_exception_handler(request, exc):
    logging.error(f"Unhandled exception: {exc}")
    return JSONResponse(status_code=500, content={"detail": "Internal server error"})

app.include_router(health.router)
app.include_router(ticker.router)
app.include_router(domain.router)
""",
    "tests/__init__.py": "",
    "tests/conftest.py": """import pytest
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
""",
    "tests/test_ticker.py": """from app.services.ticker_service import validate_symbol, get_ticker_info
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
            "Close": [100.0] * 60 + [130.0],  # 30% weekly move approx if we assume simple setup
            "Volume": [5000.0] * 60 + [25000.0]  # 5x spike, low vol
        }, index=dates)
        m.history.return_value = df
        return m
    monkeypatch.setattr(yf, "Ticker", _mock)
    
    resp = get_ticker_info("SUSP")
    assert resp.found
    assert resp.suspicious
    assert len(resp.reasons) >= 1
""",
    "tests/test_domain.py": """from app.services.domain_service import validate_hostname, get_domain_info
import whois
from unittest.mock import MagicMock
from datetime import datetime, timedelta

def test_validate_hostname():
    assert validate_hostname("google.com")
    assert validate_hostname("zerodha.com")
    assert not validate_hostname("127.0.0.1")
    assert not validate_hostname("localhost")
    assert not validate_hostname("google.com:443")
    assert not validate_hostname("my.local")

def test_lookalike(monkeypatch):
    def _mock_whois(*args, **kwargs):
        m = MagicMock()
        m.creation_date = datetime.now() - timedelta(days=30)
        return m
    monkeypatch.setattr(whois, "whois", _mock_whois)
    
    # "zer0dha" vs "zerodha"
    resp = get_domain_info("zer0dha.com")
    assert resp.suspicious
    assert any("Lookalike" in r for r in resp.reasons)

def test_whois_failure(monkeypatch):
    def _mock_whois(*args, **kwargs):
        raise Exception("WHOIS timeout")
    monkeypatch.setattr(whois, "whois", _mock_whois)
    
    resp = get_domain_info("fail.com")
    assert resp.suspicious
    assert any("failed" in r for r in resp.reasons)
""",
    "tests/test_auth.py": """def test_health_no_auth(client):
    response = client.get("/health")
    assert response.status_code == 200

def test_ticker_requires_auth(client):
    response = client.get("/ticker/RELIANCE")
    assert response.status_code == 401

def test_ticker_wrong_auth(client):
    response = client.get("/ticker/RELIANCE", headers={"X-Internal-Key": "wrong"})
    assert response.status_code == 401

def test_ticker_valid_auth(client, auth_headers, mock_yf_ticker):
    response = client.get("/ticker/RELIANCE", headers=auth_headers)
    assert response.status_code == 200
""",
    "tests/test_cache.py": """from app.services.cache import TTLCache
import time

def test_cache():
    c = TTLCache(ttl_seconds=1, max_size=2)
    c.set("k1", "v1")
    assert c.get("k1") == "v1"
    
    time.sleep(1.1)
    assert c.get("k1") is None
    
    c.set("k2", "v2")
    c.set("k3", "v3")
    c.set("k4", "v4")  # Should evict k2
    assert c.get("k2") is None
    assert c.get("k3") == "v3"
"""
}

for filepath, content in files.items():
    full_path = os.path.join(base_path, filepath)
    os.makedirs(os.path.dirname(full_path), exist_ok=True)
    with open(full_path, "w", encoding="utf-8") as f:
        f.write(content)

print("Files created.")
