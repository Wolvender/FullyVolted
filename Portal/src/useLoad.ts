import { useEffect, useState } from "react";

export type Loaded<T> = { state: "loading" } | { state: "error"; message: string } | { state: "ready"; data: T };

// Runs a loader whenever `key` changes and reports loading / error / ready.
export function useLoad<T>(loader: () => Promise<T>, key: string): Loaded<T> {
  const [result, setResult] = useState<Loaded<T>>({ state: "loading" });

  useEffect(() => {
    let cancelled = false;
    setResult({ state: "loading" });
    loader().then(
      (data) => !cancelled && setResult({ state: "ready", data }),
      (error: Error) => !cancelled && setResult({ state: "error", message: error.message }),
    );
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  return result;
}
