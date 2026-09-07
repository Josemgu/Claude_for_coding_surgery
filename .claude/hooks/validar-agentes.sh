#!/bin/bash
# validar-agentes.sh — se ejecuta A MANO, no es un hook.
#
# QUÉ HACE: comprueba que el frontmatter YAML de cada agente carga, y avisa del
#           fallo exacto que dejó a SEIS de ocho agentes sin existir.
#
# EL FALLO, medido el 2026-08-25: seis fichas llevaban un segundo ':' dentro del
#   description: sin comillas —"Abogado de Bitácora: audita licencias…"—. En YAML
#   eso es sintaxis inválida. Claude Code NO da error: el agente simplemente NO
#   APARECE en la lista de tipos disponibles. Es el mismo modo de fallo que el
#   comentario de settings.json ya avisaba para los hooks: no protesta, no existe.
#
#   Los dos que SÍ cargaban usaban un guion en vez de dos puntos. La correlación
#   era perfecta, y por eso el arreglo es de una línea: comillas SIEMPRE.
#
# 📐 Por qué esto es un script y no un hook: no hay evento que dispare cuando un
#    agente no existe. La ausencia no genera evento. Se comprueba a mano, y se
#    corre después de tocar cualquier ficha.

set -u
raiz="${CLAUDE_PROJECT_DIR:-.}"
dir="$raiz/.claude/agents"
[[ -d "$dir" ]] || { echo "No hay $dir"; exit 1; }

command -v python3 >/dev/null 2>&1 || { echo "Hace falta python3 para leer YAML."; exit 1; }

python3 - "$dir" <<'PY'
import sys, os, glob
try:
    import yaml
except ImportError:
    print("Falta PyYAML:  pip install pyyaml"); sys.exit(1)

d = sys.argv[1]
malos, buenos = [], []
for f in sorted(glob.glob(os.path.join(d, "*.md"))):
    nombre = os.path.basename(f)
    txt = open(f, encoding="utf-8").read()
    if not txt.startswith("---"):
        malos.append((nombre, "no empieza con --- : no tiene frontmatter")); continue
    fm = txt.split("---", 2)[1]
    try:
        meta = yaml.safe_load(fm)
    except Exception as e:
        motivo = str(e).split("\n")[0][:80]
        # El caso concreto que rompió seis fichas
        for linea in fm.strip().split("\n"):
            if linea.strip().startswith("description:"):
                v = linea.split("description:", 1)[1].strip()
                if ":" in v and not (v.startswith(('"', "'"))):
                    motivo = "description: lleva ':' SIN COMILLAS -> YAML invalido"
                break
        malos.append((nombre, motivo)); continue
    if not isinstance(meta, dict) or "name" not in meta or "description" not in meta:
        malos.append((nombre, "falta name o description")); continue
    buenos.append((nombre, meta["name"]))

print(f"\n  {len(buenos)} agentes cargan · {len(malos)} NO EXISTEN\n")
for n, name in buenos:
    print(f"  OK     {n:26} name: {name}")
for n, motivo in malos:
    print(f"  ROTO   {n:26} {motivo}")

if malos:
    print("\n  Arreglo: pon la description ENTRE COMILLAS DOBLES, siempre.")
    print('    description: "Abogado de Bitácora: audita licencias y datos."')
    print("\n  Un agente con YAML invalido NO da error: no aparece en la lista de tipos.")
    sys.exit(1)
print("\n  Comprueba tambien que aparecen en la lista de tipos de la sesion:")
print("  cargar el archivo y que el agente EXISTA son dos cosas distintas.")
PY
