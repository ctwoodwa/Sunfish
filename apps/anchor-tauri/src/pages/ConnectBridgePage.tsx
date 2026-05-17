// Optional Bridge-connect page. Anchor runs local-first by charter (CO directive
// 2026-05-16) — Bridge connectivity is an opt-in sync feature, not a startup
// gate. This page is reachable from the header "Connect" link in offline mode,
// and from Settings → Bridge when already connected (to swap tokens).
//
// The token is validated against Bridge before persisting (council A1.3) so a
// pasted garbage string never gets persisted into Stronghold to fail-silently
// later. Keychain probe happens here (deferred from boot) because we only need
// the OS keychain when the user is about to persist a token.

import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { invoke } from '@tauri-apps/api/core'
import { setToken as persistToken } from '@/services/credentialStore'
import { useAuthStore } from '@/stores/authStore'

export function ConnectBridgePage() {
  const setToken = useAuthStore((s) => s.setToken)
  const navigate = useNavigate()
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
      // Deferred keychain probe — only needed because we're about to persist.
      try {
        const status = await invoke<string | null>('keychain_status')
        if (status) {
          setError(`Operating-system credential store unavailable: ${status}`)
          return
        }
      } catch {
        setError('Anchor could not reach the operating-system credential store.')
        return
      }

      // Council A1.3 — validate against Bridge before persisting.
      let bridgeUrl: string
      try {
        bridgeUrl = await invoke<string>('get_bridge_url')
      } catch (err) {
        setError(`Anchor could not resolve the Bridge URL: ${err instanceof Error ? err.message : String(err)}`)
        return
      }
      let probe: Response
      try {
        probe = await fetch(`${bridgeUrl}/api/v1/whoami`, {
          method: 'GET',
          headers: { Authorization: `Bearer ${trimmed}` },
        })
      } catch (netErr) {
        const msg = netErr instanceof Error ? netErr.message : String(netErr)
        setError(`Could not reach Bridge at ${bridgeUrl}: ${msg}. Bridge is optional — you can close this page and continue offline.`)
        return
      }
      if (!probe.ok) {
        setError(`Bridge rejected the token: ${probe.status} ${probe.statusText}`)
        return
      }
      await persistToken(trimmed)
      await invoke('set_bridge_token', { token: trimmed })
      setToken(trimmed)
      navigate('/properties')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save token.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <div className="space-y-1">
        <h1 className="text-2xl font-semibold">Connect to Bridge</h1>
        <p className="text-sm text-muted-foreground">
          Bridge is optional. Anchor is local-first — you can use every feature
          on this page without connecting. Connect when you want to sync data
          across devices or pull from ERPNext.
        </p>
      </div>
      <form
        onSubmit={onSubmit}
        className="space-y-4 rounded-lg border border-border bg-card p-6 shadow-sm"
      >
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
          <p className="text-xs text-muted-foreground">
            Paste the token issued by your Bridge admin. It is validated before
            persisting, then stored encrypted in the OS keychain. It never leaves
            this machine.
          </p>
        </div>
        {error && (
          <div
            role="alert"
            className="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          >
            {error}
          </div>
        )}
        <div className="flex items-center gap-2">
          <button
            type="submit"
            disabled={submitting}
            className="rounded bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
          >
            {submitting ? 'Connecting...' : 'Connect'}
          </button>
          <button
            type="button"
            onClick={() => navigate('/properties')}
            className="rounded border border-border bg-background px-4 py-2 text-sm text-foreground hover:bg-muted"
          >
            Continue offline
          </button>
        </div>
      </form>
    </div>
  )
}
