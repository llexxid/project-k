import { readFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import path from "node:path";

const startMarker = "<!-- PUBLIC-CONTENT:START -->";
const endMarker = "<!-- PUBLIC-CONTENT:END -->";
const epsilon = 1e-9;
const fail = (message) => {
  throw new Error(`Probability disclosure: ${message}`);
};
const requireValue = (condition, message) => {
  if (!condition) fail(message);
};
const gcd = (a, b) => (b === 0n ? a : gcd(b, a % b));

function addFraction([a, b], [c, d]) {
  const numerator = a * d + c * b;
  const denominator = b * d;
  const factor = gcd(numerator, denominator);
  return [numerator / factor, denominator / factor];
}

function validateDisplay(row, label) {
  const match = /^([0-9]+(?:\.[0-9]+)?)(…)?%$/.exec(row.probabilityDisplay);
  requireValue(match, `${label}: invalid probabilityDisplay`);
  const exactPercent =
    (row.probabilityNumerator / row.probabilityDenominator) * 100;
  if (match[2]) {
    // An ellipsis displays a truncated decimal, not a rounded exact percentage.
    const digits = match[1].split(".")[1];
    requireValue(digits, `${label}: an ellipsis needs decimal digits`);
    const scale = 10n ** BigInt(digits.length);
    const shown = BigInt(match[1].replace(".", ""));
    const scaledNumerator = BigInt(row.probabilityNumerator) * 100n * scale;
    const denominator = BigInt(row.probabilityDenominator);
    requireValue(
      shown * denominator <= scaledNumerator &&
        scaledNumerator < (shown + 1n) * denominator,
      `${label}: displayed decimal differs from the exact probability`,
    );
  } else {
    requireValue(
      Math.abs(Number(match[1]) - exactPercent) < epsilon,
      `${label}: displayed percentage differs from the exact probability`,
    );
  }
}

function validateTable(table, id, count) {
  requireValue(
    table && Array.isArray(table.rows) && table.rows.length === count,
    `${id}: expected ${count} result rows`,
  );
  const ids = new Set();
  let total = [0n, 1n];
  for (const [index, row] of table.rows.entries()) {
    const label = `${id} row ${index + 1}`;
    requireValue(
      typeof row.id === "string" && row.id && !ids.has(row.id),
      `${label}: missing or duplicate id`,
    );
    ids.add(row.id);
    requireValue(
      typeof row.name === "string" && row.name.trim(),
      `${label}: missing name`,
    );
    requireValue(
      Number.isSafeInteger(row.quantity) && row.quantity > 0,
      `${label}: invalid quantity`,
    );
    const grades =
      id === "equipment" ? ["일반", "레어", "에픽", "재화"] : ["스킬", "재화"];
    requireValue(grades.includes(row.grade), `${label}: unknown grade`);
    const {
      probabilityNumerator: numerator,
      probabilityDenominator: denominator,
    } = row;
    requireValue(
      Number.isSafeInteger(numerator) &&
        Number.isSafeInteger(denominator) &&
        numerator > 0 &&
        denominator > 0 &&
        numerator <= denominator,
      `${label}: invalid fraction`,
    );
    requireValue(
      typeof row.probabilityPercent === "number" &&
        Number.isFinite(row.probabilityPercent) &&
        Math.abs(row.probabilityPercent - (numerator / denominator) * 100) <
          epsilon,
      `${label}: numeric percentage differs from the exact fraction`,
    );
    validateDisplay(row, label);
    total = addFraction(total, [BigInt(numerator), BigInt(denominator)]);
    if (id === "equipment") {
      requireValue(
        typeof row.guaranteedPercent === "number" &&
          Number.isFinite(row.guaranteedPercent) &&
          row.guaranteedPercent >= 0 &&
          row.guaranteedPercent <= 100,
        `${label}: invalid guaranteed percentage`,
      );
      requireValue(
        row.grade === "에픽" || row.guaranteedPercent === 0,
        `${label}: a non-epic result cannot occur under the disclosed epic guarantee`,
      );
    }
  }
  requireValue(
    total[0] === total[1],
    `${id}: exact normal probability sum must be 100%`,
  );
  if (id === "equipment") {
    const guaranteed = table.rows.reduce(
      (sum, row) => sum + row.guaranteedPercent,
      0,
    );
    requireValue(
      Math.abs(guaranteed - 100) < epsilon,
      "equipment: guaranteed probability sum must be 100%",
    );
  }
}

function publicTables(source) {
  requireValue(
    source.split(startMarker).length === 2 &&
      source.split(endMarker).length === 2 &&
      source.indexOf(startMarker) < source.indexOf(endMarker),
    "expected exactly one ordered public content block",
  );
  const content = source.split(startMarker)[1].split(endMarker)[0];
  const tables = [];
  let rows = null;
  for (const line of content.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed.startsWith("|")) {
      rows = null;
      continue;
    }
    requireValue(trimmed.endsWith("|"), "malformed Markdown probability table");
    const cells = trimmed
      .slice(1, -1)
      .split("|")
      .map((cell) => cell.trim());
    if (cells.every((cell) => /^:?-{3,}:?$/.test(cell))) continue;
    if (!rows) {
      rows = [];
      tables.push(rows);
    }
    rows.push(cells);
  }
  requireValue(
    tables.length === 2,
    "expected exactly two public probability tables",
  );
  return tables;
}

