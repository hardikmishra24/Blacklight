from fastapi import APIRouter, HTTPException, Query
from app.models import DomainResponse
from app.services.domain_service import get_domain_info

router = APIRouter()

@router.get("/domain", response_model=DomainResponse)
async def get_domain(host: str = Query(..., title="Hostname to check")):
    resp = get_domain_info(host)
    return resp
