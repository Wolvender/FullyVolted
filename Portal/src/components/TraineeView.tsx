import { duration, plural, when } from "../format";
import type { TraineeOverview } from "../trainees";
import { RunView } from "./RunView";

interface Props {
  trainee: TraineeOverview;
  runId: string | null;
  onSelectRun: (id: string) => void;
}

export function TraineeView({ trainee, runId, onSelectRun }: Props) {
  const longest = Math.max(...trainee.runs.map((r) => r.total_seconds), 1);
  // the newest run is open until another one is picked
  const openId = runId ?? trainee.runs[0].run_id;

  return (
    <article>
      <h2>{trainee.name}</h2>
      <p className="lede">
        {plural(trainee.runs.length, "run")}, {trainee.finishedRuns} finished
        {trainee.bestSeconds !== null && <>, fastest {duration(trainee.bestSeconds)}</>}
        {trainee.totalMistakes === 0 ? ", no mistakes at all." : `, ${plural(trainee.totalMistakes, "mistake")} in total.`}
      </p>

      <table className="runs">
        <caption className="sr">Runs, newest first. Select one to see how it went.</caption>
        <thead>
          <tr>
            <th scope="col">Started</th>
            <th scope="col">Steps</th>
            <th scope="col">Time</th>
            <th scope="col">Mistakes</th>
          </tr>
        </thead>
        <tbody>
          {trainee.runs.map((run) => (
            <tr key={run.run_id} className={run.run_id === openId ? "open" : ""}>
              <td>
                <button onClick={() => onSelectRun(run.run_id)} aria-current={run.run_id === openId}>
                  {when(run.started_at)}
                </button>
              </td>
              <td>{run.steps_done} of 5</td>
              <td className="timecell">
                <span className="timebar" style={{ width: `${(run.total_seconds / longest) * 100}%` }} />
                <span className="timetext">{duration(run.total_seconds)}</span>
              </td>
              <td className={run.total_mistakes ? "bad" : "good"}>{run.total_mistakes}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <RunView runId={openId} />
    </article>
  );
}
