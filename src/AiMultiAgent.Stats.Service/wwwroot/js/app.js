\
const fmt = {
  int: value => new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 }).format(value ?? 0),
  dec: value => new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 3 }).format(value ?? 0),
  pct: value => new Intl.NumberFormat('ru-RU', { style: 'percent', maximumFractionDigits: 1 }).format(value ?? 0),
  dt: value => value ? new Date(value).toLocaleString('ru-RU') : ''
};

const el = id => document.getElementById(id);

document.addEventListener('DOMContentLoaded', () => {
  el('refresh').addEventListener('click', load);
  el('closeDrawer').addEventListener('click', () => el('runDrawer').classList.add('hidden'));
  load();
});

async function load() {
  const days = encodeURIComponent(el('days').value);
  const componentName = encodeURIComponent(el('componentName').value || '');
  const qs = `?days=${days}&componentName=${componentName}`;

  const overview = await fetchJson(`/api/dashboard/overview${qs}`);
  renderDashboard(overview);
}

async function fetchJson(url) {
  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`HTTP ${res.status}: ${await res.text()}`);
  }
  return await res.json();
}

function renderDashboard(data) {
  renderKpis(data);
  renderLineChart('timelineChart', data.timeline.map(x => ({
    x: shortDate(x.bucketStartUtc),
    y: x.avgDurationMs
  })), 'avg duration');

  renderDualLineChart('timelineMaChart',
    data.timeline.map(x => ({ x: shortDate(x.bucketStartUtc), y: x.avgDurationMs })),
    data.timeline.map(x => ({ x: shortDate(x.bucketStartUtc), y: x.movingAverageDurationMs })),
    'avg', 'MA(3)');

  renderHistogram('durationHistogram', data.durationHistogram);
  renderHistogram('issuesHistogram', data.issueHistogram);

  renderScatter('scatterDurationChars',
    data.recentRuns.map(x => ({ x: x.durationMs, y: x.filesCount * 1000 })), // lightweight proxy when no raw x/y series are returned
    'duration', 'files*1000');

  renderScatter('scatterIssuesDuration',
    data.recentRuns.map(x => ({ x: x.durationMs, y: x.issuesTotal })),
    'duration', 'issues');

  renderBars('severityBars', data.severityDistribution.items.map(x => ({ label: x.key, value: x.value })));
  renderBars('aggregationBars', data.aggregationModeDistribution.items.map(x => ({ label: x.key, value: x.value })));
  renderBars('componentsBars', data.topComponents.map(x => ({ label: x.componentName, value: x.avgDurationMs })));
  renderBars('toolsBars', data.tools.map(x => ({ label: x.toolName, value: x.avgDurationMs })));

  renderCorrelations(data.correlations);
  renderNumericSummary(data);
  renderOutliers(data.outliers);
  renderRecentRuns(data.recentRuns);
}

function renderKpis(data) {
  const items = [
    { label: 'Runs', value: fmt.int(data.runsCount), sub: `components: ${fmt.int(data.componentsCount)}` },
    { label: 'Success rate', value: fmt.pct(data.rates.successRate), sub: `gateway fail: ${fmt.pct(data.rates.gatewayFailureRate)}` },
    { label: 'Fallback rate', value: fmt.pct(data.rates.fallbackRate), sub: `95% CI: ${fmt.pct(data.rates.fallbackRateConfidence95.lower)} .. ${fmt.pct(data.rates.fallbackRateConfidence95.upper)}` },
    { label: 'Duration mean', value: `${fmt.int(data.durationMs.mean)} ms`, sub: `p95: ${fmt.int(data.durationMs.percentile95)} ms` },
    { label: 'Issues mean', value: fmt.dec(data.issuesPerRun.mean), sub: `median: ${fmt.dec(data.issuesPerRun.median)}` },
    { label: 'Input chars mean', value: fmt.int(data.totalInputChars.mean), sub: `CV: ${fmt.dec(data.totalInputChars.coefficientOfVariation)}` }
  ];

  el('kpis').innerHTML = items.map(x => `
    <div class="kpi">
      <div class="kpi-label">${escapeHtml(x.label)}</div>
      <div class="kpi-value">${escapeHtml(String(x.value))}</div>
      <div class="kpi-sub">${escapeHtml(String(x.sub))}</div>
    </div>
  `).join('');
}

