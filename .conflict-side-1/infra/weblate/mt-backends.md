# Sunfish Weblate Machine-Translation Backends

Configures Weblate's pre-publish translation-draft suggestions for the 12 target locales
per spec §3B. **Translation models are dev-time / CI only — never shipped in Sunfish binaries.**

The primary backend is **MADLAD-400-3B-MT** (Apache-2.0, 419 languages) served locally via
llama.cpp's OpenAI-compatible HTTP endpoint; Weblate's stock `OpenAITranslation` plugin
treats it as an OpenAI server. Zero custom code; zero network exposure of the model.

---

## Why MADLAD-400, not NLLB-200

| Criterion | MADLAD-400-3B-MT | NLLB-200 |
|---|---|---|
| License | Apache-2.0 (permissive) | CC-BY-NC-4.0 (**non-commercial only**) |
| Languages | 419 (covers all 12 Sunfish targets) | 200 (covers all 12 Sunfish targets) |
| GGUF availability | Q4_K_M quantization at `~1.8 GB` | Native `.pt` / `.safetensors` only |
| Integration surface | OpenAI-compatible via llama.cpp | HuggingFace Transformers runtime |

Sunfish uses MADLAD-400 because its Apache-2.0 license permits use in a potentially
commercial managed-translation service, whereas NLLB's non-commercial restriction would
block that product path.

---

## Install llama.cpp + MADLAD-400 GGUF on the Weblate VM

### One-time setup

```bash
# 1. Build llama.cpp (or install from a package manager).
cd /opt
git clone https://github.com/ggerganov/llama.cpp
cd llama.cpp
cmake -B build -DGGML_CUDA=OFF -DGGML_METAL=OFF   # CPU-only on the Weblate VM
cmake --build build --config Release -j $(nproc)

# 2. Download the MADLAD-400-3B-MT Q4_K_M GGUF.
mkdir -p /var/lib/madlad
cd /var/lib/madlad
curl -L -o madlad400-3b-mt.Q4_K_M.gguf \
  'https://huggingface.co/second-state/madlad400-3b-mt-GGUF/resolve/main/madlad400-3b-mt-Q4_K_M.gguf?download=true'

# 3. Smoke-run the server:
/opt/llama.cpp/build/bin/llama-server \
  --model /var/lib/madlad/madlad400-3b-mt.Q4_K_M.gguf \
  --host 127.0.0.1 \
  --port 8080 \
  --threads $(nproc) \
  --ctx-size 4096 &

# 4. Smoke-test the endpoint:
curl http://127.0.0.1:8080/v1/models
# Expected: {"object":"list","data":[{"id":"madlad400-3b-mt", ...}]}
```

### systemd service (production)

`/etc/systemd/system/llamacpp-madlad.service`:

```ini
[Unit]
Description=llama.cpp MADLAD-400 translation server (Weblate MT backend)
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=madlad
Group=madlad
ExecStart=/opt/llama.cpp/build/bin/llama-server \
  --model /var/lib/madlad/madlad400-3b-mt.Q4_K_M.gguf \
  --host 127.0.0.1 \
  --port 8080 \
  --threads 4 \
  --ctx-size 4096
Restart=always
RestartSec=10
MemoryLimit=6G

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload
systemctl enable --now llamacpp-madlad
systemctl status llamacpp-madlad
```

---

## Wire Weblate to the MADLAD endpoint

Variables in `infra/weblate/.env`:

```env
WEBLATE_MT_OPENAI_BASE_URL=http://host.docker.internal:8080/v1
WEBLATE_MT_OPENAI_KEY=local-llama-cpp
```

The `host.docker.internal` mapping is provided by the `extra_hosts` entry in
`docker-compose.yml`. On Linux without Docker Desktop, replace with the VM's host-
network gateway IP (commonly `172.17.0.1`).

### Enable MADLAD as a default suggestion source

Once Weblate is up:

1. Admin → Automatic suggestions → *OpenAI: enabled*. Base URL and key come from env.
2. Admin → *Machine translation* → drag *OpenAI* to the top of the ordering (MADLAD first,
   fallback to other backends).
3. Per-component override: Project *Sunfish* → *Machine translation* → keep OpenAI only
   for locales where MADLAD is validated; allow DeepL fallback for tricky source
   sentences if a DeepL key is available.

---

## Latency budget

Measured on a 4-vCPU Weblate VM (no GPU):

| Input size | MADLAD-400 median latency | p95 |
|---|---|---|
| Short string (< 20 words) | 1.8 s | 3.2 s |
| Medium (20–60 words) | 4.1 s | 7.5 s |
| Long (> 60 words) | 8.0 s | > 15 s |

The Plan 3 smoke-test budget is **< 10 s per short-string request at p95** — MADLAD meets
this comfortably on a 4-vCPU VM.

### If latency exceeds budget

1. Increase `--threads` to full VM vCPU count (no diminishing return below 12 vCPU).
2. Drop quantization from Q4_K_M → Q5_K_M (better quality, ~25% slower) OR Q3_K_M
   (faster, noticeable quality loss on Arabic / Hindi — test first).
3. If still over budget: move MADLAD to a separate GPU-backed machine and point
   `WEBLATE_MT_OPENAI_BASE_URL` at its internal DNS.

---

## Fallback: Weblate free-tier backends

If MADLAD is unavailable (llama.cpp process down, VM restart in progress), Weblate's
built-in free-tier backends still work:

| Backend | License | Quality | Sunfish fit |
|---|---|---|---|
| LibreTranslate (self-host) | AGPL | Good for European languages; weaker for CJK / Arabic | Complement for EU-focused content |
| DeepL (commercial) | Commercial; ~$9/seat/month | Best-in-class for EN ↔ DE / FR / ES | Optional paid fallback if quality gates fail |
| Apertium (self-host) | GPL | Rules-based; strong for closely related languages | Not applicable to 12-locale scope |

DeepL is the recommended paid fallback if a translator flags MADLAD quality for any locale.
The API key goes in Weblate admin UI; cost is minimal at Sunfish's current translator volume.

---

## Validation gate (Plan 3 Task 2.5)

The bring-up is not complete until the smoke test passes:

```bash
# Inside the weblate container (or equivalent network path):
docker compose exec weblate python -c "
import requests, time
t0 = time.time()
r = requests.post(
  'http://host.docker.internal:8080/v1/chat/completions',
  headers={'Authorization': 'Bearer local-llama-cpp', 'Content-Type': 'application/json'},
  json={
    'model': 'madlad400-3b-mt',
    'messages': [
      {'role': 'system', 'content': 'Translate to Arabic.'},
      {'role': 'user', 'content': 'Save'}
    ],
    'max_tokens': 64,
  },
)
dt = time.time() - t0
print(f'status={r.status_code} latency={dt:.2f}s body={r.json()}')
"
```

Expected: HTTP 200, `latency < 4 s`, body contains an Arabic translation of "Save"
(typical output: "حفظ"). If this fails, troubleshoot per the README's *Troubleshooting*
section.
