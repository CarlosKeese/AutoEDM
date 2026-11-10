#!/usr/bin/env python3
"""apiq - consulta o inventario da API do Solid Edge.

Duas fontes, escolhidas sozinhas:
  * api.json  (gerado pelo ApiDump, ~45 MB): detalhe completo, com direcao por parametro.
  * index/*.txt (ja vem na skill): responde sem build e sem SE, com menos detalhe -
    numero de parametros, DISPID, retorno e a marca OUT, mas nao o nome/tipo de cada parametro.
Sem api.json o apiq usa os indices e avisa. Aponte o api.json com --db ou APIQ_DB.

Comandos:
  apiq type <regex>                 tipos cujo nome casa (interface, coclass, enum...)
  apiq members <Tipo>               todos os membros de um tipo, com assinatura
  apiq find <regex>                 membros (qualquer tipo) cujo nome casa
  apiq sig <Tipo>.<Membro>          assinatura detalhada, com flags por parametro
  apiq enum <regex>                 enums cujo nome casa, com valores
  apiq const <regex>                constantes cujo nome casa (em qualquer enum)
  apiq value <numero>               quais constantes tem esse valor (busca reversa)
  apiq events                       coclasses e suas interfaces de evento (so com api.json)
  apiq outparams [<regex>]          membros com parametro [out] / [in,out]
  apiq stats                        totais por typelib

Flags: --lib <regex>  limita a uma typelib    --limit <n>  (padrao 60)
       --json         saida JSON crua         --db <api.json>   --index <pasta>

So usa a biblioteca padrao (Python 3.8+).
"""
import json
import os
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
DEFAULT_DB = HERE.parent / "inventory" / "api.json"
DEFAULT_INDEX = HERE / "index"


def main(argv):
    if not argv or argv[0] in ("-h", "--help"):
        print(__doc__, file=sys.stderr)
        return 2

    opts = {"limit": 60, "json": False, "lib": None, "db": os.environ.get("APIQ_DB"), "index": None}
    positional = []
    i = 0
    while i < len(argv):
        a = argv[i]
        if a in ("--limit", "--lib", "--db", "--index"):
            if i + 1 >= len(argv):
                print(f"apiq: faltou o valor de {a}", file=sys.stderr)
                return 2
            i += 1
            value = argv[i]
            if a == "--limit":
                try:
                    opts["limit"] = int(value)
                except ValueError:
                    print(f"apiq: --limit quer um numero inteiro, veio '{value}'", file=sys.stderr)
                    return 2
            elif a == "--lib":
                opts["lib"] = re.compile(value, re.I)
            else:
                opts[a[2:]] = value
        elif a == "--json":
            opts["json"] = True
        else:
            positional.append(a)
        i += 1

    if not positional:
        print(__doc__, file=sys.stderr)
        return 2
    command, arg = positional[0], " ".join(positional[1:])
    if command == "value":
        try:
            parse_int(arg)
        except ValueError:
            print(f"apiq: 'value' quer um numero inteiro (decimal ou 0x...), veio '{arg}'", file=sys.stderr)
            return 2

    explicit_db = opts["db"] is not None
    db_path = Path(opts["db"]) if explicit_db else DEFAULT_DB
    if opts["index"] is None and db_path.is_file():
        backend = JsonBackend(db_path, opts)
    elif explicit_db and opts["index"] is None:
        print(f"apiq: api.json nao encontrado: {db_path}", file=sys.stderr)
        return 2
    else:
        index_dir = Path(opts["index"]) if opts["index"] else DEFAULT_INDEX
        if not (index_dir / "members.txt").is_file():
            print(f"apiq: sem api.json ({db_path}) e sem indices em {index_dir}.", file=sys.stderr)
            return 2
        backend = IndexBackend(index_dir, opts)
        print(f"# fonte: indices em {index_dir} (sem api.json: sem nome/tipo por parametro)", file=sys.stderr)

    handler = getattr(backend, "cmd_" + command, None)
    if handler is None:
        print(f"apiq: comando desconhecido: {command}", file=sys.stderr)
        return 2
    if command in ("type", "members", "find", "sig", "enum", "const", "value") and not arg:
        print(f"apiq: '{command}' precisa de um argumento.", file=sys.stderr)
        return 2
    return handler(arg) or 0


def parse_int(text):
    """Inteiro decimal ou com prefixo (0x...); '010' vale 10, nao erro."""
    text = text.strip()
    try:
        return int(text, 0)
    except ValueError:
        return int(text, 10)


