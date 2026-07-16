# LoadFlow — Freight Brokerage Operations Suite

**Stack:** ASP.NET Core 8 Web API · JWT Bearer · EF Core · SQLite · React (Vite) · Local POD storage

LoadFlow is an operations platform for freight brokerages: post loads, assign carriers, confirm rates, track shipments, and enforce carrier compliance before dispatch.

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)

### 1. Backend
```powershell
cd backend/LoadFlow.Api
dotnet restore
dotnet run
```
API: http://localhost:5000 · Swagger: http://localhost:5000/swagger

SQLite database (`loadflow.db`) is created and seeded on first run.

### 2. Frontend
```powershell
cd frontend/loadflow-ui
npm install
npm run dev
```
UI: http://localhost:5173 (proxies `/api` → backend)

## Demo Accounts (password: `Demo123!`)

| Account | Email | Role |
|---------|-------|------|
| Broker Admin | broker.admin@loadflow.demo | Full permissions |
| Dispatcher | dispatcher@loadflow.demo | load.create, assign, rate.confirm |
| Carrier Admin | carrier.admin@loadflow.demo | Full carrier permissions |
| Driver | driver@loadflow.demo | status update + POD |
| Shipper | shipper@loadflow.demo | View own loads only |

## Bootstrap Flow

1. **First Broker/Carrier Admin** — Register via UI (`Register → Broker/Carrier`) or use seeded demo orgs. Creates org + Admin role with all permissions.
2. **Staff** — Org Admin invites staff via **Staff & Roles**, assigns custom roles built from the permission catalog.
3. **Shipper** — Self-registers (no sub-roles); sees only loads where they are the shipper.

## RBAC Design

### Permission Catalog (fixed — code checks permissions, never role names)
| Permission | Description |
|------------|-------------|
| `load.create` | Create/edit broker loads |
| `load.assign_carrier` | Assign carriers |
| `load.override_compliance_flag` | Override compliance blocks |
| `rate.confirm` | Confirm versioned rates |
| `load.update_status` | Status transitions + accept |
| `staff.manage` | Staff + custom roles |
| `pod.upload` | Upload POD documents |
| `load.view` | View loads (scoped) |
| `compliance.manage` | Carrier compliance CRUD |

### Seeded Custom Roles
- **Broker Dispatcher** — create, assign, confirm rate
- **Broker Ops Lead** — + compliance override
- **Carrier Dispatch** — accept/decline, status
- **Carrier Driver** — status + POD

### Scoping
- **Org scoping:** Broker staff never see carrier org data (and vice versa)
- **Object scoping:** Shippers see only their loads; carriers see only assigned loads
- **API enforcement:** `[Authorize(Policy = "Permission:...")]` on every protected endpoint

Permission-denied attempts are logged to console and `PermissionDeniedLogs` table.

## Load State Machine

```
Posted → Carrier Assigned → Rate Confirmed → Dispatched → In Transit → Delivered → POD Verified → Invoiced/Closed
```

- Compliance auto-flags on carrier assignment (expired insurance, inactive authority, wrong equipment/commodity)
- **Blocks progression past Carrier Assigned** until override (Ops Lead) or compliance fixed
- Rate confirmations are **versioned** — each confirm creates a new version; loads retain the confirmed version

## API Highlights

| Endpoint | Permission |
|----------|------------|
| `POST /api/loads` | load.create |
| `POST /api/loads/{id}/assign-carrier` | load.assign_carrier |
| `POST /api/loads/{id}/override-compliance` | load.override_compliance_flag |
| `POST /api/loads/{id}/confirm-rate` | rate.confirm |
| `POST /api/loads/{id}/transition` | load.update_status |
| `GET /api/staff/roles` | staff.manage |
| `PUT /api/compliance` | compliance.manage |
| `POST /api/pods/{loadId}` | pod.upload |

## Project Structure

```
LoadFlow/
├── backend/LoadFlow.Api/     # ASP.NET Core 8 Web API
│   ├── Authorization/        # Permission policies + handler
│   ├── Controllers/
│   ├── Data/                 # EF Core + seeder
│   ├── Models/
│   ├── Services/
│   └── Middleware/           # Permission-denied logging
└── frontend/loadflow-ui/     # React + Vite
    └── src/
        ├── pages/            # Dashboards per account type
        └── api.ts
```

## Hackathon Notes

- Built with AI coding assistance; see [`AI_ASSISTED_DEVELOPMENT.md`](AI_ASSISTED_DEVELOPMENT.md)
- SQLite chosen for zero-config local demo
- POD files stored in `backend/LoadFlow.Api/uploads/pods/`
- Stretch features included: POD upload/view, compliance expiry alerts, load audit trail, permission-denied log API

## Project Status

Both production builds and API/RBAC smoke checks pass. See [`PROJECT_AUDIT.md`](PROJECT_AUDIT.md) for the requirement-by-requirement assessment and prioritized remaining production work.

## Testing the Compliance Flow

1. Log in as **Carrier Admin** → Compliance → set insurance expiry to a past date
2. Log in as **Broker Admin** → assign that carrier to a load
3. Load shows **Compliance Flagged** — rate confirm is blocked
4. Log in as **Ops Lead** (or admin) → override flag, then confirm rate

Or use **dispatcher@loadflow.demo** (no override permission) to verify API returns 403 on override endpoint — check console/logs for permission-denied entry.
