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
    <div className="flex h-[calc(100vh-10rem)] gap-0 overflow-hidden rounded-lg border border-gray-200">
      {/* Thread list */}
      <aside className="w-48 flex-none border-r border-gray-200 bg-gray-50">
        <div className="px-3 py-4 text-xs font-semibold uppercase tracking-wide text-gray-500">
          Threads
        </div>
        <nav className="space-y-0.5 px-2">
          {THREADS.map((t) => (
            <button
              key={t.id}
              onClick={() => setActiveThread(t.id)}
              className={`w-full rounded-md px-3 py-2 text-left text-sm ${
                activeThread === t.id
                  ? 'bg-white font-medium text-gray-900 shadow-sm'
                  : 'text-gray-600 hover:bg-white hover:text-gray-900'
              }`}
            >
              # {t.label}
            </button>
          ))}
        </nav>
        <div className="mt-4 px-3">
          <span
            className={`inline-flex items-center gap-1 text-xs ${
              connected ? 'text-green-600' : 'text-gray-400'
            }`}
          >
            <span
              className={`inline-block h-1.5 w-1.5 rounded-full ${
                connected ? 'bg-green-500' : 'bg-gray-400'
              }`}
            />
            {connected ? 'Connected' : 'Disconnected'}
          </span>
        </div>
      </aside>

      {/* Message pane */}
      <div className="flex flex-1 flex-col">
        <div className="flex-1 overflow-y-auto px-4 py-4 space-y-3">
          {threadMessages.length === 0 && (
            <p className="text-center text-sm text-gray-400 mt-8">
              No messages yet. Say hello!
            </p>
          )}
          {threadMessages.map((m, i) => (
            <div key={i} className="flex flex-col gap-0.5">
              <div className="flex items-baseline gap-2">
                <span className="text-sm font-medium text-gray-900">{m.sender}</span>
                <span className="text-xs text-gray-400">
                  {new Date(m.timestamp).toLocaleTimeString()}
                </span>
              </div>
              <p className="text-sm text-gray-700 whitespace-pre-wrap">{m.text}</p>
            </div>
          ))}
          <div ref={bottomRef} />
        </div>

        {/* Input */}
        <div className="border-t border-gray-200 px-4 py-3">
          <div className="flex items-end gap-2">
            <textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={`Message #${THREADS.find((t) => t.id === activeThread)?.label ?? activeThread}`}
              rows={2}
              className="flex-1 resize-none rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
            />
            <button
              onClick={() => { handleSend().catch(() => {}) }}
              disabled={!draft.trim() || !connected}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-40"
            >
              Send
            </button>
          </div>
          <p className="mt-1 text-xs text-gray-400">Enter to send · Shift+Enter for new line</p>
        </div>
      </div>
    </div>
  )
}
