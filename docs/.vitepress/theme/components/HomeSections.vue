<script setup lang="ts">
import { computed, ref } from "vue";
import { useData, withBase } from "vitepress";

const { lang } = useData();

const copy = {
  en: {
    showcaseEyebrow: "Spec in, service out",
    showcaseTitle: "Describe it once. Get a service that builds, runs and scales.",
    showcaseLead:
      "Write a few lines of YAML — or click them together in the Designer. BaseForge generates plain, readable C# that uses the library, then builds it and starts it with Docker.",
    terminal: [
      { t: "$ baseforge new-service --spec orders.yaml --yes", c: "cmd" },
      { t: "✓ spec validated · 2 entities · 1 external reference", c: "ok" },
      { t: "✓ ER diagram → orders/er.drawio", c: "ok" },
      { t: "✓ 38 files generated → ./orders", c: "ok" },
      { t: "✓ gRPC client → products/Product (rich, 5 fields)", c: "ok" },
      { t: "✓ dotnet build · 0 warnings · 0 errors", c: "ok" },
      { t: "$ docker compose up --build -d --wait", c: "cmd" },
      { t: "● orders  http://localhost:8080/scalar/v1  healthy", c: "live" },
    ],
    stepsTitle: "Three steps from idea to API",
    steps: [
      { n: "01", title: "Design", text: "Model entities, relations, access rules and Identity visually — with a live ER diagram — or in plain YAML." },
      { n: "02", title: "Generate", text: "Get controllers, CQRS handlers, EF Core entities, DTOs, validation, gRPC protos, events, Dockerfile and compose." },
      { n: "03", title: "Run", text: "One click builds and starts the whole stack. Iterate with baseforge update — your spec stays the source of truth." },
    ],
    wiredTitle: "Everything a production service needs — already wired",
    wired: [
      "REST controllers + Scalar API docs",
      "CQRS commands & queries on MediatR",
      "EF Core entities, audit fields, soft delete",
      "Dapper for heavy read queries",
      "Role & ownership authorization",
      "gRPC clients and servers between services",
      "RabbitMQ events with transactional outbox",
      "Inbox idempotency & dead-letter queues",
      "Serilog + Grafana Loki, correlation ids",
      "/health checks & Docker healthchecks",
      "Multi-tenancy & append-only entities",
      "Dockerfile + docker-compose per service",
    ],
    stackTitle: "Built on the tools you already trust",
    ctaTitle: "Forge your first service in five minutes",
    ctaText: "Install the CLI, open the Designer, press Generate.",
    ctaPrimary: "Get Started",
    ctaSecondary: "Read the architecture",
    copied: "Copied!",
    copyLabel: "Copy",
  },
  tr: {
    showcaseEyebrow: "Spec'i ver, servisi al",
    showcaseTitle: "Bir kez tarif et. Derlenen, çalışan ve ölçeklenen bir servis al.",
    showcaseLead:
      "Birkaç satır YAML yaz — ya da Designer'da tıklayarak oluştur. BaseForge kütüphaneyi kullanan sade, okunabilir C# üretir, derler ve Docker ile ayağa kaldırır.",
    terminal: [
      { t: "$ baseforge new-service --spec orders.yaml --yes", c: "cmd" },
      { t: "✓ spec doğrulandı · 2 entity · 1 dış referans", c: "ok" },
      { t: "✓ ER diyagramı → orders/er.drawio", c: "ok" },
      { t: "✓ 38 dosya üretildi → ./orders", c: "ok" },
      { t: "✓ gRPC istemcisi → products/Product (zengin, 5 alan)", c: "ok" },
      { t: "✓ dotnet build · 0 uyarı · 0 hata", c: "ok" },
      { t: "$ docker compose up --build -d --wait", c: "cmd" },
      { t: "● orders  http://localhost:8080/scalar/v1  healthy", c: "live" },
    ],
    stepsTitle: "Fikirden API'ye üç adım",
    steps: [
      { n: "01", title: "Tasarla", text: "Entity'leri, ilişkileri, erişim kurallarını ve Identity'yi görsel olarak — canlı ER diyagramıyla — ya da düz YAML ile modelle." },
      { n: "02", title: "Üret", text: "Controller'lar, CQRS handler'ları, EF Core entity'leri, DTO'lar, validasyon, gRPC proto'ları, event'ler, Dockerfile ve compose." },
      { n: "03", title: "Çalıştır", text: "Tek tıkla tüm stack derlenir ve ayağa kalkar. baseforge update ile devam et — spec her zaman tek doğru kaynak." },
    ],
    wiredTitle: "Production servisinin ihtiyacı olan her şey — hazır bağlı",
    wired: [
      "REST controller'lar + Scalar API dokümanı",
      "MediatR üzerinde CQRS komut ve sorguları",
      "EF Core entity'leri, audit alanları, soft delete",
      "Ağır okuma sorguları için Dapper",
      "Rol ve sahiplik tabanlı yetkilendirme",
      "Servisler arası gRPC istemci ve sunucuları",
      "Transactional outbox ile RabbitMQ event'leri",
      "Inbox idempotency ve dead-letter kuyrukları",
      "Serilog + Grafana Loki, correlation id",
      "/health kontrolleri ve Docker healthcheck",
      "Multi-tenancy ve append-only entity'ler",
      "Her servis için Dockerfile + docker-compose",
    ],
    stackTitle: "Zaten güvendiğin araçların üzerine kurulu",
    ctaTitle: "İlk servisini beş dakikada üret",
    ctaText: "CLI'yı kur, Designer'ı aç, Üret'e bas.",
    ctaPrimary: "Başlarken",
    ctaSecondary: "Mimariyi oku",
    copied: "Kopyalandı!",
    copyLabel: "Kopyala",
  },
};