function renderCorrelations(rows) {
  el('correlations').innerHTML = `
    <table>
      <thead><tr><th>Name</th><th>Pearson</th><th>Spearman</th></tr></thead>
      <tbody>
        ${rows.map(x => `
          <tr>
            <td class="mono">${escapeHtml(x.name)}</td>
            <td>${fmt.dec(x.pearson)}</td>
            <td>${fmt.dec(x.spearman)}</td>
          </tr>`).join('')}
      </tbody>
    </table>
  `;
}

function renderNumericSummary(data) {
  const rows = [
    ['durationMs', data.durationMs],
    ['issuesPerRun', data.issuesPerRun],
    ['warningsPerRun', data.warningsPerRun],
    ['errorsPerRun', data.errorsPerRun],
    ['filesPerRun', data.filesPerRun],
    ['inputChars', data.totalInputChars],
    ['fileSizeChars', data.fileSizeChars]
  ];

  el('numericSummary').innerHTML = `
    <table>
      <thead>
        <tr><th>Series</th><th>mean</th><th>median</th><th>std</th><th>p95</th><th>skew</th><th>kurtosis</th><th>JB</th></tr>
      </thead>
      <tbody>
        ${rows.map(([name, v]) => `
          <tr>
            <td class="mono">${escapeHtml(name)}</td>
            <td>${fmt.dec(v.mean)}</td>
            <td>${fmt.dec(v.median)}</td>
            <td>${fmt.dec(v.standardDeviation)}</td>
            <td>${fmt.dec(v.percentile95)}</td>
            <td>${fmt.dec(v.skewness)}</td>
            <td>${fmt.dec(v.kurtosisExcess)}</td>
            <td>${fmt.dec(v.jarqueBera)}</td>
          </tr>`).join('')}
      </tbody>
    </table>
  `;
}

function renderOutliers(outliers) {
  el('outliers').innerHTML = `
    <div style="margin-bottom:12px;display:flex;gap:12px;flex-wrap:wrap">
      <span class="badge warn">duration IQR: ${fmt.int(outliers.durationIqrOutliersCount)}</span>
      <span class="badge warn">duration Z: ${fmt.int(outliers.durationZScoreOutliersCount)}</span>
      <span class="badge bad">issues IQR: ${fmt.int(outliers.issueIqrOutliersCount)}</span>
      <span class="badge bad">issues Z: ${fmt.int(outliers.issueZScoreOutliersCount)}</span>
    </div>
    <table>
      <thead><tr><th>When</th><th>Component</th><th>Duration</th><th>Issues</th><th>Flags</th></tr></thead>
      <tbody>
        ${outliers.runs.map(x => `
          <tr>
            <td>${fmt.dt(x.createdAtUtc)}</td>
            <td>${escapeHtml(x.componentName)}</td>
            <td>${fmt.int(x.durationMs)} ms</td>
            <td>${fmt.int(x.issuesTotal)}</td>
            <td>
              ${x.durationOutlier ? '<span class="badge warn">duration</span>' : ''}
              ${x.issueOutlier ? '<span class="badge bad">issues</span>' : ''}
            </td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;
}

