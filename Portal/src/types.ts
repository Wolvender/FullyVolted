// The shapes the API sends back (Api/app/models.py).
export interface RunSummary {
  run_id: string;
  trainee: string;
  started_at: string;
  steps_done: number;
  finished: boolean;
  total_seconds: number;
  total_mistakes: number;
  all_correct: boolean;
  slowest_step: string | null;
}

export interface Step {
  step_order: number;
  step_name: string;
  seconds: number;
  mistakes: number;
  correct: boolean;
  mistake_reasons: string[];
  created_at: string;
}

export interface RunDetail extends RunSummary {
  steps: Step[];
}

export interface StepStat {
  step_order: number;
  step_name: string;
  runs: number;
  avg_seconds: number;
  total_mistakes: number;
  avg_mistakes: number;
  percent_correct: number;
}

// Who the next run in the game is recorded for (null until someone signs in).
export interface ActiveTrainee {
  name: string | null;
  set_at: string | null;
}
