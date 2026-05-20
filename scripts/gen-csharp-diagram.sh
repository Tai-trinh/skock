#!/usr/bin/env bash
# Generates a Mermaid classDiagram per client/src subdirectory from C# source.
# Called by .githooks/pre-commit when .cs files are staged.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"

TMPPY=$(mktemp /tmp/gen-csharp-XXXXXX.py)
trap "rm -f $TMPPY" EXIT

cat > "$TMPPY" <<'PYEOF'
import sys, re
from pathlib import Path

src_dir = Path(sys.argv[1])
out_dir = Path(sys.argv[2])
out_dir.mkdir(parents=True, exist_ok=True)

TYPE_RE = re.compile(
    r'^public\s+(?:(?:abstract|sealed|partial|static)\s+)*'
    r'(class|interface|record(?:\s+class)?|struct)\s+(\w+)'
    r'(?:<[^>]+>)?'
    r'(?:\s*:\s*([^{\n]+))?',
    re.MULTILINE,
)

# Public methods in classes (explicit 'public' keyword, 4-space indent)
CLASS_METHOD_RE = re.compile(
    r'^    public\s+(?:(?:async|static|override|virtual|new|abstract|sealed)\s+)*'
    r'([\w<>?\[\]]+)\s+(\w+)\s*\(([^)]*)\)',
    re.MULTILINE,
)

# Methods in interface bodies (no 'public' keyword, 4-space indent, end with ; or {)
IFACE_METHOD_RE = re.compile(
    r'^    ([\w<>?\[\]]+)\s+(\w+)\s*\(([^)]*)\)\s*[;{]',
    re.MULTILINE,
)

SKIP_KEYWORDS = {'if', 'return', 'var', 'throw', 'new', 'get', 'set', 'await',
                 'where', 'from', 'select', 'using', 'foreach', 'while', 'switch'}


def clean_bases(raw):
    bases = [re.sub(r'<[^>]+>', '', b).strip() for b in raw.split(',') if b.strip()]
    parent, ifaces = None, []
    for b in bases:
        if b.startswith('I') and len(b) > 1 and b[1].isupper():
            ifaces.append(b)
        elif parent is None:
            parent = b
        else:
            ifaces.append(b)
    return parent, ifaces


def fmt_method(ret, name, params, type_name):
    if ret in SKIP_KEYWORDS or name == type_name or name[0].islower():
        return None
    args = [p.strip().split()[-1] for p in params.split(',') if p.strip()]
    return f'+{name}({", ".join(args)}) {ret}'


def parse_file(path):
    text = path.read_text(encoding='utf-8', errors='ignore')
    decls = list(TYPE_RE.finditer(text))
    types = []
    for i, m in enumerate(decls):
        kind = m.group(1).split()[0]
        name = m.group(2)
        parent, ifaces = clean_bases(m.group(3) or '')
        body_end = decls[i + 1].start() if i + 1 < len(decls) else len(text)
        body = text[m.start():body_end]

        is_iface = kind == 'interface'
        method_re = IFACE_METHOD_RE if is_iface else CLASS_METHOD_RE
        methods = []
        for mm in method_re.finditer(body):
            f = fmt_method(mm.group(1), mm.group(2), mm.group(3), name)
            if f:
                methods.append(f)

        types.append({
            'name': name,
            'is_iface': is_iface,
            'parent': parent,
            'ifaces': ifaces,
            'methods': methods[:6],
        })
    return types


for subdir in sorted(src_dir.iterdir()):
    if not subdir.is_dir():
        continue
    all_types = []
    for f in sorted(subdir.glob('*.cs')):
        all_types.extend(parse_file(f))
    if not all_types:
        continue

    lines = [f'# {subdir.name}', '', '```mermaid', 'classDiagram']
    for t in all_types:
        lines.append(f'    class {t["name"]} {{')
        if t['is_iface']:
            lines.append('        <<interface>>')
        for m in t['methods']:
            lines.append(f'        {m}')
        lines.append('    }')
    for t in all_types:
        if t['parent']:
            lines.append(f'    {t["name"]} --|> {t["parent"]}')
        for iface in t['ifaces']:
            lines.append(f'    {t["name"]} ..|> {iface}')
    lines.append('```')

    (out_dir / f'{subdir.name}.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
PYEOF

python3 "$TMPPY" "$REPO_ROOT/client/src" "$REPO_ROOT/diagrams/csharp"

git add "$REPO_ROOT/diagrams/csharp"
