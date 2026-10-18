#!/usr/bin/env node
// apiq — consulta o inventário da API do Solid Edge (inventory/api.json).
//
// O api.json tem ~45 MB: grande demais para ler inteiro num contexto de agente.
// Esta ferramenta responde perguntas pontuais sobre ele, que é o modo correto de
// usá-lo: achar a assinatura exata ANTES de escrever a chamada.
//
// Comandos:
//   apiq type <regex>                 tipos cujo nome casa (interface, coclass, enum...)
//   apiq members <Tipo>               todos os membros de um tipo, com assinatura
//   apiq find <regex>                 membros (qualquer tipo) cujo nome casa
//   apiq sig <Tipo>.<Membro>          assinatura detalhada, com flags por parâmetro
//   apiq enum <regex>                 enums cujo nome casa, com valores
//   apiq const <regex>                constantes cujo nome casa (em qualquer enum)
//   apiq value <numero>               quais constantes têm esse valor (busca reversa)
//   apiq events                       coclasses e suas interfaces de evento ([source])
//   apiq outparams [<regex>]          membros com parâmetro [out] / [in,out]
//   apiq stats                        totais por typelib
//
// Flags: --lib <regex>  limita a uma typelib    --limit <n>  (padrão 60)
//        --json         saída JSON crua

import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const DEFAULT_DB = resolve(join(here, "..", "inventory", "api.json"));

const argv = process.argv.slice(2);
if (argv.length === 0) {
  console.error(readFileSync(fileURLToPath(import.meta.url), "utf8")
    .split("\n").filter((l) => l.startsWith("//")).map((l) => l.slice(3)).join("\n"));
  process.exit(2);
}

const opts = { limit: 60, json: false, lib: null, db: process.env.APIQ_DB ?? DEFAULT_DB };
const positional = [];
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a === "--limit") opts.limit = Number(argv[++i]);
  else if (a === "--json") opts.json = true;
  else if (a === "--lib") opts.lib = new RegExp(argv[++i], "i");
  else if (a === "--db") opts.db = argv[++i];
  else positional.push(a);
}

const [command, ...rest] = positional;
const arg = rest.join(" ");

const db = JSON.parse(readFileSync(opts.db, "utf8"));

/** Itera (lib, type) aplicando o filtro --lib. */
function* types() {
  for (const lib of db.typelibs) {
    if (opts.lib && !opts.lib.test(lib.name ?? "") && !opts.lib.test(lib.fileName)) continue;
    for (const t of lib.types ?? []) yield [lib, t];
  }
}

/** Monta a assinatura de um membro no estilo IDL, que é como a typelib a declara. */
function signature(t, m) {
  const params = (m.params ?? []).map((p) => {
    const dir = ["in", "out"].filter((f) => p.flags?.includes(f)).join(",") || "in";
    const extra = [];
    if (p.flags?.includes("optional")) extra.push("opt");
    if (p.flags?.includes("retval")) extra.push("retval");
    if (p.default !== undefined && p.default !== null) extra.push(`=${JSON.stringify(p.default)}`);
    const tag = `[${dir}${extra.length ? " " + extra.join(" ") : ""}]`;
    return `${tag} ${p.type} ${p.name}`;
  });
  const ret = m.effectiveReturns ? `${m.effectiveReturns} (retval)` : m.returns;
  const kind = m.kind === "method" ? "" : `${m.kind} `;
  return `${kind}${t.name}.${m.name}(${params.join(", ")}) -> ${ret}  [dispid ${m.dispId}]`;
}

const out = [];
const push = (s) => { if (out.length < opts.limit) out.push(s); };
let truncated = 0;
const count = (hit) => { if (out.length >= opts.limit) truncated++; return hit; };

