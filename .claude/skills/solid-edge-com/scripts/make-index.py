#!/usr/bin/env python3
"""make-index.py - gera indices PESQUISAVEIS (grep) a partir do api.json do ApiDump.

O api.json tem ~45 MB e nao cabe num contexto nem num pacote de skill. Estes indices
sao texto plano, uma linha por item, pensados para `grep` (ou `apiq.py`) - nunca para
leitura inteira.

Tres compactacoes deliberadas, para o indice caber num bundle de skill:
  1. As interfaces duais `_I...Auto` ficam de FORA. Sao a mesma API da publica de mesmo
     nome, so com HRESULT + [retval] + [lcid] explicitos - dobrariam o arquivo sem
     acrescentar nada para quem chama por IDispatch.
  2. propget + propput da mesma propriedade viram UMA linha (`prop r`, `w` ou `rw`).
  3. Constantes identicas repetidas em varias typelibs colapsam numa linha, com as
     typelibs juntas por "+". Quando o VALOR difere entre typelibs as linhas ficam
     SEPARADAS - que e exatamente o conflito (ApplicationGlobalConstants,
     DocumentStatus) que precisa ficar visivel.

Quem precisar do detalhe completo (direcao por parametro, defaults, offset de vtable)
consulta o api.json com `apiq.py`.

Uso: python make-index.py [--api api.json] [--out index]
So usa a biblioteca padrao (Python 3.8+).
"""
import json
import sys
from pathlib import Path

TAB = "\t"
PLUMBING = {"QueryInterface", "AddRef", "Release", "GetTypeInfoCount", "GetTypeInfo", "GetIDsOfNames", "Invoke"}


def text(value):
    """Como o valor aparece no indice: inteiros e textos crus, o resto em JSON (true/null)."""
    if isinstance(value, str):
        return value
    if isinstance(value, float) and value.is_integer():
        return str(int(value))
    return json.dumps(value, ensure_ascii=False)


def first(*values):
    """O primeiro valor que nao e None (o `??` do JavaScript)."""
    for v in values:
        if v is not None:
            return v
    return None


def main(argv):
    here = Path(__file__).resolve().parent
    api, out = Path("api.json"), here / "index"
    i = 0
    while i < len(argv):
        if argv[i] in ("--api", "--out") and i + 1 < len(argv):
            if argv[i] == "--api":
                api = Path(argv[i + 1])
            else:
                out = Path(argv[i + 1])
            i += 2
        else:
            print(__doc__, file=sys.stderr)
            return 2

    with open(api, encoding="utf-8-sig") as f:
        db = json.load(f)
    out.mkdir(parents=True, exist_ok=True)

    enum_map = {}      # (valor, nome, enum) -> registro; dict preserva a ordem de insercao
    members = []
    types = []

    for lib in db.get("typelibs", []):
        lib_name = str(first(lib.get("name"), lib.get("fileName"), "?"))

        def member_line(type_name, member_name, kind, m):
            ret = first(m.get("effectiveReturns"), m.get("returns"), "?")
            params = m.get("params") or []
            # OUT marca o que preve erro de marshaling: [out] ou [in,out]
            io = TAB + "OUT" if any("out" in (p.get("flags") or []) for p in params) else ""
            return TAB.join([f"{type_name}.{member_name}", kind, f"{len(params)}p", f"d{text(m.get('dispId'))}",
                             text(ret), lib_name]) + io

        for t in lib.get("types") or []:
            types.append(TAB.join([t["kind"], t["name"], lib_name, f"{len(t.get('members') or [])}m",
                                   f"{len(t.get('vars') or [])}v", t.get("guid") or ""]).rstrip())

            if t["kind"] == "enum":
                for v in t.get("vars") or []:
                    key = (text(v.get("value")), v.get("name"), t["name"])
                    prev = enum_map.get(key)
                    if prev:
                        if lib_name not in prev["libs"]:
                            prev["libs"].append(lib_name)
                    else:
                        enum_map[key] = {"value": key[0], "name": v.get("name"), "enum": t["name"], "libs": [lib_name]}
                continue

            if t["name"].startswith("_I") and t["name"].endswith("Auto"):
                continue   # dual: ver o topo

            props = {}     # nome -> {r, w, ref}
            for m in t.get("members") or []:
                if m["name"] in PLUMBING:
                    continue
                if m.get("kind") in ("propget", "propput", "propputref"):
                    e = props.setdefault(m["name"], {"r": False, "w": False, "ref": m})
                    if m["kind"] == "propget":
                        e["r"], e["ref"] = True, m
                    else:
                        e["w"] = True
                    continue
                members.append(member_line(t["name"], m["name"], "method", m))

            for name, e in props.items():
                members.append(member_line(t["name"], name, "prop " + ("r" if e["r"] else "") + ("w" if e["w"] else ""),
                                           e["ref"]))

    enum_lines = [TAB.join([e["value"].rjust(12), text(e["name"]), e["enum"], "+".join(e["libs"])])
                  for e in enum_map.values()]

    files = {
        "enums.txt": [
            "# Constantes de enum do Solid Edge.  VALOR <tab> NOME <tab> ENUM <tab> TYPELIBS",
            "# Varias typelibs juntas por '+' = mesma constante, mesmo valor.",
            "# Mesmo NOME em linhas separadas com valores diferentes = CONFLITO REAL; a typelib decide.",
            "#   grep -P 'TABigFeatureOKTAB' enums.txt       -> pelo nome   (TAB = tabulacao real)",
            "#   grep -P '^ +1216476310TAB' enums.txt        -> BUSCA REVERSA por valor",
            *enum_lines,
        ],
        "members.txt": [
            "# Membros.  TIPO.MEMBRO <tab> kind <tab> Nparams <tab> dDISPID <tab> retorno <tab> typelib [<tab>OUT]",
            "# kind = method | prop r | prop w | prop rw",
            "# OUT  = tem parametro [out]/[in,out] -> cuidado com marshaling (ver com-rules.md)",
            "# As interfaces duais _I...Auto NAO entram (duplicam as publicas).",
            "#   grep -P '^Face\\.' members.txt        |   grep -P 'OUT$' members.txt",
            *members,
        ],
        "types.txt": [
            "# Tipos.  kind <tab> NOME <tab> typelib <tab> Nmembros <tab> Nvars [<tab> guid]",
            *types,
        ],
    }

    for name, lines in files.items():
        path = out / name
        data = ("\n".join(lines) + "\n").encode("utf-8")
        path.write_bytes(data)    # bytes: fim de linha LF sempre, tambem no Windows
        print(f"{str(path):<42} {len(lines) - 1:>6} linhas  {round(len(data) / 1024):>6} KB")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
