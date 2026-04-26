// RESX <comment> XSS scanner gate (Plan 5 Task 6 — v1.3 Seat-2 P5 carry-forward).
//
// PROBLEM: Translator-facing tools (Weblate) render the RESX <comment> element
// as part of the translator UX. If a comment contains an unescaped `<`, `>`, or
// `&`, that is both invalid XML inside the .resx and a potential XSS vector
// when rendered downstream. RESX requires comments to be well-formed XML text;
// the offending characters MUST be expressed via XML entities (&lt;, &gt;,
// &amp;, &quot;, &apos;) or numeric character references (&#NNN;).
//
// SCOPE: Scan every *.resx under the supplied root(s), pull each <comment>
// element, and assert no unescaped `<`, `>`, or `&` survives after stripping
// recognised entities. Exits 1 on any violation, 0 on clean.

export function scanResxComment(content) {
  // Allow valid entities: &lt;, &gt;, &amp;, &quot;, &apos;, &#NNN;
  const stripped = content.replace(/&(?:lt|gt|amp|quot|apos|#\d+);/g, '');
  const match = stripped.match(/[<>&]/);
  if (match) return { violation: true, character: match[0] };
  return { violation: false };
}

// CLI: walk all *.resx, parse <comment> elements, scan
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const roots = process.argv.slice(2);
  if (roots.length === 0) { console.error('Usage: check.mjs <root> [<root>...]'); process.exit(2); }
  const violations = [];
  function walk(dir) {
    let entries;
    try { entries = readdirSync(dir); } catch { return; }
    for (const entry of entries) {
      if (entry === 'node_modules' || entry === 'bin' || entry === 'obj') continue;
      const p = join(dir, entry);
      let stat;
      try { stat = statSync(p); } catch { continue; }
      if (stat.isDirectory()) walk(p);
      else if (p.endsWith('.resx')) {
        const raw = readFileSync(p, 'utf8');
        // Strip XML comment regions (<!-- ... -->) first so doc-block prose
        // mentioning literal "<comment>" is not falsely captured as an
        // element. The XML parser ignores these regions; we mirror that.
        const xml = raw.replace(/<!--[\s\S]*?-->/g, '');
        const matches = [...xml.matchAll(/<comment>([\s\S]*?)<\/comment>/g)];
        for (const [, comment] of matches) {
          const r = scanResxComment(comment);
          if (r.violation) violations.push({ file: p, character: r.character, snippet: comment.slice(0, 60) });
        }
      }
    }
  }
  for (const root of roots) walk(root);
  if (violations.length) {
    console.error(`SUNFISH_I18N_XSS: ${violations.length} <comment> XSS risk(s):`);
    for (const v of violations) console.error(`  ${v.file}: unescaped '${v.character}' near "${v.snippet}"`);
    process.exit(1);
  }
  console.log(`SUNFISH_I18N_XSS: clean — scanned roots [${roots.join(', ')}]`);
}
