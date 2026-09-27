import { defineConfig, type DefaultTheme } from "vitepress";

// GitHub Pages serves the site under /BaseForge/. Override with DOCS_BASE (e.g. "/") for a custom domain.
const base = process.env.DOCS_BASE ?? "/BaseForge/";
const repo = "https://github.com/DropDeart/BaseForge";

function sidebarEn(): DefaultTheme.Sidebar {
  return [
    {
      text: "Introduction",
      items: [
        { text: "What is BaseForge?", link: "/guide/introduction" },
        { text: "Getting Started", link: "/guide/getting-started" },
      ],
    },
    {
      text: "Guide",
      items: [
        { text: "Designer", link: "/guide/designer" },
        { text: "Service Spec", link: "/guide/service-spec" },
        { text: "Identity & Authorization", link: "/guide/identity" },
        { text: "CLI Reference", link: "/guide/cli" },
        { text: "Deploying to Production", link: "/guide/deployment" },
      ],
    },
    {
      text: "Reference",
      items: [
        { text: "Architecture Decisions", link: "/architecture" },
        { text: "Coding Conventions", link: "/conventions" },
      ],
    },
    {
      text: "Release Notes",
      collapsed: true,
      items: [
        { text: "v0.6.2-beta", link: "/releases/v0.6.2-beta" },
        { text: "v0.6.1-beta", link: "/releases/v0.6.1-beta" },
        { text: "v0.6.0-beta", link: "/releases/v0.6.0-beta" },
      ],
    },
  ];
}

function sidebarTr(): DefaultTheme.Sidebar {
  return [
    {
      text: "Giriş",
      items: [
        { text: "BaseForge nedir?", link: "/tr/guide/introduction" },
        { text: "Başlarken", link: "/tr/guide/getting-started" },
      ],
    },
    {
      text: "Rehber",
      items: [
        { text: "Designer", link: "/tr/guide/designer" },
        { text: "Servis Spec'i", link: "/tr/guide/service-spec" },
        { text: "Identity ve Yetkilendirme", link: "/tr/guide/identity" },
        { text: "CLI Referansı", link: "/tr/guide/cli" },
        { text: "Production'a Yayın", link: "/tr/guide/deployment" },
      ],
    },
    {
      text: "Referans",
      items: [
        { text: "Mimari Kararlar", link: "/tr/architecture" },
        { text: "Kod Standartları", link: "/tr/conventions" },
      ],
    },
    {
      text: "Sürüm Notları",
      collapsed: true,
      items: [
        { text: "v0.6.2-beta", link: "/tr/releases/v0.6.2-beta" },
        { text: "v0.6.1-beta", link: "/tr/releases/v0.6.1-beta" },
        { text: "v0.6.0-beta", link: "/tr/releases/v0.6.0-beta" },
      ],
    },
  ];
}

export default defineConfig({
  base,
  title: "BaseForge",
  description: "An opinionated base library and code generator for .NET 10 microservices.",
  cleanUrls: true,
  lastUpdated: true,
  srcExclude: ["logs/**", "**/README*.md"],
  // Keep the historic file names in the repo (CLAUDE.md and old links point at them) but publish clean URLs.
  rewrites: {
    "ARCH.md": "architecture.md",
    "CONVENTIONS.md": "conventions.md",
    "tr/ARCH.md": "tr/architecture.md",
    "tr/CONVENTIONS.md": "tr/conventions.md",
  },

  head: [
    ["link", { rel: "icon", type: "image/svg+xml", href: `${base}logo.svg` }],
    ["meta", { name: "theme-color", content: "#f2612f" }],
    ["meta", { property: "og:type", content: "website" }],
    ["meta", { property: "og:title", content: "BaseForge — forge .NET microservices from a spec" }],
    ["meta", { property: "og:description", content: "Opinionated base library + visual code generator for .NET 10 microservices." }],
  ],

  markdown: {
    theme: { light: "github-light", dark: "github-dark" },
  },

  themeConfig: {
    logo: "/logo.svg",
    socialLinks: [{ icon: "github", link: repo }],
    search: {
      provider: "local",
      options: {
        locales: {
          tr: {
            translations: {
              button: { buttonText: "Ara", buttonAriaLabel: "Ara" },
              modal: {
                noResultsText: "Sonuç bulunamadı",
                resetButtonTitle: "Aramayı temizle",
                footer: { selectText: "seç", navigateText: "gezin", closeText: "kapat" },
              },
            },
          },
        },
      },
    },
  },

  locales: {
    root: {
      label: "English",
      lang: "en",
      themeConfig: {
        nav: [
          { text: "Guide", link: "/guide/getting-started", activeMatch: "/guide/" },
          { text: "Architecture", link: "/architecture" },
          {
            text: "v0.6.2-beta",
            items: [
              { text: "Release notes", link: "/releases/v0.6.2-beta" },
              { text: "NuGet", link: "https://www.nuget.org/packages?q=BaseForge" },
              { text: "Changelog on GitHub", link: `${repo}/releases` },
            ],
          },
        ],
        sidebar: sidebarEn(),
        editLink: { pattern: `${repo}/edit/main/docs/:path`, text: "Edit this page on GitHub" },
        footer: {
          message: "Released under the MIT License.",
          copyright: "Copyright © 2026 DropDeart",
        },
      },
    },
    tr: {
      label: "Türkçe",
      lang: "tr",
      link: "/tr/",
      description: ".NET 10 mikroservisleri için opinionated temel kütüphane ve kod üretici.",
      themeConfig: {
        nav: [
          { text: "Rehber", link: "/tr/guide/getting-started", activeMatch: "/tr/guide/" },
          { text: "Mimari", link: "/tr/architecture" },
          {
            text: "v0.6.2-beta",
            items: [
              { text: "Sürüm notları", link: "/tr/releases/v0.6.2-beta" },
              { text: "NuGet", link: "https://www.nuget.org/packages?q=BaseForge" },
              { text: "GitHub sürümleri", link: `${repo}/releases` },
            ],
          },
        ],
        sidebar: sidebarTr(),
        editLink: { pattern: `${repo}/edit/main/docs/:path`, text: "Bu sayfayı GitHub'da düzenle" },
        footer: {
          message: "MIT Lisansı ile yayınlanmıştır.",
          copyright: "Copyright © 2026 DropDeart",
        },
        docFooter: { prev: "Önceki sayfa", next: "Sonraki sayfa" },
        outline: { label: "Bu sayfada" },
        lastUpdated: { text: "Son güncelleme" },
        langMenuLabel: "Dil",
        returnToTopLabel: "Başa dön",
        sidebarMenuLabel: "Menü",
        darkModeSwitchLabel: "Görünüm",
        lightModeSwitchTitle: "Açık temaya geç",
        darkModeSwitchTitle: "Koyu temaya geç",
      },
    },
  },
});
