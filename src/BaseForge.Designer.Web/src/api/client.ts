import type {
  AuthSpec,
  GenerateResponse,
  Meta,
  RunResponse,
  ServiceSpec,
  StopResponse,
  UiDesignLaunchResponse,
  WorkspaceEntry,
} from "../types";

async function json<T>(res: Response): Promise<T> {
  if (!res.ok && res.status !== 400) {
    throw new Error(`${res.status} ${res.statusText}`);
  }
  return (await res.json()) as T;
}

export const api = {
  meta: () => fetch("/api/meta").then((r) => json<Meta>(r)),

  workspace: () => fetch("/api/workspace").then((r) => json<WorkspaceEntry[]>(r)),

  spec: () => fetch("/api/spec").then((r) => json<{ service: ServiceSpec; auth: AuthSpec }>(r)),

  validate: (spec: ServiceSpec) =>
    fetch("/api/validate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(spec),
    }).then((r) => json<{ errors: string[] }>(r)),

  generateService: (spec: ServiceSpec, includeInSolution: boolean, output?: string) =>
    fetch("/api/generate/service", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ spec, output, includeInSolution }),
    }).then((r) => json<GenerateResponse | { errors: string[] }>(r)),

  generateIdentity: (spec: AuthSpec, includeInSolution: boolean) =>
    fetch("/api/generate/identity", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ spec, includeInSolution }),
    }).then((r) => json<GenerateResponse | { errors: string[] }>(r)),

  run: (output: string, restPort: number) =>
    fetch("/api/run", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ output, restPort }),
    }).then((r) => json<RunResponse>(r)),

  stop: (output: string) =>
    fetch("/api/stop", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ output }),
    }).then((r) => json<StopResponse>(r)),

  shutdown: () => fetch("/api/shutdown", { method: "POST" }),

  launchUiDesign: (services: string[]) =>
    fetch("/api/ui-design/launch", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ services }),
    }).then((r) => json<UiDesignLaunchResponse>(r)),
};
