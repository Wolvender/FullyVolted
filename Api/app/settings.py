"""Reads the configuration from Api/.env (gitignored), so no password or key is ever in the code."""
import os
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv

load_dotenv(Path(__file__).resolve().parent.parent / ".env")


def _required(name: str) -> str:
    value = os.getenv(name)
    if not value:
        raise RuntimeError(f"{name} is missing from Api/.env (see Api/.env.example)")
    return value


@dataclass(frozen=True)
class Settings:
    db_host: str
    db_port: int
    db_name: str
    db_user: str
    db_password: str
    api_key: str
    portal_origins: list[str]


settings = Settings(
    db_host=os.getenv("DB_HOST", "localhost"),
    db_port=int(os.getenv("DB_PORT", "3306")),
    db_name=os.getenv("DB_NAME", "fullyvolted"),
    db_user=_required("DB_USER"),
    db_password=_required("DB_PASSWORD"),
    api_key=_required("API_KEY"),
    portal_origins=[o.strip() for o in os.getenv("PORTAL_ORIGINS", "http://localhost:5173").split(",")],
)
