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
  - icon: 🎨
    title: Görsel Designer
    details: Entity'leri, ilişkileri, erişim kurallarını ve Identity'yi tarayıcıda canlı ER diyagramıyla modelle. Tek tıkla üret, derle ve çalıştır.
    link: /tr/guide/designer
    linkText: Designer turu
  - icon: 🧱
    title: Clean Architecture + CQRS
    details: Core → Infrastructure → API katmanlaması, MediatR komut ve sorguları, repository'ler, audit alanları ve soft delete hazır gelir.
    link: /tr/architecture
    linkText: Neden böyle tasarlandı
  - icon: 🔐
    title: Merkezi Identity
    details: Sosyal girişler, roller, sahiplik kuralları ve kullanıcı profil alanlarıyla OpenIddict + ASP.NET Identity — hepsi YAML'dan.
    link: /tr/guide/identity
    linkText: Identity ve yetkilendirme
  - icon: 🔌
    title: gRPC ve RabbitMQ
    details: Kardeş spec'lerden üretilen tipli gRPC istemcileri; transactional outbox, inbox idempotency ve dead-letter kuyruklu event'ler.
    link: /tr/architecture#_5-mikroservis-iletisimi
    linkText: Servisler arası iletişim
  - icon: 🔭
    title: Varsayılan olarak izlenebilir
    details: Serilog + Grafana Loki, HTTP → gRPC → RabbitMQ boyunca tek correlation id, her serviste /health ve canlı durum paneli.
    link: /tr/architecture#_5-6-merkezi-loglama-ve-correlation-id
    linkText: Loglama ve sağlık
  - icon: 🐳
    title: Docker'a hazır
    details: Her servis Dockerfile, docker-compose, health check ve çakışmayan portlarla gelir. Production rehberi dahil.
    link: /tr/guide/deployment
    linkText: Production'a yayın
---
