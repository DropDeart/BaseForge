export interface MeResponse {
  id: string;
  email: string;
  fullName: string | null;
  avatarUrl: string | null;
  hasPassword: boolean;
  roles: string[];
}

export interface AdminUserRow {
  id: string;
  email: string;
  fullName: string | null;
  avatarUrl: string | null;
  emailConfirmed: boolean;
  roles: string[];
}

export interface ServiceRegistryEntry {
  name: string;
  restPort: number | null;
  grpcPort: number | null;
  entityCount: number | null;
  isIdentity: boolean;
  authority: string | null;
  audience: string | null;
  protected: boolean;
}

export interface ServiceStatusRow {
  name: string;
  healthy: boolean;
}
