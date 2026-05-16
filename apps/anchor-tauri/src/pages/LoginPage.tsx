// W#60 P4 PR 1 — interim manual-paste login page.
//
// First-launch / logged-out flow: user obtains a Bridge auth token out-of-band
// (CO admin tool, env, or pre-Path-II Bridge `/auth/login` page) and pastes it
// here. The token is persisted in the Stronghold-backed credentialStore + set
// in the Rust state via the `set_bridge_token` command.
//
// The hand-off doc envisions a `Bridge /auth/login?redirect=tauri://localhost`
// SSO redirect flow. That's deferred behind this paste-interim because (a) the
// Bridge architecture is in flux post-Path-II ratification (ADR 0088), and
// (b) wiring a tauri:// deep-link handler + redirect URI allowlist on Bridge is
// substantively more work than fits in PR 1. The interim is replaceable with
// the full SSO flow without touching credentialStore or the gate logic.

import { useState } from 'react'
import { invoke } from '@tauri-apps/api/core'
import { setToken as persistToken } from '@/services/credentialStore'
import { useAuthStore } from '@/stores/authStore'

export function LoginPage() {
  const setToken = useAuthStore((s) => s.setToken)
  const [pasted, setPasted] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    const trimmed = pasted.trim()
    if (!trimmed) {
      setError('Token cannot be empty.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      // Council A1.3 — validate the token against Bridge BEFORE persisting.
      // Without this, any pasted string (a redirect URL, a JSON fragment, an
      // attacker-supplied token) ends up in Stronghold and the app silently
      // 401-spirals with no surfaced cause. Use `Authorization: Bearer` so we
      // exercise the same auth path the sync code uses, not just cookies.
      let probe: Response
      try {
        probe = await fetch('/api/v1/whoami', {
          method: 'GET',
          headers: { Authorization: `Bearer ${trimmed}` },
        })
      } catch (netErr) {
        const msg = netErr instanceof Error ? netErr.message : String(netErr)
        setError(`Could not reach Bridge to validate token: ${msg}`)
        return
      }
      if (!probe.ok) {
        setError(`Bridge rejected the token: ${probe.status} ${probe.statusText}`)
        return
      }
      // Token verified. Persist + advance.
      await persistToken(trimmed)
      await invoke('set_bridge_token', { token: trimmed })
      setToken(trimmed)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save token.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-6 bg-background text-foreground">
      <form
        onSubmit={onSubmit}
        className="w-full max-w-md space-y-4 rounded-lg border border-border bg-card p-8 shadow-sm"
      >
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold">Sign in to Anchor</h1>
          <p className="text-sm text-muted-foreground">
            Paste your Bridge auth token to connect this device. The token is
            stored encrypted in the OS keychain and never leaves this machine.
          </p>
        </div>
        <div className="space-y-2">
          <label htmlFor="bridge-token" className="text-sm font-medium">
            Bridge auth token
          </label>
          <textarea
            id="bridge-token"
            value={pasted}
            onChange={(e) => setPasted(e.target.value)}
            rows={4}
            autoFocus
            spellCheck={false}
            className="w-full rounded border border-input bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            placeholder="eyJhbGciOiJIUzI1NiIs..."
          />
        </div>
        {error && (
          <div
            role="alert"
            className="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {error}
          </div>
        )}
        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
        >
          {submitting ? 'Saving...' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}
