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
  /** List sorgusu sayfalı mı (PagedResult<Dto>)? Varsayılan true — false ise bare liste. */
  paginated?: boolean;
  /** paginated=true iken SortBy dikkate alınsın mı? Varsayılan true. */
  sortable?: boolean;
  /** paginated=true iken Search (string alanlarda arama) dikkate alınsın mı? Varsayılan true. */
  searchable?: boolean;
  /** true ise Update/Delete hiç üretilmez — yalnızca Create/GetById/List kalır (append-only). */
  appendOnly?: boolean;
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
  /** true ise tüm entity'ler ITenantEntity (TenantId) ile üretilir ve options.EnableMultiTenancy() çağrılır. */
  multiTenant?: boolean;
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
  solutionFound: boolean;
  solutionName?: string | null;
}

export interface GenerateResponse {
  output: string;
  files: string[];
  buildSuccess: boolean;
  buildOutput: string;
  solutionMessage?: string | null;
}

export interface RunResponse {
  success: boolean;
  url: string;
  dockerOutput: string;
}

export interface StopResponse {
  stopped: boolean;
}
