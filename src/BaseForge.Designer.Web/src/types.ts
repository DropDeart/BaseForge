// C# BaseForge.CodeGen.Contracts sınıflarının TypeScript karşılıkları (camelCase).

export interface RelationSpec {
  kind: string; // one-to-many | many-to-one | one-to-one
  target: string;
  /** Opsiyonel ilişki (FK Guid?) — yalnızca many-to-one/one-to-one'da. */
  nullable?: boolean;
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
  /** Eski (geriye dönük) biçim: [AllowAnonymous] olacak action'lar. Designer düzenlemede 'access'e taşır. */
  anonymousActions?: string[];
  /** Action başına erişim kuralı (bkz. docs/ARCH.md §6.1). Belirtilmeyen action servis varsayılanını kullanır. */
  access?: Record<string, AccessRule>;
  /** Kaydın sahibini tutan guid alan; create'te token'dan damgalanır, 'owner' kuralı buna bakar. */
  ownerField?: string | null;
}

/** "anonymous" | "authenticated" | rol listesi (listede "owner" = kaydın sahibi). */
export type AccessRule = string | string[];

export interface ServiceAuthSpec {
  authority: string;
  audience: string;
  requireHttpsMetadata: boolean;
  protect: boolean;
  /** access'te belirtilmeyen action'lar için kural (varsayılan: authenticated). */
  defaultAccess?: AccessRule | null;
  /** Tüm rol ve sahiplik kurallarını otomatik geçen roller (örn. SuperAdmin). */
  superRoles?: string[];
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
  /** Seed edilecek ek roller — Admin ve User her zaman var. */
  roles?: string[];
  /** Kendi kendine kayıt (varsayılan kapalı). Kapalıyken dış sağlayıcıyla da yeni hesap açılmaz. */
  registration?: { enabled: boolean; defaultRole: string };
}

/** Identity'de her zaman var olan roller (AuthSpecValidator.AllRoles ile aynı). */
export const BUILT_IN_ROLES = ["Admin", "User"] as const;

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

/** UiDesignRunner.cs'deki LaunchResult'ın camelCase karşılığı — 'uidesign' tool'unu başlatma sonucu. */
export interface UiDesignLaunchResponse {
  success: boolean;
  url?: string | null;
  message: string;
}