function renderRecentRuns(rows) {
  el('recentRuns').innerHTML = `
    <table>
      <thead><tr><th>When</th><th>Component</th><th>Files</th><th>Duration</th><th>Issues</th><th>Aggregation</th><th>Flags</th><th></th></tr></thead>
      <tbody>
        ${rows.map(x => `
          <tr>
            <td>${fmt.dt(x.createdAtUtc)}</td>
            <td>${escapeHtml(x.componentName)}</td>
            <td>${fmt.int(x.filesCount)}</td>
            <td>${fmt.int(x.durationMs)} ms</td>
            <td>${fmt.int(x.issuesTotal)}</td>
            <td class="mono">${escapeHtml(x.aggregationMode)}</td>
            <td>
              ${x.usedAnyFallback ? '<span class="badge warn">fallback</span>' : ''}
              ${x.isGatewayFailure ? '<span class="badge bad">gateway</span>' : ''}
              ${x.isDurationOutlier ? '<span class="badge warn">dur outlier</span>' : ''}
              ${x.isIssueOutlier ? '<span class="badge bad">issue outlier</span>' : ''}
            </td>
            <td><span class="linklike" data-run-id="${x.runId}">open</span></td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;

  document.querySelectorAll('[data-run-id]').forEach(node => {
    node.addEventListener('click', async () => {
      const id = node.getAttribute('data-run-id');
      const details = await fetchJson(`/api/runs/${id}`);
      renderRunDetails(details);
    });
  });
}

function renderRunDetails(run) {
  el('runDetails').innerHTML = `
    <div class="card" style="margin-bottom:16px">
      <div><strong>ID:</strong> <span class="mono">${escapeHtml(run.runId)}</span></div>
      <div><strong>When:</strong> ${fmt.dt(run.createdAtUtc)}</div>
      <div><strong>Component:</strong> ${escapeHtml(run.componentName)}</div>
      <div><strong>Duration:</strong> ${fmt.int(run.durationMs)} ms</div>
      <div><strong>Aggregation:</strong> <span class="mono">${escapeHtml(run.aggregationMode)}</span></div>
      <div><strong>Summary:</strong> ${escapeHtml(run.summary || '')}</div>
      ${run.failureReason ? `<div><strong>Failure:</strong> ${escapeHtml(run.failureReason)}</div>` : ''}
    </div>

    <div class="card" style="margin-bottom:16px">
      <h3>Files</h3>
      <table>
        <thead><tr><th>File</th><th>Chars</th><th>Lines</th><th>Issues</th><th>Flags</th><th>Review ms</th></tr></thead>
        <tbody>
          ${run.files.map(x => `
            <tr>
              <td class="mono">${escapeHtml(x.fileName)}</td>
              <td>${fmt.int(x.charCount)}</td>
              <td>${fmt.int(x.lineCount)}</td>
              <td>i:${fmt.int(x.infoCount)} / w:${fmt.int(x.warningCount)} / e:${fmt.int(x.errorCount)}</td>
              <td>
                ${x.containsTodo ? '<span class="badge warn">TODO</span>' : ''}
                ${x.containsFixme ? '<span class="badge warn">FIXME</span>' : ''}
                ${x.containsPotentialSecret ? '<span class="badge bad">secret?</span>' : ''}
                ${x.truncatedForLlm ? '<span class="badge warn">truncated</span>' : ''}
                ${x.usedFallback ? '<span class="badge warn">fallback</span>' : ''}
              </td>
              <td>${x.reviewDurationMs == null ? '' : fmt.int(x.reviewDurationMs)}</td>
            </tr>`).join('')}
        </tbody>
      </table>
    </div>

    <div class="card" style="margin-bottom:16px">
      <h3>Tools</h3>
      <table>
        <thead><tr><th>Tool</th><th>Sequence</th><th>Label</th><th>Duration</th><th>Status</th><th>Details</th></tr></thead>
        <tbody>
          ${run.tools.map(x => `
            <tr>
              <td class="mono">${escapeHtml(x.toolName)}</td>
              <td>${fmt.int(x.sequence)}</td>
              <td>${escapeHtml(x.label || '')}</td>
              <td>${x.durationMs == null ? '' : `${fmt.int(x.durationMs)} ms`}</td>
              <td>
                ${x.succeeded ? '<span class="badge good">ok</span>' : '<span class="badge bad">error</span>'}
                ${x.usedFallback ? '<span class="badge warn">fallback</span>' : ''}
              </td>
              <td>${escapeHtml(x.details || '')}</td>
            </tr>`).join('')}
        </tbody>
      </table>
    </div>

    <div class="card" style="margin-bottom:16px">
      <h3>Raw request</h3>
      <pre>${escapeHtml(run.rawRequestJson || '')}</pre>
    </div>

    <div class="card">
      <h3>Raw report</h3>
      <pre>${escapeHtml(run.rawReportJson || '')}</pre>
    </div>
  `;

  el('runDrawer').classList.remove('hidden');
}

