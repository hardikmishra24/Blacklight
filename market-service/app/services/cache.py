import threading
import time
from typing import Any

class TTLCache:
    """Thread-safe in-memory cache with TTL and bounded size."""
    
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
            # Evict expired entries if at capacity
            if len(self._cache) >= self._max_size:
                now = time.monotonic()
                expired = [k for k, (_, exp) in self._cache.items() if now >= exp]
                for k in expired:
                    del self._cache[k]
            # If still at capacity, evict oldest
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
