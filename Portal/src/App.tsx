import { useMemo, useState } from "react";
import { api } from "./api";
import { Overview } from "./components/Overview";
import { SignIn } from "./components/SignIn";
import { Status } from "./components/Status";
import { TraineeList } from "./components/TraineeList";
import { TraineeView } from "./components/TraineeView";
import { groupByTrainee } from "./trainees";
import { useLoad } from "./useLoad";

const STORAGE_KEY = "fullyvolted.me";

// localStorage can be blocked (private windows), so a failure just means "not remembered".
function remembered(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function remember(name: string | null) {
  try {
    if (name === null) localStorage.removeItem(STORAGE_KEY);
    else localStorage.setItem(STORAGE_KEY, name);
  } catch {
    /* not remembered, nothing else to do */
  }
}

export function App() {
  const runs = useLoad(api.runs, "runs");
  const [me, setMe] = useState<string | null>(remembered);
  const [asInstructor, setAsInstructor] = useState(false);
  const [selected, setSelected] = useState<string | null>(me);
  const [runId, setRunId] = useState<string | null>(null);

  const trainees = useMemo(() => (runs.state === "ready" ? groupByTrainee(runs.data) : []), [runs]);
  const current = trainees.find((t) => t.name === selected) ?? null;

  const selectTrainee = (name: string | null) => {
    setSelected(name);
    setRunId(null);
  };

  // "senna" and "Senna " are the same person: reuse the spelling already in the results.
  const signIn = async (typed: string) => {
    const cleaned = typed.trim().replace(/\s+/g, " ");
    const existing = trainees.find((t) => t.name.toLowerCase() === cleaned.toLowerCase());
    const saved = await api.signIn(existing?.name ?? cleaned);
    const name = saved.name ?? cleaned;
    remember(name);
    setMe(name);
    selectTrainee(name);
  };

  const signOut = () => {
    remember(null);
    setMe(null);
    setAsInstructor(false);
    selectTrainee(null);
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
        {(me || asInstructor) && (
          <div className="whoami">
            {me ? (
              <span>
                Signed in as <strong>{me}</strong>. The game records your next run under this name.
              </span>
            ) : (
              <span>Instructor view</span>
            )}
            <button className="link" onClick={signOut}>
              {me ? "Sign out" : "Sign in"}
            </button>
          </div>
        )}
      </header>

      <Status loaded={runs}>
        {() =>
          !me && !asInstructor ? (
            <SignIn knownNames={trainees.map((t) => t.name)} onSignIn={signIn} onSkip={() => setAsInstructor(true)} />
          ) : (
            <div className="layout">
              <TraineeList trainees={trainees} selected={selected} onSelect={selectTrainee} />
              <main>
                {current ? (
                  <TraineeView trainee={current} runId={runId} onSelectRun={setRunId} />
                ) : me && selected === me ? (
                  <p className="note">
                    Welcome, {me}. You have no saved runs yet. Finish a run in the game and it appears here.
                  </p>
                ) : trainees.length === 0 ? (
                  <p className="note">No runs saved yet. Finish a run in the game and it appears here.</p>
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
