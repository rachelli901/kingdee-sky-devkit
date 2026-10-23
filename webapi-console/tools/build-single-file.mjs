#!/usr/bin/env node
/**
 * Offline single-file builder for the webapi-console (banlan provider).
 *
 * Reads prototype/index.html (dev form: babel.min.js + importmap + text/babel JSX),
 * compiles the ESM dependency graph + app JSX into ONE classic <script> via the
 * vendored Babel standalone (transform-modules-commonjs + react preset), and emits
 * dist/index.html with every style/script inlined so it can be opened via file://.
 */

import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import process from 'node:process';
const nodeRequire = createRequire(import.meta.url);

const PROJ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const PROTOTYPE = path.join(PROJ, 'prototype');
const VENDOR = path.join(PROTOTYPE, 'vendor');
const OUT_DIR = path.join(PROJ, 'dist');
const OUT_FILE = path.join(OUT_DIR, 'index.html');

const BARES = {
  'react': 'esm.sh/react@18.3.1/__entry.js',
  'react/jsx-runtime': 'esm.sh/react@18.3.1/jsx-runtime/__entry.js',
  'react-dom': 'esm.sh/react-dom@18.3.1/__entry.js',
  'react-dom/client': 'esm.sh/react-dom@18.3.1/client/__entry.js',
  'antd': 'esm.sh/antd@5.29.3/__entry.js',
  'antd/locale/zh_CN': 'esm.sh/antd@5.29.3/locale/zh_CN/__entry.js',
  '@ant-design/icons': 'esm.sh/@ant-design/icons@6.1.0/__entry.js',
  'rc-util/lib/Dom/canUseDom': 'esm.sh/rc-util@5.44.4/lib/Dom/canUseDom/__entry.js',
  '@baidu/qianfan-antd-kit': 'qianfan-antd-kit.js',
};

async function loadBabel() {
  const src = await readFile(path.join(VENDOR, 'babel.min.js'), 'utf8');
  const module = { exports: {} };
  // UMD wrapper calls t(exports) when module/exports exist; factory populates exports.Babel
  new Function('module', 'exports', 'require', src)(module, module.exports, nodeRequire);
  const Babel = module.exports.Babel || module.exports;
  if (!Babel || !Babel.version) throw new Error('babel standalone failed to load');
  return Babel;
}

const posix = path.posix;

function stripQuery(spec) {
  const q = spec.indexOf('?');
  return q === -1 ? spec : spec.slice(0, q);
}

// esm.sh locale shims sometimes emit `export{b as __esModule,y as default};`.
// Babel transform-modules-commonjs forbids exporting `__esModule`; strip that
// named export before transform (the flag is irrelevant inside our registry).
function sanitizeEsModuleExport(src) {
  return src.replace(/export\{([^}]*)\};/g, (whole, inner) => {
    const kept = inner.split(',')
      .map((s) => s.trim())
      .filter((it) => it && !/as\s+__esModule\s*$/.test(it));
    return kept.length ? 'export{' + kept.join(',') + '};' : '';
  });
}

function fileExists(id) {
  return new Promise((resolve) => {
    import('node:fs').then((fs) => fs.readFile(path.join(VENDOR, id)).then(() => resolve(true)).catch(() => resolve(false)));
  });
}

function collectStaticSpecifiers(ast) {
  const specs = [];
  const seen = new Set();
  function walk(node) {
    if (!node || typeof node !== 'object') return;
    if (Array.isArray(node)) {
      for (const c of node) walk(c);
      return;
    }
    const type = node.type;
    if ((type === 'ImportDeclaration' || type === 'ExportNamedDeclaration' || type === 'ExportAllDeclaration')
        && node.source && node.source.value && typeof node.source.value === 'string') {
      if (!seen.has(node.source.value)) {
        seen.add(node.source.value);
        specs.push(node.source.value);
      }
    }
    for (const key of Object.keys(node)) {
      if (key === 'loc' || key === 'start' || key === 'end' || key === 'tokens' || key === 'extra' || key === 'directives') continue;
      walk(node[key]);
    }
  }
  walk(ast && ast.program ? ast.program : ast);
  return specs;
}

