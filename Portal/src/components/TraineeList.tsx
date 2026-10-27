import { duration, plural } from "../format";
import type { TraineeOverview } from "../trainees";

interface Props {
  trainees: TraineeOverview[];
  selected: string | null;
  onSelect: (name: string | null) => void;
}

export function TraineeList({ trainees, selected, onSelect }: Props) {
  return (
    <nav className="sidebar" aria-label="Trainees">
      <button className={`all ${selected === null ? "on" : ""}`} onClick={() => onSelect(null)}>
        Everyone
      </button>
      <ul>
        {trainees.map((t) => (
          <li key={t.name}>
            <button className={selected === t.name ? "on" : ""} onClick={() => onSelect(t.name)}>
              <span className="who">{t.name}</span>
              <span className="meta">
                {plural(t.runs.length, "run")}
                {t.bestSeconds !== null && `, best ${duration(t.bestSeconds)}`}
              </span>
              <span className={`mistakes ${t.totalMistakes === 0 ? "none" : ""}`}>
                {t.totalMistakes === 0 ? "no mistakes" : plural(t.totalMistakes, "mistake")}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </nav>
  );
}