function renderHistogram(id, histogram) {
  const bars = histogram.buckets.map(x => ({
    label: `${Math.round(x.fromInclusive)}-${Math.round(x.toExclusive)}`,
    value: x.count
  }));
  renderBars(id, bars);
}

function renderBars(id, items) {
  const host = el(id);
  const width = host.clientWidth || 600;
  const height = host.clientHeight || 260;
  const pad = { top: 16, right: 12, bottom: 64, left: 44 };
  const innerW = width - pad.left - pad.right;
  const innerH = height - pad.top - pad.bottom;
  const max = Math.max(1, ...items.map(x => Number(x.value) || 0));
  const barW = innerW / Math.max(items.length, 1);

  let bars = '';
  let labels = '';
  items.forEach((item, idx) => {
    const h = ((Number(item.value) || 0) / max) * innerH;
    const x = pad.left + idx * barW + 4;
    const y = pad.top + innerH - h;
    const cls = item.label.includes('error') ? 'bar bad' :
      item.label.includes('warning') ? 'bar warn' :
      item.label.includes('llm') || item.label.includes('ok') ? 'bar good' : 'bar';
    bars += `<rect class="${cls}" x="${x}" y="${y}" width="${Math.max(6, barW - 8)}" height="${h}" rx="6"></rect>`;
    labels += `<text class="axis" x="${x + Math.max(6, barW - 8) / 2}" y="${height - 26}" text-anchor="middle" transform="rotate(18 ${x + Math.max(6, barW - 8) / 2} ${height - 26})">${escapeHtml(String(item.label).slice(0, 20))}</text>`;
  });

  host.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}">
      <line class="grid-line" x1="${pad.left}" y1="${pad.top + innerH}" x2="${width - pad.right}" y2="${pad.top + innerH}"></line>
      ${bars}
      ${labels}
      <text class="axis" x="8" y="${pad.top + 12}">${fmt.int(max)}</text>
    </svg>
  `;
}

function renderLineChart(id, points) {
  const host = el(id);
  const width = host.clientWidth || 600;
  const height = host.clientHeight || 260;
  const pad = { top: 16, right: 16, bottom: 42, left: 44 };
  const innerW = width - pad.left - pad.right;
  const innerH = height - pad.top - pad.bottom;
  const ys = points.map(x => Number(x.y) || 0);
  const max = Math.max(1, ...ys);
  const min = Math.min(...ys, 0);

  const coords = points.map((p, i) => {
    const x = pad.left + (points.length <= 1 ? 0 : i / (points.length - 1)) * innerW;
    const y = pad.top + innerH - ((Number(p.y) - min) / Math.max(1, max - min)) * innerH;
    return { x, y, label: p.x, value: p.y };
  });

  const path = coords.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x} ${c.y}`).join(' ');
  const dots = coords.map(c => `<circle class="dot" cx="${c.x}" cy="${c.y}" r="3"></circle>`).join('');
  const labels = coords.filter((_, i) => i % Math.ceil(points.length / 8 || 1) === 0).map(c =>
    `<text class="axis" x="${c.x}" y="${height - 12}" text-anchor="middle">${escapeHtml(c.label)}</text>`
  ).join('');

  host.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}">
      <line class="grid-line" x1="${pad.left}" y1="${pad.top + innerH}" x2="${width - pad.right}" y2="${pad.top + innerH}"></line>
      <path class="line" d="${path}"></path>
      ${dots}
      ${labels}
      <text class="axis" x="8" y="${pad.top + 12}">${fmt.int(max)}</text>
    </svg>
  `;
}

function renderDualLineChart(id, pointsA, pointsB) {
  const host = el(id);
  const width = host.clientWidth || 600;
  const height = host.clientHeight || 260;
  const pad = { top: 16, right: 16, bottom: 42, left: 44 };
  const innerW = width - pad.left - pad.right;
  const innerH = height - pad.top - pad.bottom;

  const ys = [...pointsA, ...pointsB].map(x => Number(x.y) || 0);
  const max = Math.max(1, ...ys);
  const min = Math.min(...ys, 0);

  const project = (points) => points.map((p, i) => ({
    x: pad.left + (points.length <= 1 ? 0 : i / (points.length - 1)) * innerW,
    y: pad.top + innerH - ((Number(p.y) - min) / Math.max(1, max - min)) * innerH,
    label: p.x
  }));

  const a = project(pointsA);
  const b = project(pointsB);

  const pathA = a.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x} ${c.y}`).join(' ');
  const pathB = b.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x} ${c.y}`).join(' ');
  const labels = a.filter((_, i) => i % Math.ceil(pointsA.length / 8 || 1) === 0).map(c =>
    `<text class="axis" x="${c.x}" y="${height - 12}" text-anchor="middle">${escapeHtml(c.label)}</text>`
  ).join('');

  host.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}">
      <line class="grid-line" x1="${pad.left}" y1="${pad.top + innerH}" x2="${width - pad.right}" y2="${pad.top + innerH}"></line>
      <path class="line" d="${pathA}"></path>
      <path class="line-alt" d="${pathB}"></path>
      ${labels}
      <text class="axis" x="8" y="${pad.top + 12}">${fmt.int(max)}</text>
    </svg>
  `;
}

