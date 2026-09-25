// MES Agent Swarm dashboard — zero-dependency local control panel.
// Run: node scripts/dashboard/server.mjs   (then open http://127.0.0.1:5178)
//
// Manual controls: mes-researcher, mes-analyst, mes-e2e-tester.
// Everything else (implementer/reviewer/e2e transitions + feature tracker) is
// driven automatically by scripts/agent-dispatcher.ps1.
import http from 'node:http'
import net from 'node:net'
import { execFile, spawn } from 'node:child_process'
import { promisify } from 'node:util'
import fs from 'node:fs'
import fsp from 'node:fs/promises'
import path from 'node:path'
import os from 'node:os'
import { fileURLToPath } from 'node:url'

const execFileP = promisify(execFile)
const __dirname = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.resolve(__dirname, '..', '..')
const LOG_DIR = path.join(os.tmpdir(), 'opencode')
const PORT = Number(process.env.DASHBOARD_PORT || 5178)
const HOST = '127.0.0.1'

// Agents a human may start by hand from the dashboard.
const MANUAL_AGENTS = ['mes-researcher', 'mes-analyst', 'mes-e2e-tester']
const BACKEND_PORT = 5243
const FRONTEND_PORT = 5173

const running = new Map()

async function sh(cmd, args, opts = {}) {
  try {
    const { stdout } = await execFileP(cmd, args, {
      cwd: ROOT,
      windowsHide: true,
      maxBuffer: 20 * 1024 * 1024,
      timeout: 20000,
      ...opts,
    })
    return stdout.trim()
  } catch {
    return ''
  }
}

async function ghJson(args) {
  const out = await sh('gh', args)
  try {
    return JSON.parse(out)
  } catch {
    return []
  }
}

function tcp(port) {
  return new Promise((resolve) => {
    const socket = net.connect(port, '127.0.0.1')
    socket.setTimeout(700)
    socket.on('connect', () => { socket.destroy(); resolve(true) })
    socket.on('error', () => resolve(false))
    socket.on('timeout', () => { socket.destroy(); resolve(false) })
  })
}

function labelNames(item) {
  return (item.labels || []).map((l) => (typeof l === 'string' ? l : l.name))
}

function stageOf(labels) {
  const has = (n) => labels.includes(n)
  if (has('ai:blocked')) return 'blocked'
  if (has('ai:ready')) return 'ready'
  if (has('ai:e2e')) return 'e2e'
  if (has('ai:changes')) return 'changes'
  if (has('ai:review')) return 'review'
  if (has('ai:verify')) return 'verify'
  if (has('ai:running')) return 'running'
  if (has('ai:implement')) return 'queued'
  return 'other'
}

function checksOf(rollup) {
  if (!rollup || !rollup.length) return 'none'
  const states = rollup.map((c) => String(c.conclusion || c.state || '').toUpperCase())
  const fail = ['FAILURE', 'ERROR', 'CANCELLED', 'TIMED_OUT', 'ACTION_REQUIRED', 'STARTUP_FAILURE']
  const pend = ['PENDING', 'QUEUED', 'IN_PROGRESS', 'STALE', 'EXPECTED', '']
  if (states.some((s) => fail.includes(s))) return 'fail'
  if (states.some((s) => pend.includes(s))) return 'pending'
  return 'pass'
}

