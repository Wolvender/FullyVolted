"""FullyVolted results API. Routes only: check the request, call the repository, shape the answer.

Start it from the Api folder:   .venv\\Scripts\\python -m uvicorn app.main:app --reload
Interactive docs:               http://localhost:8000/docs
"""
from uuid import UUID, uuid4

from fastapi import Depends, FastAPI, HTTPException, status
from fastapi.middleware.cors import CORSMiddleware

from . import repository
from .auth import require_api_key
from .db import get_connection
from .models import RunCreate, RunCreated, RunDetail, RunSummary, StepCreate, StepStat
from .settings import settings

app = FastAPI(title="FullyVolted Results API", version="1.0.0")

# The web portal runs on another port, so the browser asks permission first (CORS). Only the portal's
# addresses may read, and only with GET: writes come from Unity, which is not a browser.
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.portal_origins,
    allow_methods=["GET"],
    allow_headers=["*"],
)


@app.get("/health", tags=["status"])
def health(connection=Depends(get_connection)):
    repository.ping(connection)
    return {"status": "ok", "database": "connected"}


@app.post("/runs", status_code=status.HTTP_201_CREATED, response_model=RunCreated, tags=["runs"],
          dependencies=[Depends(require_api_key)])
def create_run(run: RunCreate, connection=Depends(get_connection)):
    run_id = run.id or uuid4()
    try:
        repository.create_run(connection, str(run_id), run.trainee)
    except repository.AlreadyExists as error:
        raise HTTPException(status.HTTP_409_CONFLICT, str(error))
    return RunCreated(id=run_id)


@app.post("/runs/{run_id}/steps", status_code=status.HTTP_201_CREATED, tags=["runs"],
          dependencies=[Depends(require_api_key)])
def add_step(run_id: UUID, step: StepCreate, connection=Depends(get_connection)):
    try:
        repository.add_step(connection, str(run_id), step)
    except repository.RunNotFound as error:
        raise HTTPException(status.HTTP_404_NOT_FOUND, str(error))
    except repository.AlreadyExists as error:
        raise HTTPException(status.HTTP_409_CONFLICT, str(error))
    return {"run_id": run_id, "step_order": step.step_order}


@app.get("/runs", response_model=list[RunSummary], tags=["runs"])
def list_runs(connection=Depends(get_connection)):
    return repository.list_summaries(connection)


@app.get("/stats/steps", response_model=list[StepStat], tags=["stats"])
def step_stats(connection=Depends(get_connection)):
    return repository.step_stats(connection)


@app.get("/runs/{run_id}", response_model=RunDetail, tags=["runs"])
def get_run(run_id: UUID, connection=Depends(get_connection)):
    try:
        return repository.get_run(connection, str(run_id))
    except repository.RunNotFound as error:
        raise HTTPException(status.HTTP_404_NOT_FOUND, str(error))
