from app.services.domain_service import validate_hostname, get_domain_info
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
        m.registrar = "Test Registrar"
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