const t = computed(() => (lang.value === "tr" ? copy.tr : copy.en));
const prefix = computed(() => (lang.value === "tr" ? "/tr" : ""));

const stack = [
  ".NET 10",
  "ASP.NET Core",
  "EF Core 10",
  "Dapper",
  "MediatR",
  "PostgreSQL",
  "gRPC",
  "RabbitMQ",
  "OpenIddict",
  "YARP",
  "Serilog",
  "Grafana Loki",
  "Docker",
  "React + Vite",
];

const installCmd = "dotnet tool install -g BaseForge.CodeGen --prerelease";
const copied = ref(false);
async function copyInstall() {
  try {
    await navigator.clipboard.writeText(installCmd);
    copied.value = true;
    setTimeout(() => (copied.value = false), 1600);
  } catch {
    // Clipboard may be unavailable (insecure context); the command is still visible to copy by hand.
  }
}
</script>

<template>
  <div class="bf-home">
    <!-- Showcase: YAML → terminal -->
    <section class="bf-section">
      <div class="bf-head">
        <span class="bf-eyebrow">{{ t.showcaseEyebrow }}</span>
        <h2 class="bf-title">{{ t.showcaseTitle }}</h2>
        <p class="bf-lead">{{ t.showcaseLead }}</p>
      </div>

      <div class="bf-showcase">
        <div class="bf-window">
          <div class="bf-window-bar">
            <span class="dot r" /><span class="dot y" /><span class="dot g" />
            <span class="bf-window-title">orders.yaml</span>
          </div>
<pre class="bf-code"><span class="k">service</span>: <span class="s">orders</span>
<span class="k">database</span>: <span class="s">orders_db</span>
<span class="k">auth</span>: { <span class="k">protect</span>: <span class="b">true</span> }

