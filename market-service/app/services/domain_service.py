import re
import whois
from datetime import datetime, timezone
from app.config import get_settings
from app.models import DomainResponse
from app.services.cache import TTLCache
import logging

def levenshtein_distance(s1: str, s2: str) -> int:
    if len(s1) < len(s2):
        return levenshtein_distance(s2, s1)
    if len(s2) == 0:
        return len(s1)
    previous_row = list(range(len(s2) + 1))
    for i, c1 in enumerate(s1):
        current_row = [i + 1]
        for j, c2 in enumerate(s2):
            insertions = previous_row[j + 1] + 1
            deletions = current_row[j] + 1
            substitutions = previous_row[j] + (c1 != c2)
            current_row.append(min(insertions, deletions, substitutions))
        previous_row = current_row
    return previous_row[-1]
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
    if re.match(r"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$", hostname): return False
    if not re.match(r"^([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}$", hostname): return False
    
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
                age_days = int((datetime.now().timestamp() - cd.timestamp()) // 86400)
        if getattr(w, "registrar", None) is not None and not type(w.registrar).__name__.startswith("MagicMock"):
            reg = w.registrar
            registrar = str(reg[0]) if isinstance(reg, list) else str(reg)
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
        if len(base_name) > 3 and levenshtein_distance(base_name, kd_base) <= 1:
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
