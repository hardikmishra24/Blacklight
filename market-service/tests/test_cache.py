from app.services.cache import TTLCache
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
