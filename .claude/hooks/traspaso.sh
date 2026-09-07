#!/bin/bash
# traspaso.sh — PreCompact y SessionEnd
#
# QUÉ HACE: escribe HANDOFF-auto.md con el estado de la sesión, para que el traspaso
#           no dependa de que alguien se acuerde.
# QUÉ NO LE TOCA: ESTADO.md, que lo escribe el supervisor. Esto es material para eso.
#
# ⚠️ ARREGLADO 2026-08-25, y el fallo merece quedar escrito porque es de manual:
#   1. SIGPIPE. `find ... | head -30` bajo `set -euo pipefail` mata el script:
#      head cierra el tubo, find muere con 141, pipefail lo propaga y set -e aborta
#      A MITAD del bloque { } > archivo. Resultado medido: HANDOFF-auto.md cortado
#      en la línea 60, sin las tres secciones finales. Y corre justo en los dos
#      momentos en que perder el traspaso duele: al comprimir y al cerrar.
#      → Se quita pipefail, y el recorte se hace con sed, no con head en un tubo.
#   2. El find no excluía .venv: casaba 2292 archivos y las 30 que salían eran
#      TODAS de site-packages. Un traspaso lleno de dependencias no informa de nada.
#      → Se podan .venv, node_modules, .git y __pycache__.
#
# 📐 Por qué NO lleva `set -e`: un hook de cierre que aborta a mitad deja un archivo
#    truncado que PARECE completo. Aquí es preferible que un comando falle y el
#    resto se escriba igual. Cada sección se defiende sola.

set -u
motivo="${1:-desconocido}"
cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0
[[ -f CLAUDE.md ]] || exit 0

destino="HANDOFF-auto.md"
tmp=$(mktemp 2>/dev/null || echo "/tmp/traspaso.$$")

{
  echo "# Traspaso automático — $(date '+%Y-%m-%d %H:%M') ($motivo)"
  echo
  echo "> Lo escribe un hook. **No sustituye a ESTADO.md**, que escribe el supervisor."
  echo "> Se sobrescribe en cada corte: aquí no se acumula historia."
  echo
  echo "## Git"
  echo "Rama: $(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo '?')"
  echo
  echo '```'
  git status --short 2>/dev/null | sed -n '1,40p'
  echo '```'
  echo "Últimos commits:"
  echo '```'
  git log --oneline -10 2>/dev/null || echo "(sin commits, o no es un repositorio)"
  echo '```'
  echo
  echo "## Archivos del PROYECTO tocados en las últimas 4 horas"
  echo "> Podados .venv, node_modules, .git y __pycache__: un traspaso lleno de"
  echo "> dependencias no informa de nada."
  echo '```'
  # Sin tubo a head: find vuelca a un temporal y sed recorta. Así no hay SIGPIPE.
  find . \( -name .venv -o -name node_modules -o -name .git -o -name __pycache__ \
            -o -name .mypy_cache -o -name .pytest_cache \) -prune -o \
       -type f \( -name '*.py' -o -name '*.js' -o -name '*.html' -o -name '*.css' \
                  -o -name '*.md' -o -name '*.json' -o -name '*.sh' \) \
       -newermt '-4 hours' -print > "$tmp" 2>/dev/null
  total=$(wc -l < "$tmp" 2>/dev/null | tr -d ' ')
  sed -n '1,30p' "$tmp" 2>/dev/null
  [[ "${total:-0}" -gt 30 ]] && echo "… y $(( total - 30 )) más (total: $total)"
  [[ "${total:-0}" -eq 0 ]] && echo "(ninguno)"
  echo '```'
  echo
  echo "## Entregables congelados"
  if [[ -s .claude/congelados.txt ]]; then
    grep -v '^#' .claude/congelados.txt 2>/dev/null | grep -v '^$' || echo "ninguno"
  else
    echo "ninguno"
  fi
  echo
  echo "## Suite de pruebas"
  echo "No se ejecuta aquí a propósito: correr pytest en un hook de cierre añade"
  echo "minutos al apagado y el número quedaría sin verificar por nadie. Se mide al abrir."
  echo
  echo "## Lo que este archivo NO sabe"
  echo "- Qué espera decisión del dueño. Eso lo escribe el supervisor."
  echo "- Qué agentes quedaron corriendo."
  echo "- Por qué se hizo lo que se hizo."
} > "$destino" 2>/dev/null

rm -f "$tmp" 2>/dev/null
echo "Traspaso escrito en $destino ($motivo)." >&2
exit 0
