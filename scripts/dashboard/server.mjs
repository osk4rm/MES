// MES Agent Swarm dashboard — zero-dependency local control panel.
// Run: node scripts/dashboard/server.mjs   (then open http://127.0.0.1:5178)
import http from 'node:http'
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

const AGENTS = [
  'mes-researcher',
  'mes-analyst',
  'mes-implementer',
  'mes-reviewer',
  'mes-verifier',
  'mes-tracker',
]

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

async function getState() {
  const [branch, porcelain, prs, ready, blocked] = await Promise.all([
    sh('git', ['branch', '--show-current']),
    sh('git', ['status', '--porcelain']),
    ghJson(['pr', 'list', '--state', 'open', '--limit', '60', '--json', 'number,title,headRefName,url,isDraft']),
    ghJson(['issue', 'list', '--label', 'ai:implement', '--state', 'open', '--limit', '60', '--json', 'number,title,url']),
    ghJson(['issue', 'list', '--label', 'ai:blocked', '--state', 'open', '--limit', '60', '--json', 'number,title,url']),
  ])
  const dirty = porcelain ? porcelain.split(/\r?\n/).filter(Boolean).length : 0
  return {
    root: ROOT,
    branch: branch || '(detached)',
    dirty,
    prs: (prs || []).filter((p) => (p.headRefName || '').startsWith('ai/')),
    issuesReady: ready || [],
    issuesBlocked: blocked || [],
    running: [...running.values()].sort((a, b) => b.startedAt - a.startedAt),
    agents: AGENTS,
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

function startRun({ kind, agent, prompt }) {
  const id = `${kind === 'loop' ? 'loop' : agent}-${Date.now()}`
  const log = path.join(LOG_DIR, `${id}.log`)
  const env = { ...process.env }
  let script
  if (kind === 'loop') {
    const maxRounds = Math.max(1, Number(prompt.maxRounds) || 3)
    const sync = prompt.syncTracker ? ' -SyncTracker' : ''
    script = `& '${path.join(ROOT, 'scripts', 'agent-loop.ps1')}' -MaxRounds ${maxRounds}${sync} *>&1`
  } else {
    env.MES_PROMPT = prompt.text || 'Proceed with your role.'
    script = `& opencode run --agent '${agent}' $env:MES_PROMPT *>&1`
  }
  const out = fs.createWriteStream(log, { flags: 'a' })
  const child = spawn('pwsh', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', script], {
    cwd: ROOT,
    env,
    windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  child.stdout.pipe(out)
  child.stderr.pipe(out)
  const rec = {
    id,
    kind,
    name: kind === 'loop' ? 'agent-loop.ps1' : agent,
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

async function killRun(id) {
  const rec = running.get(id)
  if (!rec) return false
  await sh('taskkill', ['/PID', String(rec.pid), '/T', '/F'])
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
      if (body.kind === 'loop') {
        return send(res, 200, startRun({ kind: 'loop', prompt: body }))
      }
      if (!AGENTS.includes(body.agent)) return send(res, 400, { error: 'unknown agent' })
      return send(res, 200, startRun({ kind: 'agent', agent: body.agent, prompt: body }))
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
