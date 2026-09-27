import http from "node:http";
import { readFile, stat } from "node:fs/promises";
import { watch } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { execFile } from "node:child_process";
import { promisify } from "node:util";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const runBuild = promisify(execFile);

const publicRoot = path.join(root, "dist");
const port = Number(process.env.LUDOS_PREVIEW_PORT || 8765);
const mime = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".webp": "image/webp",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".txt": "text/plain; charset=utf-8",
  ".xml": "application/xml; charset=utf-8",
};
let buildQueue = Promise.resolve();
function rebuild() {
  buildQueue = buildQueue
    .catch(() => {})
    .then(async () => {
    // A short-lived build process releases Windows source-file handles after each build.
    await runBuild(process.execPath, [path.join(root, "scripts/build.mjs")], {
      windowsHide: true,
    });
    });
  return buildQueue;
}
await rebuild();
let timer;
const watchers = [
  "scripts",
  "src",
  "public",
  "content",
  "../Docs/Publishing",
].map((folder) =>
  watch(path.join(root, folder), { recursive: true }, () => {
    clearTimeout(timer);
    timer = setTimeout(
      () =>
        rebuild()
          .then(() => console.log("Updated site files"))
          .catch((e) => console.error("Build failed:", e.message)),
      250,
    );
  }),
);
const server = http.createServer(async (req, res) => {
  try {
    if (!["GET", "HEAD"].includes(req.method)) {
      res.writeHead(405, { Allow: "GET, HEAD" });
      res.end();
      return;
    }
    await buildQueue;
    const pathname = decodeURIComponent(
      new URL(req.url, "http://localhost").pathname,
    );
    const target = path.resolve(publicRoot, "." + pathname);
    if (
      (target !== publicRoot && !target.startsWith(publicRoot + path.sep)) ||
      pathname.includes("\\")
    ) {
      res.writeHead(403);
      res.end();
      return;
    }
    let file = target;
    try {
      if ((await stat(file)).isDirectory())
        file = path.join(file, "index.html");
    } catch {
      file = path.join(publicRoot, "404.html");
      res.statusCode = 404;
    }
    let content;
    try {
      content = await readFile(file);
    } catch {
      res.statusCode = 404;
      file = path.join(publicRoot, "404.html");
      content = await readFile(file);
    }
    res.setHeader(
      "Content-Type",
      mime[path.extname(file)] || "application/octet-stream",
    );
    res.setHeader("Cache-Control", "no-store");
    // Mirror the production static headers during local browser verification.
    const headers = await readFile(path.join(publicRoot, "_headers"), "utf8");
    for (const row of headers.split("\n")) {
      const match = row.match(/^\s+([\w-]+):\s*(.+)$/);
      if (match) res.setHeader(match[1], match[2].trim());
    }
    res.end(req.method === "HEAD" ? undefined : content);
  } catch {
    res.writeHead(500);
    res.end("Preview build unavailable. Check terminal diagnostics.");
  }
});
server.listen(port, "127.0.0.1", () =>
  console.log(`Ludos Interactive preview: http://127.0.0.1:${port}/`),
);
function shutdown() {
  clearTimeout(timer);
  watchers.forEach((w) => w.close());
  server.close();
}
process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
