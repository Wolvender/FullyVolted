r"""Fills the database with made-up runs so the web portal has something to show.
Every demo trainee starts with "Demo " so they are easy to tell from real runs.
    .venv\Scripts\python seed_demo.py           add demo runs
    .venv\Scripts\python seed_demo.py --clear   remove them again"""
import random
import sys
import uuid

from app.db import connect

STEPS = ["PpeCheck", "ClimbToMiddle", "ClimbToTop", "DeEnergize", "DescendToBottom"]
# typical seconds per step, and how likely a mistake is
TYPICAL = [(20, 0.0), (120, 0.25), (150, 0.45), (70, 0.55), (110, 0.20)]
REASONS = [
    [],  # nothing can go wrong with a single helmet
    ["Climbing without a carabiner clipped within reach.", "Fell while climbing."],
    ["Climbing without a carabiner clipped within reach.", "Fell while climbing."],
    ["Did Ground And Lock before Switch Off.", "Did Test Voltage before Switch Off."],
    ["Off the platform without a carabiner clipped within reach.", "Fell while climbing."],
]
PEOPLE = ["Demo Daan de Vries", "Demo Sanne Bakker", "Demo Mohamed El Idrissi", "Demo Lotte Jansen", "Demo Tim Visser"]


def main() -> None:
    connection = connect()
    cursor = connection.cursor()
    if "--clear" in sys.argv:
        cursor.execute("delete from runs where trainee like 'Demo %'")
        connection.commit()
        print("Removed", cursor.rowcount, "demo runs")
        return

    random.seed(7)
    for person in PEOPLE:
        skill = random.uniform(0.6, 1.3)  # lower = better
        for attempt in range(random.randint(2, 4)):
            run_id = str(uuid.uuid4())
            improve = 1 - 0.12 * attempt
            cursor.execute("insert into runs (id, trainee, started_at) values (%s, %s, now() - interval %s hour)",
                           (run_id, person, random.randint(1, 240)))
            steps_to_do = 5 if random.random() > 0.12 else random.randint(2, 4)
            for index in range(steps_to_do):
                seconds, chance = TYPICAL[index]
                seconds = seconds * skill * improve * random.uniform(0.8, 1.25)
                mistakes = sum(random.random() < chance * skill * improve for _ in range(3))
                cursor.execute(
                    "insert into step_results (run_id, step_order, step_name, seconds, mistakes, correct) "
                    "values (%s, %s, %s, %s, %s, %s)",
                    (run_id, index + 1, STEPS[index], round(seconds, 3), mistakes, mistakes == 0))
                for reason in random.choices(REASONS[index], k=mistakes):
                    cursor.execute("insert into step_mistakes (run_id, step_order, reason) values (%s, %s, %s)",
                                   (run_id, index + 1, reason))
    connection.commit()
    print("Added demo runs for", len(PEOPLE), "people")


if __name__ == "__main__":
    main()