<span class="k">entities</span>:
  <span class="e">Order</span>:
    <span class="k">props</span>:
      <span class="k">Status</span>: { <span class="k">type</span>: <span class="s">enum</span>, <span class="k">values</span>: [<span class="s">Draft</span>, <span class="s">Paid</span>] }
      <span class="k">Total</span>: <span class="s">decimal</span>
      <span class="k">BuyerId</span>: <span class="s">guid</span>
    <span class="k">ownerField</span>: <span class="s">BuyerId</span>
    <span class="k">access</span>:
      <span class="k">list</span>: [<span class="s">Admin</span>, <span class="s">owner</span>]
    <span class="k">publishes</span>: [<span class="s">created</span>]

  <span class="e">OrderLine</span>:
    <span class="k">relations</span>:
      <span class="k">order</span>: { <span class="k">kind</span>: <span class="s">many-to-one</span>, <span class="k">target</span>: <span class="s">Order</span> }
    <span class="k">externalRefs</span>:
      <span class="k">product</span>: { <span class="k">target</span>: <span class="s">products/Product</span>, <span class="k">via</span>: <span class="s">grpc</span> }</pre>
        </div>

        <div class="bf-arrow" aria-hidden="true">
          <svg viewBox="0 0 48 48"><path d="M8 24h28m-10-10 10 10-10 10" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" /></svg>
        </div>

        <div class="bf-window bf-terminal">
          <div class="bf-window-bar">
            <span class="dot r" /><span class="dot y" /><span class="dot g" />
            <span class="bf-window-title">terminal</span>
          </div>
          <div class="bf-term">
            <div
              v-for="(line, i) in t.terminal"
              :key="lang + i"
              class="bf-line"
              :class="line.c"
              :style="{ animationDelay: `${0.25 + i * 0.35}s` }"
            >{{ line.t }}</div>
          </div>
        </div>
      </div>
    </section>

    <!-- Steps -->
    <section class="bf-section">
      <h2 class="bf-title center">{{ t.stepsTitle }}</h2>
      <div class="bf-steps">
        <div v-for="s in t.steps" :key="s.n" class="bf-step">
          <span class="bf-step-n">{{ s.n }}</span>
          <h3>{{ s.title }}</h3>
          <p>{{ s.text }}</p>
        </div>
      </div>
    </section>

    <!-- What gets wired -->
    <section class="bf-section">
      <h2 class="bf-title center">{{ t.wiredTitle }}</h2>
      <div class="bf-wired">
        <div v-for="w in t.wired" :key="w" class="bf-wired-item">
          <svg viewBox="0 0 20 20" aria-hidden="true"><path d="M5 10.5 8.5 14 15 6.5" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" /></svg>
          <span>{{ w }}</span>
        </div>
      </div>
    </section>

    <!-- Stack -->
    <section class="bf-section">
      <h2 class="bf-title center small">{{ t.stackTitle }}</h2>
      <div class="bf-stack">
        <span v-for="s in stack" :key="s" class="bf-chip">{{ s }}</span>
      </div>
    </section>

    <!-- CTA -->
    <section class="bf-section">
      <div class="bf-cta">
        <h2>{{ t.ctaTitle }}</h2>
        <p>{{ t.ctaText }}</p>
        <button class="bf-install" type="button" @click="copyInstall" :title="t.copyLabel">
          <span class="prompt">$</span>
          <code>{{ installCmd }}</code>
          <span class="bf-copy">{{ copied ? t.copied : t.copyLabel }}</span>
        </button>
        <div class="bf-cta-actions">
          <a class="bf-btn primary" :href="withBase(`${prefix}/guide/getting-started`)">{{ t.ctaPrimary }}</a>
          <a class="bf-btn" :href="withBase(`${prefix}/architecture`)">{{ t.ctaSecondary }}</a>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.bf-home {
  padding: 0 24px 96px;
}

@media (min-width: 640px) {
  .bf-home { padding: 0 48px 112px; }
}

@media (min-width: 960px) {
  .bf-home { padding: 0 64px 128px; }
}

.bf-section {
  max-width: 1152px;
  margin: 0 auto;
  padding-top: 88px;
}

