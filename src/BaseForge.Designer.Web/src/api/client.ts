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
  if (!res.ok && res.status !== 400 && res.status !== 409) {
    throw new Error(`${res.status} ${res.statusText}`);
  }
  return (await res.json()) as T;
}

export const api = {
  meta: () => fetch("/api/meta").then((r) => json<Meta>(r)),

  workspace: () => fetch("/api/workspace").then((r) => json<WorkspaceEntry[]>(r)),

  spec: () =>
    fetch("/api/spec").then((r) =>
      json<{ service: ServiceSpec; auth: AuthSpec; serviceHash: string | null; authHash: string | null }>(r),
    ),

  validate: (spec: ServiceSpec) =>
    fetch("/api/validate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(spec),
    }).then((r) => json<{ errors: string[] }>(r)),

  /** baseHash: formun açıldığı andaki spec.yaml; dosya değiştiyse sunucu 409 (stale) döner, force ile üzerine yazılır. */
  generateService: (spec: ServiceSpec, includeInSolution: boolean, baseHash: string | null, force = false) =>
    fetch("/api/generate/service", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ spec, includeInSolution, baseHash, force }),
    }).then((r) => json<GenerateResponse | { errors: string[]; stale?: boolean }>(r)),

  generateIdentity: (spec: AuthSpec, includeInSolution: boolean, baseHash: string | null, force = false) =>
    fetch("/api/generate/identity", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ spec, includeInSolution, baseHash, force }),
    }).then((r) => json<GenerateResponse | { errors: string[]; stale?: boolean }>(r)),

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
  /** Sekmenin açık olduğunu bildirir; kesilirse sunucu birkaç dakika sonra kendini kapatır. */
  heartbeat: () => fetch("/api/heartbeat", { method: "POST" }).catch(() => undefined),

  launchUiDesign: (services: string[]) =>
    fetch("/api/ui-design/launch", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ services }),
    }).then((r) => json<UiDesignLaunchResponse>(r)),
};
