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
  /** Sayaç olarak işaretlenmiş int alanların adları — her biri için herkese açık bir increment ucu üretilir. */
  counters?: string[];
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

export interface RabbitMqTuningSpec {
  /** Bir outbox satırının kaç başarısız denemeden sonra dead işaretleneceği (varsayılan 10). */
  outboxMaxRetries?: number | null;
  /** İşlenmiş outbox satırlarının kaç gün sonra silineceği (varsayılan 7). */
  outboxRetentionDays?: number | null;
}

export interface ServiceSpec {
  service: string;
  database: string;
  entities: Record<string, EntitySpec>;
  auth?: ServiceAuthSpec | null;
  dockerPorts?: DockerPortsSpec | null;
  /** true ise tüm entity'ler ITenantEntity (TenantId) ile üretilir ve options.EnableMultiTenancy() çağrılır. */
  multiTenant?: boolean;
  /** RabbitMQ outbox/DLQ ince ayarları (opsiyonel) — yalnızca publishes/subscribes kullanan servislerde anlamlı. */
  rabbitMqTuning?: RabbitMqTuningSpec | null;
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
  /** Bu servisin spec.yaml'i diskte henüz yoksa true — port/authority önerisi yalnızca bu durumda uygulanır. */
  serviceIsNew: boolean;
  /** identity/auth.yaml diskte henüz yoksa true. */
  identityIsNew: boolean;
}

/** ServiceRegistry.cs'deki ServiceRegistryEntry'nin camelCase karşılığı — workspace'te daha önce üretilmiş servisler. */
export interface WorkspaceEntry {
  name: string;
  restPort?: number | null;
  grpcPort?: number | null;
  postgresPort?: number | null;
  entityCount?: number | null;
  isIdentity: boolean;
  authority?: string | null;
  audience?: string | null;
  protected: boolean;
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
