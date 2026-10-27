import type { RunDetail, RunSummary, StepStat } from "./types";

const baseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:8000";

// The portal only reads, so it never needs the API key.
async function get<T>(path: string): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`);
  if (!response.ok) throw new Error(`The API answered ${response.status} for ${path}`);
  return response.json() as Promise<T>;
}

export const api = {
  runs: () => get<RunSummary[]>("/runs"),
  run: (id: string) => get<RunDetail>(`/runs/${id}`),
  stepStats: () => get<StepStat[]>("/stats/steps"),
};
