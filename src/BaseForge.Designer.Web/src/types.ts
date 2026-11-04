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
  /** Referans opsiyonel mi (Guid?)? */
  nullable?: boolean;
}

export interface PropSpec {
  type: string;
  nullable?: boolean;
  maxLength?: number | null; // yalnızca string/text tipinde anlamlı
  default?: string | null; // yalnızca C# tarafı (in-memory initializer); datetime/date/guid'de desteklenmez
  /** Yalnızca enum tipinde: izinli değerler (C# tanımlayıcısı, örn. Draft, InReview). */
  values?: string[];
  /** Yalnızca auth.yaml userProfile: self (varsayılan, kullanıcı düzenler) | admin (yalnızca admin paneli). */
  editableBy?: string | null;
  /** Yalnızca auth.yaml userProfile: JWT claim'i olarak token'a eklensin mi. */
  inToken?: boolean;
  /** Sunucu yönetir: üretilen Create/Update komutlarına girmez, istemciden yazılamaz (ARCH §6.4). */
  serverManaged?: boolean;
  /** Sayısal alanlarda en küçük / en büyük değer (dahil). */
  min?: number | null;
  max?: number | null;
  /** string/text alanlarda en az uzunluk ve düzenli ifade. */
  minLength?: number | null;
  pattern?: string | null;
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
  /** Liste ucunda eşitlik filtresi olarak açılan alanlar (?status=Active). Yalnızca sayfalı listelerde. */
  filterable?: string[];
  /** Aralık filtreleri (?alanFrom=&alanTo=). */
  rangeFilters?: string[];
  /** Çoklu değer filtreleri (?alanIn=A&alanIn=B). */
  inFilters?: string[];
  /** sortBy verilmezse sıralama (örn. "PlannedAt asc"). */
  defaultSort?: string | null;
  /** Benzersiz alan grupları (kiracılı serviste kiracı içinde). */
  unique?: string[][];
  /** SortBy beyaz listesi; boş = tüm skaler alanlar + CreatedAt/UpdatedAt (ARCH §6.2). */
  sortFields?: string[];
  /** Search'ün arandığı string/text alanlar; boş = hepsi (ARCH §6.2). */
  searchFields?: string[];
  /** HTTP okumalarında görünürlük filtresi (örn. taslakları gizle). Bkz. docs/ARCH.md §6.2. */
  readFilter?: ReadFilterSpec | null;
}

export interface ReadFilterSpec {
  /** alan → değer eşitlikleri (VE), örn. { IsPublished: "true" } */
  where: Record<string, string>;
  bypassRoles?: string[];
  bypassOwner?: boolean;
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
  /** Multi-tenant serviste kiracısız token'la tüm kiracıları okuyabilen roller (yalnız GET). */
  crossTenantRoles?: string[];
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
  registration?: { enabled: boolean; defaultRole: string; selfAssignableRoles?: string[] };
  /** Şirketler (kiracılar) — 0.6.4. */
  multiTenancy?: MultiTenancySpec;
  /** Tek kullanımlık kod (SMS/e-posta) — 0.6.4. */
  otp?: OtpSpec;
  /** E-posta/SMS göndericileri (SMTP parolası .env'de) — 0.6.4. */
  messaging?: MessagingSpec;
  /** Şifremi unuttum — 0.6.4. */
  passwordReset?: PasswordResetSpec;
  /** Access token ömrü (dk). */
  accessTokenLifetimeMinutes?: number;
  /** Kullanıcıya eklenecek domain alanları (docs/ARCH.md §6.3). */
  userProfile?: { props: Record<string, PropSpec> } | null;
}

export interface MultiTenancySpec {
  enabled: boolean;
  adminRole: string;
  memberRoles: string[];
  requireApproval: boolean;
  approverRoles: string[];
}

export interface OtpSpec {
  enabled: boolean;
  codeLength: number;
  lifetimeSeconds: number;
  maxAttempts: number;
  resendCooldownSeconds: number;
  maxPerHour: number;
  defaultCountryCode: string;
}

export interface MessagingSpec {
  emailProvider: string;
  smsProvider: string;
  smtp: { host: string; port: number; username?: string | null; fromAddress: string; fromName: string };
}

export interface PasswordResetSpec {
  enabled: boolean;
  resetUrl: string;
  tokenLifetimeHours: number;
  cooldownSeconds: number;
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