.bf-head {
  max-width: 760px;
  margin: 0 auto 40px;
  text-align: center;
}

.bf-eyebrow {
  display: inline-block;
  padding: 4px 12px;
  border-radius: 999px;
  background: var(--vp-c-brand-soft);
  color: var(--vp-c-brand-1);
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.02em;
}

.bf-title {
  margin: 14px 0 0;
  font-size: 28px;
  line-height: 1.25;
  font-weight: 700;
  letter-spacing: -0.02em;
  border: none;
  padding: 0;
}

.bf-title.center {
  text-align: center;
  margin-bottom: 36px;
}

.bf-title.small {
  font-size: 22px;
}

@media (min-width: 768px) {
  .bf-title { font-size: 34px; }
  .bf-title.small { font-size: 24px; }
}

.bf-lead {
  margin: 16px 0 0;
  color: var(--vp-c-text-2);
  font-size: 17px;
  line-height: 1.65;
}

/* ---------- showcase ---------- */
.bf-showcase {
  display: grid;
  grid-template-columns: 1fr;
  gap: 20px;
  align-items: stretch;
}

@media (min-width: 960px) {
  .bf-showcase { grid-template-columns: 1fr 48px 1fr; }
}

.bf-window {
  min-width: 0;
  border-radius: 14px;
  overflow: hidden;
  background: var(--bf-code-bg);
  border: 1px solid rgba(255, 255, 255, 0.06);
  box-shadow: 0 24px 60px -24px rgba(60, 20, 5, 0.55);
}