function renderScatter(id, points) {
  const host = el(id);
  const width = host.clientWidth || 600;
  const height = host.clientHeight || 260;
  const pad = { top: 16, right: 16, bottom: 42, left: 44 };
  const innerW = width - pad.left - pad.right;
  const innerH = height - pad.top - pad.bottom;
  const xs = points.map(x => Number(x.x) || 0);
  const ys = points.map(x => Number(x.y) || 0);
  const minX = Math.min(...xs, 0), maxX = Math.max(...xs, 1);
  const minY = Math.min(...ys, 0), maxY = Math.max(...ys, 1);

  const dots = points.map(p => {
    const x = pad.left + ((Number(p.x) - minX) / Math.max(1, maxX - minX)) * innerW;
    const y = pad.top + innerH - ((Number(p.y) - minY) / Math.max(1, maxY - minY)) * innerH;
    return `<circle class="dot" cx="${x}" cy="${y}" r="4"></circle>`;
  }).join('');

  host.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}">
      <line class="grid-line" x1="${pad.left}" y1="${pad.top + innerH}" x2="${width - pad.right}" y2="${pad.top + innerH}"></line>
      <line class="grid-line" x1="${pad.left}" y1="${pad.top}" x2="${pad.left}" y2="${pad.top + innerH}"></line>
      ${dots}
      <text class="axis" x="${pad.left}" y="${height - 12}">${fmt.int(minX)}</text>
      <text class="axis" x="${width - pad.right}" y="${height - 12}" text-anchor="end">${fmt.int(maxX)}</text>
      <text class="axis" x="8" y="${pad.top + 12}">${fmt.int(maxY)}</text>
    </svg>
  `;
}

function shortDate(value) {
  const d = new Date(value);
  return `${String(d.getDate()).padStart(2,'0')}.${String(d.getMonth() + 1).padStart(2,'0')}`;
}

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}
