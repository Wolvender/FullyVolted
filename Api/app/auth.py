"""The X-Api-Key check. Put on the routes that write; reading stays open."""
import secrets

from fastapi import HTTPException, Security, status
from fastapi.security import APIKeyHeader

from .settings import settings

# auto_error=False: we answer the missing-key case ourselves, with a 401 and a readable message.
_api_key_header = APIKeyHeader(name="X-Api-Key", auto_error=False)


def require_api_key(key: str | None = Security(_api_key_header)) -> None:
    # compare_digest takes the same time whether the first or the last character is wrong,
    # so the key can't be guessed one character at a time by measuring response times.
    if key is None or not secrets.compare_digest(key, settings.api_key):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Missing or wrong X-Api-Key header")