switch (command) {
  case "type": {
    const re = new RegExp(arg, "i");
    const rows = [];
    for (const [lib, t] of types()) {
      if (!re.test(t.name)) continue;
      rows.push({ lib: lib.name, file: lib.fileName, name: t.name, kind: t.kind, guid: t.guid,
        members: (t.members ?? []).length, vars: (t.vars ?? []).length,
        flags: t.flags, implements: (t.implements ?? []).map((i) => i.name + (i.flags?.length ? `[${i.flags}]` : "")) });
    }
    emitRows(rows, (r) => `${r.kind.padEnd(14)} ${r.name.padEnd(42)} ${String(r.members).padStart(5)} membros ${String(r.vars).padStart(5)} vars  ${r.lib}${r.implements.length ? "  : " + r.implements.join(", ") : ""}`);
    break;
  }

  case "members": {
    const re = new RegExp(`^${arg}$`, "i");
    const rows = [];
    for (const [lib, t] of types()) {
      if (!re.test(t.name)) continue;
      for (const m of t.members ?? []) rows.push({ lib: lib.name, type: t.name, member: m.name, kind: m.kind, sig: signature(t, m), flags: m.flags, doc: m.doc });
      for (const v of t.vars ?? []) rows.push({ lib: lib.name, type: t.name, member: v.name, kind: `var/${v.varKind}`, sig: `${t.name}.${v.name} : ${v.type}${v.value !== undefined && v.value !== null ? ` = ${v.value}` : ""}  [dispid ${v.dispId}]`, flags: v.flags, doc: v.doc });
    }
    emitRows(rows, (r) => `${r.sig}${r.flags?.length ? `  {${r.flags.join(",")}}` : ""}`);
    break;
  }

  case "find": {
    const re = new RegExp(arg, "i");
    const rows = [];
    for (const [lib, t] of types()) {
      for (const m of t.members ?? []) {
        if (!re.test(m.name)) continue;
        rows.push({ lib: lib.name, type: t.name, kind: t.kind, member: m.name, sig: signature(t, m) });
      }
    }
    emitRows(rows, (r) => `${r.sig}   (${r.lib})`);
    break;
  }

  case "sig": {
    const dot = arg.lastIndexOf(".");
    const typeRe = new RegExp(`^${arg.slice(0, dot)}$`, "i");
    const memberRe = new RegExp(`^${arg.slice(dot + 1)}$`, "i");
    const rows = [];
    for (const [lib, t] of types()) {
      if (!typeRe.test(t.name)) continue;
      for (const m of t.members ?? []) {
        if (!memberRe.test(m.name)) continue;
        rows.push({ lib: lib.name, type: t.name, ...m, signature: signature(t, m) });
      }
    }
    if (opts.json) { console.log(JSON.stringify(rows, null, 2)); break; }
    for (const r of rows) {
      console.log(`\n${r.signature}`);
      console.log(`  typelib     : ${r.lib}`);
      console.log(`  kind        : ${r.kind} (${r.funcKind}, ${r.callConv}), vtbl offset ${r.vtblOffset}`);
      if (r.flags?.length) console.log(`  flags       : ${r.flags.join(", ")}`);
      if (r.doc) console.log(`  doc         : ${r.doc}`);
      console.log(`  retorno     : ${r.returns}${r.effectiveReturns ? `  (retval efetivo: ${r.effectiveReturns})` : ""}`);
      console.log(`  params      : ${r.params.length} (${r.optionalParams} opcionais)`);
      for (const [i, p] of r.params.entries()) {
        const d = p.default !== undefined && p.default !== null ? `  default=${JSON.stringify(p.default)}` : "";
        console.log(`    ${String(i + 1).padStart(2)}. ${p.name.padEnd(24)} ${p.type.padEnd(30)} [${(p.flags ?? []).join(",")}]${d}`);
      }
    }
    if (rows.length === 0) console.log("(nada encontrado)");
    break;
  }

  case "enum": {
    const re = new RegExp(arg, "i");
    const rows = [];
    for (const [lib, t] of types()) {
      if (t.kind !== "enum" || !re.test(t.name)) continue;
      rows.push({ lib: lib.name, name: t.name, values: (t.vars ?? []).map((v) => ({ name: v.name, value: v.value })) });
    }
    if (opts.json) { console.log(JSON.stringify(rows, null, 2)); break; }
    for (const r of rows.slice(0, opts.limit)) {
      console.log(`\nenum ${r.name}   (${r.lib}, ${r.values.length} valores)`);
      for (const v of r.values) console.log(`    ${String(v.value).padStart(12)}  ${v.name}`);
    }
    if (rows.length > opts.limit) console.log(`\n... ${rows.length - opts.limit} enums a mais (use --limit)`);
    if (rows.length === 0) console.log("(nada encontrado)");
    break;
  }

  case "const": {
    const re = new RegExp(arg, "i");
    const rows = [];
    for (const [lib, t] of types()) {
      if (t.kind !== "enum") continue;
      for (const v of t.vars ?? []) {
        if (!re.test(v.name ?? "")) continue;
        rows.push({ lib: lib.name, enum: t.name, name: v.name, value: v.value });
      }
    }
    emitRows(rows, (r) => `${String(r.value).padStart(12)}  ${r.name.padEnd(42)} ${r.enum}   (${r.lib})`);
    break;
  }

  case "value": {
    const target = Number(arg);
    const rows = [];
    for (const [lib, t] of types()) {
      if (t.kind !== "enum") continue;
      for (const v of t.vars ?? []) {
        // Constantes COM aparecem como int32 assinado; comparar também o
        // equivalente sem sinal evita perder valores como 1216476310 vs negativos.
        const nv = Number(v.value);
        if (nv === target || (nv < 0 && nv + 4294967296 === target)) {
          rows.push({ lib: lib.name, enum: t.name, name: v.name, value: v.value });
        }
      }
    }
    emitRows(rows, (r) => `${r.name.padEnd(44)} ${r.enum.padEnd(40)} (${r.lib})`);
    break;
  }

  case "events": {
    const rows = [];
    for (const [lib, t] of types()) {
      const src = (t.implements ?? []).filter((i) => i.flags?.includes("source"));
      if (src.length === 0) continue;
      rows.push({ lib: lib.name, coclass: t.name, kind: t.kind, events: src.map((s) => s.name) });
    }
    emitRows(rows, (r) => `${r.coclass.padEnd(40)} -> ${r.events.join(", ")}   (${r.lib})`);
    break;
  }

  case "outparams": {
    const re = arg ? new RegExp(arg, "i") : null;
    const rows = [];
    for (const [lib, t] of types()) {
      if (re && !re.test(t.name)) continue;
      for (const m of t.members ?? []) {
        const outs = (m.params ?? []).filter((p) => p.flags?.includes("out"));
        if (outs.length === 0) continue;
        rows.push({ lib: lib.name, type: t.name, member: m.name,
          outs: outs.map((p) => `${p.name}:${p.type}[${p.flags.join(",")}]`), sig: signature(t, m) });
      }
    }
    emitRows(rows, (r) => `${r.type}.${r.member}  <-  ${r.outs.join("  ")}`);
    break;
  }

  case "stats": {
    const rows = db.typelibs.map((l) => ({
      file: l.fileName, name: l.name, libid: l.libid, version: `${l.major}.${l.minor}`,
      types: (l.types ?? []).length,
      interfaces: (l.types ?? []).filter((t) => t.kind === "interface" || t.kind === "dispinterface").length,
      coclasses: (l.types ?? []).filter((t) => t.kind === "coclass").length,
      enums: (l.types ?? []).filter((t) => t.kind === "enum").length,
      members: (l.types ?? []).reduce((a, t) => a + (t.members ?? []).length, 0),
      error: l.error ?? null,
    }));
    if (opts.json) { console.log(JSON.stringify({ totals: db.totals, typelibs: rows }, null, 2)); break; }
    console.log(`totais: ${JSON.stringify(db.totals)}\n`);
    console.log("arquivo".padEnd(28) + "typelib".padEnd(28) + "tipos  itf  cocl  enum  membros");
    for (const r of rows) {
      console.log(r.file.padEnd(28) + String(r.name ?? "-").padEnd(28) +
        String(r.types).padStart(5) + String(r.interfaces).padStart(5) +
        String(r.coclasses).padStart(6) + String(r.enums).padStart(6) +
        String(r.members).padStart(9) + (r.error ? `   ERRO: ${r.error}` : ""));
    }
    break;
  }

  default:
    console.error(`apiq: comando desconhecido: ${command}`);
    process.exit(2);
}

function emitRows(rows, fmt) {
  if (opts.json) { console.log(JSON.stringify(rows, null, 2)); return; }
  for (const r of rows.slice(0, opts.limit)) console.log(fmt(r));
  if (rows.length > opts.limit) console.log(`... ${rows.length - opts.limit} a mais (use --limit)`);
  if (rows.length === 0) console.log("(nada encontrado)");
}
