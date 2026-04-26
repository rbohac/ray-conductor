import type {
  Agent,
  CreateAgentRequest,
  CreateRepoRequest,
  CreateWorkspaceRequest,
  DiffResult,
  Repo,
  SystemHealth,
  Workspace,
} from './types';

export class ApiError extends Error {
  status: number;
  payload: unknown;
  constructor(message: string, status: number, payload: unknown) {
    super(message);
    this.status = status;
    this.payload = payload;
  }
}

async function request<T>(
  url: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  const res = await fetch(url, { ...init, headers });
  if (!res.ok) {
    let payload: unknown = null;
    let message = `${res.status} ${res.statusText}`;
    try {
      payload = await res.json();
      if (
        payload
        && typeof payload === 'object'
        && 'error' in payload
        && typeof (payload as { error?: unknown }).error === 'string'
      ) {
        message = (payload as { error: string }).error;
      }
    } catch {
      // body was not json; keep default message
    }
    throw new ApiError(message, res.status, payload);
  }
  if (res.status === 204) {
    return undefined as T;
  }
  return (await res.json()) as T;
}

export const api = {
  health: () => request<SystemHealth>('/api/health'),

  listRepos: () => request<Repo[]>('/api/repos'),
  createRepo: (req: CreateRepoRequest) =>
    request<Repo>('/api/repos', { method: 'POST', body: JSON.stringify(req) }),
  deleteRepo: (id: string) =>
    request<void>(`/api/repos/${id}`, { method: 'DELETE' }),

  listWorkspaces: () => request<Workspace[]>('/api/workspaces'),
  getWorkspace: (id: string) => request<Workspace>(`/api/workspaces/${id}`),
  createWorkspace: (req: CreateWorkspaceRequest) =>
    request<Workspace>('/api/workspaces', {
      method: 'POST',
      body: JSON.stringify(req),
    }),
  deleteWorkspace: (id: string, deleteBranch = true) =>
    request<void>(
      `/api/workspaces/${id}?deleteBranch=${deleteBranch}`,
      { method: 'DELETE' },
    ),

  getDiff: (workspaceId: string) =>
    request<DiffResult>(`/api/workspaces/${workspaceId}/diff`),

  startAgent: (workspaceId: string, req: CreateAgentRequest) =>
    request<Agent>(`/api/workspaces/${workspaceId}/agents`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),
  stopAgent: (workspaceId: string, agentId: string) =>
    request<void>(`/api/workspaces/${workspaceId}/agents/${agentId}`, {
      method: 'DELETE',
    }),
  sendAgentMessage: (workspaceId: string, agentId: string, content: string) =>
    request<void>(
      `/api/workspaces/${workspaceId}/agents/${agentId}/messages`,
      { method: 'POST', body: JSON.stringify({ content }) },
    ),
  stopAllAgents: () =>
    request<void>('/api/agents/stop-all', { method: 'POST' }),

  mergeWorkspace: (id: string) =>
    request<{ ok: boolean; conflicts?: string[] }>(
      `/api/workspaces/${id}/merge`,
      { method: 'POST' },
    ),
};
