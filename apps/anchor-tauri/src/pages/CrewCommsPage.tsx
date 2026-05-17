import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'

interface Message {
  threadId: string
  sender: string
  text: string
  timestamp: string
}

const THREADS = [
  { id: 'general', label: 'General' },
  { id: 'maintenance', label: 'Maintenance' },
  { id: 'payments', label: 'Payments' },
]

export function CrewCommsPage() {
  const [activeThread, setActiveThread] = useState(THREADS[0].id)
  const [messages, setMessages] = useState<Message[]>([])
  const [draft, setDraft] = useState('')
  const [connected, setConnected] = useState(false)
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/bridge', { withCredentials: true })
      .withAutomaticReconnect()
      .build()

    conn.on('ReceiveMessage', (threadId: string, sender: string, text: string, timestamp: string) => {
      setMessages((prev) => [...prev, { threadId, sender, text, timestamp }])
    })

    conn
      .start()
      .then(() => {
        setConnected(true)
        return conn.invoke('JoinThread', activeThread)
      })
      .catch((err) => {
        console.warn('SignalR connection failed (Bridge may not be running):', err)
      })

    connectionRef.current = conn

    return () => {
      conn.invoke('LeaveThread', activeThread).catch(() => {})
      conn.stop().catch(() => {})
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    const conn = connectionRef.current
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) return
    conn.invoke('LeaveThread', activeThread).catch(() => {})
    conn.invoke('JoinThread', activeThread).catch(() => {})
  }, [activeThread])

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const threadMessages = messages.filter((m) => m.threadId === activeThread)

  async function handleSend() {
    const text = draft.trim()
    if (!text) return
    const conn = connectionRef.current
    if (!conn || conn.state !== signalR.HubConnectionState.Connected) {
      alert('Not connected to Bridge. Make sure Bridge is running.')
      return
    }
    await conn.invoke('SendMessage', activeThread, text)
    setDraft('')
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend().catch(() => {})
    }
  }

  return (
    <div className="flex h-[calc(100vh-10rem)] gap-0 overflow-hidden rounded-lg border border-border bg-card">
      {/* Thread list */}
      <aside className="w-48 flex-none border-r border-border bg-muted">
        <div className="px-3 py-4 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          Threads
        </div>
        <nav className="space-y-0.5 px-2">
          {THREADS.map((t) => (
            <button
              key={t.id}
              onClick={() => setActiveThread(t.id)}
              className={`w-full rounded-md px-3 py-2 text-left text-sm ${
                activeThread === t.id
                  ? 'bg-background font-medium text-foreground shadow-sm'
                  : 'text-muted-foreground hover:bg-background hover:text-foreground'
              }`}
            >
              # {t.label}
            </button>
          ))}
        </nav>
        <div className="mt-4 px-3">
          <span
            className={`inline-flex items-center gap-1 text-xs ${
              connected ? 'text-status-online' : 'text-status-offline'
            }`}
          >
            <span
              className={`inline-block h-1.5 w-1.5 rounded-full ${
                connected ? 'bg-status-online' : 'bg-status-offline'
              }`}
            />
            {connected ? 'Connected' : 'Disconnected'}
          </span>
        </div>
      </aside>

      {/* Message pane */}
      <div className="flex flex-1 flex-col bg-background">
        <div className="flex-1 overflow-y-auto px-4 py-4 space-y-3">
          {threadMessages.length === 0 && (
            <p className="text-center text-sm text-muted-foreground mt-8">
              No messages yet. Say hello!
            </p>
          )}
          {threadMessages.map((m, i) => (
            <div key={i} className="flex flex-col gap-0.5">
              <div className="flex items-baseline gap-2">
                <span className="text-sm font-medium text-foreground">{m.sender}</span>
                <span className="text-xs text-muted-foreground">
                  {new Date(m.timestamp).toLocaleTimeString()}
                </span>
              </div>
              <p className="text-sm text-foreground whitespace-pre-wrap">{m.text}</p>
            </div>
          ))}
          <div ref={bottomRef} />
        </div>

        {/* Input */}
        <div className="border-t border-border px-4 py-3">
          <div className="flex items-end gap-2">
            <textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={`Message #${THREADS.find((t) => t.id === activeThread)?.label ?? activeThread}`}
              rows={2}
              className="flex-1 resize-none rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:border-ring focus:outline-none focus:ring-2 focus:ring-ring"
            />
            <button
              onClick={() => { handleSend().catch(() => {}) }}
              disabled={!draft.trim() || !connected}
              className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-40"
            >
              Send
            </button>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">Enter to send · Shift+Enter for new line</p>
        </div>
      </div>
    </div>
  )
}