.bf-window-bar {
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 11px 14px;
  background: rgba(255, 255, 255, 0.04);
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.dot { width: 11px; height: 11px; border-radius: 50%; }
.dot.r { background: #ff5f57; }
.dot.y { background: #febc2e; }
.dot.g { background: #28c840; }

.bf-window-title {
  margin-left: 8px;
  color: #8b9bb4;
  font-family: var(--vp-font-family-mono);
  font-size: 12px;
}

.bf-code {
  margin: 0;
  padding: 18px 20px;
  overflow-x: auto;
  color: var(--bf-code-text);
  font-family: var(--vp-font-family-mono);
  font-size: 13px;
  line-height: 1.7;
  background: transparent;
}

.bf-code .k { color: #7dd3fc; }
.bf-code .s { color: #86efac; }
.bf-code .b { color: #fbbf24; }
.bf-code .e { color: #f0abfc; font-weight: 600; }

.bf-arrow {
  display: none;
  align-self: center;
  color: var(--vp-c-brand-1);
}

@media (min-width: 960px) {
  .bf-arrow { display: block; }
}

.bf-arrow svg { width: 48px; height: 48px; }

.bf-term {
  padding: 18px 20px;
  font-family: var(--vp-font-family-mono);
  font-size: 13px;
  line-height: 1.9;
  overflow-x: auto;
}

.bf-line {
  white-space: pre;
  opacity: 0;
  transform: translateY(4px);
  animation: bf-in 0.4s ease forwards;
}

.bf-line.cmd { color: #e2e8f0; }
.bf-line.ok { color: #86efac; }
.bf-line.live {
  color: #fb923c;
  font-weight: 600;
}

@keyframes bf-in {
  to { opacity: 1; transform: none; }
}

/* ---------- steps ---------- */
.bf-steps {
  display: grid;
  grid-template-columns: 1fr;
  gap: 20px;
}

@media (min-width: 768px) {
  .bf-steps { grid-template-columns: repeat(3, 1fr); }
}

.bf-step {
  position: relative;
  padding: 28px 24px 24px;
  border-radius: 14px;
  border: 1px solid var(--bf-card-border);
  background: var(--bf-card);
}

.bf-step-n {
  font-family: var(--vp-font-family-mono);
  font-size: 13px;
  font-weight: 700;
  background: var(--bf-grad);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.bf-step h3 {
  margin: 6px 0 8px;
  font-size: 20px;
  font-weight: 700;
}

.bf-step p {
  margin: 0;
  color: var(--vp-c-text-2);
  font-size: 15px;
  line-height: 1.6;
}

/* ---------- wired ---------- */
.bf-wired {
  display: grid;
  grid-template-columns: 1fr;
  gap: 12px;
}

@media (min-width: 640px) {
  .bf-wired { grid-template-columns: repeat(2, 1fr); }
}

@media (min-width: 960px) {
  .bf-wired { grid-template-columns: repeat(3, 1fr); }
}

.bf-wired-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 16px;
  border-radius: 12px;
  background: var(--vp-c-bg-soft);
  font-size: 15px;
}

.bf-wired-item svg {
  flex: none;
  width: 20px;
  height: 20px;
  padding: 2px;
  border-radius: 6px;
  color: var(--vp-c-brand-1);
  background: var(--vp-c-brand-soft);
}

/* ---------- stack ---------- */
.bf-stack {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 10px;
  max-width: 860px;
  margin: 0 auto;
}

.bf-chip {
  padding: 7px 14px;
  border-radius: 999px;
  border: 1px solid var(--vp-c-divider);
  background: var(--vp-c-bg);
  font-size: 14px;
  font-weight: 500;
  transition: border-color 0.2s, color 0.2s;
}

.bf-chip:hover {
  border-color: var(--vp-c-brand-1);
  color: var(--vp-c-brand-1);
}

/* ---------- CTA ---------- */
.bf-cta {
  position: relative;
  overflow: hidden;
  padding: 56px 24px;
  border-radius: 20px;
  text-align: center;
  color: #fff7ed;
  background:
    radial-gradient(600px 240px at 90% 0%, rgba(251, 191, 36, 0.4), transparent 60%),
    radial-gradient(600px 260px at 0% 100%, rgba(124, 45, 18, 0.45), transparent 60%),
    linear-gradient(135deg, #9a3412, #d9501f 55%, #f2612f);
}

.bf-cta h2 {
  margin: 0;
  font-size: 28px;
  font-weight: 700;
  letter-spacing: -0.02em;
  border: none;
  padding: 0;
  color: #fff;
}

@media (min-width: 768px) {
  .bf-cta h2 { font-size: 34px; }
}

.bf-cta p {
  margin: 12px 0 28px;
  font-size: 17px;
  color: rgba(255, 247, 237, 0.9);
}

.bf-install {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  max-width: 100%;
  padding: 12px 14px 12px 18px;
  border-radius: 12px;
  background: rgba(40, 12, 2, 0.4);
  border: 1px solid rgba(255, 255, 255, 0.14);
  color: #fff7ed;
  cursor: pointer;
  font-family: var(--vp-font-family-mono);
  font-size: 14px;
  text-align: left;
}

.bf-install code {
  background: none;
  color: inherit;
  padding: 0;
  overflow-x: auto;
  white-space: nowrap;
}

.bf-install .prompt { color: #fdba74; }

.bf-copy {
  flex: none;
  padding: 4px 10px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.12);
  font-family: var(--vp-font-family-base);
  font-size: 12px;
  font-weight: 600;
}

.bf-cta-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 12px;
  margin-top: 28px;
}

.bf-btn {
  padding: 10px 22px;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.35);
  color: #fff !important;
  font-weight: 600;
  font-size: 15px;
  text-decoration: none !important;
  transition: background 0.2s, transform 0.2s;
}

.bf-btn:hover {
  background: rgba(255, 255, 255, 0.12);
  transform: translateY(-1px);
}

.bf-btn.primary {
  background: #fff;
  border-color: #fff;
  color: #9a3412 !important;
}

.bf-btn.primary:hover {
  background: #fff7ed;
}

@media (prefers-reduced-motion: reduce) {
  .bf-line { animation: none; opacity: 1; transform: none; }
}
</style>
