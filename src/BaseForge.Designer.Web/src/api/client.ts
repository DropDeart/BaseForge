import type { AuthSpec, GenerateResponse, Meta, ServiceSpec } from "../types";

async function json<T>(res: Response): Promise<T> {
  if (!res.ok && res.status !== 400) {
    throw new Error(`${res.status} ${res.statusText}`);
  }
  return (await res.json()) as T;
}

export const api = {
  meta: () => fetch("/api/meta").then((r) => json<Meta>(r)),

  spec: () => fetch("/api/spec").then((r) => json<{ service: ServiceSpec; auth: AuthSpec }>(r)),

  validate: (spec: ServiceSpec) =>
    fetch("/api/validate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(spec),
    }).then((r) => json<{ errors: string[] }>(r)),

  generateService: (spec: ServiceSpec, output?: string) =>
    fetch("/api/generate/service", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ spec, output }),
    }).then((r) => json<GenerateResponse | { errors: string[] }>(r)),

  generateIdentity: (spec: AuthSpec) =>
    fetch("/api/generate/identity", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(spec),
    }).then((r) => json<GenerateResponse | { errors: string[] }>(r)),

  shutdown: () => fetch("/api/shutdown", { method: "POST" }),
};
