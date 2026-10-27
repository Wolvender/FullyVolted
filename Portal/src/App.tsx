import { useMemo, useState } from "react";
import { api } from "./api";
import { Overview } from "./components/Overview";
import { Status } from "./components/Status";
import { TraineeList } from "./components/TraineeList";
import { TraineeView } from "./components/TraineeView";
import { groupByTrainee } from "./trainees";
import { useLoad } from "./useLoad";

export function App() {
  const runs = useLoad(api.runs, "runs");
  const [selected, setSelected] = useState<string | null>(null);
  const [runId, setRunId] = useState<string | null>(null);

  const trainees = useMemo(() => (runs.state === "ready" ? groupByTrainee(runs.data) : []), [runs]);
  const current = trainees.find((t) => t.name === selected) ?? null;

  const selectTrainee = (name: string | null) => {
    setSelected(name);
    setRunId(null);
  };

  return (
    <div className="app">
      <header>
        <svg viewBox="0 0 24 32" aria-hidden="true" className="mast">
          <path
            d="M12 1 L21 31 H3 Z M7 21 H17 M9.5 11 H14.5"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinejoin="round"
          />
        </svg>
        <h1>Fully Volted</h1>
        <p>Safety training results</p>
      </header>

      <Status loaded={runs}>
        {() =>
          trainees.length === 0 ? (
            <p className="note">No runs saved yet. Finish a run in the game and it appears here.</p>
          ) : (
            <div className="layout">
              <TraineeList trainees={trainees} selected={selected} onSelect={selectTrainee} />
              <main>
                {current ? (
                  <TraineeView trainee={current} runId={runId} onSelectRun={setRunId} />
                ) : (
                  <Overview trainees={trainees} />
                )}
              </main>
            </div>
          )
        }
      </Status>
    </div>
  );
}
