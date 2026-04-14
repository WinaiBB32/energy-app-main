import * as signalR from '@microsoft/signalr'

let connection: signalR.HubConnection | null = null

function resolveHubUrl(): string {
  const apiBase = import.meta.env.VITE_API_URL || 'http://localhost:5008/api/v1'
  const root = apiBase.replace(/\/api\/v1\/?$/i, '')
  return `${root}/hubs/maintenance`
}

function buildConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(resolveHubUrl(), {
      accessTokenFactory: () => localStorage.getItem('jwt_token') || '',
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.None)
    .build()
}

export async function startRealtimeConnection(): Promise<signalR.HubConnection> {
  if (!connection) {
    connection = buildConnection()
  }

  if (connection.state === signalR.HubConnectionState.Disconnected) {
    await connection.start()
  }

  return connection
}

export async function joinRequestGroup(requestId: string): Promise<void> {
  const conn = await startRealtimeConnection()
  await conn.invoke('JoinRequestGroup', requestId)
}

export async function leaveRequestGroup(requestId: string): Promise<void> {
  if (!connection || connection.state !== signalR.HubConnectionState.Connected) return
  await connection.invoke('LeaveRequestGroup', requestId)
}

export function onRealtimeEvent(eventName: string, callback: (...args: unknown[]) => void): void {
  if (!connection) return
  connection.on(eventName, callback)
}

export function offRealtimeEvent(eventName: string, callback: (...args: unknown[]) => void): void {
  if (!connection) return
  connection.off(eventName, callback)
}
