---
layout: home
title: BaseForge
titleTemplate: .NET mikroservislerini spec'ten üret

hero:
  name: BaseForge
  text: .NET mikroservislerini spec'ten üret
  tagline: .NET 10 için opinionated bir temel kütüphane ve görsel kod üretici. Entity'lerini tarif et — CQRS, kimlik doğrulama, gRPC, event'ler ve Docker'ı hazır bağlanmış, temiz ve production'a hazır servisler al.
  image:
    src: /logo.svg
    alt: BaseForge
  actions:
    - theme: brand
      text: Başlarken →
      link: /tr/guide/getting-started
    - theme: alt
      text: BaseForge nedir?
      link: /tr/guide/introduction
    - theme: alt
      text: GitHub'da gör
      link: https://github.com/DropDeart/BaseForge

features:
  - icon: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ><path d="M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z" /><circle cx="13.5" cy="6.5" r=".5" fill="currentColor" /><circle cx="17.5" cy="10.5" r=".5" fill="currentColor" /><circle cx="6.5" cy="12.5" r=".5" fill="currentColor" /><circle cx="8.5" cy="7.5" r=".5" fill="currentColor" /></svg>'
    title: Görsel Designer
    details: Entity'leri, ilişkileri, erişim kurallarını ve Identity'yi tarayıcıda canlı ER diyagramıyla modelle. Tek tıkla üret, derle ve çalıştır.
    link: /tr/guide/designer
    linkText: Designer turu
  - icon: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ><path d="M12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83z" /><path d="M2 12a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 12" /><path d="M2 17a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 17" /></svg>'
    title: Clean Architecture + CQRS
    details: Core → Infrastructure → API katmanlaması, MediatR komut ve sorguları, repository'ler, audit alanları ve soft delete hazır gelir.
    link: /tr/architecture
    linkText: Neden böyle tasarlandı
  - icon: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ><path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z" /><path d="m9 12 2 2 4-4" /></svg>'
    title: Merkezi Identity
    details: Sosyal girişler, roller, sahiplik kuralları ve kullanıcı profil alanlarıyla OpenIddict + ASP.NET Identity — hepsi YAML'dan.
    link: /tr/guide/identity
    linkText: Identity ve yetkilendirme
  - icon: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ><rect x="16" y="16" width="6" height="6" rx="1" /><rect x="2" y="16" width="6" height="6" rx="1" /><rect x="9" y="2" width="6" height="6" rx="1" /><path d="M5 16v-3a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v3" /><path d="M12 12V8" /></svg>'
    title: gRPC ve RabbitMQ
    details: Kardeş spec'lerden üretilen tipli gRPC istemcileri; transactional outbox, inbox idempotency ve dead-letter kuyruklu event'ler.
    link: /tr/architecture#_5-mikroservis-iletisimi
    linkText: Servisler arası iletişim
  - icon: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ><path d="M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2" /></svg>'
    title: Varsayılan olarak izlenebilir
    details: Serilog + Grafana Loki, HTTP → gRPC → RabbitMQ boyunca tek correlation id, her serviste /health ve canlı durum paneli.
    link: /tr/architecture#_5-6-merkezi-loglama-ve-correlation-id
    linkText: Loglama ve sağlık
  - icon: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ><path d="M22 7.7c0-.6-.4-1.2-.8-1.5l-6.3-3.9a1.72 1.72 0 0 0-1.7 0l-10.3 6c-.5.2-.9.8-.9 1.4v6.6c0 .5.4 1.2.8 1.5l6.3 3.9a1.72 1.72 0 0 0 1.7 0l10.3-6c.5-.3.9-1 .9-1.5Z" /><path d="M10 21.9V14L2.1 9.1" /><path d="m10 14 11.9-6.9" /><path d="M14 19.8v-8.1" /><path d="M18 17.5V9.4" /></svg>'
    title: Docker'a hazır
    details: Her servis Dockerfile, docker-compose, health check ve çakışmayan portlarla gelir. Production rehberi dahil.
    link: /tr/guide/deployment
    linkText: Production'a yayın
---
