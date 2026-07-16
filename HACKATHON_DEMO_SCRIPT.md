# LoadFlow Hackathon Walkthrough Script

Target length: 4 minutes 15 seconds. Keep the browser, terminal, GitHub repository, and Codex conversation open before recording.

## Before recording

1. Start the API:

   ```powershell
   cd backend/LoadFlow.Api
   dotnet run
   ```

2. Start the React application in another terminal:

   ```powershell
   cd frontend/loadflow-ui
   npm run dev
   ```

3. Open `http://localhost:5173`, the GitHub repository, `AI_ASSISTED_DEVELOPMENT.md`, and the Codex conversation.
4. Use the seeded demo accounts and password `Demo123!`.
5. Turn off notifications and zoom the browser to a readable level.

## Recording script

### 0:00–0:25 — Problem and stack

Show the README and repository.

Say:

> LoadFlow is a freight brokerage operations suite connecting brokers, carriers, and shippers. It uses ASP.NET Core 8, JWT authentication, permission-based custom RBAC, Entity Framework Core with SQLite, React, and local POD storage.

Briefly scroll past the repository structure and run instructions. Do not read every line.

### 0:25–1:15 — Broker dashboard and load board

Log in as `broker.admin@loadflow.demo`.

Say:

> The broker dashboard shows active loads and compliance alerts. Every load query is scoped to the broker organization at the API layer.

Open **Loads**. Demonstrate the search and status filters, then open the seeded load.

Say:

> A load links the broker, shipper, and assigned carrier. Status changes are timestamped and attributed in the audit trail.

Assign **Swift Haul Logistics** to the posted load.

Say:

> Carrier assignment automatically checks insurance expiry, MC and DOT authority, equipment, and commodity approval. A failure blocks progression until compliance is corrected or an authorized user records an override.

### 1:15–1:55 — Carrier workflow and object scoping

Sign out and log in as `driver@loadflow.demo`.

Open the assigned load and accept it.

Say:

> Carrier users see only loads assigned to their own carrier organization. Available actions come from permissions, not hardcoded role names. A driver can accept the load, update shipment status, and upload a POD, but cannot manage broker staff or rates.

Mention that decline returns the load to the broker's posted board. Do not decline the load during this take.

### 1:55–2:35 — Rate confirmation and auditability

Sign out and return as `broker.admin@loadflow.demo`. Open the same load and confirm a rate.

Say:

> Rate confirmations are versioned. Confirming a revised amount creates a new immutable version, while the load retains the version actually confirmed. The workflow prevents generic status updates from bypassing carrier assignment or rate confirmation.

Point to the active rate and audit trail. If time permits, confirm a second version to show the version number increasing.

### 2:35–3:15 — Custom RBAC

Open **Staff & Roles** and show the roles tab and permission catalog.

Say:

> Broker and carrier admins create staff and define custom roles from a fixed permission catalog. API endpoints use authorization policies such as load assignment, rate confirmation, status update, staff management, and POD upload. Direct forbidden requests return 403 and are logged for audit.

Briefly show the Dispatcher role permissions. Avoid creating throwaway users during the recording.

### 3:15–3:55 — How AI was used

Switch to the Codex conversation or IDE and show one real prompt plus the resulting diff. Then show `AI_ASSISTED_DEVELOPMENT.md` and the Git history.

Say:

> I used Codex as a coding and review partner. My prompts included the business requirements, stack constraints, and expected server-side security behavior rather than asking only for UI code. I reviewed proposed changes as diffs, kept unrelated files out of commits, ran both production builds, tested authentication and object scoping, and deliberately called a restricted endpoint to verify the 403 response and denial log. AI usage and the verification performed are documented in the repository.

Good prompt to show from the real conversation:

> Please go through the whole project against these requirements, check what remains, and verify permission-based RBAC, organization scoping, the load state machine, compliance blocking, and rate versioning.

Show these commands or their successful output:

```powershell
dotnet build LoadFlow.sln --no-restore
cd frontend/loadflow-ui
npm run build
git log --oneline
```

### 3:55–4:15 — Close

Return to the dashboard.

Say:

> The hackathon must-haves are runnable locally with seeded demo accounts. With more time I would add comprehensive integration tests, complete staff lifecycle and delete operations, EF Core migrations, production secret management, and cloud-backed POD storage.

End the recording on the repository README or broker dashboard.

## Final upload checklist

- Recording is between 3 and 5 minutes.
- Text, terminal output, and URLs are readable.
- No passwords, tokens, personal notifications, or unrelated browser tabs are exposed.
- The video contains both the working application and a real AI prompt/review moment.
- The narration mentions prompt style and review habits explicitly.
- Upload the video using the hackathon's accepted service and verify link access in a private/incognito window.
- Add the final video URL to the README and submission form.