function compareMarkdown(source, equipment, mage) {
  const tables = publicTables(source);
  const expected = [
    [
      [
        "결과",
        "등급",
        "지급 수량",
        "일반 상태의 확률",
        "에픽 보장 상태의 확률",
      ],
      ...equipment.rows.map((row) => [
        row.name,
        row.grade,
        `${row.quantity}개`,
        row.probabilityDisplay,
        `${row.guaranteedPercent}%`,
      ]),
    ],
    [
      ["결과", "최초 획득 시 지급", "전체 1회 확률"],
      ...mage.rows.map((row) => [
        row.name,
        row.grade === "스킬" ? `스킬 ${row.quantity}종` : `${row.quantity}개`,
        row.probabilityDisplay,
      ]),
    ],
  ];
  for (let table = 0; table < expected.length; table++) {
    const label = table === 0 ? "equipment" : "mage";
    requireValue(
      tables[table].length === expected[table].length,
      `${label}: Markdown row count differs from JSON`,
    );
    for (let row = 0; row < expected[table].length; row++) {
      requireValue(
        JSON.stringify(tables[table][row]) ===
          JSON.stringify(expected[table][row]),
        `${label}: Markdown ${row === 0 ? "header" : `row ${row}`} differs from JSON`,
      );
    }
  }
}

function validEffectiveAt(value) {
  if (typeof value !== "string") return false;
  const match =
    /^(\d{4}-\d{2}-\d{2})T([01]\d|2[0-3]):([0-5]\d):([0-5]\d)(?:\.\d{1,3})?(Z|[+-](?:0\d|1[0-4]):[0-5]\d)$/.exec(
      value,
    );
  if (!match || !Number.isFinite(Date.parse(value))) return false;
  if (/^[+-]14:(?!00)/.test(match[5])) return false;
  // Date.parse normalizes impossible dates such as February 30, so check the day separately.
  const day = new Date(`${match[1]}T00:00:00Z`);
  return (
    Number.isFinite(day.getTime()) &&
    day.toISOString().slice(0, 10) === match[1]
  );
}

/** Verify the reviewed source snapshot and both public tables before any site build. */
export async function validateProbabilities(root, { production = false } = {}) {
  requireValue(
    typeof root === "string" && path.isAbsolute(root),
    "root must be the absolute Website directory",
  );
  const repository = path.resolve(root, "..");
  const publishing = path.join(repository, "Docs/Publishing");
  const [jsonText, source, settings] = await Promise.all([
    readFile(path.join(publishing, "data/probabilities.json"), "utf8"),
    readFile(path.join(publishing, "PROBABILITY_DISCLOSURE.md"), "utf8"),
    readFile(
      path.join(repository, "ProjectSettings/ProjectSettings.asset"),
      "utf8",
    ),
  ]);
  const data = JSON.parse(jsonText);
  requireValue(data.schemaVersion === 1, "unsupported JSON schema version");
  requireValue(
    Array.isArray(data.tables) && data.tables.length === 2,
    "expected equipment and mage JSON tables",
  );
  const equipment = data.tables.find((table) => table.id === "equipment");
  const mage = data.tables.find((table) => table.id === "mage");
  validateTable(equipment, "equipment", 13);
  validateTable(mage, "mage", 11);
  compareMarkdown(source, equipment, mage);

  const version = /^\s*bundleVersion:\s*([^\r\n]+)\s*$/m
    .exec(settings)?.[1]
    .trim()
    .replace(/^['"]|['"]$/g, "");
  requireValue(
    version && data.observedBundleVersion === version,
    "JSON game version differs from PlayerSettings.bundleVersion",
  );
  requireValue(
    /`PlayerSettings\.bundleVersion`\s+([^\s,]+)/.exec(source)?.[1] === version,
    "Markdown source-review version differs from PlayerSettings.bundleVersion",
  );
  requireValue(
    /마탑 카탈로그\s+`([^`]+)`/.exec(source)?.[1] === data.catalogVersion,
    "Markdown catalog version differs from JSON",
  );

  requireValue(
    Array.isArray(data.sources) && data.sources.length === 36,
    "expected 36 reviewed source fingerprints",
  );
  const seen = new Set();
  for (const file of data.sources) {
    requireValue(
      typeof file.path === "string" &&
        file.path.startsWith("Assets/") &&
        !file.path.includes("\\") &&
        !file.path
          .split("/")
          .some((part) => !part || part === "." || part === ".."),
      "source paths must stay under repository Assets",
    );
    requireValue(
      !seen.has(file.path),
      `duplicate source fingerprint: ${file.path}`,
    );
    seen.add(file.path);
    requireValue(
      typeof file.sha256 === "string" && /^[a-f0-9]{64}$/.test(file.sha256),
      `invalid SHA-256: ${file.path}`,
    );
  }
  const comparisons = await Promise.allSettled(
    data.sources.map(async (file) => {
      const bytes = await readFile(path.join(repository, file.path));
      const actual = createHash("sha256").update(bytes).digest("hex");
      requireValue(
        actual === file.sha256,
        `source changed; review disclosure before updating fingerprint: ${file.path}`,
      );
    }),
  );
  const failures = comparisons.filter((result) => result.status === "rejected");
  if (failures.length)
    fail(failures.map((result) => result.reason.message).join("; "));

  if (production) {
    requireValue(
      data.publicationReady === true,
      "publicationReady must be true for publication",
    );
    requireValue(
      validEffectiveAt(data.effectiveAt),
      "effectiveAt must be a real ISO date-time with time zone, e.g. 2026-09-27T00:00:00+09:00",
    );
  }
  return data;
}