async function main() {
  const html = await readFile(path.join(PROTOTYPE, 'index.html'), 'utf8');

  // ---- extract app JSX block ----
  const babelBlock = html.match(/<script type="text\/babel" data-type="module" data-presets="react">([\s\S]*?)<\/script>/);
  if (!babelBlock) throw new Error('text/babel app script not found');
  const appSource = babelBlock[1];

  // ---- extract importmap (bare -> vendor file) ----
  const importmapBlock = html.match(/<script type="importmap">([\s\S]*?)<\/script>/);
  if (!importmapBlock) throw new Error('importmap not found');
  const importMap = JSON.parse(importmapBlock[1]).imports || {};
  for (const [bare, file] of Object.entries(importMap)) {
    const rel = file.replace(/^\.\/vendor\//, '').replace(/\?.*$/, '');
    if (BARES[bare] !== undefined && BARES[bare] !== rel) {
      throw new Error(`importmap mismatch for ${bare}: contract ${BARES[bare]} vs html ${rel}`);
    }
  }

  const Babel = await loadBabel();
  console.log(`babel ${Babel.version} loaded`);

  // ---- module registry ----
  const registry = new Map(); // id -> { file, code, deps: Map(spec->id), source }

  async function ensureModule(id, isApp = false) {
    if (registry.has(id)) return;
    if (isApp) {
      registry.set(id, { id, file: null, source: appSource, deps: new Map() });
    } else {
      const file = path.join(VENDOR, id);
      const source = await readFile(file, 'utf8');
      const entry = { id, file, source, deps: new Map() };
      registry.set(id, entry);
    }
  }

  // ---- transform phase (after graph discovery, transform each once) ----
  async function discover(id, isApp = false) {
    if (registry.get(id)?.discovered) return;
    const entry = registry.get(id);
    if (!entry) throw new Error(`registry missing ${id}`);
    entry.discovered = true;
    // Discovery is PARSE-ONLY: no commonjs plugin here, otherwise Babel still runs
    // the plugin even with code:false and returns a transformed AST (imports already
    // replaced by require calls) which collectStaticSpecifiers cannot see.
    const opts = {
      filename: isApp ? 'app.jsx' : id.replace(/\//g, '_'),
      sourceType: 'module',
      ast: true,
      code: false,
      plugins: [],
    };
    if (isApp) opts.presets = [['react', { runtime: 'classic' }]];
    const result = Babel.transform(entry.source, opts);
    const specs = collectStaticSpecifiers(result.ast);
    for (const spec of specs) {
      let targetId;
      if (spec.startsWith('./') || spec.startsWith('../')) {
        const base = stripQuery(spec);
        targetId = posix.normalize(posix.join(posix.dirname(id), base));
      } else {
        targetId = BARES[spec];
        if (!targetId) throw new Error(`unresolved bare import "${spec}" in ${id}`);
      }
      if (id === '__app__') {
        // app source lives outside vendor; bare specs point to vendor ids (already mapped)
      }
      entry.deps.set(spec, targetId);
      if (!registry.has(targetId)) {
        await ensureModule(targetId);
        await discover(targetId);
      }
    }
  }

  await ensureModule('__app__', true);
  await discover('__app__', true);
  console.log(`graph discovered: ${registry.size} modules`);

  // ---- transform to CJS + record require specifiers ----
  for (const entry of registry.values()) {
    if (entry.transformed) continue;
    const isApp = entry.id === '__app__';
    const opts = {
      filename: isApp ? 'app.jsx' : entry.id.replace(/[\/@^]/g, '_'),
      sourceType: 'module',
      ast: false,
      code: true,
      plugins: [['transform-modules-commonjs', { strict: false }]],
    };
    if (isApp) opts.presets = [['react', { runtime: 'classic' }]];
    const src = isApp ? entry.source : sanitizeEsModuleExport(entry.source);
    const result = Babel.transform(src, opts);
    entry.code = result.code;
    entry.transformed = true;
  }

  // ---- augment deps from static require() calls in raw vendor source ----
  // CJS-style vendor files (e.g. rspack UMD-ish output) keep require() calls in
  // source with no import/export to discover; map them so localRequire resolves.
  for (const entry of registry.values()) {
    if (entry.id === '__app__' || !entry.source) continue;
    const re = /\brequire\s*\(\s*("([^"]+)"|'([^']+)')\s*\)/g;
    let m;
    while ((m = re.exec(entry.source)) !== null) {
      const spec = m[2] !== undefined ? m[2] : m[3];
      if (entry.deps.has(spec)) continue;
      let targetId;
      if (spec.startsWith('./') || spec.startsWith('../')) {
        targetId = posix.normalize(posix.join(posix.dirname(entry.id), stripQuery(spec)));
      } else if (BARES[spec]) {
        targetId = BARES[spec];
      } else {
        continue; // unknown spec: leave for runtime diagnostic
      }
      if (targetId && !registry.has(targetId)) {
        await ensureModule(targetId);
        await discover(targetId);
      }
      if (targetId) entry.deps.set(spec, targetId);
    }
  }

  // ---- verify no stray bare requires without mapping (sanity) ----
  for (const entry of registry.values()) {
    const code = entry.code;
    const re = /\brequire\(("([^"]+)"|'([^']+)')\)/g;
    let m;
    while ((m = re.exec(code)) !== null) {
      const spec = m[2] !== undefined ? m[2] : m[3];
      if (spec === 'util') continue; // resolved by runtime util shim
      if (!entry.deps.has(spec)) {
        // allow only local require shims (jsx-runtime defines its own `var require`),
        // but flag anything that looks like a module-level call we missed.
        const pre = code.slice(Math.max(0, m.index - 120), m.index);
        // If previous non-space chars contain "var require" or "= n=>" style local shim, skip.
        if (!/var\s+require\s*=\s*\(?n\s*=>/.test(pre) && !/function\s+require\s*\(/.test(pre)) {
          console.warn(`[warn] un-mapped require(${spec}) in ${entry.id}`);
        }
      }
    }
  }

  // ---- emit bundle ----
  const parts = [];
  for (const entry of registry.values()) {
    const depsObj = Object.create(null);
    for (const [spec, tid] of entry.deps) depsObj[spec] = tid;
    parts.push(JSON.stringify(entry.id) + ': [function (module, exports, require) {\n' + entry.code + '\n}, ' + JSON.stringify(entry.id === '__app__' ? depsObj : depsObj) + ']');
  }
  const modulesLit = '{\n' + parts.join(',\n') + '\n}';

  const bundle = `(function () {
  "use strict";
  if (typeof window !== "undefined" && !window.process) {
    window.process = { env: { NODE_ENV: "production" } };
  }
  var __modules = ${modulesLit};
  var __cache = Object.create(null);
  var __bare = ${JSON.stringify(BARES)};
  var __utilShim = {
    types: {},
    inherits: function (ctor, superCtor) {
      if (superCtor) {
        ctor.prototype = Object.create(superCtor.prototype);
        ctor.prototype.constructor = ctor;
      }
    },
    deprecate: function (fn) { return fn; },
    format: function () { return Array.prototype.slice.call(arguments).join(' '); },
    promisify: function (fn) {
      return function () {
        var self = this;
        var args = Array.prototype.slice.call(arguments);
        return new Promise(function (resolve, reject) {
          args.push(function (err) {
            if (err) return reject(err);
            var rest = Array.prototype.slice.call(arguments, 1);
            resolve(rest.length > 1 ? rest : rest[0]);
          });
          fn.apply(self, args);
        });
      };
    }
  };
  function __norm(baseSegments, spec) {
    var segments = baseSegments.slice();
    var raw = spec.split('/');
    for (var i = 0; i < raw.length; i++) {
      var seg = raw[i];
      if (!seg || seg === '.') continue;
      if (seg === '..') { if (segments.length) segments.pop(); }
      else segments.push(seg);
    }
    return segments.join('/');
  }
  function __req(id) {
    var cached = __cache[id];
    if (cached) return cached.exports;
    var record = __modules[id];
    if (!record) throw new Error("module not found: " + id);
    var module = { exports: {} };
    __cache[id] = module;
    var deps = record[1] || null;
    var fn = record[0];
    var base = id.indexOf('/') === -1 ? [] : id.slice(0, id.lastIndexOf('/')).split('/');
    function localRequire(spec) {
      if (spec === 'util') return __utilShim; // node builtin shim
      var target = deps ? deps[spec] : undefined;
      if (!target && spec.charAt(0) === '.') target = __norm(base, spec);
      if (!target) target = __bare[spec];
      if (!target) throw new Error("unresolved require(\\"" + spec + "\\") in " + id);
      return __req(target);
    }
    fn.call(module.exports, module, module.exports, localRequire);
    return module.exports;
  }
  __req("__app__");
})();`;

  // Neutralise literal HTML tags inside the JS bundle so the static exporter's
  // tag regexes (<style>, <img>, <link>, ...) never mistake JS strings for HTML.
  // \u003c is just "<" inside a JS string literal, so semantics are unchanged.
  const ESC_TAGS = ['style', 'script', 'link', 'img', 'source', 'video', 'audio', 'input', 'embed', 'object'];
  let safeBundle = bundle;
  for (const tag of ESC_TAGS) {
    safeBundle = safeBundle.replace(new RegExp(`</?${tag}\\b`, 'g'), (m) => (m.startsWith('</') ? `\\u003c/${tag}` : `\\u003c${tag}`));
  }

  // ---- assemble final html ----
  const cssLink = html.match(/<link rel="stylesheet" href="\.\/vendor\/qianfan-antd-kit\.css">/);
  if (!cssLink) throw new Error('kit css link not found');
  const kitCss = await readFile(path.join(VENDOR, 'qianfan-antd-kit.css'), 'utf8');

  const boot = `if (typeof window.process === 'undefined') { window.process = {env: {NODE_ENV: 'production'}}; }
window.__errs = [];
window.addEventListener('error', function (e) {
  window.__errs.push(e.message || 'resource error');
  var b = document.getElementById('boot');
  if (b) b.innerHTML = '<pre style="text-align:left;color:#d00;white-space:pre-wrap">' + window.__errs.join('\\n\\n') + '</pre>';
}, true);
window.addEventListener('unhandledrejection', function (e) {
  window.__errs.push('rejection: ' + e.reason);
});
window.__probe = { 'react': 'ok', 'react-dom/client': 'ok', 'antd': 'ok', '@ant-design/icons': 'ok', '@baidu/qianfan-antd-kit': 'ok' };
window.__verify = async function () {
  var sleep = function (ms) { return new Promise(function (r) { setTimeout(r, ms); }); };
  var items = {};
  var t0 = Date.now(), rendered = false;
  while (Date.now() - t0 < 20000) {
    if (document.querySelector('.ant-table-tbody tr')) { rendered = true; break; }
    await sleep(300);
  }
  items.render = rendered;
  items.probe = 'ok';
  items.errs = (window.__errs || []).slice(0, 5);
  items.rows = rendered ? document.querySelectorAll('.ant-table-tbody tr:not(.ant-table-measure-row)').length : 0;
  items.hash = location.hash;
  var sw = document.querySelector('[data-od-id="theme-switch"]');
  var switched = false;
  if (sw) {
    sw.click(); await sleep(500);
    switched = document.documentElement.getAttribute('data-qdk-theme') === 'dark';
    if (switched) { sw.click(); await sleep(200); }
  }
  items.darkMode = { switched: switched };
  items.ok = items.render && items.probe === 'ok' && !items.errs.length && items.rows > 0 && switched;
  return items;
};`;

  const headStart = '<head>';
  const headEnd = '</head>';
  let head = html.slice(0, html.indexOf(headStart) + headStart.length);
  let rest = html.slice(html.indexOf(headStart) + headStart.length);
  const bodyIdx = rest.indexOf('<body>');
  head += rest.slice(0, bodyIdx); // includes all <head> content up to </head>
  // strip legacy scripts/links that must not remain
  head = head
    .replace(/<script src="\.\/vendor\/babel\.min\.js"><\/script>\s*/g, '')
    .replace(/<script type="importmap">[\s\S]*?<\/script>\s*/g, '')
    .replace(/<link rel="stylesheet" href="\.\/vendor\/qianfan-antd-kit\.css">\s*/g, '')
    // Replacer function: a string replacement arg would expand "$&", "$'", "$`"
    // inside kitCss and corrupt any such literal; functions return verbatim.
    .replace(/<style>([\s\S]*)<\/style>/, (whole, inner) => `<style>\n${kitCss}\n${inner}\n</style>`);

  // Provenance metadata: declare the route this bundle was built from. The
  // contract hash is computed at build time so it can never drift from the file.
  const contractFile = path.join(PROJ, '.design-suite', 'b2b-contract.json');
  let contractSha = null;
  let contractProvider = null;
  try {
    const contractBytes = await readFile(contractFile);
    contractSha = createHash('sha256').update(contractBytes).digest('hex');
    contractProvider = JSON.parse(contractBytes.toString('utf8')).provider || null;
  } catch (err) {
    console.warn(`provenance: contract not readable (${err.code || err.message}); design-suite meta skipped`);
  }
  if (contractSha) {
    head = head.replace(/<meta name="design-suite-[a-z-]+" content="[^"]*">\s*/g, '');
    const provenance = [
      '<meta name="design-suite-artifact-kind" content="b2b-page">',
      '<meta name="design-suite-delivery-adapter" content="static-export">',
      '<meta name="design-suite-delivery-format" content="single-html">',
      '<meta name="design-suite-asset-policy" content="inline-base64">',
      '<meta name="design-suite-export-mode" content="single-file">',
      contractProvider ? `<meta name="design-suite-visual-provider" content="${contractProvider}">` : null,
      `<meta name="design-suite-contract-sha256" content="${contractSha}">`,
    ].filter(Boolean).join('\n');
    head = head.replace('</head>', `${provenance}\n</head>`);
  }

  const body = rest.slice(bodyIdx);
  const bodyInner = body.replace(/<script>[\s\S]*?<\/script>\s*/m, () => `<script>\n${boot}\n</script>\n`).replace(/<script type="text\/babel"[^>]*>[\s\S]*?<\/script>\s*/m, '');

  let final = head + bodyInner;
  // Inject the bundle right before the REAL trailing </body>. A string
  // replacement arg would expand every "$&" literal inside safeBundle into the
  // matched "</body>" (String.replace $& semantics) — exactly the corruption
  // seen earlier. A replacer function returns the text verbatim, and
  // lastIndexOf targets the template's own closing tag even if vendor strings
  // contain a literal "</body>".
  const closeTag = '</body>';
  const tagIdx = final.lastIndexOf(closeTag);
  if (tagIdx === -1) throw new Error('sanity: </body> not found');
  final = final.slice(0, tagIdx) + `<script id="bundle">\n${safeBundle}\n</script>\n${closeTag}` + final.slice(tagIdx + closeTag.length);

  if (final.includes('</script>') === false) throw new Error('sanity: no closing script tag?');
  // assert bundle does not contain literal "</script>" (would break html parsing)
  if (bundle.includes('</script>')) throw new Error('bundle contains literal </script>');

  await mkdir(OUT_DIR, { recursive: true });
  await writeFile(OUT_FILE, final, 'utf8');

  const size = Buffer.byteLength(final, 'utf8');
  console.log(`wrote ${OUT_FILE} (${size} bytes, ${registry.size} modules)`);
  console.log('module ids:');
  for (const id of registry.keys()) console.log('  -', id);
}

main().catch((error) => {
  console.error('BUILD FAILED:', error && error.stack || error);
  process.exitCode = 1;
});