#!/usr/bin/env node
// make-index.mjs — gera índices PESQUISÁVEIS (grep) a partir de inventory/api.json.
//
// O api.json tem ~45 MB e não cabe num contexto nem num pacote de skill. Estes índices
// são texto plano, uma linha por item, pensados para `grep` — nunca para leitura inteira.
//
// Três compactações deliberadas, para o índice caber num bundle de skill:
//   1. As interfaces duais `_I...Auto` ficam de FORA. São a mesma API da pública de mesmo
//      nome, só com HRESULT + [retval] + [lcid] explícitos — dobrariam o arquivo sem
//      acrescentar nada para quem chama por IDispatch.
//   2. propget + propput da mesma propriedade viram UMA linha (`prop r`, `w` ou `rw`).
//   3. Constantes idênticas repetidas em várias typelibs colapsam numa linha, com as
//      typelibs juntas por "+". Quando o VALOR difere entre typelibs as linhas ficam
//      SEPARADAS — que é exatamente o conflito (ApplicationGlobalConstants,
//      DocumentStatus) que precisa ficar visível.
//
// Quem precisar do detalhe completo (direção por parâmetro, defaults, offset de vtable)
// consulta o api.json com `apiq.mjs`.
//
// Uso: node tools/make-index.mjs [--api inventory/api.json] [--out skill-nova/scripts/index]

import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const TAB = "\t";

let api = "inventory/api.json";
let out = "skill-nova/scripts/index";
for (let i = 2; i < process.argv.length; i++) {
  if (process.argv[i] === "--api") api = process.argv[++i];
  else if (process.argv[i] === "--out") out = process.argv[++i];
}

const PLUMBING = new Set(["QueryInterface", "AddRef", "Release", "GetTypeInfoCount",
  "GetTypeInfo", "GetIDsOfNames", "Invoke"]);

const db = JSON.parse(readFileSync(api, "utf8"));
mkdirSync(out, { recursive: true });

const enumMap = new Map();
const members = [];
const types = [];

for (const lib of db.typelibs) {
  const L = lib.name ?? lib.fileName;

  const memberLine = (typeName, memberName, kind, m) => {
    const ret = m.effectiveReturns ?? m.returns ?? "?";
    const np = (m.params ?? []).length;
    // OUT marca o que prevê erro de marshaling: [out] ou [in,out]
    const io = (m.params ?? []).some((p) => p.flags?.includes("out")) ? TAB + "OUT" : "";
    return [`${typeName}.${memberName}`, kind, `${np}p`, `d${m.dispId}`, ret, L]
      .join(TAB) + io;
  };

  for (const t of lib.types ?? []) {
    types.push([t.kind, t.name, L, `${(t.members ?? []).length}m`,
                `${(t.vars ?? []).length}v`, t.guid ?? ""].join(TAB).trimEnd());

    if (t.kind === "enum") {
      for (const v of t.vars ?? []) {
        const key = [v.value, v.name, t.name].join("\u0000");
        const prev = enumMap.get(key);
        if (prev) { if (!prev.libs.includes(L)) prev.libs.push(L); }
        else enumMap.set(key, { value: v.value, name: v.name, en: t.name, libs: [L] });
      }
      continue;
    }

    if (t.name.startsWith("_I") && t.name.endsWith("Auto")) continue;   // dual: ver topo

    const props = new Map();   // nome -> { r, w, ref }

    for (const m of t.members ?? []) {
      if (PLUMBING.has(m.name)) continue;

      if (m.kind === "propget" || m.kind === "propput" || m.kind === "propputref") {
        const e = props.get(m.name) ?? { r: false, w: false, ref: m };
        if (m.kind === "propget") { e.r = true; e.ref = m; } else { e.w = true; }
        props.set(m.name, e);
        continue;
      }
      members.push(memberLine(t.name, m.name, "method", m));
    }

    for (const [name, e] of props) {
      members.push(memberLine(t.name, name, `prop ${(e.r ? "r" : "") + (e.w ? "w" : "")}`, e.ref));
    }
  }
}

const enumLines = [...enumMap.values()].map((e) =>
  [String(e.value).padStart(12), e.name, e.en, e.libs.join("+")].join(TAB));

const files = {
  "enums.txt": [
    "# Constantes de enum do Solid Edge.  VALOR <tab> NOME <tab> ENUM <tab> TYPELIBS",
    "# Varias typelibs juntas por '+' = mesma constante, mesmo valor.",
    "# Mesmo NOME em linhas separadas com valores diferentes = CONFLITO REAL; a typelib decide.",
    "#   grep -P 'TABigFeatureOKTAB' enums.txt       -> pelo nome   (TAB = tabulacao real)",
    "#   grep -P '^ +1216476310TAB' enums.txt        -> BUSCA REVERSA por valor",
    ...enumLines,
  ],
  "members.txt": [
    "# Membros.  TIPO.MEMBRO <tab> kind <tab> Nparams <tab> dDISPID <tab> retorno <tab> typelib [<tab>OUT]",
    "# kind = method | prop r | prop w | prop rw",
    "# OUT  = tem parametro [out]/[in,out] -> cuidado com marshaling (ver com-rules.md)",
    "# As interfaces duais _I...Auto NAO entram (duplicam as publicas).",
    "#   grep -P '^Face\\.' members.txt        |   grep -P 'OUT$' members.txt",
    ...members,
  ],
  "types.txt": [
    "# Tipos.  kind <tab> NOME <tab> typelib <tab> Nmembros <tab> Nvars [<tab> guid]",
    ...types,
  ],
};

for (const [name, lines] of Object.entries(files)) {
  const path = join(out, name);
  const text = lines.join("\n") + "\n";
  writeFileSync(path, text);
  console.log(`${path.padEnd(42)} ${String(lines.length - 1).padStart(6)} linhas  ` +
              `${String(Math.round(Buffer.byteLength(text) / 1024)).padStart(6)} KB`);
}
