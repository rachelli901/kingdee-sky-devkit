#!/usr/bin/env node
// Author: rachelli901 <rachelli901@users.noreply.github.com>
// Repository: https://github.com/rachelli901/kingdee-sky-devkit

/**
 * Real-browser (Chrome DevTools Protocol) verification for the single-file
 * console. Launches headless Chrome on a unique temp profile, drives the page
 * with Runtime.evaluate, records per-feature assertions, saves screenshots,
 * then kills only the Chrome instance it started.
 *
 * Usage: node tools/cdp-verify.mjs [path-to-html]
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
const PORT = Number(process.env.CDP_PORT || 9333);
const CHROME = process.env.CHROME_BIN || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const PROFILE = path.join(os.tmpdir(), 'cdp-verify-' + Date.now());
const SHOTS = path.join(PROJ, 'tools', 'evidence');
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
  if (r.exceptionDetails) throw new Error('page exception: ' + JSON.stringify(r.exceptionDetails).slice(0, 500));
  return r.result && r.result.value;
}
async function shot(name) {
  const r = await send('Page.captureScreenshot', { format: 'png' });
  fs.mkdirSync(SHOTS, { recursive: true });
  fs.writeFileSync(path.join(SHOTS, name), Buffer.from(r.data, 'base64'));
}

const PAGE_SCRIPT = `(async () => {
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const out = { ok: [], fail: [], logs: [] };
  const rec = (name, cond, extra) => {
    const line = name + (extra !== undefined ? ' :: ' + String(extra).slice(0, 160) : '');
    (cond ? out.ok : out.fail).push(line);
  };
  const norm = (s) => (s || '').replace(/\\s+/g, ' ').trim();
  const has = (sel, t) => [...document.querySelectorAll(sel)].some((e) => norm(e.innerText).includes(t) || norm(e.textContent).includes(t));
  const byText = (sel, t) => [...document.querySelectorAll(sel)].find((e) => norm(e.innerText).includes(t) || norm(e.textContent).includes(t));
  const byExact = (sel, t) => [...document.querySelectorAll(sel)].find((e) => norm(e.innerText) === t || norm(e.textContent) === t);
  const click = (el) => { if (el) { el.click(); return true; } return false; };
  const fire = (el, type) => { if (el) { el.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window })); return true; } return false; };
  const waitFor = async (fn, ms) => { const t0 = Date.now(); const lim = ms || 12000; while (Date.now() - t0 < lim) { try { if (fn()) return true; } catch (e) {} await sleep(200); } return false; };
  const nav = async (h) => { location.hash = h; await sleep(900); };
  const msgText = () => [...document.querySelectorAll('.ant-message-notice-content, .ant-message span')].map((e) => e.innerText).join(' | ');
  const visibleSwitch = () => document.querySelector('[data-od-id="security-allow-switch"]');
  const tableRows = () => [...document.querySelectorAll('.ant-table-tbody tr:not(.ant-table-measure-row)')].filter((r) => r.closest('main') && !r.classList.contains('ant-table-placeholder')).length;
  const codeText = () => { const e = document.querySelector('.app-code'); return e ? e.innerText : ''; };
  const tags = () => [...document.querySelectorAll('.ant-tag')];
  const setInput = (el, val) => {
    if (!el) return false;
    const proto = Object.getPrototypeOf(el);
    const desc = Object.getOwnPropertyDescriptor(proto, 'value');
    if (desc && desc.set) desc.set.call(el, val); else el.value = val;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    return true;
  };
  const firstNo = () => norm([...document.querySelectorAll('.ant-table-tbody tr td')].map((t) => t.innerText).find((t) => norm(t)) || '');

  try {
    /* ---- 0) boot / render / shell ---- */
    const rendered = await waitFor(() => tableRows() > 0, 30000);
    rec('render.table-rows', rendered, 'rows=' + tableRows());
    rec('shell.data-attr', !!document.querySelector('[data-dumate-shell="foundation-cloud"]'));
    rec('shell.top-nav', !!document.querySelector('header[data-dumate-top-nav]'));
    const navLabels = [...document.querySelectorAll('.side .ant-menu-item')].map((e) => norm(e.innerText));
    rec('nav.item-count=6', navLabels.length === 6, navLabels.join('|'));
    const expect = ['操作台总览', '部署自检', '登录鉴权', '采购订单', '自定义单据', '传输安全策略'];
    rec('nav.labels', expect.every((x) => navLabels.some((l) => l.includes(x))), navLabels.join('|'));
    rec('runtime.no-errs', (window.__errs || []).length === 0, (window.__errs || []).slice(0, 3).join(' ; '));
    const bootEl = document.getElementById('boot');
    rec('boot.replaced', !bootEl || !/正在加载/.test(bootEl.innerText), bootEl ? norm(bootEl.innerText).slice(0, 60) : 'removed');
    const stepCount = (cls) => document.querySelectorAll('.ant-steps-item-' + cls).length;
    rec('overview.selfcheck-stat', has('.ant-statistic', '未运行'), norm([...document.querySelectorAll('.ant-statistic')].map((e) => e.innerText).join(' / ')).slice(0, 100));
    rec('overview.steps-fresh', stepCount('finish') === 0 && stepCount('process') === 1, 'finish=' + stepCount('finish') + ' process=' + stepCount('process') + ' wait=' + stepCount('wait'));
    rec('overview.steps-no-premature-check', document.querySelectorAll('.ant-steps .anticon-check-circle').length === 0, 'check-circle-icons=' + document.querySelectorAll('.ant-steps .anticon-check-circle').length);
    if (window.__probe && typeof window.__probe === 'object') {
      const bad = Object.keys(window.__probe).filter((k) => window.__probe[k] !== 'ok');
      rec('probe.modules', bad.length === 0, bad.length ? bad.join(';') : 'all-ok');
    }

    /* ---- 1) 部署自检：文本 / JSON 输出 + 退出码 + 密钥来源切换 ---- */
    await nav('/selfcheck');
    rec('selfcheck.page', await waitFor(() => byText('button', '运行部署自检')));
    click(byText('button', '运行部署自检'));
    await sleep(1400);
    rec('selfcheck.text-output', /\\[PASS\\]/.test(codeText()) && /ExitCode=0/.test(codeText()), norm(codeText()).slice(0, 90));
    rec('selfcheck.exitcode-tag', tags().some((t) => /ExitCode=0/.test(t.innerText)));
    click(byText('.ant-segmented-item', 'JSON'));
    await sleep(700);
    let parsed = null; try { parsed = JSON.parse(codeText()); } catch (e) {}
    rec('selfcheck.json-output', !!(parsed && parsed.exitCode === 0 && Array.isArray(parsed.checks)), norm(codeText()).slice(0, 70));
    click(byText('.ant-segmented-item', '文本输出'));
    await sleep(600);
    const sel = document.querySelector('.ant-select-selector') || document.querySelector('.ant-select');
    if (fire(sel, 'mousedown')) {
      await sleep(700);
      click(byText('.ant-select-item-option', 'kms'));
      await sleep(800);
    }
    click(byText('button', '运行部署自检'));
    await sleep(1400);
    rec('selfcheck.secret-source-switch', /kms/.test(codeText()) && /\\[PASS\\]/.test(codeText()), norm(codeText()).slice(0, 130));

    /* ---- 2) 登录鉴权：密码模式 + AppID/AppSec 模式 ---- */
    await nav('/auth');
    rec('auth.page', await waitFor(() => byText('button', '登录')));
    click(byText('button', '登录'));
    await sleep(1400);
    rec('auth.password-login-success', has('.ant-alert', '登录成功'), msgText().slice(0, 90));
    click(byText('.ant-segmented-item', 'AppID'));
    await sleep(700);
    rec('auth.appsec-mode', !!byText('button', '登录') && has('label, .ant-typography', 'AppID'));
    click(byText('button', '登录'));
    await sleep(1400);
    rec('auth.appsec-login-success', has('.ant-alert', '登录成功'), msgText().slice(0, 90));

    /* ---- 3) 采购订单全流程 ---- */
    await nav('/purchase');
    rec('purchase.page', await waitFor(() => tableRows() >= 3), 'rows=' + tableRows());
    rec('purchase.seed-3-orders', tableRows() === 3, tableRows());
    click([...document.querySelectorAll('.ant-table-tbody tr .ant-btn')].find((b) => /查看/.test(b.innerText)));
    await sleep(1000);
    rec('purchase.drawer-open', !!document.querySelector('.ant-drawer-open, .ant-drawer-content'),
        document.querySelector('.ant-drawer-title') ? norm(document.querySelector('.ant-drawer-title').innerText) : '');
    click(document.querySelector('.ant-drawer-close'));
    await sleep(800);
    click([...document.querySelectorAll('.ant-table-tbody tr .ant-btn')].find((b) => /提交/.test(b.innerText)));
    await sleep(1400);
    rec('purchase.submit-action', /成功/.test(msgText()), msgText().slice(0, 110));
    rec('purchase.submitted-tag', tags().some((t) => /已提交/.test(t.innerText)));
    click([...document.querySelectorAll('.ant-table-tbody tr .ant-btn')].find((b) => /审核/.test(b.innerText) && !/反审核/.test(b.innerText)));
    await sleep(1400);
    rec('purchase.audit-action', /成功/.test(msgText()), msgText().slice(0, 110));
    rec('purchase.audited-tag', tags().some((t) => /已审核/.test(t.innerText)));
    click(byText('button', '新建草稿'));
    await sleep(1000);
    const modal = document.querySelector('.ant-modal-content');
    rec('purchase.create-modal', !!modal);
    if (modal) {
      const okBtn = [...modal.querySelectorAll('.ant-modal-footer button')].find((b) => /保存/.test(b.innerText)) || [...modal.querySelectorAll('button')].find((b) => /保存/.test(b.innerText));
      click(okBtn);
      await sleep(1600);
      rec('purchase.create-saved', /成功/.test(msgText()) && !document.querySelector('.ant-modal-content'), msgText().slice(0, 110));
      rec('purchase.rows-increased', tableRows() >= 4, 'rows=' + tableRows());
      if (document.querySelector('.ant-modal-content')) click([...document.querySelectorAll('.ant-modal-footer button')].find((b) => /取消/.test(b.innerText)));
      await sleep(600);
    }

    /* ---- 3b) 采购订单：行内删除（证明 Delete 端点未被反审核顶替） ---- */
    const beforeDel = tableRows();
    click(document.querySelector('[data-od-id="purchase-delete"]'));
    await sleep(900);
    const delPop = [...document.querySelectorAll('.ant-popover')].find((p) => !p.classList.contains('ant-popover-hidden') && /删除单据/.test(norm(p.innerText)));
    rec('purchase.delete-confirm', !!delPop, delPop ? norm(delPop.innerText).slice(0, 60) : 'no popconfirm');
    if (delPop) {
      click([...delPop.querySelectorAll('button')].find((b) => norm(b.innerText) === '删除'));
      await sleep(1600);
      rec('purchase.delete-action', /删除成功|成功/.test(msgText()), msgText().slice(0, 110));
      rec('purchase.rows-decreased', tableRows() === beforeDel - 1, 'rows ' + beforeDel + ' -> ' + tableRows());
      if (document.querySelector('.ant-popover:not(.ant-popover-hidden)')) {
        click([...document.querySelectorAll('.ant-popover button')].find((b) => /取消/.test(b.innerText)));
        await sleep(500);
      }
    } else {
      rec('purchase.delete-action', false, 'no confirm dialog');
      rec('purchase.rows-decreased', false, 'rows ' + beforeDel + ' -> ' + tableRows());
    }

    /* ---- 4) 自定义单据 SKY_ExpReimburse ---- */
    await nav('/custom-doc');
    rec('customdoc.page', await waitFor(() => byText('button', '保存并提交')));
    const cd0 = tableRows();
    click(byText('button', '保存（Save）'));
    await sleep(1600);
    rec('customdoc.save-only', /成功/.test(msgText()), msgText().slice(0, 110));
    rec('customdoc.draft-tag', tags().some((t) => /草稿/.test(t.innerText)));
    click(byText('button', '保存并提交'));
    await sleep(1800);
    rec('customdoc.save-submit', /成功/.test(msgText()), msgText().slice(0, 110));
    rec('customdoc.row-created', tableRows() > cd0, 'rows ' + cd0 + ' -> ' + tableRows());
    rec('customdoc.submitted-tag', tags().some((t) => /已提交/.test(t.innerText)));
    const cdRows = tableRows();
    const cdNo = firstNo();
    const cdSearch = document.querySelector('[data-od-id="customdoc-search"]');
    rec('customdoc.search-box', !!cdSearch, cdSearch ? 'ok' : 'missing');
    if (cdSearch && cdNo && cdRows > 1) {
      setInput(cdSearch, cdNo);
      await sleep(800);
      rec('customdoc.search-filter', tableRows() >= 1 && tableRows() < cdRows, 'rows ' + cdRows + ' -> ' + tableRows() + ' kw=' + cdNo);
      setInput(cdSearch, '');
      await sleep(800);
      rec('customdoc.search-clear', tableRows() === cdRows, 'rows=' + tableRows());
    } else {
      rec('customdoc.search-filter', false, 'precondition rows=' + cdRows + ' no=' + cdNo);
      rec('customdoc.search-clear', false, 'precondition');
    }

    /* ---- 5) 传输安全策略 ---- */
    await nav('/security');
    rec('security.page', await waitFor(() => byText('button', '探测 HTTPS')));
    rec('security.default-https', has('.ant-alert', '强制 HTTPS'));
    click(byExact('button', '探测 HTTPS'));
    await sleep(1000);
    click(byExact('button', '探测 HTTP'));
    await sleep(1000);
    rec('security.probe-logs', tableRows() >= 2, 'rows=' + tableRows());
    rec('security.http-blocked', tags().some((t) => /已拦截/.test(t.innerText)), tags().map((t) => norm(t.innerText)).join('/'));
    const allowedBefore = tags().filter((t) => /已放行/.test(t.innerText)).length;
    click(visibleSwitch());
    await sleep(900);
    rec('security.allow-toggled', has('.ant-alert', '放行'));
    click(byExact('button', '探测 HTTP'));
    await sleep(1000);
    const allowedAfter = tags().filter((t) => /已放行/.test(t.innerText)).length;
    rec('security.http-allowed', allowedAfter > allowedBefore, 'allowed ' + allowedBefore + ' -> ' + allowedAfter);
    await nav('/selfcheck');
    click(byText('.ant-segmented-item', '文本输出'));
    await sleep(500);
    click(byText('button', '运行部署自检'));
    await sleep(1400);
    rec('selfcheck.insecure-fails-exit-1', /ExitCode=1/.test(codeText()), norm(codeText()).slice(0, 90));

    /* ---- 6) 数据源双模 / 协议调用日志 / 下推（PUSH） ---- */
    await nav('/purchase');
    await waitFor(() => tableRows() >= 1);
    const dsBtn = document.querySelector('[data-od-id="data-source-btn"]');
    rec('datasource.button', !!dsBtn && /离线|在线/.test(norm(dsBtn.innerText)), dsBtn ? norm(dsBtn.innerText) : 'missing');
    rec('datasource.default-offline', !!dsBtn && /离线/.test(norm(dsBtn.innerText)), dsBtn ? norm(dsBtn.innerText) : '');
    click(document.querySelector('[data-od-id="proto-log-open"]'));
    await sleep(1100);
    const drawerEl = [...document.querySelectorAll('.ant-drawer')].find((d) => d.classList.contains('ant-drawer-open'));
    rec('protolog.drawer-open', !!drawerEl, drawerEl ? norm(drawerEl.innerText).slice(0, 40) : 'no drawer');
    const logRows = [...document.querySelectorAll('.ant-drawer .ant-table-tbody tr')].filter((r) => !r.classList.contains('ant-table-placeholder') && !r.classList.contains('ant-table-measure-row')).length;
    rec('protolog.rows>5', logRows > 5, 'rows=' + logRows);
    click(document.querySelector('.ant-drawer-close'));
    await sleep(900);
    rec('protolog.drawer-closed', ![...document.querySelectorAll('.ant-drawer')].some((d) => d.classList.contains('ant-drawer-open')));
    click(document.querySelector('[data-od-id="purchase-push"]'));
    await sleep(1700);
    const pushModal = document.querySelector('.ant-modal-content');
    rec('push.modal-open', !!pushModal);
    rec('push.success', !!pushModal && /下推成功/.test(norm(pushModal.innerText)), pushModal ? norm(pushModal.innerText).slice(0, 80) : 'no modal');
    if (pushModal) {
      click([...pushModal.querySelectorAll('button')].find((b) => /关闭/.test(b.innerText)));
      await sleep(800);
    }
    const pushModalVisible = () => [...document.querySelectorAll('.ant-modal-content')].some((el) => el.offsetParent !== null);
    rec('push.modal-closed', !pushModalVisible());
    const pRows = tableRows();
    const pNo = firstNo();
    const pSearch = document.querySelector('[data-od-id="purchase-search"]');
    rec('purchase.search-box', !!pSearch, pSearch ? 'ok' : 'missing');
    if (pSearch && pNo && pRows > 1) {
      setInput(pSearch, pNo);
      await sleep(800);
      rec('purchase.search-filter', tableRows() >= 1 && tableRows() < pRows, 'rows ' + pRows + ' -> ' + tableRows() + ' kw=' + pNo);
      setInput(pSearch, '');
      await sleep(800);
      rec('purchase.search-clear', tableRows() === pRows, 'rows=' + tableRows());
    } else {
      rec('purchase.search-filter', false, 'precondition rows=' + pRows + ' no=' + pNo);
      rec('purchase.search-clear', false, 'precondition');
    }

    /* ---- 7) 总览步骤条随演示进度推进（不再恒显对勾） ---- */
    await nav('/overview');
    await waitFor(() => stepCount('finish') >= 1);
    rec('overview.steps-progress', stepCount('finish') >= 1 && stepCount('finish') + stepCount('process') + stepCount('wait') === 5,
      'finish=' + stepCount('finish') + ' process=' + stepCount('process') + ' wait=' + stepCount('wait'));
    const stepIconGlyph = (cls) => [...document.querySelectorAll('.ant-steps-item-' + cls + ' .ant-steps-item-icon')].filter((e) => !!e.querySelector('svg')).length;
    const firstFinishIcon = (() => { const e = document.querySelector('.ant-steps-item-finish .ant-steps-item-icon'); return e ? e.outerHTML.replace(/<svg[\\s\\S]*?<\\/svg>/g, '[svg]').slice(0, 150) : 'none'; })();
    rec('overview.steps-checked-when-done', stepIconGlyph('finish') === stepCount('finish') && stepIconGlyph('finish') > 0,
      'finish-glyph=' + stepIconGlyph('finish') + ' finish=' + stepCount('finish') + ' first=' + firstFinishIcon);
  } catch (e) {
    out.logs.push('SCRIPT ERROR: ' + (e && e.message || e));
  }

  out.summary = { ok: out.ok.length, fail: out.fail.length, total: out.ok.length + out.fail.length };
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
    const result = await evaluate(PAGE_SCRIPT);
    console.log(JSON.stringify(result, null, 2));
    for (const pair of [['/overview', '01-overview.png'], ['/purchase', '02-purchase.png'], ['/security', '03-security.png']]) {
      await evaluate('location.hash="' + pair[0] + '"; true');
      await sleep(1400);
      await shot(pair[1]);
    }
    const pass = result.fail.length === 0 && result.ok.length > 0;
    console.log(pass ? 'CDP_VERIFY: PASS (' + result.ok.length + ' checks)' : 'CDP_VERIFY: FAIL (' + result.fail.length + ' failed of ' + result.summary.total + ')');
    process.exitCode = pass ? 0 : 2;
  } catch (e) {
    console.error('CDP_VERIFY ERROR:', (e && e.stack) || e);
    process.exitCode = 1;
  } finally {
    try { if (ws) ws.close(); } catch (e) {}
    try { chrome.kill(); } catch (e) {}
    await sleep(600);
    try { fs.rmSync(PROFILE, { recursive: true, force: true }); } catch (e) {}
  }
})();