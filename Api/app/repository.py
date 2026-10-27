"""All SQL lives here. Routes call these functions and never write SQL themselves.
Every value goes in as a %s parameter, never pasted into the string, so SQL injection is impossible."""
import pymysql

from .models import StepCreate


class RunNotFound(Exception):
    pass


class AlreadyExists(Exception):
    pass


# MySQL error numbers we turn into a clear answer instead of a crash.
_DUPLICATE_KEY = 1062
_FOREIGN_KEY_MISSING = 1452


def create_run(connection, run_id: str, trainee: str) -> None:
    try:
        with connection.cursor() as cursor:
            cursor.execute("insert into runs (id, trainee) values (%s, %s)", (run_id, trainee))
        connection.commit()
    except pymysql.err.IntegrityError as error:
        connection.rollback()
        if error.args[0] == _DUPLICATE_KEY:
            raise AlreadyExists(f"Run {run_id} already exists") from error
        raise


def add_step(connection, run_id: str, step: StepCreate) -> None:
    try:
        with connection.cursor() as cursor:
            cursor.execute(
                "insert into step_results (run_id, step_order, step_name, seconds, mistakes, correct) "
                "values (%s, %s, %s, %s, %s, %s)",
                (run_id, step.step_order, step.step_name, step.seconds, step.mistakes, step.correct),
            )
            if step.mistake_reasons:
                cursor.executemany(
                    "insert into step_mistakes (run_id, step_order, reason) values (%s, %s, %s)",
                    [(run_id, step.step_order, reason) for reason in step.mistake_reasons],
                )
        # One commit for the step and its reasons: both are saved, or neither.
        connection.commit()
    except pymysql.err.IntegrityError as error:
        connection.rollback()
        if error.args[0] == _FOREIGN_KEY_MISSING:
            raise RunNotFound(f"Run {run_id} does not exist") from error
        if error.args[0] == _DUPLICATE_KEY:
            raise AlreadyExists(f"Step {step.step_order} of run {run_id} is already saved") from error
        raise


def list_summaries(connection) -> list[dict]:
    with connection.cursor() as cursor:
        cursor.execute("select * from run_summary order by started_at desc")
        return cursor.fetchall()


def step_stats(connection) -> list[dict]:
    with connection.cursor() as cursor:
        cursor.execute(
            "select step_order, min(step_name) as step_name, count(*) as `runs`, "
            "round(avg(seconds), 3) as avg_seconds, sum(mistakes) as total_mistakes, "
            "round(avg(mistakes), 2) as avg_mistakes, round(100 * avg(correct), 1) as percent_correct "
            "from step_results group by step_order order by step_order"
        )
        return cursor.fetchall()


def get_run(connection, run_id: str) -> dict:
    with connection.cursor() as cursor:
        cursor.execute("select * from run_summary where run_id = %s", (run_id,))
        run = cursor.fetchone()
        if run is None:
            raise RunNotFound(f"Run {run_id} does not exist")

        cursor.execute(
            "select step_order, step_name, seconds, mistakes, correct, created_at "
            "from step_results where run_id = %s order by step_order",
            (run_id,),
        )
        steps = cursor.fetchall()

        cursor.execute("select step_order, reason from step_mistakes where run_id = %s order by id", (run_id,))
        reasons: dict[int, list[str]] = {}
        for row in cursor.fetchall():
            reasons.setdefault(row["step_order"], []).append(row["reason"])

        for step in steps:
            step["mistake_reasons"] = reasons.get(step["step_order"], [])
        run["steps"] = steps
        return run


def ping(connection) -> None:
    with connection.cursor() as cursor:
        cursor.execute("select 1")
