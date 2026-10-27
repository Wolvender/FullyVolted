"""Builds the tables and the run_summary view in MySQL, as the fullyvolted_api user.
Run from the Api folder after create_user.local.sql:   .venv\\Scripts\\python init_db.py
Safe to run again: the schema only creates what is missing."""
from pathlib import Path

from app.db import connect

SCHEMA = Path(__file__).resolve().parent.parent / "Database" / "mysql" / "schema.sql"


def statements(sql: str) -> list[str]:
    # Drop the -- comment lines, then split on ";". Fine for our schema (no ";" inside strings).
    lines = [line for line in sql.splitlines() if not line.strip().startswith("--")]
    return [part.strip() for part in "\n".join(lines).split(";") if part.strip()]


def main() -> None:
    connection = connect()
    try:
        with connection.cursor() as cursor:
            for statement in statements(SCHEMA.read_text(encoding="utf-8")):
                cursor.execute(statement)
        connection.commit()
        with connection.cursor() as cursor:
            cursor.execute("show full tables")
            for row in cursor.fetchall():
                print(" ".join(str(value) for value in row.values()))
    finally:
        connection.close()
    print("Schema up to date.")


if __name__ == "__main__":
    main()
