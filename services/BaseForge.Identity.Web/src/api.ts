import type { AdminUserRow, MeResponse, ServiceRegistryEntry, ServiceStatusRow } from "./types";

async function req<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    ...init,
    headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
    credentials: "same-origin",
  });

  if (!res.ok) {
    let message = res.statusText;
    try {
      const body = (await res.json()) as { error?: string };
      message = body.error ?? message;
    } catch {
      // gövde yoksa/JSON değilse statusText'e düş
    }
    throw new Error(message);
  }

  // login/register/logout/addRole 200 dönüyor ama gövdesiz — res.json() boş gövdede SyntaxError fırlatır.
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  me: async (): Promise<MeResponse | null> => {
    const res = await fetch("/api/account/me", { credentials: "same-origin" });
    if (res.status === 401) {
      return null;
    }
    if (!res.ok) {
      throw new Error(res.statusText);
    }
    return (await res.json()) as MeResponse;
  },

  login: (email: string, password: string) =>
    req<void>("/api/account/login", { method: "POST", body: JSON.stringify({ email, password }) }),

  register: (fullName: string, email: string, password: string) =>
    req<void>("/api/account/register", {
      method: "POST",
      body: JSON.stringify({ fullName, email, password }),
    }),

  logout: () => req<void>("/api/account/logout", { method: "POST" }),

  providers: () => req<string[]>("/api/account/providers"),

  externalLoginUrl: (provider: string, returnUrl: string) =>
    `/api/account/external/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`,

  listUsers: () => req<AdminUserRow[]>("/api/admin/users"),

  addUser: (fullName: string, email: string) =>
    req<AdminUserRow>("/api/admin/users", { method: "POST", body: JSON.stringify({ fullName, email }) }),

  deleteUser: (id: string) => req<void>(`/api/admin/users/${id}`, { method: "DELETE" }),

  addRole: (id: string, role: string) =>
    req<void>(`/api/admin/users/${id}/roles`, { method: "POST", body: JSON.stringify({ role }) }),

  removeRole: (id: string, role: string) =>
    req<void>(`/api/admin/users/${id}/roles/${encodeURIComponent(role)}`, { method: "DELETE" }),

  roles: () => req<string[]>("/api/admin/roles"),

  updateProfile: (fullName: string) =>
    req<void>("/api/account/profile", { method: "PUT", body: JSON.stringify({ fullName }) }),

  changePassword: (currentPassword: string | null, newPassword: string) =>
    req<void>("/api/account/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword }),
    }),

  uploadAvatar: async (file: File): Promise<{ avatarUrl: string }> => {
    const form = new FormData();
    form.append("file", file);
    const res = await fetch("/api/account/avatar", { method: "POST", body: form, credentials: "same-origin" });
    const text = await res.text();
    const body = text ? JSON.parse(text) : undefined;
    if (!res.ok) {
      throw new Error((body as { error?: string } | undefined)?.error ?? res.statusText);
    }
    return body as { avatarUrl: string };
  },

  services: async (): Promise<ServiceRegistryEntry[]> => {
    const res = await fetch("/services.json", { credentials: "same-origin" });
    if (!res.ok) {
      return [];
    }
    const data = (await res.json()) as { services?: ServiceRegistryEntry[] };
    return data.services ?? [];
  },

  serviceStatus: async (): Promise<ServiceStatusRow[]> => {
    const res = await fetch("/api/services/status", { credentials: "same-origin" });
    if (!res.ok) {
      return [];
    }
    return (await res.json()) as ServiceStatusRow[];
  },
};
