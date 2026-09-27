# Deploying to Production

This guide takes a typical BaseForge platform — an **Identity** service plus one or more business services — from your machine to a Linux server with Docker, behind a reverse proxy with automatic HTTPS.

The examples use `example.com`, an Identity service and a `blog` service. Replace them with your own.

## 1. What you need

| Component | Required? | Purpose |
| --- | --- | --- |
| **Identity** | Yes | Sign-in, OAuth2/OIDC, JWT for every service |
| **Your services** | Yes | Generated from your specs |
| **PostgreSQL** | Yes | One instance, one database per service |
| **RabbitMQ** | If you use `publishes` / `subscribes` | Async events |
| **Reverse proxy (Caddy)** | Yes | Single HTTPS entry point, automatic certificates, routing by domain |
| **Gateway service** | Usually no | Only when a frontend needs one origin for many services — see [Architecture §5.8](/architecture#_5-8-gateway-bff-—-yarp-based-reverse-proxy) |

::: tip You probably don't need a gateway
With a handful of services, a **reverse proxy** (TLS termination + routing, no business logic) is enough. A BFF/gateway service is worth it only when the browser needs aggregated data from several services through one origin.
:::

## 2. Architecture

```
                         example.com (browser)
                                  │
                                  ▼
               ┌──────────────────────────────────┐
               │   Caddy (reverse proxy + TLS)    │
               │   example.com          → frontend │
               │   identity.example.com → Identity │
               │   blog.example.com     → Blog     │
               └───────┬──────────────┬───────────┘
                       │              │
               ┌───────▼─────┐  ┌─────▼──────┐
               │  Identity   │◄─┤    Blog    │  gRPC (identity/User)
               └──────┬──────┘  └─────┬──────┘
                      │  RabbitMQ events
               ┌──────▼───────────────▼──────┐
               │  PostgreSQL + RabbitMQ      │
               └─────────────────────────────┘
```

The frontend signs in against Identity with authorization code + PKCE and sends the token to your services as `Authorization: Bearer …`. Services talk to each other over gRPC/RabbitMQ on the internal Docker network — the browser never sees that traffic.

## 3. Prepare the server

Any Linux VPS (Ubuntu 22.04+ recommended) with Docker:

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # log in again afterwards
```

Point DNS `A` records for `example.com`, `identity.example.com` and `blog.example.com` at the server.

## 4. Prepare Identity for production

1. **A persistent signing certificate.** Without one, Identity creates an ephemeral key on every start — and every issued token becomes invalid on restart.

   ```bash
   openssl req -x509 -newkey rsa:2048 -keyout identity.key -out identity.crt -days 3650 -nodes -subj "/CN=identity.example.com"
   openssl pkcs12 -export -out identity-signing.pfx -inkey identity.key -in identity.crt -passout pass:CHANGE_ME
   ```

   ```yaml
   # auth.yaml
   signing:
     certificatePath: /app/certs/identity-signing.pfx
   ```

   Put the password in `.env` and mount the `.pfx` into the container.

2. **Redirect URIs** — add your real frontend callback (e.g. `https://example.com/auth/callback`) to the browser client in `auth.yaml`.
3. **CORS** — add `https://example.com` to `corsOrigins` in `auth.yaml` and in every service spec the browser calls.
4. **Secrets** — use strong values for the seed admin password, provider secrets and database passwords in `.env`. Never commit them.

## 5. Point services at Identity

Generated services read these settings from the environment, so no code change is needed:

```yaml
environment:
  Auth__Authority: http://identity:8080     # address inside the Docker network
  Auth__Issuer: https://identity.example.com/
```

`Auth__Issuer` must match Identity's own issuer; it is accepted as a fallback so requests keep working while Identity restarts.

## 6. One compose file for the whole platform

Each generated service comes with its own isolated `docker-compose.yml` (great for local development). On a server, consolidate them into one file with a single PostgreSQL:

```yaml
services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: baseforge
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U baseforge"]
      interval: 10s
      retries: 5

  rabbitmq:
    image: rabbitmq:4-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}

  identity:
    build: ./identity
    env_file: ./identity/.env
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=postgres;Database=identity_db;Username=baseforge;Password=${POSTGRES_PASSWORD}"
    volumes: [./identity/certs:/app/certs:ro]
    depends_on: { postgres: { condition: service_healthy } }

  blog:
    build: ./blog
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=postgres;Database=blog_db;Username=baseforge;Password=${POSTGRES_PASSWORD}"
      RabbitMq__Host: rabbitmq
      Grpc__Identity: http://identity:8081
      Auth__Authority: http://identity:8080
      Auth__Issuer: https://identity.example.com/
    depends_on: [postgres, rabbitmq, identity]

  caddy:
    image: caddy:2-alpine
    ports: ["80:80", "443:443"]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data

volumes:
  pgdata:
  caddy-data:
```

Create the per-service databases once:

```bash
docker compose up -d postgres
docker compose exec postgres psql -U baseforge -c "CREATE DATABASE identity_db;"
docker compose exec postgres psql -U baseforge -c "CREATE DATABASE blog_db;"
```

Services create their schema on startup.

## 7. Reverse proxy with automatic HTTPS

```
# Caddyfile
identity.example.com {
    reverse_proxy identity:8080
}

blog.example.com {
    reverse_proxy blog:8080
}
```

Caddy obtains Let's Encrypt certificates automatically on the first request. Generated services already honor `X-Forwarded-*` headers, so URLs in discovery documents use `https` and your real domain.

## 8. Go live

```bash
docker compose up --build -d
curl -s https://identity.example.com/.well-known/openid-configuration | head -c 200
curl -s -o /dev/null -w "%{http_code}\n" https://blog.example.com/api/posts   # 401 without a token
```

## Checklist

- [ ] DNS records point at the server
- [ ] Identity has a persistent `.pfx` signing certificate
- [ ] Browser client redirect URIs use the real domain
- [ ] `corsOrigins` include your frontend origin
- [ ] Services have `Auth__Authority` (internal address) and `Auth__Issuer` (public issuer)
- [ ] One database per service exists
- [ ] `.env` files hold strong secrets and are not committed
- [ ] Caddy obtained certificates for every subdomain
- [ ] `/health` returns `Healthy` for every service
