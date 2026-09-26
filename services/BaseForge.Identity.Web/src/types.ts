export interface MeResponse {
  id: string;
  email: string;
  fullName: string | null;
  avatarUrl: string | null;
  hasPassword: boolean;
  roles: string[];
  /** auth.yaml userProfile değerleri (camelCase anahtar). */
  profile: ProfileValues;
}

export interface AdminUserRow {
  id: string;
  email: string;
  fullName: string | null;
  avatarUrl: string | null;
  emailConfirmed: boolean;
  roles: string[];
  /** auth.yaml userProfile değerleri (camelCase anahtar). */
  profile: ProfileValues;
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

/** auth.yaml userProfile alanının metadata'sı (GET /api/account/profile-schema). */
export interface ProfileField {
  /** camelCase alan adı — `profile` sözlüğündeki anahtar. */
  name: string;
  /** auth.yaml spec tipi: string, text, int, long, short, decimal, double, float, bool, datetime, date, guid, uuid, enum. */
  type: string;
  nullable: boolean;
  /** true ise yalnızca admin panelinden düzenlenir (editableBy: admin). */
  adminOnly: boolean;
  inToken: boolean;
  maxLength: number | null;
  default: string | null;
  values: string[];
}

export type ProfileValues = Record<string, unknown>;
