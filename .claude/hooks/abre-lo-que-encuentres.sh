#!/bin/bash
# abre-lo-que-encuentres.sh — PostToolUse sobre Grep|Glob
#
# QUÉ HACE: cuando una búsqueda devuelve varios resultados, mete un recordatorio
#           en el contexto. No bloquea: PostToolUse no puede, y aquí no debe.
# POR QUÉ EXISTE: mandamientos III y XVI. "Citar el índice no es citar la fuente."
#                 "El modo de fallo exacto no es no buscar: es buscar y no abrir.
#                 Un grep que devuelve diez archivos no es una respuesta, es una
#                 lista de sitios donde mirar."

set -euo pipefail

# Este hook solo avisa; no puede bloquear. Sin jq sale limpio: perder un aviso no
# justifica ensuciar el flujo con un error.
command -v jq >/dev/null 2>&1 || exit 0
input=$(cat)
salida=$(jq -r '.tool_response // .tool_result // empty' <<<"$input" 2>/dev/null || echo "")
[[ -z "$salida" ]] && exit 0

lineas=$(printf '%s' "$salida" | grep -c '' || echo 0)
[[ "$lineas" -lt 3 ]] && exit 0

jq -n '{
  hookSpecificOutput: {
    hookEventName: "PostToolUse",
    additionalContext: "La búsqueda devolvió varios resultados. En este proyecto una lista de rutas no cuenta como respuesta: hay que abrir lo que devuelve antes de apoyar una afirmación en ello. Un número sacado de un título presta autoridad a cualquier cosa que se escriba a su lado. Si no se abrió, la frase correcta es «no verificado»."
  }
}'