def same_value(candidate, target):
    """Constantes COM aparecem como int32 com sinal; aceita tambem o equivalente sem sinal
    (1216476310 nunca e negativo, mas igCylinder e - e o log pode trazer qualquer das formas)."""
    try:
        c = int(candidate)
    except (TypeError, ValueError):
        return False
    return c == target or (c < 0 and c + 4294967296 == target) or (target < 0 and target + 4294967296 == c)


class Backend:
    def __init__(self, opts):
        self.opts = opts

    def lib_ok(self, *names):
        rx = self.opts["lib"]
        return rx is None or any(n and rx.search(n) for n in names)

    def emit(self, rows, fmt):
        if self.opts["json"]:
            print(json.dumps(rows, indent=2, ensure_ascii=False))
            return
        limit = self.opts["limit"]
        for r in rows[:limit]:
            print(fmt(r))
        if len(rows) > limit:
            print(f"... {len(rows) - limit} a mais (use --limit)")
        if not rows:
            print("(nada encontrado)")

    def emit_enums(self, rows):
        if self.opts["json"]:
            print(json.dumps(rows, indent=2, ensure_ascii=False))
            return
        limit = self.opts["limit"]
        for r in rows[:limit]:
            print(f"\nenum {r['name']}   ({r['lib']}, {len(r['values'])} valores)")
            for v in r["values"]:
                print(f"    {str(v['value']).rjust(12)}  {v['name']}")
        if len(rows) > limit:
            print(f"\n... {len(rows) - limit} enums a mais (use --limit)")
        if not rows:
            print("(nada encontrado)")


