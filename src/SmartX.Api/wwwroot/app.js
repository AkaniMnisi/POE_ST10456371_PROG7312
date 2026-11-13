'use strict';
// (MDN contributors, n.d.a): asynchronous Fetch keeps navigation responsive while the C# API works.
const $ = id => document.getElementById(id);
const escapeHtml = v => String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let state = null, mesh = null, lastHistory = null, currentPage = 'telemetry', simulating = false, simulationTimer;
let refreshing = false, selectSignature = '', messageTimer, meshVersion = 0;
const date = value => new Date(value).toLocaleString();
const unit = category => ({Moisture:'%', Temperature:'°C', Power:'V'}[category] ?? '');
async function api(path, method = 'GET', body) {
  const options = {method, headers: {}};
  if (body instanceof FormData) options.body = body;
  else if (body !== undefined) { options.headers['Content-Type'] = 'application/json'; options.body = JSON.stringify(body); }
  const response = await fetch('/api' + path, options);
  const result = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(result.error || result.title || `Request failed (${response.status}).`);
  return result;
}
function notify(text, error = false) {
  clearTimeout(messageTimer); $('message').textContent = text; $('message').className = error ? 'error' : '';
  $('message').hidden = false; messageTimer = setTimeout(() => $('message').hidden = true, 10000);
}
async function action(work, success) {
  try { await work(); if (success) notify(success); await refresh(); }
  catch (error) { notify(error.message, true); }
}
function options(id, rows, fallback) {
  const select = $(id), old = select.value;
  select.innerHTML = rows.map(d => `<option value="${escapeHtml(d.id)}">${escapeHtml(d.id)} · ${escapeHtml(d.name)}${id==='routeSource' ? ' · '+escapeHtml(d.mac) : ''}</option>`).join('');
  if (rows.some(d => d.id === old)) select.value = old;
  else if (fallback && rows.some(d => d.id === fallback)) select.value = fallback;
}
function updateSelects() {
  const signature = JSON.stringify(state.devices.map(d => [d.id, d.name])); if (signature === selectSignature) return;
  selectSignature = signature;
  for (const id of ['ingestDevice','uploadDevice','historyDevice','routeSource','linkFrom','linkTo']) options(id, state.devices, 'MS-01');
  options('commandDevice', state.devices.filter(d => ['Pump','Relay'].includes(d.category)), 'PM-01');
  options('routeTarget', state.devices.filter(d => d.category === 'Gateway'), 'GW-01');
  if ($('linkTo').value === $('linkFrom').value) $('linkTo').value = 'GW-01';
}
async function refresh() {
  if (refreshing) return;
  refreshing = true;
  try {
    state = await api('/state'); updateSelects(); renderState();
    $('connection').textContent = '● Gateway connected'; $('connection').className = 'badge';
    if (currentPage === 'topology') await refreshMesh();
  } catch (error) { $('connection').textContent = 'Gateway unavailable'; $('connection').className = 'badge danger'; }
  finally { refreshing = false; }
}
function renderState() {
  $('deviceCount').textContent = state.devices.length;
  $('onlineCount').textContent = `${state.devices.filter(d=>d.online).length} currently online`;
  $('readingCount').textContent = state.historyCount.toLocaleString();
  $('alertCount').textContent = state.alerts.length;
  $('queueCount').textContent = state.queue.standard + state.queue.critical;
  $('queueDetail').textContent = `${state.queue.standard} FIFO / ${state.queue.critical} priority`;
  $('storageWarning').hidden = !state.storageWarning; $('storageWarning').textContent = state.storageWarning || '';
  $('pause').textContent = state.queue.paused ? 'Resume worker' : 'Pause worker';
  $('processingStatus').textContent = state.queue.paused ? 'Worker paused · incoming packets remain queued' : 'Worker running · priority queue is drained first';
  $('undo').disabled = state.undoCount === 0;
  const devices = new Map(state.devices.map(d=>[d.id,d]));
  $('telemetryRows').innerHTML = state.recent.map(r=>`<tr><td>#${r.sequence}</td><td>${escapeHtml(r.sensorId)}</td><td>${escapeHtml(devices.get(r.sensorId)?.category)}</td><td>${Number(r.value).toFixed(2)} ${unit(devices.get(r.sensorId)?.category)}</td><td>${date(r.timestamp)}</td><td><span class="badge ${r.critical?'danger':'neutral'}">${r.critical?'Critical priority':'Standard FIFO'}</span></td></tr>`).join('') || '<tr><td colspan="6">No processed readings yet.</td></tr>';
  $('attachments').innerHTML = state.attachments.map(a=>`<a href="/api/attachments/${encodeURIComponent(a.id)}" download>${escapeHtml(a.sensorId)} · ${escapeHtml(a.name)}</a>`).join('');
  $('commandHistory').innerHTML = state.commands.slice(0,12).map(c=>`<div class="history-item">#${c.id} · ${escapeHtml(c.sensorId)} · ${c.before?'ON':'OFF'} → ${c.after?'ON':'OFF'}<br>${date(c.timestamp)}</div>`).join('') || '<p>No manual overrides yet.</p>';
  $('auditRows').innerHTML = state.audit.map(a=>`<tr><td>${date(a.timestamp)}</td><td>${escapeHtml(a.kind)}</td><td class="wrap">${escapeHtml(a.message)}</td></tr>`).join('');
  $('insights').innerHTML = state.insights.map((a,i)=>`<article class="insight"><h4>${escapeHtml(a.title)}</h4><p>${escapeHtml(a.sensorId)} · ${escapeHtml(a.reason)}</p>${a.targetId?`<button class="primary" data-insight="${i}">Apply suggestion</button>`:''}</article>`).join('') || '<div class="empty">No intervention suggested.<p class="hint">Send low moisture, then search for “Pump A” twice to demonstrate a learned association.</p></div>';
  renderDevices();
}
function renderDevices() {
  if (!state) return;
  const query = $('deviceSearch').value.trim().toLowerCase(), category = $('categoryFilter').value, filter = $('alertFilter').value;
  const rows = state.devices.filter(d=>{
    const active = state.alerts.some(a=>a.endsWith(':'+d.id));
    return (!query || `${d.id} ${d.mac} ${d.name} ${d.location}`.toLowerCase().includes(query)) && (!category || d.category === category) && (!filter || filter==='alert' && active || filter==='healthy' && !active || filter==='offline' && !d.online);
  });
  $('deviceCards').innerHTML = rows.map(d=>`<div class="device-card"><div><strong>${escapeHtml(d.name)}</strong><small>${escapeHtml(d.id)} · ${escapeHtml(d.category)} · ${escapeHtml(d.location)}</small><small>${escapeHtml(d.mac)}</small><span class="badge ${!d.online?'danger':state.alerts.some(a=>a.endsWith(':'+d.id))?'amber':''}">${d.online?'Online':'Offline'}</span></div><div class="device-value">${['Pump','Relay'].includes(d.category)?(d.enabled?'ON':'OFF'):(d.value===null?'—':Number(d.value).toFixed(1)+unit(d.category))}<br><button data-toggle="${escapeHtml(d.id)}">${d.online?'Disconnect':'Reconnect'}</button></div></div>`).join('') || '<p class="empty">No devices match these filters.</p>';
}
function showPage(page) {
  if (!['telemetry','commands','topology'].includes(page)) page = 'telemetry'; currentPage = page;
  for (const section of document.querySelectorAll('.page')) section.hidden = section.id !== page;
  for (const button of document.querySelectorAll('[data-page]')) { button.classList.toggle('active', button.dataset.page === page); button.setAttribute('aria-current',button.dataset.page===page?'page':'false'); }
  const titles = {telemetry:['Sensor telemetry','A live view of your connected environment.'],commands:['Command stream','Control devices and turn sensor events into action.'],topology:['Network topology','Understand your mesh. Keep every node connected.']};
  $('pageTitle').textContent = titles[page][0]; $('pageSubtitle').textContent = titles[page][1];
  if (page === 'topology' && state) action(refreshMesh);
}
document.querySelectorAll('[data-page]').forEach(b=>b.addEventListener('click',()=>{location.hash=b.dataset.page;}));
window.addEventListener('hashchange',()=>showPage(location.hash.slice(1)));
$('ingestForm').addEventListener('submit',e=>{e.preventDefault(); action(()=>api('/telemetry/ingest','POST',{sensorId:$('ingestDevice').value,payload:Number($('ingestValue').value),timestamp:$('ingestTime').value?new Date($('ingestTime').value).toISOString():null}),'Packet accepted. The worker will process it shortly.');});
$('registerForm').addEventListener('submit',e=>{e.preventDefault(); action(async()=>{await api('/devices','POST',{id:$('regId').value,mac:$('regMac').value,name:$('regName').value,category:$('regCategory').value,location:$('regLocation').value});$('registerForm').reset();},'Device registered. Add wireless links in Network topology to connect it.');});
$('uploadForm').addEventListener('submit',e=>{e.preventDefault();action(async()=>{const file=$('uploadFile').files[0];if(!file||file.size>2*1024*1024)throw new Error('Choose a file no larger than 2 MiB.');const body=new FormData();body.append('file',file);await api('/attachments/'+encodeURIComponent($('uploadDevice').value),'POST',body);$('uploadFile').value='';},'Attachment saved.');});
$('commandForm').addEventListener('submit',e=>{e.preventDefault();action(()=>api('/commands','POST',{sensorId:$('commandDevice').value,enabled:e.submitter.value==='on'}),'Actuator state updated.');});
$('undo').addEventListener('click',()=>action(()=>api('/commands/undo','POST'),'Last command undone; previous state restored.'));
$('pause').addEventListener('click',()=>action(()=>api('/processing','POST',{paused:!state.queue.paused})));
$('lowMoisture').addEventListener('click',()=>action(()=>api('/telemetry/ingest','POST',{sensorId:'MS-01',payload:15}),'Low-moisture packet sent. Stop live simulation to keep this reading stable.'));
$('queueDemo').addEventListener('click',()=>action(async()=>{
  stopSimulation(); await api('/processing','POST',{paused:true});
  await api('/telemetry/ingest','POST',{sensorId:'TP-01',payload:26});
  await api('/telemetry/ingest','POST',{sensorId:'PW-01',payload:220});
  await api('/telemetry/ingest','POST',{sensorId:'MS-01',payload:12});
},'Queued two standard packets, then one critical packet. Click Resume worker and inspect the audit.'));
$('searchForm').addEventListener('submit',e=>{e.preventDefault();renderDevices();if($('deviceSearch').value.trim())action(()=>api('/search','POST',{query:$('deviceSearch').value}),'Search recorded for pattern analysis.');});
for (const id of ['deviceSearch','categoryFilter','alertFilter']) $(id).addEventListener('input',renderDevices);
$('deviceCards').addEventListener('click',e=>{const b=e.target.closest('[data-toggle]');if(!b)return;const d=state.devices.find(d=>d.id===b.dataset.toggle);action(()=>api('/devices/'+encodeURIComponent(d.id)+'/online','POST',{online:!d.online}),`${d.name} ${d.online?'disconnected':'reconnected'}. Routes update automatically.`);});
$('insights').addEventListener('click',e=>{const b=e.target.closest('[data-insight]');if(!b)return;const i=state.insights[Number(b.dataset.insight)];action(()=>api('/commands','POST',{sensorId:i.targetId,enabled:i.enabled}),'Suggested command applied.');});
function stopSimulation(){simulating=false;clearTimeout(simulationTimer);$('simulation').textContent='Start live simulation';}
async function simulationTick(){
  if(!simulating)return;
  try {for(const d of state.devices.filter(d=>d.online&&['Moisture','Power','Temperature'].includes(d.category))){if(!simulating)break;const value=d.category==='Moisture'?35+Math.random()*25:d.category==='Temperature'?23+Math.random()*7:215+Math.random()*15;await api('/telemetry/ingest','POST',{sensorId:d.id,payload:Math.round(value*100)/100});}await refresh();}
  catch(error){stopSimulation();notify(error.message,true);}
  if(simulating)simulationTimer=setTimeout(simulationTick,2500);
}
$('simulation').addEventListener('click',()=>{if(simulating)stopSimulation();else if(state){simulating=true;$('simulation').textContent='Stop live simulation';simulationTick();}});
async function refreshMesh(){
  const version=++meshVersion;
  const result=await api(`/topology?source=${encodeURIComponent($('routeSource').value)}&target=${encodeURIComponent($('routeTarget').value)}`);
  if(version!==meshVersion)return;mesh=result;renderMesh();
}
for(const id of ['routeSource','routeTarget'])$(id).addEventListener('change',()=>action(refreshMesh));
$('showMst').addEventListener('change',renderMesh);
function renderMesh(){
  if(!mesh)return;
  const points=new Map(), n=mesh.nodes.length;
  const preset=[[400,75],[190,210],[405,210],[600,210],[190,350],[600,350]];
  mesh.nodes.forEach((d,i)=>{const a=i/n*2*Math.PI;points.set(d.id,n<=6?preset[i]:[400+330*Math.cos(a),210+160*Math.sin(a)]);});
  const same=(e,a,b)=>(e.from===a&&e.to===b)||(e.from===b&&e.to===a);
  let svg='';
  for(const e of mesh.links){
    const a=points.get(e.from),b=points.get(e.to);if(!a||!b)continue;
    const live=mesh.nodes.find(d=>d.id===e.from).online&&mesh.nodes.find(d=>d.id===e.to).online;
    const mst=$('showMst').checked&&mesh.forest.some(f=>same(f,e.from,e.to));
    const route=mesh.route.some((id,i)=>i>0&&same(e,id,mesh.route[i-1]));
    const color=!live?'#d4dce1':route?'#df9828':mst?'#138f8a':'#c5d2dc';
    svg+=`<line x1="${a[0]}" y1="${a[1]}" x2="${b[0]}" y2="${b[1]}" stroke="${color}" stroke-width="${route?5:mst?3:1.5}" ${!live?'stroke-dasharray="6 6"':''}/><text x="${(a[0]+b[0])/2+6}" y="${(a[1]+b[1])/2-6}">${e.weight}</text>`;
  }
  for(const d of mesh.nodes){const [x,y]=points.get(d.id);svg+=`<g class="node" data-node="${escapeHtml(d.id)}" tabindex="0" role="button" aria-label="Inspect ${escapeHtml(d.name)}"><circle cx="${x}" cy="${y}" r="24" fill="${d.online?(d.category==='Gateway'?'#173a50':'#e0f4f0'):'#edf0f2'}" stroke="${d.online?'#168f89':'#b8c4ce'}" stroke-width="2"/><text x="${x}" y="${y+4}" text-anchor="middle" ${d.category==='Gateway'?'class="gateway-label"':''}>${d.category==='Gateway'?'GW':escapeHtml(d.category.slice(0,2).toUpperCase())}</text><text x="${x}" y="${y+42}" text-anchor="middle">${escapeHtml(d.id)}</text></g>`;}
  // SVG is built from validated IDs and escaped text, never raw uploaded HTML.
  $('mesh').innerHTML=svg;
  $('meshStats').textContent=`${mesh.nodes.length} nodes · ${mesh.links.length} configured links · ${mesh.components} online component(s) · MST/forest cost ${mesh.forestCost.toFixed(2)}`;
  $('routeResult').textContent=mesh.route.length?`${mesh.route.join(' → ')} · Cost ${mesh.routeCost.toFixed(2)}`:'No route available. Reconnect nodes or add wireless links.';
  $('connectivity').textContent=`BFS from ${$('routeTarget').value}: ${mesh.reachable.join(', ')||'none'}. Unreachable/offline: ${mesh.unreachable.join(', ')||'none'}.`;
}
$('mesh').addEventListener('click',e=>{const node=e.target.closest('[data-node]');if(node){$('routeSource').value=node.dataset.node;$('historyDevice').value=node.dataset.node;action(refreshMesh);}});
$('mesh').addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){const node=e.target.closest('[data-node]');if(node){e.preventDefault();node.dispatchEvent(new MouseEvent('click',{bubbles:true}));}}});
$('linkForm').addEventListener('submit',e=>{e.preventDefault();action(()=>api('/links','POST',{from:$('linkFrom').value,to:$('linkTo').value,weight:Number($('linkWeight').value)}),'Wireless link saved.');});
$('removeLink').addEventListener('click',()=>action(()=>api(`/links?from=${encodeURIComponent($('linkFrom').value)}&to=${encodeURIComponent($('linkTo').value)}`,'DELETE'),'Wireless link removed.'));
$('historyForm').addEventListener('submit',e=>{e.preventDefault();action(async()=>{
  const params=new URLSearchParams();if($('historyFrom').value)params.set('from',new Date($('historyFrom').value).toISOString());if($('historyTo').value)params.set('to',new Date($('historyTo').value).toISOString());
  lastHistory=await api(`/history/${encodeURIComponent($('historyDevice').value)}?${params}`);
  $('queryStats').textContent=`${lastHistory.totalMatches.toLocaleString()} matches · ${lastHistory.visitedNodes.toLocaleString()} AVL nodes visited / ${lastHistory.treeCount.toLocaleString()} indexed · Tree height ${lastHistory.treeHeight}. Display/export limited to first 5,000 matches.`;
  $('historyRows').innerHTML=lastHistory.readings.map(r=>`<tr><td>#${r.sequence}</td><td>${date(r.timestamp)}</td><td>${r.value}</td><td>${r.critical?'Yes':'No'}</td></tr>`).join('')||'<tr><td colspan="4">No readings in this range.</td></tr>';
  drawChart(lastHistory.readings);
});});
function drawChart(rows){
  const canvas=$('historyChart'),ctx=canvas.getContext('2d'),w=canvas.width,h=canvas.height;ctx.clearRect(0,0,w,h);ctx.font='12px Segoe UI';ctx.fillStyle='#687c8d';
  if(!rows.length){ctx.fillText('No data for the selected period.',30,50);return;}
  const values=rows.map(r=>r.value),min=Math.min(...values),max=Math.max(...values),range=max-min||1,t0=+new Date(rows[0].timestamp),t1=+new Date(rows.at(-1).timestamp);
  ctx.strokeStyle='#e1e9ef';ctx.lineWidth=1;
  for(let i=0;i<=4;i++){const y=20+i*38;ctx.beginPath();ctx.moveTo(60,y);ctx.lineTo(w-20,y);ctx.stroke();ctx.fillText((max-i*range/4).toFixed(1),8,y+4);}
  ctx.strokeStyle='#138f8a';ctx.lineWidth=2;ctx.beginPath();rows.forEach((r,i)=>{const x=60+(+new Date(r.timestamp)-t0)/(t1-t0||1)*(w-90),y=20+(max-r.value)/range*152;i?ctx.lineTo(x,y):ctx.moveTo(x,y);});ctx.stroke();
  if(rows.length===1){ctx.fillStyle='#138f8a';ctx.beginPath();ctx.arc(60,20,4,0,2*Math.PI);ctx.fill();}
  ctx.fillStyle='#687c8d';ctx.fillText(date(rows[0].timestamp),60,h-10);ctx.fillText(date(rows.at(-1).timestamp),w-220,h-10);
}
$('exportHistory').addEventListener('click',()=>{
  if(!lastHistory?.readings.length){notify('Run a history query with results first.',true);return;}
  const csv='sequence,sensorId,timestamp,value,critical\r\n'+lastHistory.readings.map(r=>[r.sequence,r.sensorId,r.timestamp,r.value,r.critical].join(',')).join('\r\n');
  const url=URL.createObjectURL(new Blob([csv],{type:'text/csv'})),a=document.createElement('a');a.href=url;a.download='smartx-history.csv';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
});
showPage(location.hash.slice(1));refresh();setInterval(refresh,2000);
/* Reference list:
MDN contributors (n.d.a) Using the Fetch API. https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API/Using_Fetch
MDN contributors (n.d.b) Canvas API. https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API
(Accessed: 9 November 2026). UI, graph rendering and simulator implementation are original.
*/
