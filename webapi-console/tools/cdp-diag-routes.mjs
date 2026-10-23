#!/usr/bin/env node
// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

/** Route-by-route diagnostic: load dist/index.html and report per-hash errors. */
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { spawn } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const PROJ = path.resolve(here, '..');
const target = path.join(PROJ, 'dist', 'index.html');
const PORT = Number(process.env.CDP_PORT || 9344);
const CHROME = process.env.CHROME_BIN || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PROFILE = path.join(os.tmpdir(), 'cdp-diag-' + Date.now());
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function getJson(u) {
  return new Promise((res, rej) => {
    http.get(u, (r) => { let d = ''; r.on('data', (c) => (d += c)); r.on('end', () => { try { res(JSON.parse(d)); } catch (e) { rej(e); } }); }).on('error', rej);
  });
}

const chrome = spawn(CHROME, ['--headless=new', '--remote-debugging-port=' + PORT, '--remote-allow-origins=*', '--user-data-dir=' + PROFILE, '--no-first-run', '--disable-gpu', '--no-default-browser-check', 'about:blank'], { stdio: 'ignore' });
let ws = null, id = 0;
const waiting = new Map();
function send(m, p) { const i = ++id; return new Promise((res, rej) => { waiting.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method: m, params: p || {} })); }); }
async function ev(e) { const r = await send('Runtime.evaluate', { expression: e, awaitPromise: true, returnByValue: true }); return r.result && r.result.value; }

try {
  let page = null;
  for (let i = 0; i < 60 && !page; i++) { try { const l = await getJson('http://127.0.0.1:' + PORT + '/json/list'); page = l.find((t) => t.type === 'page'); } catch (e) {} if (!page) await sleep(400); }
  ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
  ws.onmessage = (e) => { const m = JSON.parse(e.data); if (m.id && waiting.has(m.id)) { const p = waiting.get(m.id); waiting.delete(m.id); if (m.error) p.rej(new Error(JSON.stringify(m.error))); else p.res(m.result); } };
  await send('Page.enable');
  await send('Runtime.enable');
  await send('Page.navigate', { url: pathToFileURL(target).href });
  await sleep(4500);
  const routes = ['/overview', '/selfcheck', '/auth', '/purchase', '/custom-doc', '/security'];
  const out = [];
  for (const h of routes) {
    await ev('location.hash="' + h + '"; true');
    await sleep(2600);
    const info = await ev(`(()=>{const r=document.getElementById('root');return {hash:location.hash, errs:(window.__errs||[]).slice(0,6), rootLen:r?r.innerHTML.length:0, rows:document.querySelectorAll('.ant-table-tbody tr').length, btns:[...document.querySelectorAll('button')].map(b=>(b.innerText||'').trim()).filter(Boolean).slice(0,10), text:(r?r.innerText:'').replace(/\\s+/g,' ').slice(0,200)}})()`);
    out.push(info);
  }
  console.log(JSON.stringify(out, null, 2));
} catch (e) {
  console.error('DIAG ERROR:', (e && e.stack) || e);
  process.exitCode = 1;
} finally {
  try { if (ws) ws.close(); } catch (e) {}
  try { chrome.kill(); } catch (e) {}
  await sleep(600);
  try { fs.rmSync(PROFILE, { recursive: true, force: true }); } catch (e) {}
}