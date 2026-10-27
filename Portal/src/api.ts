import type { ActiveTrainee, RunDetail, RunSummary, StepStat } from "./types";

const baseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:8000";

// The portal never needs the API key: it reads, and the only thing it writes is who has signed in.
async function get<T>(path: string): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`);
  if (!response.ok) throw new Error(`The API answered ${response.status} for ${path}`);
  return response.json() as Promise<T>;
}

export const api = {
  runs: () => get<RunSummary[]>("/runs"),
  run: (id: string) => get<RunDetail>(`/runs/${id}`),
  stepStats: () => get<StepStat[]>("/stats/steps"),
  // Tells the game who the next run belongs to.
  signIn: async (name: string): Promise<ActiveTrainee> => {
    const response = await fetch(`${baseUrl}/active-trainee`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name }),
    });
    if (!response.ok) throw new Error(`The API answered ${response.status} when signing in`);
    return response.json() as Promise<ActiveTrainee>;
  },
};
