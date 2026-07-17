#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
generate_api_docs.py

Parseia o dump da type library do Solid Edge gerado por AutoEDM.Core.Com.ComDiagnostics
(e.g. SE_API_dump_223.00.13.05.txt) e produz documentação Markdown estruturada em
`docs/api/`.

Uso:
    python tools/generate_api_docs.py [caminho/para/SE_API_dump_X.txt]

Sem argumento, procura o dump mais recente em %LOCALAPPDATA%\\AutoEDM\\logs (o dump
CUMULATIVO do add-in, que cresce a cada "Inspecionar seleção" no SE) e em
src/AutoEDM/bin/Debug/net472/logs (a GUI de debug).
"""

import os
import re
import sys
from pathlib import Path
from collections import defaultdict

# Diretórios base (relativo à raiz do projeto)
ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "docs" / "api"

# Regexes. O cabeçalho de cada lib ganhou um "[guid]" opcional (ComDiagnostics 2026-07-17,
# dump virou CUMULATIVO — o guid é o que permite o merge detectar lib já conhecida); o
# grupo de nome é non-greedy e o "[...]" é opcional, então dumps antigos (sem guid) também
# continuam parseando.
RE_TYPELIB = re.compile(r"^########## TYPELIB\s+(.+?)(?:\s+\[[0-9a-fA-F-]{36}\])?\s+—\s+(\d+)\s+tipo\(s\)\s+##########$")
RE_TYPE = re.compile(r"^\[TKIND_(\w+)\s+(.+?)\]$")
RE_ENUM_MEMBER = re.compile(r"^\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$")
RE_METHOD = re.compile(r"^\s+(.+?)\s*\((.*)\)\s*(?:->\s*(.+?))?\s*\[(\d+)\s+params\]$")


def find_dump_path() -> Path:
    """Acha o dump mais recente a usar. Prioridade: (1) caminho passado na linha de
    comando; (2) o dump CUMULATIVO do add-in em %LOCALAPPDATA%\\AutoEDM\\logs — cresce a
    cada clique em "Inspecionar seleção" no SE, é normalmente o mais completo; (3) o dump
    da GUI de debug em bin/Debug/net472/logs (herança da sonda ModelingProbe). Entre
    candidatos, pega o mais RECENTE (mtime) — útil se houver dumps de versões diferentes
    do SE."""
    if len(sys.argv) > 1:
        p = Path(sys.argv[1])
        if p.exists():
            return p
        print(f"Aviso: caminho informado não existe, caindo para busca automática: {p}", file=sys.stderr)

    search_dirs = [ROOT / "src" / "AutoEDM" / "bin" / "Debug" / "net472" / "logs"]
    local_appdata = os.environ.get("LOCALAPPDATA")
    if local_appdata:
        search_dirs.append(Path(local_appdata) / "AutoEDM" / "logs")

    candidates = [p for d in search_dirs if d.exists() for p in d.glob("SE_API_dump_*.txt")]
    if not candidates:
        print(f"Erro: nenhum SE_API_dump_*.txt encontrado em {[str(d) for d in search_dirs]}", file=sys.stderr)
        print("Gere um dump primeiro (rode o AutoEDM conectado ao SE e clique 'Inspecionar seleção', ou rode ModelingProbe).", file=sys.stderr)
        sys.exit(1)
    best = max(candidates, key=lambda p: p.stat().st_mtime)
    if len(candidates) > 1:
        print(f"{len(candidates)} dump(s) encontrado(s); usando o mais recente: {best}")
    return best


def parse_dump(path: Path):
    """Parseia o arquivo de dump e retorna estrutura por type library."""
    if not path.exists():
        print(f"Erro: dump não encontrado em {path}", file=sys.stderr)
        sys.exit(1)

    typelibs = []
    current_typelib = None
    current_type = None

    with path.open("r", encoding="utf-8") as f:
        for raw in f:
            line = raw.rstrip("\n")

            # Nova type library?
            m = RE_TYPELIB.match(line)
            if m:
                current_typelib = {
                    "name": m.group(1).strip(),
                    "count": int(m.group(2)),
                    "types": []
                }
                typelibs.append(current_typelib)
                current_type = None
                continue

            if current_typelib is None:
                continue

            # Novo tipo?
            m = RE_TYPE.match(line)
            if m:
                kind = m.group(1)
                name = m.group(2).strip()
                current_type = {"kind": kind, "name": name, "members": []}
                current_typelib["types"].append(current_type)
                continue

            if current_type is None:
                continue

            # Membro de enum
            m = RE_ENUM_MEMBER.match(line)
            if m and current_type["kind"] == "ENUM":
                current_type["members"].append({
                    "name": m.group(1),
                    "value": m.group(2).strip()
                })
                continue

            # Método/propriedade
            m = RE_METHOD.match(line)
            if m:
                current_type["members"].append({
                    "signature": m.group(1).strip(),
                    "params": m.group(2).strip(),
                    "return": (m.group(3) or "").strip(),
                    "param_count": int(m.group(4))
                })
                continue

            # Linha não reconhecida dentro de um tipo (pode ser continuação rara)
            # Ignoramos silenciosamente.

    return typelibs


def escape_md(text: str) -> str:
    """Escapa caracteres Markdown problemáticos em texto inline."""
    return text.replace("|", "\\|").replace("\n", " ")


def build_type_anchor(name: str) -> str:
    """Cria âncora GitHub-friendly para um tipo."""
    return re.sub(r"[^a-zA-Z0-9_-]", "-", name).lower().strip("-")


def write_typelib_doc(typelib: dict, out_path: Path):
    """Gera um arquivo Markdown para uma type library."""
    out_path.parent.mkdir(parents=True, exist_ok=True)

    # Agrupa por kind
    by_kind = defaultdict(list)
    for t in typelib["types"]:
        by_kind[t["kind"]].append(t)

    lines = []
    tname = typelib["name"]
    lines.append(f"# Type Library `{tname}`")
    lines.append("")
    lines.append(f"- **Tipos documentados:** {len(typelib['types'])} (declarados na type library: {typelib['count']})")
    lines.append(f"- **Origem:** dump `SE_API_dump_223.00.13.05.txt` gerado por `ComDiagnostics.DumpTypeLibraries`")
    lines.append("- **Nota:** o contador da type library pode incluir tipos internos/aliases não listados no dump.")
    lines.append("")
    lines.append("## Sumário por categoria")
    lines.append("")

    for kind in sorted(by_kind.keys()):
        types = by_kind[kind]
        lines.append(f"- **{kind}**: {len(types)} tipo(s)")
    lines.append("")

    for kind in sorted(by_kind.keys()):
        types = by_kind[kind]
        lines.append(f"## {kind}")
        lines.append("")

        if kind == "ENUM":
            # Tabela resumida de enums
            lines.append("| Enum | Membros |")
            lines.append("|------|---------|")
            for t in types:
                anchor = build_type_anchor(t["name"])
                lines.append(f"| [{t['name']}](#{anchor}) | {len(t['members'])} |")
            lines.append("")

            for t in types:
                anchor = build_type_anchor(t["name"])
                lines.append(f"### <a name=\"{anchor}\"></a>`{t['name']}`")
                lines.append("")
                lines.append("| Constante | Valor |")
                lines.append("|-----------|-------|")
                for m in t["members"]:
                    lines.append(f"| `{m['name']}` | {m['value']} |")
                lines.append("")
        else:
            # INTERFACE / DISPATCH / RECORD
            lines.append("| Tipo | Membros |")
            lines.append("|------|---------|")
            for t in types:
                anchor = build_type_anchor(t["name"])
                lines.append(f"| [{t['name']}](#{anchor}) | {len(t['members'])} |")
            lines.append("")

            for t in types:
                anchor = build_type_anchor(t["name"])
                lines.append(f"### <a name=\"{anchor}\"></a>`{t['name']}`")
                lines.append("")
                if not t["members"]:
                    lines.append("_Sem membros documentados._")
                    lines.append("")
                    continue

                lines.append("| Assinatura | Parâmetros | Retorno |")
                lines.append("|------------|------------|---------|")
                for m in t["members"]:
                    sig = escape_md(m["signature"])
                    params = escape_md(m["params"]) if m["params"] else "—"
                    ret = escape_md(m["return"]) if m["return"] else "—"
                    lines.append(f"| `{sig}` | {params} | {ret} |")
                lines.append("")

    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Escrito: {out_path}")


def write_constants_doc(typelibs: list, out_path: Path):
    """Gera um arquivo consolidado com todas as enums."""
    out_path.parent.mkdir(parents=True, exist_ok=True)

    all_enums = []
    for tl in typelibs:
        for t in tl["types"]:
            if t["kind"] == "ENUM":
                all_enums.append((tl["name"], t))

    lines = []
    lines.append("# Constantes e Enums do Solid Edge (COM)")
    lines.append("")
    lines.append(f"- **Total de enums:** {len(all_enums)}")
    lines.append("- **Fonte:** dump `SE_API_dump_223.00.13.05.txt` (SE 2023 `223.00.13.05`)")
    lines.append("- **Nota:** os valores são os encontrados na instalação que gerou o dump. Verifique a versão do Solid Edge ao usar constantes não listadas aqui.")
    lines.append("")

    # Índice
    lines.append("## Índice de enums")
    lines.append("")
    for tl_name, e in sorted(all_enums, key=lambda x: x[1]["name"]):
        anchor = build_type_anchor(e["name"])
        lines.append(f"- [`{e['name']}`](#{anchor}) ({tl_name})")
    lines.append("")

    # Detalhes
    for tl_name, e in sorted(all_enums, key=lambda x: x[1]["name"]):
        anchor = build_type_anchor(e["name"])
        lines.append(f"## <a name=\"{anchor}\"></a>`{e['name']}`")
        lines.append("")
        lines.append(f"_Type library:_ `{tl_name}`  ")
        lines.append(f"_Membros:_ {len(e['members'])}")
        lines.append("")
        lines.append("| Constante | Valor |")
        lines.append("|-----------|-------|")
        for m in e["members"]:
            lines.append(f"| `{m['name']}` | {m['value']} |")
        lines.append("")

    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Escrito: {out_path}")


def write_api_index(typelibs: list, out_path: Path):
    """Gera o índice do catálogo de API."""
    out_path.parent.mkdir(parents=True, exist_ok=True)

    lines = []
    lines.append("# Catálogo de API COM do Solid Edge")
    lines.append("")
    lines.append("Este diretório contém a documentação de API gerada a partir do dump da type library do Solid Edge.")
    lines.append("")
    lines.append("## Type libraries disponíveis")
    lines.append("")
    for tl in typelibs:
        file_name = f"{tl['name']}.md"
        lines.append(f"- [`{tl['name']}`](./{file_name}) — {len(tl['types'])} tipos")
    lines.append("")
    lines.append("## Constantes")
    lines.append("")
    lines.append("- [Constantes e Enums consolidados](./constants.md)")
    lines.append("")
    lines.append("## Notas")
    lines.append("")
    lines.append("- Toda a geometria usa **metros/radianos** internamente.")
    lines.append("- Coleções COM são **1-based** (primeiro elemento em `Item(1)`).")
    lines.append("- A coluna `Retorno` mostra o tipo COM declarado na type library; em late binding pode ser necessário fazer cast/unmarshal manualmente.")
    lines.append("- Para descobrir novos métodos ou comparar versões, reexecute `ComDiagnostics.DumpTypeLibraries` e regenere estes arquivos com `tools/generate_api_docs.py`.")

    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Escrito: {out_path}")


def main():
    dump_path = find_dump_path()
    print(f"Lendo dump: {dump_path}")
    typelibs = parse_dump(dump_path)
    print(f"Type libraries encontradas: {len(typelibs)}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)

    for tl in typelibs:
        out_file = OUT_DIR / f"{tl['name']}.md"
        write_typelib_doc(tl, out_file)

    write_constants_doc(typelibs, OUT_DIR / "constants.md")
    write_api_index(typelibs, OUT_DIR / "README.md")

    print("\nDocumentação gerada com sucesso em:", OUT_DIR)


if __name__ == "__main__":
    main()
