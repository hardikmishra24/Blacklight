from fastapi import APIRouter, HTTPException, Path
from app.models import TickerResponse
from app.services.ticker_service import get_ticker_info

router = APIRouter()

@router.get("/ticker/{symbol}", response_model=TickerResponse)
async def get_ticker(symbol: str = Path(..., title="Ticker Symbol", min_length=1, max_length=20)):
    resp = get_ticker_info(symbol)
    if resp.error == "invalid_symbol":
        raise HTTPException(status_code=400, detail="Invalid ticker symbol")
    return resp
