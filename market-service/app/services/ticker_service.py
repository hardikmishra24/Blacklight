import re
import yfinance as yf
from app.config import get_settings
from app.models import TickerResponse
from app.services.cache import TTLCache
import logging

logger = logging.getLogger(__name__)

ticker_cache = TTLCache()

COMPANY_ALIASES = {
    "INFOSYS": "INFY",
    "SBI": "SBIN",
    "STATEBANK": "SBIN",
    "HDFC": "HDFCBANK",
    "ICICI": "ICICIBANK",
    "TATAMOTORS": "TATAMOTORS",
    "TATASTEEL": "TATASTEEL",
    "TATAPOWER": "TATAPOWER",
    "TATACHEM": "TATACHEM",
    "AIRTEL": "BHARTIARTL",
    "BHARTI": "BHARTIARTL",
    "KOTAK": "KOTAKBANK",
    "AXIS": "AXISBANK",
    "BAJAJ": "BAJFINANCE",
    "BAJAJFINANCE": "BAJFINANCE",
    "L&T": "LT",
    "LARSEN": "LT",
    "HUL": "HINDUNILVR",
    "HINDUSTANUNILEVER": "HINDUNILVR",
    "ADANI": "ADANIENT",
    "ADANIENT": "ADANIENT",
    "ADANIPORTS": "ADANIPORTS",
    "ADANIPOWER": "ADANIPOWER",
    "MAHINDRA": "M&M"
}

def validate_symbol(symbol: str) -> bool:
    return bool(re.match(r"^[A-Za-z0-9&\-]{1,20}$", symbol))

def get_ticker_info(symbol: str) -> TickerResponse:
    symbol = symbol.strip().upper()
    symbol = COMPANY_ALIASES.get(symbol, symbol)
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