async function getState() {
  const [branch, porcelain, issues, prs, backendUp, frontendUp] = await Promise.all([
    sh('git', ['branch', '--show-current']),
    sh('git', ['status', '--porcelain']),
    ghJson(['issue', 'list', '--state', 'open', '--limit', '100', '--json', 'number,title,url,labels']),
    ghJson(['pr', 'list', '--state', 'open', '--limit', '100', '--json', 'number,title,url,labels,headRefName,isDraft,statusCheckRollup']),
    tcp(BACKEND_PORT),
    tcp(FRONTEND_PORT),
  ])

  const dirty = porcelain ? porcelain.split(/\r?\n/).filter(Boolean).length : 0

  const pipeline = []
  for (const i of issues || []) {
    const labels = labelNames(i)
    if (!labels.some((l) => l.startsWith('ai:'))) continue
    pipeline.push({ kind: 'issue', number: i.number, title: i.title, url: i.url, labels, stage: stageOf(labels) })
  }
  for (const p of prs || []) {
    const labels = labelNames(p)
    const isAi = labels.some((l) => l.startsWith('ai:')) || (p.headRefName || '').startsWith('ai/')
    if (!isAi) continue
    pipeline.push({
      kind: 'pr', number: p.number, title: p.title, url: p.url, labels,
      branch: p.headRefName, draft: p.isDraft,
      stage: stageOf(labels), checks: checksOf(p.statusCheckRollup),
    })
  }
  const order = { changes: 0, review: 1, verify: 2, e2e: 3, queued: 4, running: 5, ready: 6, blocked: 7, other: 8 }
  pipeline.sort((a, b) => (order[a.stage] ?? 9) - (order[b.stage] ?? 9) || b.number - a.number)

  const dispatcher = [...running.values()].find((r) => r.kind === 'dispatcher' && r.exitCode == null) || null

  const queued = pipeline.filter((p) => p.stage === 'queued').length
  const inFlight = pipeline.filter((p) => labelNames({ labels: p.labels }).includes('ai:running')).length

  return {
    root: ROOT,
    branch: branch || '(detached)',
    dirty,
    queue: { queued, inFlight, limit: 3 },
    pipeline,
    dispatcher: dispatcher
      ? { id: dispatcher.id, pid: dispatcher.pid, startedAt: dispatcher.startedAt, logName: dispatcher.logName }
      : null,
    app: { backendUp, frontendUp, backendUrl: `http://localhost:${BACKEND_PORT}`, frontendUrl: `http://localhost:${FRONTEND_PORT}` },
    running: [...running.values()].sort((a, b) => b.startedAt - a.startedAt),
    agents: MANUAL_AGENTS,
  }
}

async function getLogs() {
  let files = []
  try {
    files = await fsp.readdir(LOG_DIR)
  } catch {
    return []
  }
  const logs = []
  for (const f of files.filter((f) => f.endsWith('.log'))) {
    const full = path.join(LOG_DIR, f)
    try {
      const st = await fsp.stat(full)
      const content = await fsp.readFile(full, 'utf8')
      logs.push({
        name: f,
        mtime: st.mtimeMs,
        size: st.size,
        tail: content.split(/\r?\n/).slice(-200).join('\n'),
      })
    } catch {
      /* ignore */
    }
  }
  logs.sort((a, b) => b.mtime - a.mtime)
  return logs
}

