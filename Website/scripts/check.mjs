import { readFile, readdir, stat } from "node:fs/promises";
import path from "node:path";
import assert from "node:assert/strict";
import { root } from "./build.mjs";
import { validatePublication } from "./publication.mjs";
import { validateProbabilities } from "./probabilities.mjs";

const probabilityData = await validateProbabilities(root);
const output = path.join(root, "dist");
async function filesIn(folder) {
  const entries = await readdir(folder, { withFileTypes: true });
  return (
    await Promise.all(
      entries.map((e) =>
        e.isDirectory()
          ? filesIn(path.join(folder, e.name))
          : path.join(folder, e.name),
      ),
    )
  ).flat();
}
const files = await filesIn(output);
const documents = new Map();
let links = 0;
for (const file of files.filter((f) => f.endsWith(".html"))) {
  const html = await readFile(file, "utf8");
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map((m) => m[1]);
  assert.equal(new Set(ids).size, ids.length, `Duplicate ID: ${file}`);
  assert.equal(
    [...html.matchAll(/<h1\b/g)].length,
    1,
    `Exactly one h1: ${file}`,
  );
  assert.match(html, /<html lang="ko">/);
  assert.match(html, /<meta name="robots" content="noindex,nofollow">/);
  assert(
    !/publicationReady|게시 차단 항목|PUBLIC-CONTENT|source_sha256/.test(html),
    `Internal notes leaked: ${file}`,
  );
  assert(!/\]\(\//.test(html), `Unrendered policy link: ${file}`);
  documents.set(file, { html, ids: new Set(ids) });
}
assert.equal(documents.size, 10, "Nine pages and one 404 page");
for (const [file, { html }] of documents) {
  for (const [, kind, value] of html.matchAll(/\b(href|src)="([^"]+)"/g)) {
    if (/^(https:|mailto:)/.test(value)) continue;
    if (!value) continue; // Empty dialog image receives its source only on opening.
    const [pathname, hash] = value.split("#");
    let target = pathname ? path.resolve(output, "." + pathname) : file;
    assert(
      target.startsWith(output + path.sep) || target === output,
      `Escaping asset: ${value}`,
    );
    const info = await stat(target).catch(() => null);
    assert(info, `Missing ${kind}: ${file} -> ${value}`);
    if (info.isDirectory()) target = path.join(target, "index.html");
    if (hash)
      assert(
        documents.get(target)?.ids.has(hash),
        `Missing anchor: ${file} -> ${value}`,
      );
    links++;
  }
}
for (const route of ["privacy", "terms", "delete-account", "probabilities"]) {
  const html = documents.get(path.join(output, route, "index.html")).html;
  assert.match(html, /공개 전 검토 중인 문서입니다/);
  assert(
    (html.match(/<h[234]/g) || []).length >= 4,
    `Policy body missing: ${route}`,
  );
}
const config = JSON.parse(
  await readFile(path.join(root, "content/site.json"), "utf8"),
);
await assert.rejects(
  () => validatePublication(root, config),
  /Publication not ready/,
);
await assert.rejects(
  () => validatePublication(root, { ...config, publicationChecks: {} }),
  /businessIdentityVerified/,
);
const bytes = (
  await Promise.all(files.map(async (f) => (await stat(f)).size))
).reduce((a, b) => a + b, 0);
assert(bytes < 3_000_000, "Small static site asset budget: 3 MB");
console.log(
  JSON.stringify(
    {
      pages: documents.size,
      probabilitySources: probabilityData.sources.length,
      localLinksAndSources: links,
      files: files.length,
      bytes,
      draftPublicationBlocked: true,
      missingCheckKeysBlocked: true,
    },
    null,
    2,
  ),
);
