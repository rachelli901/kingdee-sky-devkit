#!/usr/bin/env node
// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

/**
 * Focused diagnosis: (a) antd Select interaction on /selfcheck, (b) result tags
 * on /security. Reuses the CDP harness pattern from cdp-verify.mjs.
 *
 * Usage: node tools/cdp-diag2.mjs [path-to-html]
 */
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { spawn } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const PROJ = path.resolve(here, '..');
const target = process.argv[2] ? path.resolve(process.argv[2]) : path.join(PROJ, 'dist', 'index.html');
const PORT = Number(process.env.CDP_PORT || 9345);
const CHROME = process.env.CHROME_BIN || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PROFILE = path.join(os.tmpdir(), 'cdp-diag2-' + Date.now());
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function getJson(url) {
  return new Promise((resolve, reject) => {
    http.get(url, (res) => {
      let d = '';
      res.on('data', (c) => (d += c));
      res.on('end', () => { try { resolve(JSON.parse(d)); } catch (e) { reject(e); } });
    }).on('error', reject);
  });
}

const chrome = spawn(CHROME, [
  '--headless=new',
  '--remote-debugging-port=' + PORT,
  '--remote-allow-origins=*',
  '--user-data-dir=' + PROFILE,
  '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--disable-extensions',
  '--window-size=1500,1000',
  'about:blank',
], { stdio: 'ignore' });

let ws = null;
let msgId = 0;
const waiting = new Map();

function send(method, params) {
  const id = ++msgId;
  return new Promise((resolve, reject) => {
    waiting.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, method, params: params || {} }));
  });
}
async function evaluate(expression) {
  const r = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error('page exception: ' + JSON.stringify(r.exceptionDetails).slice(0, 600));
  return r.result && r.result.value;
}

const SCRIPT = `(async () => {
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const out = {};
  const norm = (s) => (s || '').replace(/\\s+/g, ' ').trim();
  const byText = (sel, t) => [...document.querySelectorAll(sel)].find((e) => norm(e.innerText).includes(t) || norm(e.textContent).includes(t));

  /* ---- A) Select on /selfcheck ---- */
  location.hash = '/selfcheck'; await sleep(1200);
  const selCount = document.querySelectorAll('.ant-select').length;
  const selectorCount = document.querySelectorAll('.ant-select-selector').length;
  out.select = { selCount, selectorCount,
    classes: [...document.querySelectorAll('.ant-select')].map((e) => e.className).slice(0, 4) };
  const sel = document.querySelector('.ant-select-selector') || document.querySelector('.ant-select');
  const fire = (el, type) => el && el.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
  fire(sel, 'mousedown');
  await sleep(700);
  out.afterMousedown = {
    dropdowns: document.querySelectorAll('.ant-select-dropdown').length,
    openDropdowns: document.querySelectorAll('.ant-select-dropdown:not(.ant-select-dropdown-hidden)').length,
    options: [...document.querySelectorAll('.ant-select-item-option')].map((e) => norm(e.innerText)).slice(0, 8),
  };
  const kms = byText('.ant-select-item-option', 'kms');
  if (kms) { fire(kms, 'mousedown'); kms.click(); await sleep(500); fire(kms, 'click'); await sleep(500); }
  out.afterPick = { hasKms: !!kms, selected: norm((document.querySelector('.ant-select-selection-item') || {}).innerText || '') };

  /* ---- B) security tags on /security ---- */
  location.hash = '/security'; await sleep(1200);
  byText('button', '探测 HTTPS') && byText('button', '探测 HTTPS').click();
  await sleep(1000);
  out.afterHttps = { tags: [...document.querySelectorAll('.ant-tag')].map((e) => norm(e.innerText)),
    rows: document.querySelectorAll('.ant-table-tbody tr:not(.ant-table-measure-row)').length,
    tbody: norm((document.querySelector('.ant-table-tbody') || {}).innerText || '').slice(0, 400) };
  byText('button', '探测 HTTP') && byText('button', '探测 HTTP').click();
  await sleep(1200);
  out.afterHttpBlocked = { tags: [...document.querySelectorAll('.ant-tag')].map((e) => norm(e.innerText)),
    alertMsgs: [...document.querySelectorAll('.ant-alert-message')].map((e) => norm(e.innerText)),
    tbody: norm((document.querySelector('.ant-table-tbody') || {}).innerText || '').slice(0, 500) };
  const sw = [...document.querySelectorAll('.ant-switch')].find((e) => e.offsetParent !== null);
  if (sw) { sw.click(); await sleep(900); }
  out.afterToggle = { tags: [...document.querySelectorAll('.ant-tag')].map((e) => norm(e.innerText)),
    alerts: [...document.querySelectorAll('.ant-alert')].map((e) => norm(e.innerText).slice(0, 80)) };
  byText('button', '探测 HTTP') && byText('button', '探测 HTTP').click();
  await sleep(1200);
  out.afterHttpAllowed = { tags: [...document.querySelectorAll('.ant-tag')].map((e) => norm(e.innerText)),
    tbody: norm((document.querySelector('.ant-table-tbody') || {}).innerText || '').slice(0, 500) };

  out.errs = (window.__errs || []).slice(0, 5);
  return out;
})()`;

(async () => {
  try {
    let page = null;
    for (let i = 0; i < 60 && !page; i++) {
      try {
        const list = await getJson('http://127.0.0.1:' + PORT + '/json/list');
        page = list.find((t) => t.type === 'page');
      } catch (e) {}
      if (!page) await sleep(400);
    }
    if (!page) throw new Error('devtools target not found on port ' + PORT);
    ws = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
    ws.onmessage = (ev) => {
      const m = JSON.parse(ev.data);
      if (m.id && waiting.has(m.id)) {
        const p = waiting.get(m.id);
        waiting.delete(m.id);
        if (m.error) p.reject(new Error(JSON.stringify(m.error)));
        else p.resolve(m.result);
      }
    };
    await send('Page.enable');
    await send('Runtime.enable');
    await send('Page.navigate', { url: pathToFileURL(target).href });
    await sleep(3500);
    const result = await evaluate(SCRIPT);
    console.log(JSON.stringify(result, null, 2));
  } catch (e) {
    console.error('DIAG ERROR:', (e && e.stack) || e);
    process.exitCode = 1;
  } finally {
    try { if (ws) ws.close(); } catch (e) {}
    try { chrome.kill(); } catch (e) {}
    await sleep(600);
    try { fs.rmSync(PROFILE, { recursive: true, force: true }); } catch (e) {}
  }
})();