class JsonBackend(Backend):
    """Detalhe completo, a partir do api.json do ApiDump."""

    def __init__(self, path, opts):
        super().__init__(opts)
        with open(path, encoding="utf-8-sig") as f:
            self.db = json.load(f)

    def types(self):
        for lib in self.db.get("typelibs", []):
            if not self.lib_ok(lib.get("name"), lib.get("fileName")):
                continue
            for t in lib.get("types") or []:
                yield lib, t

    @staticmethod
    def signature(t, m):
        params = []
        for p in m.get("params") or []:
            flags = p.get("flags") or []
            direction = ",".join(f for f in ("in", "out") if f in flags) or "in"
            extra = []
            if "optional" in flags:
                extra.append("opt")
            if "retval" in flags:
                extra.append("retval")
            if p.get("default") is not None:
                extra.append("=" + json.dumps(p["default"], ensure_ascii=False))
            tag = f"[{direction}{' ' + ' '.join(extra) if extra else ''}]"
            params.append(f"{tag} {p.get('type')} {p.get('name')}")
        ret = f"{m['effectiveReturns']} (retval)" if m.get("effectiveReturns") else m.get("returns")
        kind = "" if m.get("kind") == "method" else f"{m.get('kind')} "
        return f"{kind}{t['name']}.{m['name']}({', '.join(params)}) -> {ret}  [dispid {m.get('dispId')}]"

    def cmd_type(self, arg):
        rx = re.compile(arg, re.I)
        rows = []
        for lib, t in self.types():
            if not rx.search(t["name"]):
                continue
            rows.append({
                "lib": lib.get("name"), "file": lib.get("fileName"), "name": t["name"], "kind": t["kind"],
                "guid": t.get("guid"), "members": len(t.get("members") or []), "vars": len(t.get("vars") or []),
                "flags": t.get("flags"),
                "implements": [i["name"] + (f"[{','.join(i['flags'])}]" if i.get("flags") else "")
                               for i in t.get("implements") or []],
            })
        self.emit(rows, lambda r: f"{r['kind']:<14} {r['name']:<42} {r['members']:>5} membros {r['vars']:>5} vars  "
                                  f"{r['lib']}{'  : ' + ', '.join(r['implements']) if r['implements'] else ''}")

    def cmd_members(self, arg):
        rx = re.compile(f"^(?:{arg})$", re.I)
        rows = []
        for lib, t in self.types():
            if not rx.search(t["name"]):
                continue
            for m in t.get("members") or []:
                rows.append({"lib": lib.get("name"), "type": t["name"], "member": m["name"], "kind": m.get("kind"),
                             "sig": self.signature(t, m), "flags": m.get("flags"), "doc": m.get("doc")})
            for v in t.get("vars") or []:
                value = f" = {v['value']}" if v.get("value") is not None else ""
                rows.append({"lib": lib.get("name"), "type": t["name"], "member": v["name"],
                             "kind": f"var/{v.get('varKind')}",
                             "sig": f"{t['name']}.{v['name']} : {v.get('type')}{value}  [dispid {v.get('dispId')}]",
                             "flags": v.get("flags"), "doc": v.get("doc")})
        self.emit(rows, lambda r: r["sig"] + ("  {" + ",".join(r["flags"]) + "}" if r.get("flags") else ""))

    def cmd_find(self, arg):
        rx = re.compile(arg, re.I)
        rows = []
        for lib, t in self.types():
            for m in t.get("members") or []:
                if rx.search(m["name"]):
                    rows.append({"lib": lib.get("name"), "type": t["name"], "kind": t["kind"],
                                 "member": m["name"], "sig": self.signature(t, m)})
        self.emit(rows, lambda r: f"{r['sig']}   ({r['lib']})")

    def cmd_sig(self, arg):
        type_part, _, member_part = arg.rpartition(".")
        type_rx = re.compile(f"^(?:{type_part})$", re.I)
        member_rx = re.compile(f"^(?:{member_part})$", re.I)
        rows = []
        for lib, t in self.types():
            if not type_rx.search(t["name"]):
                continue
            for m in t.get("members") or []:
                if member_rx.search(m["name"]):
                    rows.append({"lib": lib.get("name"), "type": t["name"], **m, "signature": self.signature(t, m)})
        if self.opts["json"]:
            print(json.dumps(rows, indent=2, ensure_ascii=False))
            return
        for r in rows:
            params = r.get("params") or []
            print(f"\n{r['signature']}")
            print(f"  typelib     : {r['lib']}")
            print(f"  kind        : {r.get('kind')} ({r.get('funcKind')}, {r.get('callConv')}), "
                  f"vtbl offset {r.get('vtblOffset')}")
            if r.get("flags"):
                print(f"  flags       : {', '.join(r['flags'])}")
            if r.get("doc"):
                print(f"  doc         : {r['doc']}")
            effective = f"  (retval efetivo: {r['effectiveReturns']})" if r.get("effectiveReturns") else ""
            print(f"  retorno     : {r.get('returns')}{effective}")
            print(f"  params      : {len(params)} ({r.get('optionalParams')} opcionais)")
            for n, p in enumerate(params, 1):
                default = f"  default={json.dumps(p['default'], ensure_ascii=False)}" if p.get("default") is not None else ""
                print(f"    {n:>2}. {str(p.get('name')):<24} {str(p.get('type')):<30} "
                      f"[{','.join(p.get('flags') or [])}]{default}")
        if not rows:
            print("(nada encontrado)")

    def cmd_enum(self, arg):
        rx = re.compile(arg, re.I)
        rows = [{"lib": lib.get("name"), "name": t["name"],
                 "values": [{"name": v.get("name"), "value": v.get("value")} for v in t.get("vars") or []]}
                for lib, t in self.types() if t["kind"] == "enum" and rx.search(t["name"])]
        self.emit_enums(rows)

    def cmd_const(self, arg):
        rx = re.compile(arg, re.I)
        rows = []
        for lib, t in self.types():
            if t["kind"] != "enum":
                continue
            for v in t.get("vars") or []:
                if rx.search(v.get("name") or ""):
                    rows.append({"lib": lib.get("name"), "enum": t["name"], "name": v["name"], "value": v.get("value")})
        self.emit(rows, lambda r: f"{str(r['value']).rjust(12)}  {r['name']:<42} {r['enum']}   ({r['lib']})")

    def cmd_value(self, arg):
        target = parse_int(arg)
        rows = []
        for lib, t in self.types():
            if t["kind"] != "enum":
                continue
            for v in t.get("vars") or []:
                if same_value(v.get("value"), target):
                    rows.append({"lib": lib.get("name"), "enum": t["name"], "name": v["name"], "value": v.get("value")})
        self.emit(rows, lambda r: f"{r['name']:<44} {r['enum']:<40} ({r['lib']})")

    def cmd_events(self, arg):
        rows = []
        for lib, t in self.types():
            sources = [i for i in t.get("implements") or [] if "source" in (i.get("flags") or [])]
            if sources:
                rows.append({"lib": lib.get("name"), "coclass": t["name"], "kind": t["kind"],
                             "events": [s["name"] for s in sources]})
        self.emit(rows, lambda r: f"{r['coclass']:<40} -> {', '.join(r['events'])}   ({r['lib']})")

    def cmd_outparams(self, arg):
        rx = re.compile(arg, re.I) if arg else None
        rows = []
        for lib, t in self.types():
            if rx and not rx.search(t["name"]):
                continue
            for m in t.get("members") or []:
                outs = [p for p in m.get("params") or [] if "out" in (p.get("flags") or [])]
                if outs:
                    rows.append({"lib": lib.get("name"), "type": t["name"], "member": m["name"],
                                 "outs": [f"{p.get('name')}:{p.get('type')}[{','.join(p['flags'])}]" for p in outs],
                                 "sig": self.signature(t, m)})
        self.emit(rows, lambda r: f"{r['type']}.{r['member']}  <-  {'  '.join(r['outs'])}")

    def cmd_stats(self, arg):
        rows = []
        for lib in self.db.get("typelibs", []):
            types = lib.get("types") or []
            rows.append({
                "file": lib.get("fileName"), "name": lib.get("name"), "libid": lib.get("libid"),
                "version": f"{lib.get('major')}.{lib.get('minor')}", "types": len(types),
                "interfaces": sum(1 for t in types if t["kind"] in ("interface", "dispinterface")),
                "coclasses": sum(1 for t in types if t["kind"] == "coclass"),
                "enums": sum(1 for t in types if t["kind"] == "enum"),
                "members": sum(len(t.get("members") or []) for t in types),
                "error": lib.get("error"),
            })
        if self.opts["json"]:
            print(json.dumps({"totals": self.db.get("totals"), "typelibs": rows}, indent=2, ensure_ascii=False))
            return
        print(f"totais: {json.dumps(self.db.get('totals'), ensure_ascii=False)}\n")
        print("arquivo".ljust(28) + "typelib".ljust(28) + "tipos  itf  cocl  enum  membros")
        for r in rows:
            print(str(r["file"]).ljust(28) + str(r["name"] or "-").ljust(28) + f"{r['types']:>5}{r['interfaces']:>5}"
                  f"{r['coclasses']:>6}{r['enums']:>6}{r['members']:>9}" + (f"   ERRO: {r['error']}" if r["error"] else ""))


