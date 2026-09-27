---
layout: home
title: BaseForge
titleTemplate: Forge .NET microservices from a spec

hero:
  name: BaseForge
  text: Forge .NET microservices from a spec
  tagline: An opinionated base library and a visual code generator for .NET 10. Describe your entities — get clean, production-ready services with CQRS, auth, gRPC, events and Docker already wired.
  image:
    src: /logo.svg
    alt: BaseForge
  actions:
    - theme: brand
      text: Get Started →
      link: /guide/getting-started
    - theme: alt
      text: What is BaseForge?
      link: /guide/introduction
    - theme: alt
      text: View on GitHub
      link: https://github.com/DropDeart/BaseForge

features:
  - icon: 🎨
    title: Visual Designer
    details: Model entities, relations, access rules and Identity in the browser with a live ER diagram. Generate, build and run with one click.
    link: /guide/designer
    linkText: Tour the Designer
  - icon: 🧱
    title: Clean Architecture + CQRS
    details: Core → Infrastructure → API layering, MediatR commands and queries, repositories, audit fields and soft delete out of the box.
    link: /architecture
    linkText: Why it's built this way
  - icon: 🔐
    title: Central Identity
    details: OpenIddict + ASP.NET Identity with social logins, roles, ownership rules and user profile fields — all from YAML.
    link: /guide/identity
    linkText: Identity & authorization
  - icon: 🔌
    title: gRPC & RabbitMQ
    details: Typed gRPC clients generated from sibling specs, and events with a transactional outbox, inbox idempotency and dead-letter queues.
    link: /architecture#_5-microservice-communication
    linkText: Service communication
  - icon: 🔭
    title: Observable by default
    details: Serilog + Grafana Loki, one correlation id across HTTP → gRPC → RabbitMQ, /health on every service and a live status dashboard.
    link: /architecture#_5-6-centralized-logging-and-correlation-id
    linkText: Logging & health
  - icon: 🐳
    title: Docker-ready
    details: Every service ships with a Dockerfile, docker-compose, health checks and conflict-free ports. Production guide included.
    link: /guide/deployment
    linkText: Deploy to production
---
