"""Empacota a skill solid-edge-com num .skill (zip) pronto para subir no claude.ai.

O bundle tem o layout que o Claude espera:

    solid-edge-com/SKILL.md
    solid-edge-com/references/<cada outro .md>

SKILL.md fica na raiz da pasta da skill; todo o resto vai para references/, que é
como o próprio SKILL.md referencia os arquivos ("references/errors.md", ...).
A pasta OLD/ e o próprio .skill ficam de fora.

    python tools/pack_skill.py
"""
import os
import zipfile

SKILL_DIR = os.path.join('.claude', 'skills', 'solid-edge-com')
NAME = 'solid-edge-com'
OUT = os.path.join(SKILL_DIR, NAME + '.skill')


def main():
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
    src = os.path.join(root, SKILL_DIR)
    out = os.path.join(root, OUT)
    files = sorted(f for f in os.listdir(src) if f.endswith('.md'))
    if 'SKILL.md' not in files:
        raise SystemExit('SKILL.md nao encontrado em ' + src)

    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
        for f in files:
            arc = NAME + '/SKILL.md' if f == 'SKILL.md' else NAME + '/references/' + f
            z.write(os.path.join(src, f), arc)
            print('  +', arc)
    print('gerado:', os.path.normpath(out), os.path.getsize(out), 'bytes')


if __name__ == '__main__':
    main()
