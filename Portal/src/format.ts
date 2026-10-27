// Plain words for what the trainee is doing, so anyone reading the results understands them.
const stepLabels: Record<string, string> = {
  PpeCheck: "Putting on the helmet",
  ClimbToMiddle: "Climbing to the middle platform",
  ClimbToTop: "Climbing to the top",
  DeEnergize: "Switching off the power",
  DescendToBottom: "Climbing back down",
};

export const stepLabel = (name: string) => stepLabels[name] ?? name;

export function duration(seconds: number): string {
  const whole = Math.round(seconds);
  const minutes = Math.floor(whole / 60);
  const rest = String(whole % 60).padStart(2, "0");
  return minutes > 0 ? `${minutes}:${rest} min` : `${whole} s`;
}

export function when(iso: string): string {
  // The API sends the server's local time without a zone, so show it as it is.
  return new Date(iso).toLocaleString("en-GB", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}

export const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? "" : "s"}`;

// Rounds a chart's top value up to something with tidy tick marks, e.g. 83 -> 90 with ticks every 30.
export function niceScale(max: number, steps = 4): { top: number; ticks: number[] } {
  const rough = Math.max(max, 1) / steps;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const unit = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((u) => u >= rough) ?? rough;
  const top = unit * Math.ceil(Math.max(max, 1) / unit);
  return { top, ticks: Array.from({ length: Math.round(top / unit) + 1 }, (_, i) => i * unit) };
}

// The procedure in order, so a run that stopped early still shows the steps it never reached.
export const ALL_STEPS = ["PpeCheck", "ClimbToMiddle", "ClimbToTop", "DeEnergize", "DescendToBottom"];
