export type WorkspaceStatus = 'idle' | 'running' | 'completed' | 'errored';

export type AgentProvider = 'claude' | 'codex';

export type AgentStatus =
  | 'starting'
  | 'running'
  | 'completed'
  | 'errored'
  | 'stopped'
  | 'orphaned';

export interface Repo {
  id: string;
  path: string;
  displayName: string;
  defaultBranch?: string | null;
  registeredAt: string;
}

export interface Agent {
  id: string;
  provider: AgentProvider;
  model?: string | null;
  initialPrompt: string;
  status: AgentStatus;
  processId?: number | null;
  exitCode?: number | null;
  startedAt: string;
  endedAt?: string | null;
}

export interface Workspace {
  id: string;
  repoId: string;
  name: string;
  branchName: string;
  baseBranch: string;
  worktreePath: string;
  status: WorkspaceStatus;
  createdAt: string;
  agents: Agent[];
}

export interface CreateRepoRequest {
  path: string;
  displayName?: string;
}

export interface CreateWorkspaceRequest {
  repoId: string;
  name: string;
  branchName: string;
  baseBranch: string;
}

export interface CreateAgentRequest {
  provider: AgentProvider;
  model?: string;
  initialPrompt: string;
  autonomyLevel?: 'auto';
}

export interface DiffHunk {
  header: string;
  oldStart: number;
  oldLines: number;
  newStart: number;
  newLines: number;
  body: string;
}

export interface DiffFile {
  path: string;
  oldPath?: string | null;
  status: 'added' | 'modified' | 'deleted' | 'renamed';
  additions: number;
  deletions: number;
  hunks: DiffHunk[];
}

export interface DiffResult {
  baseBranch: string;
  headBranch: string;
  files: DiffFile[];
}

export interface SystemHealth {
  git: boolean;
  claude: boolean;
  codex: boolean;
  errors: string[];
}
