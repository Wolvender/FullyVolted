"""The shape of the data going in and out of the API. FastAPI checks every request against these
and answers 422 (with the reason) before any SQL runs."""
from datetime import datetime
from typing import Annotated
from uuid import UUID

from pydantic import BaseModel, Field, StringConstraints


class RunCreate(BaseModel):
    # Unity makes the id itself (a GUID) so it never has to wait for an answer. Leave it out (Bruno)
    # and the API makes one.
    id: UUID | None = None
    trainee: str = Field("Trainee", min_length=1, max_length=100)


class RunCreated(BaseModel):
    id: UUID


class StepCreate(BaseModel):
    step_order: int = Field(ge=1, description="1 = PpeCheck ... 5 = DescendToBottom")
    step_name: str = Field(min_length=1, max_length=50)
    seconds: float = Field(ge=0, description="Split time of this step")
    mistakes: int = Field(0, ge=0, description="Faults made during this step")
    correct: bool
    # What went wrong, one line per mistake, in the order they happened. Optional: older clients send none.
    mistake_reasons: list[Annotated[str, Field(min_length=1, max_length=255)]] = Field(
        default_factory=list, max_length=100)


class Step(StepCreate):
    created_at: datetime


class RunSummary(BaseModel):
    run_id: UUID
    trainee: str
    started_at: datetime
    steps_done: int
    finished: bool
    total_seconds: float
    total_mistakes: int
    all_correct: bool
    slowest_step: str | None


class RunDetail(RunSummary):
    steps: list[Step]


class StepStat(BaseModel):
    """How one step went across every run: the group view on the portal."""
    step_order: int
    step_name: str
    runs: int
    avg_seconds: float
    total_mistakes: int
    avg_mistakes: float
    percent_correct: float


# A name as typed on the portal: surrounding spaces dropped, 1-100 characters.
TraineeName = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=100)]


class ActiveTraineeSet(BaseModel):
    name: TraineeName


class ActiveTrainee(BaseModel):
    """Who the next run is recorded for. name is null when nobody has signed in yet."""
    name: str | None
    set_at: datetime | None
