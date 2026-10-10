import hmac
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
