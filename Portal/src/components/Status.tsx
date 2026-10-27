import type { ReactNode } from "react";
import type { Loaded } from "../useLoad";

// Shows loading / error text, or hands the loaded data to `children`.
export function Status<T>({ loaded, children }: { loaded: Loaded<T>; children: (data: T) => ReactNode }) {
  if (loaded.state === "loading") return <p className="note">Loading results…</p>;
  if (loaded.state === "error")
    return (
      <p className="note note-error">
        Could not reach the results API. Start it from the Api folder with{" "}
        <code>.venv\Scripts\python -m uvicorn app.main:app</code>. ({loaded.message})
      </p>
    );
  return <>{children(loaded.data)}</>;
}
