import { api } from "../api";
import { duration, plural, stepLabel } from "../format";
import type { TraineeOverview } from "../trainees";
import { useLoad } from "../useLoad";
import { BarChart } from "./BarChart";
import { Status } from "./Status";

export function Overview({ trainees }: { trainees: TraineeOverview[] }) {
  const stats = useLoad(api.stepStats, "stats");
  const runs = trainees.flatMap((t) => t.runs);
  const finished = runs.filter((r) => r.finished);
  const averageTime = finished.length ? finished.reduce((s, r) => s + r.total_seconds, 0) / finished.length : null;

  return (
    <article>
      <h2>Everyone</h2>
      <dl className="facts">
        <div>
          <dt>Trainees</dt>
          <dd>{trainees.length}</dd>
        </div>
        <div>
          <dt>Runs</dt>
          <dd>{runs.length}</dd>
        </div>
        <div>
          <dt>Runs finished</dt>
          <dd>{finished.length}</dd>
        </div>
        <div>
          <dt>Average time for a finished run</dt>
          <dd>{averageTime === null ? "none yet" : duration(averageTime)}</dd>
        </div>
      </dl>

      <Status loaded={stats}>
        {(rows) => {
          const worst = rows.length ? rows.reduce((a, b) => (b.percent_correct < a.percent_correct ? b : a)) : null;
          return (
            <>
              <h3>Which steps go wrong</h3>
              {worst && (
                <p className="lede">
                  People struggle most with <strong>{stepLabel(worst.step_name).toLowerCase()}</strong>: only{" "}
                  {Math.round(worst.percent_correct)}% of attempts were done safely and without mistakes.
                </p>
              )}
              <BarChart
                title="Share of attempts done safely and without mistakes"
                fixedMax={100}
                axisLabel={(v) => `${v}%`}
                bars={rows.map((s) => ({
                  label: stepLabel(s.step_name),
                  value: s.percent_correct,
                  tone: s.percent_correct < 50 ? "wrong" : "right",
                  caption: `${Math.round(s.percent_correct)}%`,
                  detail: plural(s.total_mistakes, "mistake"),
                }))}
              />
              <p className="legend">
                <span className="key right" /> At least half went well <span className="key wrong" /> Fewer than half
                went well
              </p>

              <h3>How long each step takes</h3>
              <BarChart
                title="Average time per step"
                axisLabel={duration}
                bars={rows.map((s) => ({
                  label: stepLabel(s.step_name),
                  value: s.avg_seconds,
                  tone: "none",
                  caption: duration(s.avg_seconds),
                  detail: plural(s.runs, "attempt"),
                }))}
              />
            </>
          );
        }}
      </Status>
    </article>
  );
}
