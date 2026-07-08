// C# BaseForge.CodeGen.Spec sınıflarının TypeScript karşılıkları (camelCase).

export interface RelationSpec {
  kind: string; // one-to-many | many-to-one | one-to-one
  target: string;
}

export interface ExternalRefSpec {
  target: string; // servis/Entity
  store: string; // ID alan adı
  via: string; // grpc | event
}

export interface PropSpec {
  type: string;
  nullable?: boolean;
  maxLength?: number | null; // yalnızca string/text tipinde anlamlı
  default?: string | null; // yalnızca C# tarafı (in-memory initializer); datetime/date/guid'de desteklenmez
}

export interface EntitySpec {
  props: Record<string, PropSpec>; // ad -> tanım
  relations?: Record<string, RelationSpec>;
  externalRefs?: Record<string, ExternalRefSpec>;
}

export interface ServiceAuthSpec {
  authority: string;
  audience: string;
  requireHttpsMetadata: boolean;
  protect: boolean;
}

export interface DockerPortsSpec {
  rest?: number | null;
  grpc?: number | null;
  postgres?: number | null;
}

export interface ServiceSpec {
  service: string;
  database: string;
  entities: Record<string, EntitySpec>;
  auth?: ServiceAuthSpec | null;
  dockerPorts?: DockerPortsSpec | null;
}

export interface ProviderSpec {
  clientId: string;
  clientSecret: string;
}

export interface ProvidersSpec {
  google?: ProviderSpec | null;
  gitHub?: ProviderSpec | null;
  microsoft?: ProviderSpec | null;
  facebook?: ProviderSpec | null;
}

export interface SeedAdminSpec {
  email: string;
  password: string;
}

export interface AuthScopeSpec {
  name: string;
  resource?: string | null;
}

export interface AuthClientSpec {
  clientId: string;
  secret?: string | null;
  public: boolean;
  grants: string[];
  scopes: string[];
  redirectUris: string[];
}

export interface AuthSpec {
  service: string;
  database: string;
  issuer: string;
  signing?: { certificatePath?: string | null; certificatePassword?: string | null } | null;
  scopes: AuthScopeSpec[];
  clients: AuthClientSpec[];
  seedAdmin?: SeedAdminSpec | null;
  providers: ProvidersSpec;
  dockerPorts?: DockerPortsSpec | null;
}

export interface Meta {
  types: string[];
  relationKinds: string[];
  via: string[];
  providers: string[];
}

export interface GenerateResponse {
  output: string;
  files: string[];
  buildSuccess: boolean;
  buildOutput: string;
}

export interface RunResponse {
  success: boolean;
  url: string;
  dockerOutput: string;
}

export interface StopResponse {
  stopped: boolean;
}