class IndexBackend(Backend):
    """Resposta rapida a partir de index/{types,members,enums}.txt (ver make-index.py).

    O indice omite as interfaces duais _I...Auto, funde propget/propput numa linha e nao
    guarda nome/tipo de cada parametro - para isso e preciso o api.json."""

    def __init__(self, folder, opts):
        super().__init__(opts)
        self.folder = folder

    def rows(self, name, columns):
        with open(self.folder / name, encoding="utf-8") as f:
            for line in f:
                if line.startswith("#") or not line.strip():
                    continue
                parts = line.rstrip("\n").split("\t")
                if len(parts) >= columns:
                    yield parts

    def members(self):
        for p in self.rows("members.txt", 6):
            type_name, _, member = p[0].partition(".")
            if self.lib_ok(p[5]):
                yield {"type": type_name, "member": member, "kind": p[1], "params": int(p[2].rstrip("p") or 0),
                       "dispId": p[3].lstrip("d"), "returns": p[4], "lib": p[5], "out": len(p) > 6 and p[6] == "OUT"}

    def enums(self):
        for p in self.rows("enums.txt", 4):
            if self.lib_ok(*p[3].split("+")):
                yield {"value": p[0].strip(), "name": p[1], "enum": p[2], "lib": p[3]}

    @staticmethod
    def line(m):
        out = "   OUT: tem [out]/[in,out]" if m["out"] else ""
        kind = "" if m["kind"] == "method" else f"{m['kind']} "
        return f"{kind}{m['type']}.{m['member']}({m['params']} params) -> {m['returns']}  [dispid {m['dispId']}]{out}"

    def cmd_type(self, arg):
        rx = re.compile(arg, re.I)
        rows = [{"kind": p[0], "name": p[1], "lib": p[2], "members": int(p[3].rstrip("m")), "vars": int(p[4].rstrip("v")),
                 "guid": p[5] if len(p) > 5 else None}
                for p in self.rows("types.txt", 5) if rx.search(p[1]) and self.lib_ok(p[2])]
        self.emit(rows, lambda r: f"{r['kind']:<14} {r['name']:<42} {r['members']:>5} membros {r['vars']:>5} vars  {r['lib']}")

    def cmd_members(self, arg):
        rx = re.compile(f"^(?:{arg})$", re.I)
        rows = [dict(m, sig=self.line(m)) for m in self.members() if rx.search(m["type"])]
        self.emit(rows, lambda r: f"{r['sig']}   ({r['lib']})")

    def cmd_find(self, arg):
        rx = re.compile(arg, re.I)
        rows = [dict(m, sig=self.line(m)) for m in self.members() if rx.search(m["member"])]
        self.emit(rows, lambda r: f"{r['sig']}   ({r['lib']})")

    def cmd_sig(self, arg):
        type_part, _, member_part = arg.rpartition(".")
        type_rx = re.compile(f"^(?:{type_part})$", re.I)
        member_rx = re.compile(f"^(?:{member_part})$", re.I)
        rows = [dict(m, sig=self.line(m)) for m in self.members()
                if type_rx.search(m["type"]) and member_rx.search(m["member"])]
        if self.opts["json"]:
            print(json.dumps(rows, indent=2, ensure_ascii=False))
            return
        for r in rows:
            print(f"\n{r['sig']}")
            print(f"  typelib     : {r['lib']}")
            print(f"  kind        : {r['kind']}")
            print(f"  retorno     : {r['returns']}")
            print(f"  params      : {r['params']}" + ("  - ao menos um e [out] ou [in,out] (ver com-rules.md)" if r["out"] else ""))
        if rows:
            print("\n(indice: sem nome, tipo e direcao por parametro - gere o api.json com o ApiDump para o detalhe)")
        else:
            print("(nada encontrado - o indice nao traz as interfaces duais _I...Auto)")

    def grouped_enums(self, keep):
        groups = {}
        for e in self.enums():
            if keep(e):
                groups.setdefault((e["enum"], e["lib"]), []).append({"name": e["name"], "value": int(e["value"])})
        return [{"lib": lib, "name": name, "values": values} for (name, lib), values in groups.items()]

    def cmd_enum(self, arg):
        rx = re.compile(arg, re.I)
        self.emit_enums(self.grouped_enums(lambda e: rx.search(e["enum"])))

    def cmd_const(self, arg):
        rx = re.compile(arg, re.I)
        rows = [e for e in self.enums() if rx.search(e["name"])]
        self.emit(rows, lambda r: f"{r['value'].rjust(12)}  {r['name']:<42} {r['enum']}   ({r['lib']})")

    def cmd_value(self, arg):
        target = parse_int(arg)
        rows = [e for e in self.enums() if same_value(e["value"], target)]
        self.emit(rows, lambda r: f"{r['name']:<44} {r['enum']:<40} ({r['lib']})")

    def cmd_events(self, arg):
        print("apiq: 'events' precisa do api.json (o indice nao guarda as interfaces de evento).", file=sys.stderr)
        return 2

    def cmd_outparams(self, arg):
        rx = re.compile(arg, re.I) if arg else None
        rows = [dict(m, sig=self.line(m)) for m in self.members() if m["out"] and (rx is None or rx.search(m["type"]))]
        self.emit(rows, lambda r: f"{r['sig']}   ({r['lib']})")

    def cmd_stats(self, arg):
        stats = {}

        def bump(lib, key):
            stats.setdefault(lib, {"types": 0, "enums": 0, "members": 0})[key] += 1

        for p in self.rows("types.txt", 5):
            if self.lib_ok(p[2]):
                bump(p[2], "types")
                if p[0] == "enum":
                    bump(p[2], "enums")
        for m in self.members():
            bump(m["lib"], "members")
        rows = [dict(name=lib, **counts) for lib, counts in stats.items()]
        if self.opts["json"]:
            print(json.dumps(rows, indent=2, ensure_ascii=False))
            return
        print("typelib".ljust(32) + "tipos  enum  membros (sem as duais _I...Auto)")
        for r in rows:
            print(str(r["name"]).ljust(32) + f"{r['types']:>5}{r['enums']:>6}{r['members']:>9}")


if __name__ == "__main__":
    # O console do Windows nem sempre e UTF-8; sem isto um acento num 'doc' derruba o print.
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure:
            try:
                reconfigure(encoding="utf-8", errors="replace")
            except ValueError:
                pass
    try:
        sys.exit(main(sys.argv[1:]))
    except BrokenPipeError:
        sys.exit(0)
    except re.error as e:
        print(f"apiq: expressao regular invalida: {e}", file=sys.stderr)
        sys.exit(2)
