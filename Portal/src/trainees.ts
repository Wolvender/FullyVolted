import type { RunSummary, Step } from "./types";

// Pure functions, no React: grouping runs per person and judging a step.
export interface TraineeOverview {
  name: string;
  runs: RunSummary[];     // newest first
  finishedRuns: number;
  bestSeconds: number | null;  // fastest finished run
  totalMistakes: number;
}

export function groupByTrainee(runs: RunSummary[]): TraineeOverview[] {
  const byName = new Map<string, RunSummary[]>();
  for (const run of runs) byName.set(run.trainee, [...(byName.get(run.trainee) ?? []), run]);

  return [...byName.entries()]
    .map(([name, own]) => {
      const finished = own.filter((run) => run.finished);
      return {
        name,
        runs: [...own].sort((a, b) => b.started_at.localeCompare(a.started_at)),
        finishedRuns: finished.length,
        bestSeconds: finished.length ? Math.min(...finished.map((run) => run.total_seconds)) : null,
        totalMistakes: own.reduce((sum, run) => sum + run.total_mistakes, 0),
      };
    })
    .sort((a, b) => a.name.localeCompare(b.name));
}

// A step went right only if it was done safely AND without a single fault on the way.
export const wentRight = (step: Step) => step.correct && step.mistakes === 0;
