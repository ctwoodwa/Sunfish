# Live-API Verification Runbook

Operational checklist for smoke-testing Sunfish integrations against real external APIs.
Run locally before cutting a release that touches any of the adapters below.

---

## Prerequisites

- .NET 9 SDK and the Aspire workload (`dotnet workload install aspire`)
- `dotnet user-secrets` or environment variables set as described per-section
- Network access to the relevant external API endpoint

---

## 1. Whisper (OpenAI speech-to-text)

**Modality:** `Sunfish.Ingestion.Voice` → `WhisperTranscriberAdapter`

**Endpoint:** `https://api.openai.com/v1/audio/transcriptions`

**Credentials:**

| Variable | Source |
|---|---|
| `OPENAI_API_KEY` | OpenAI platform → API keys |

**Smoke-test command:**

```bash
cd packages/ingestion-voice/tests/Sunfish.Ingestion.Voice.LiveTests
OPENAI_API_KEY=<key> dotnet test --filter "Category=LiveApi"
```

**Success criteria:**
- HTTP 200 with a non-empty `text` field in the response JSON
- Returned transcript matches the known phrase in `test-assets/hello-world.wav` (SHA-256: `a3f1...`)
- Test output includes `[LIVE] Whisper round-trip OK`

**If the API has drifted:**
- Check the OpenAI changelog for model deprecations
- Update `WhisperTranscriberAdapter` model name constant and re-run
- If unresolvable, file an issue tagged `live-api-drift` and notify `@infra`

---

## 2. Azure Cognitive Services Speech (Azure Speech SDK)

**Modality:** `Sunfish.Ingestion.Voice` → `AzureSpeechTranscriberAdapter`

**Endpoint:** `https://<region>.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1`

**Credentials:**

| Variable | Source |
|---|---|
| `AZURE_SPEECH_KEY` | Azure Portal → Cognitive Services → Keys and Endpoint |
| `AZURE_SPEECH_REGION` | Same blade (e.g., `eastus`) |

**Smoke-test command:**

```bash
cd packages/ingestion-voice/tests/Sunfish.Ingestion.Voice.LiveTests
AZURE_SPEECH_KEY=<key> AZURE_SPEECH_REGION=eastus dotnet test --filter "Category=LiveApi&Provider=Azure"
```

**Success criteria:**
- HTTP 200 with `RecognitionStatus: Success`
- Recognized phrase matches expected transcript for `test-assets/hello-world.wav`
- Test output includes `[LIVE] Azure Speech round-trip OK`

**If the API has drifted:**
- Verify the region endpoint format hasn't changed in the Azure Speech REST docs
- Check that the `api-version` query parameter matches the currently supported value
- Escalate to `@infra` if the key or region is stale

---

## 3. DAB MCP DML tools (live-Aspire verification)

**Context:** PR #19 (merged 577e34d) wires DAB 1.7.90 MCP SQL server with six DML tools.
This section covers the local-Aspire smoke probe deferred from G30.

**Prerequisites:**
- Docker Desktop running
- Aspire workload installed (see top of file)
- `ConnectionStrings__SunfishDb` set to a local or dev SQL Server instance

**Start the Aspire host:**

```bash
cd apps/apphost
dotnet run
```

Wait for the Aspire dashboard to show all resources as `Running` (typically ~30 s).

**Probe the MCP endpoint:**

```bash
# List available DML tools
curl -s http://localhost:5100/mcp/tools | jq '.[].name'
# Expected: ["dab_create","dab_read","dab_update","dab_delete","dab_execute","dab_query"]

# Smoke-read (should return empty array or seed rows, not a 5xx)
curl -s -X POST http://localhost:5100/mcp/tools/dab_read \
  -H "Content-Type: application/json" \
  -d '{"entity":"Inspection","filter":""}' | jq '.status'
# Expected: "ok"
```

**Success criteria:**
- All six tool names present in `/mcp/tools`
- `dab_read` returns `{"status":"ok"}` with a `data` array (may be empty)
- No 5xx errors in the Aspire dashboard structured logs

**If the live Aspire run fails:**
- Check DAB container logs in the Aspire dashboard for SQL connection errors
- Verify `ConnectionStrings__SunfishDb` is reachable from Docker
- If DAB version has changed, update the image tag in `apps/apphost/AppHost.cs` and re-run
- Escalate unresolvable issues to `@platform` tagged `aspire-dab`
