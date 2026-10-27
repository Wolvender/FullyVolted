import { api } from "../api";
import { ALL_STEPS, duration, plural, stepLabel, when } from "../format";
import { wentRight } from "../trainees";
import type { Step } from "../types";
import { useLoad } from "../useLoad";
import { BarChart, type Bar } from "./BarChart";
import { Status } from "./Status";

const mistakeText = (count: number) => (count === 0 ? "no mistakes" : plural(count, "mistake"));

function timeBars(steps: Step[]): Bar[] {
  const done = new Map(steps.map((s) => [s.step_name, s]));
  return ALL_STEPS.map((name) => {
    const step = done.get(name);
    if (!step) return { label: stepLabel(name), value: 0, tone: "none", caption: "not reached" };
    return {
      label: stepLabel(name),
      value: step.seconds,
      tone: wentRight(step) ? "right" : "wrong",
      caption: duration(step.seconds),
      detail: mistakeText(step.mistakes),
    };
  });
}

// One sentence a non-expert can read: what happened in this step.
function wrongSentence(step: Step): string {
  const what = stepLabel(step.step_name);
  if (step.mistakes > 0 && !step.correct) return `${what}: ${mistakeText(step.mistakes)}, and it was finished in an unsafe way.`;
  if (step.mistakes > 0) return `${what}: ${mistakeText(step.mistakes)}, but it was finished safely in the end.`;
  return `${what}: it was finished in an unsafe way.`;
}

export function RunView({ runId }: { runId: string }) {
  const loaded = useLoad(() => api.run(runId), runId);

  return (
    <Status loaded={loaded}>
      {(run) => {
        const right = run.steps.filter(wentRight);
        const wrong = run.steps.filter((s) => !wentRight(s));
        return (
          <section className="run">
            <h3>
              Run of {when(run.started_at)}
              {!run.finished && <span className="badge">stopped after {run.steps_done} of 5 steps</span>}
            </h3>

            <BarChart title="Time spent on each step" bars={timeBars(run.steps)} axisLabel={duration} />
            <p className="legend">
              <span className="key right" /> No mistakes <span className="key wrong" /> Mistakes or done unsafely
            </p>

            <div className="verdict">
              <div>
                <h4>What went well</h4>
                {right.length === 0 ? (
                  <p className="none">No step was done without mistakes yet.</p>
                ) : (
                  <ul className="right">
                    {right.map((s) => (
                      <li key={s.step_order}>{stepLabel(s.step_name)}: done safely, with no mistakes.</li>
                    ))}
                  </ul>
                )}
              </div>
              <div>
                <h4>What went wrong</h4>
                {wrong.length === 0 ? (
                  <p className="none">Nothing went wrong in this run.</p>
                ) : (
                  <ul className="wrong">
                    {wrong.map((s) => (
                      <li key={s.step_order}>
                        {wrongSentence(s)}
                        {s.mistake_reasons.length > 0 && (
                          <ul className="reasons">
                            {s.mistake_reasons.map((reason, i) => (
                              <li key={i}>{reason}</li>
                            ))}
                          </ul>
                        )}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </section>
        );
      }}
    </Status>
  );
}
