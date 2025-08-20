# CargoHub Envanter – 18.08.2025 06:51

Bu doküman, **CargoHub projesinde** şu ana kadar kurulan tüm ortamların, sunucuların, konteynerlerin, ağ yapıların, CI/CD süreçlerinin ve güvenlik kurallarının detaylı envanteridir.

---

## 1. Ortamlar ve Rolleri

| Ortam | Amaç | Domain | IP | SSH Kullanıcı | Not |
|---|---|---|---|---|---|
| **Local (Mac)** | Geliştirme & hızlı test | Yok | Bilinmiyor | `umit@laptop` | Docker Compose ile API+DB+MQ; .env yerelde |
| **Production** | Canlı yayın (kök path `/`) | `api.tugek.com` | (aynı Hetzner VM) Bilinmiyor | `root` | Caddy kökten prod API’ye proxy eder |
| **Staging** | Prod’dan izole test (path `/stg`) | `api.tugek.com/stg` | **91.99.205.208** | `root` | Caddy `/stg` isteklerini staging API’ye yönlendirir |

---

## 2. Hetzner Sunucu – Sistem & Konumlar

| Sunucu | OS/Kernel | Rol | Yol/Dizinler | Açık Portlar (beklenen) |
|---|---|---|---|---|
| **hetzner-vm** | Ubuntu 24.04 LTS | Caddy reverse proxy, Prod API, Staging stack, Watchtower, Uptime Kuma | Prod: `/opt/cargo-hub` • Stg: `/opt/cargo-hub-stg` • Caddyfile: `/opt/cargo-hub/Caddyfile` | 22/80/443 (açık), 8081 (staging api), 15672 (rabbitmq mgmt) |

---

## 3. Caddy Reverse Proxy

- **Etki Alanı:** `api.tugek.com`
- **Kurallar:**
  - `/` → Prod API
  - `/stg*` → Staging API
  - HTTPS: Let’s Encrypt TLS 1.3
  - HTTP→HTTPS redirect (308)
  - Gizli yollar 404: `/.env*`, `/.git*`, `/backup/*`, `/*.bak`, vb.
  - `/healthz` → `Cache-Control: no-store`
- **Ağ:** Caddy hem prod hem staging Docker ağlarına bağlı.

---

## 4. Docker Yapıları

### 4.1 Production (`cargo-hub`)
- **Ağ:** `cargo-hub_default`
- **Konteynerler:**
  - `caddy`
  - `api` (`ghcr.io/<owner>/cargo-hub:latest`)
  - `watchtower`
  - `uptime-kuma`
- **Notlar:** 
  - GHCR’den çekilen imajlar
  - Watchtower ile otomatik güncelleme
  - JSON log formatı, log rotasyonu aktif

### 4.2 Staging (`cargo-hub-stg`)
- **Ağ:** `cargo-hub-stg_default`
- **Konteynerler:**
  - `db` (Postgres 16, volume `./data/db`)
  - `mq` (RabbitMQ + Management, volume `mqdata`)
  - `api` (CargoHub API, `8081:8080`)
- **Secrets:** `/opt/cargo-hub-stg/secrets/`
  - `db_password`
  - `mq_password`
  - `mq_app_password`
- **Bağlantılar:** API → DB & MQ (file-based password)
- **Caddy:** `/stg/*` → `cargo-hub-stg-api-1:8080`

---

## 5. GitHub & CI/CD

- **Repo:** `cargo-hub`
- **Registry:** GHCR → `ghcr.io/<owner>/cargo-hub`
- **Tags:** `latest`, `sha`, `vX.Y.Z`
- **Workflow:** Build & push → GHCR
- **Deploy:** 
  - **Staging:** SSH ile `/opt/cargo-hub-stg` → `.env` (600) → `docker compose up -d`
  - **Prod:** Watchtower otomatik
- **Secrets:** 
  - `staging`, `production` environment ayrımı
  - DB/MQ/App secrets tanımlı

---

## 6. Local (Mac) Geliştirme

- **Araçlar:** Docker Desktop, .NET 8 SDK, Terraform, hcloud, gh
- **Compose (dev):** API + Postgres + RabbitMQ
- **Örnek .env:** 
  - `ASPNETCORE_ENVIRONMENT=Development`
  - `DB_HOST=db`
  - `DB_NAME=cargohub`
  - `DB_USER=app`
  - `DB_PASSWORD=app123`
  - `RABBITMQ_URL=amqp://guest:guest@mq:5672`

---

## 7. API Çalışma Kuralları

- .NET 8 Minimal API
- Config sırası: appsettings + Env Vars + Secrets (file-based `_FILE`)
- Endpoints: 
  - `/healthz` (200 OK)
  - `/config-check` (maskeli)
- Loglar: JSON format
- Prod: `caddy → api:8080`
- Staging: `caddy → cargo-hub-stg-api-1:8080`

---

## 8. RabbitMQ (Staging)

- Kullanıcılar:
  - `admin` (secret: `mq_password`)
  - `app` (secret: `mq_app_password`)
- Vhost: `cargohub`
- Management UI: `:15672` (kısıtlama önerilir)
- Parolalar file-based

---

## 9. PostgreSQL (Staging)

- Sürüm: 16
- Kullanıcı/DB: `.env` + secrets
- Volume: `./data/db`
- Healthcheck aktif

---

## 10. İzleme & Günlükler

- Uptime Kuma: Dashboard + Telegram entegrasyonu
- Caddy access log: `/healthz` istekleri görünüyor
- API log: JSON
- Docker log rotasyonu: `max-size=10m`, `max-file=3`, `compress=true`

---

## 11. Ağ/Topoloji

```
İnternet
   │
   ▼
[ Caddy ] — TLS, Redirect, Block Rules
   │
   ├─► `/` → Prod API (cargo-hub)
   │         ├─ api
   │         ├─ uptime-kuma
   │         └─ watchtower
   │
   └─► `/stg` → Staging API (cargo-hub-stg)
              ├─ api
              ├─ db (Postgres 16)
              └─ mq (RabbitMQ)
```

---

## 12. Güvenlik İlkeleri

- HTTPS zorunlu
- Gizli yollar 404
- Secrets: file-based (600 izin)
- SSH: anahtar bazlı (`root`)
- Firewall: 22, 80, 443 açık
- Öneri: 8081 & 15672 dış erişim kısıtlanmalı

---

## 13. CI/CD Çalışma Kuralları

- **CI:** Build → GHCR push
- **CD (stg):** SSH deploy → healthcheck
- **CD (prod):** Watchtower auto-update
- **Sürümleme:** `git tag vX.Y.Z` → GHCR

---

**Hazırlanma Tarihi:** 18.08.2025 06:51
