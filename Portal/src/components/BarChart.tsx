import { niceScale } from "../format";

export interface Bar {
  label: string;      // under the bar
  value: number;      // height
  tone: "right" | "wrong" | "none";
  caption: string;    // above the bar, e.g. "1:12 min"
  detail?: string;    // second line above the bar, e.g. "2 mistakes"
}

interface Props {
  title: string;
  bars: Bar[];
  axisLabel: (value: number) => string;
  fixedMax?: number;  // e.g. 100 for percentages; otherwise the scale fits the data
}

// A plain vertical bar chart made of HTML/CSS so long labels wrap and it shrinks on small screens.
export function BarChart({ title, bars, axisLabel, fixedMax }: Props) {
  const { top, ticks } = fixedMax
    ? { top: fixedMax, ticks: [0, fixedMax / 4, fixedMax / 2, (fixedMax * 3) / 4, fixedMax] }
    : niceScale(Math.max(...bars.map((b) => b.value), 1));

  return (
    <figure className="chart">
      <figcaption>{title}</figcaption>
      <div className="chart-body">
        <div className="axis" aria-hidden="true">
          {ticks.map((tick) => (
            <span key={tick} style={{ bottom: `${(tick / top) * 100}%` }}>
              {axisLabel(tick)}
            </span>
          ))}
        </div>
        <div className="plot">
          {ticks.map((tick) => (
            <i key={tick} className="grid" style={{ bottom: `${(tick / top) * 100}%` }} aria-hidden="true" />
          ))}
          <ol>
            {bars.map((bar) => (
              <li key={bar.label} className={bar.tone}>
                <span className="figure">
                  <b>{bar.caption}</b>
                  {bar.detail && <small>{bar.detail}</small>}
                </span>
                <span className="column" style={{ height: `${(bar.value / top) * 100}%` }} />
              </li>
            ))}
          </ol>
        </div>
        <ol className="labels">
          {bars.map((bar) => (
            <li key={bar.label}>{bar.label}</li>
          ))}
        </ol>
      </div>
    </figure>
  );
}
