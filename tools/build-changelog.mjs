// Offline reader for the deliberately limited CHANGELOG.md format; no dependencies.
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const read = path => readFileSync(resolve(root, path), 'utf8').replace(/\r\n/g, '\n');
const escape = value => value.replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
const inline = value => escape(value).replace(/`([^`]+)`/g, '<code>$1</code>');
const config = JSON.parse(read('changelog.config.json'));
if (typeof config.projectName !== 'string' || !config.projectName.trim()) throw new Error('缺少项目名称');
new Intl.DateTimeFormat('en', { timeZone: config.timeZone }).format();
const markdown = read('CHANGELOG.md');
const source = markdown.replace(/<!--[\s\S]*?-->/g, '');
const section = source.split(/^## 正式记录\s*$/m)[1];
if (section === undefined) throw new Error('CHANGELOG.md 缺少“## 正式记录”');
const blocks = [...section.matchAll(/^### (.+)\n([\s\S]*?)(?=^### |$(?![\s\S]))/gm)];
const entries = blocks.map(([, heading, body]) => {
  const field = name => {
    const line = body.split('\n').find(line => line.startsWith(`- ${name}：`));
    if (!line || !line.slice(name.length + 3).trim()) throw new Error(`${heading} 缺少字段：${name}`);
    return line.slice(name.length + 3).trim();
  };
  const parseTime = name => {
    const value = field(name);
    if (!/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} [+-]\d{2}:\d{2}$/.test(value)) throw new Error(`${heading} 时间格式错误：${name}`);
    const iso = value.replace(' ', 'T').replace(/ ([+-])/, '$1');
    if (!Number.isFinite(Date.parse(iso))) throw new Error(`${heading} 无效时间：${name}`);
    return { value, iso };
  };
  const commits = [...field('功能提交').matchAll(/`([a-f0-9]{40})`/g)].map(match => match[1]);
  if (!commits.length) throw new Error(`${heading} 缺少完整 Git SHA`);
  const changesBlock = body.match(/^- 修改内容：\s*\n((?:[ \t]+- .+(?:\n|$))+)/m)?.[1];
  if (!changesBlock) throw new Error(`${heading} 缺少修改内容列表`);
  const changes = changesBlock.trim().split('\n').map(line => line.trim().replace(/^- /, ''));
  return { title: heading.replace(/^\d{4}-\d{2}-\d{2}\s*—\s*/, ''), completed: parseTime('修改完成时间'), time: parseTime('功能提交时间'), commits, changes, verification: field('验证'), notes: field('兼容性 / 部署注意事项'), id: `commit-${commits[0]}` };
}).sort((a, b) => Date.parse(b.time.iso) - Date.parse(a.time.iso));
if (new Set(entries.map(entry => entry.id)).size !== entries.length) throw new Error('正式记录存在重复的功能提交');

const sidebar = entries.map((entry, index) => `<a class="record-link" href="#${entry.id}"><span class="nav-date">${escape(entry.time.value.slice(0, 10))}${index === 0 ? ' · 最新' : ''}</span><span>${escape(entry.title)}</span><code>${entry.commits[0].slice(0, 8)}</code></a>`).join('\n');
const cards = entries.map((entry, index) => `<a class="update-card" href="#${entry.id}"><div class="update-meta"><time datetime="${entry.time.iso}">${escape(entry.time.value)}</time>${index === 0 ? '<span class="badge">最新记录</span>' : ''}</div><h3>${escape(entry.title)}</h3><p>${inline(entry.changes[0])}</p><span class="read-more">查看修改详情 <span aria-hidden="true">↗</span></span></a>`).join('\n');
const details = entries.map(entry => `<article class="release panel" id="${entry.id}" aria-labelledby="title-${entry.id}">
<a class="back" href="#home">← 返回首页</a><div class="eyebrow">CHANGE RECORD / 功能变更</div><h1 id="title-${entry.id}">${escape(entry.title)}</h1>
<div class="release-times"><p><span>修改完成</span><time datetime="${entry.completed.iso}">${escape(entry.completed.value)}</time></p><p><span>功能提交</span><time datetime="${entry.time.iso}">${escape(entry.time.value)}</time></p></div>
<div class="commit-box"><span>Git 提交编号</span>${entry.commits.map(sha => `<code>${sha}</code>`).join('')}</div>
<section class="detail-section"><h2>修改了哪些功能</h2><ul>${entry.changes.map(change => `<li>${inline(change)}</li>`).join('')}</ul></section>
<section class="detail-section"><h2>验证结果</h2><p>${inline(entry.verification)}</p></section>
<section class="detail-section"><h2>兼容性与部署说明</h2><p>${inline(entry.notes)}</p></section>
</article>`).join('\n');
const slots = { PROJECT_NAME: escape(config.projectName), TIME_ZONE: escape(config.timeZone), SIDEBAR: sidebar || '<p class="empty-nav">暂无正式记录</p>', CARDS: cards || '<p class="empty">第一条功能记录将在提交完成后显示。</p>', DETAILS: details, SOURCE: escape(markdown), COUNT: String(entries.length), LATEST: entries[0]?.time.value.slice(0, 10) || '待首次提交' };
const html = read('tools/changelog-template.html.in').replace(/\{\{([A-Z_]+)\}\}/g, (_, key) => {
  if (!(key in slots)) throw new Error(`未知网页模板字段：${key}`);
  return slots[key];
});
if (process.argv.includes('--check')) {
  if (read('CHANGELOG.html') !== html) throw new Error('CHANGELOG.html 未同步，请运行 node tools/build-changelog.mjs');
  console.log(`网页与 Markdown 一致，${entries.length} 条记录。`);
} else {
  writeFileSync(resolve(root, 'CHANGELOG.html'), html, 'utf8');
  console.log(`已生成 CHANGELOG.html，${entries.length} 条记录（最新优先）。`);
}