function spawnScript({ id, name, script, env = {} }) {
  const log = path.join(LOG_DIR, `${id}.log`)
  const out = fs.createWriteStream(log, { flags: 'a' })
  const pwshArgs = process.platform === 'win32'
    ? ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', script]
    : ['-NoProfile', '-Command', script]
  const child = spawn('pwsh', pwshArgs, {
    cwd: ROOT,
    env: { ...process.env, ...env },
    windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  child.stdout.pipe(out)
  child.stderr.pipe(out)
  const rec = {
    id,
    name,
    pid: child.pid,
    log,
    logName: path.basename(log),
    startedAt: Date.now(),
    exitCode: null,
  }
  running.set(id, rec)
  child.on('exit', (code) => {
    rec.exitCode = code
    rec.endedAt = Date.now()
  })
  return rec
}

function startRun({ kind, agent, prompt }) {
  if (kind === 'dispatcher') {
    const interval = Math.max(5, Number(prompt.intervalSeconds) || 20)
    const maxRounds = Math.max(0, Number(prompt.maxRounds) || 0)
    const noTracker = prompt.noTracker ? ' -NoTracker' : ''
    return spawnScript({
      id: `dispatcher-${Date.now()}`,
      name: 'agent-dispatcher.ps1',
      script: `& '${path.join(ROOT, 'scripts', 'agent-dispatcher.ps1')}' -IntervalSeconds ${interval} -MaxRounds ${maxRounds}${noTracker} *>&1`,
    })
  }
  return spawnScript({
    id: `${agent}-${Date.now()}`,
    name: agent,
    script: `& opencode run --agent '${agent}' $env:MES_PROMPT *>&1`,
    env: { MES_PROMPT: prompt.text || 'Proceed with your role.' },
  })
}

function startApp(action) {
  return spawnScript({
    id: `e2e-app-${action}-${Date.now()}`,
    name: `e2e app ${action}`,
    script: `& '${path.join(ROOT, 'scripts', 'e2e', 'app.ps1')}' -Action ${action} *>&1`,
  })
}

async function killRun(id) {
  const rec = running.get(id)
  if (!rec) return false
  if (process.platform === 'win32') {
    await sh('taskkill', ['/PID', String(rec.pid), '/T', '/F'])
  } else {
    try { process.kill(rec.pid, 'SIGKILL') } catch { /* already gone */ }
  }
  rec.exitCode = rec.exitCode ?? -1
  rec.endedAt = Date.now()
  return true
}

function send(res, code, body, type = 'application/json') {
  const data = type === 'application/json' ? JSON.stringify(body) : body
  res.writeHead(code, { 'content-type': type, 'cache-control': 'no-store' })
  res.end(data)
}

async function readBody(req) {
  const chunks = []
  for await (const c of req) chunks.push(c)
  if (!chunks.length) return {}
  try {
    return JSON.parse(Buffer.concat(chunks).toString('utf8'))
  } catch {
    return {}
  }
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${HOST}`)
  try {
    if (req.method === 'GET' && (url.pathname === '/' || url.pathname === '/index.html')) {
      const html = await fsp.readFile(path.join(__dirname, 'index.html'), 'utf8')
      return send(res, 200, html, 'text/html; charset=utf-8')
    }
    if (req.method === 'GET' && url.pathname === '/favicon.ico') {
      return send(res, 204, '')
    }
    if (req.method === 'GET' && url.pathname === '/api/state') {
      return send(res, 200, await getState())
    }
    if (req.method === 'GET' && url.pathname === '/api/logs') {
      return send(res, 200, await getLogs())
    }
    if (req.method === 'POST' && url.pathname === '/api/run') {
      const body = await readBody(req)
      if (body.kind === 'dispatcher') return send(res, 200, startRun({ kind: 'dispatcher', prompt: body }))
      if (!MANUAL_AGENTS.includes(body.agent)) return send(res, 400, { error: 'agent not manually runnable' })
      return send(res, 200, startRun({ kind: 'agent', agent: body.agent, prompt: body }))
    }
    if (req.method === 'POST' && url.pathname === '/api/app') {
      const body = await readBody(req)
      if (!['start', 'stop', 'status'].includes(body.action)) return send(res, 400, { error: 'bad action' })
      if (body.action === 'status') {
        return send(res, 200, { backendUp: await tcp(BACKEND_PORT), frontendUp: await tcp(FRONTEND_PORT) })
      }
      return send(res, 200, startApp(body.action))
    }
    if (req.method === 'POST' && url.pathname === '/api/kill') {
      const body = await readBody(req)
      const ok = await killRun(body.id)
      return send(res, ok ? 200 : 404, { ok })
    }
    return send(res, 404, { error: 'not found' })
  } catch (e) {
    return send(res, 500, { error: String(e) })
  }
})

fs.mkdirSync(LOG_DIR, { recursive: true })
server.listen(PORT, HOST, () => {
  console.log(`MES Agent Swarm dashboard -> http://${HOST}:${PORT}`)
  console.log(`repo: ${ROOT}`)
  console.log(`logs: ${LOG_DIR}`)
})
