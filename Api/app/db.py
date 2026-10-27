"""Opens MySQL connections. Only this file knows which driver we use."""
from collections.abc import Iterator

import pymysql
from pymysql.cursors import DictCursor

from .settings import settings


def connect() -> pymysql.connections.Connection:
    return pymysql.connect(
        host=settings.db_host,
        port=settings.db_port,
        user=settings.db_user,
        password=settings.db_password,
        database=settings.db_name,
        cursorclass=DictCursor,  # rows come back as {"column": value}
        autocommit=False,
    )


def get_connection() -> Iterator[pymysql.connections.Connection]:
    """FastAPI dependency: one connection per request, always closed afterwards."""
    connection = connect()
    try:
        yield connection
    finally:
        connection.close()
