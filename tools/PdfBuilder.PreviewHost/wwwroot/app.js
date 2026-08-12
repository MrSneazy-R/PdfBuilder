const state = { manifest: null, page: 1, trace: [], hierarchy: [] };
const $ = id => document.getElementById(id);

async function request(url, options) {
  const response = await fetch(url, options);
  if (!response.ok) {
    let problem;
    try { problem = await response.json(); } catch { problem = { message: response.statusText }; }
    throw problem;
  }
  return response.json();
}

function showError(error) {
  const panel = $('error');
  panel.hidden = false;
  panel.textContent = `${error.type || 'PreviewError'}: ${error.message || error}`;
}

async function load() {
  try {
    $('error').hidden = true;
    [state.manifest, state.trace, state.hierarchy] = await Promise.all([
      request('/api/manifest'), request('/api/trace'), request('/api/hierarchy')
    ]);
    if (state.page > state.manifest.pages.length) state.page = 1;
    render();
  } catch (error) { showError(error); }
}

function render() {
  renderThumbnails();
  renderPage();
  renderMetrics();
  $('hierarchy').innerHTML = hierarchyHtml(state.hierarchy);
  renderTiming();
  renderTrace();
}

function renderThumbnails() {
  $('thumbnails').innerHTML = state.manifest.pages.map(page => `
    <button class="thumbnail ${page.number === state.page ? 'selected' : ''}" data-page="${page.number}">
      <img src="/api/pages/${page.number}.png?dpi=48&guides=true" alt="Page ${page.number}"><span>Page ${page.number}</span>
    </button>`).join('');
  document.querySelectorAll('.thumbnail').forEach(button => button.onclick = () => {
    state.page = Number(button.dataset.page); renderThumbnails(); renderPage();
  });
}

function renderPage() {
  const page = state.manifest.pages.find(item => item.number === state.page);
  const guides = $('guides').checked;
  $('page').src = `/api/pages/${state.page}.png?dpi=120&guides=${guides}&v=${Date.parse(state.manifest.generatedUtc)}`;
  const overlay = $('margin-overlay');
  overlay.style.left = `${page.marginLeft / page.width * 100}%`;
  overlay.style.right = `${page.marginRight / page.width * 100}%`;
  overlay.style.top = `${page.marginTop / page.height * 100}%`;
  overlay.style.bottom = `${page.marginBottom / page.height * 100}%`;
  overlay.hidden = !$('margins').checked;
}

function renderMetrics() {
  const m = state.manifest.generation || {};
  const rows = [['Pages', state.manifest.pages.length], ['PDF bytes', state.manifest.pdfBytes.toLocaleString()], ['Objects', m.objectsWritten ?? '—'], ['Images', m.uniqueImageResources ?? 0], ['Elapsed', m.elapsed || '—'], ['Trace events', state.manifest.traceEvents]];
  $('metrics').innerHTML = rows.map(([k,v]) => `<dt>${k}</dt><dd>${v}</dd>`).join('');
}

function hierarchyHtml(nodes) {
  if (!nodes?.length) return '<p>No trace hierarchy.</p>';
  return `<ul>${nodes.map(node => `<li><span>${escapeHtml(node.name)}</span><small>${node.events || ''}</small>${hierarchyHtml(node.children)}</li>`).join('')}</ul>`;
}

function renderTiming() {
  const entries = state.manifest.timing.entries || [];
  $('timing').innerHTML = entries.length ? `<table><thead><tr><th>Component</th><th>Measure ms</th><th>Draw ms</th><th>Cache</th></tr></thead><tbody>${entries.map(entry => `<tr><td>${escapeHtml(entry.component)}</td><td>${entry.measureTotalMs.toFixed(2)}</td><td>${entry.drawTotalMs.toFixed(2)}</td><td>${entry.cacheHits}</td></tr>`).join('')}</tbody></table>` : '<p>No timing data.</p>';
}

function renderTrace() {
  const entries = state.trace.filter(entry => entry.pageNumber === state.page).slice(0, 250);
  $('trace').innerHTML = `<table><thead><tr><th>Event</th><th>Component / label</th><th>Path</th><th>ms</th></tr></thead><tbody>${entries.map(entry => `<tr><td>${escapeHtml(entry.event)}</td><td>${escapeHtml(entry.component)}</td><td>${escapeHtml(entry.componentPath)}</td><td>${entry.elapsedMilliseconds.toFixed(2)}</td></tr>`).join('')}</tbody></table>`;
}

function escapeHtml(value) {
  const div = document.createElement('div'); div.textContent = value ?? ''; return div.innerHTML;
}

$('reload').onclick = async () => { try { await request('/api/reload', { method: 'POST' }); await load(); } catch (error) { showError(error); } };
$('margins').onchange = renderPage;
$('guides').onchange = renderPage;
load();
