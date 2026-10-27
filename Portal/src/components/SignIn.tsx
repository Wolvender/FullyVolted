import { useState } from "react";

interface Props {
  knownNames: string[];
  onSignIn: (name: string) => Promise<void>;
  onSkip: () => void;
}

// Type your name to find your results, and to tell the game who the next run is for.
// No password: this is a training station, not a vault. Names already in the results are suggested.
export function SignIn({ knownNames, onSignIn, onSkip }: Props) {
  const [name, setName] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!name.trim()) return;
    setBusy(true);
    setProblem(null);
    try {
      await onSignIn(name);
    } catch (error) {
      setProblem((error as Error).message);
      setBusy(false);
    }
  };

  return (
    <form className="signin" onSubmit={submit}>
      <h2>Who is climbing?</h2>
      <p>
        Type your name. Your results show up under it, and the game saves your next run with this name. Already
        been here? Pick your name from the list to see your earlier runs.
      </p>
      <label htmlFor="trainee-name">Your name</label>
      <input
        id="trainee-name"
        list="known-names"
        value={name}
        maxLength={100}
        autoComplete="off"
        autoFocus
        onChange={(event) => setName(event.target.value)}
      />
      <datalist id="known-names">
        {knownNames.map((known) => (
          <option key={known} value={known} />
        ))}
      </datalist>
      <div className="signin-actions">
        <button type="submit" className="primary" disabled={busy || !name.trim()}>
          {busy ? "Signing in…" : "Continue"}
        </button>
        <button type="button" className="link" onClick={onSkip}>
          I am an instructor: show everyone
        </button>
      </div>
      {problem && <p className="note note-error">{problem}</p>}
    </form>
  );
}
