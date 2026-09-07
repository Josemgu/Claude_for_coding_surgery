#!/bin/bash
# cuenta-antes-de-borrar.sh — PreToolUse sobre Bash(rm *)
#
# QUÉ HACE: deniega cualquier rm recursivo y devuelve el conteo de lo que había
#           dentro, para que la decisión se tome con el número delante.
# POR QUÉ EXISTE: un corte a hoja en blanco trató static/ como código y se llevó
#                 13.626 archivos de material: la marca entera, las cuatro
#                 tipografías con su OFL-NOTICE.txt y 13.608 iconos. Se deshizo
#                 SOLO porque el corte se hizo en una rama aparte.
# LA REGLA: antes de borrar una carpeta, se cuenta qué hay dentro. Un nombre de
#           carpeta no dice qué contiene.

set -euo pipefail

# Sin jq el script sale con 127, y Claude Code trata ese código como error NO
# BLOQUEANTE: la acción seguiría adelante y la puerta quedaría desactivada en
# silencio. Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo (apt install jq) o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)
cmd=$(jq -r '.tool_input.command // empty' <<<"$input")

if ! echo "$cmd" | grep -Eq 'rm[[:space:]]+(-[a-zA-Z]*[rR][a-zA-Z]*|--recursive)'; then
  exit 0
fi

# Sacar el primer argumento que parezca una ruta, para poder contar.
objetivo=$(echo "$cmd" | tr ' ' '\n' | grep -v '^-' | grep -v '^rm$' | head -1 || true)
detalle="no se pudo determinar la ruta"
if [[ -n "${objetivo:-}" && -e "$objetivo" ]]; then
  n=$(find "$objetivo" -type f 2>/dev/null | wc -l | tr -d ' ')
  detalle="$objetivo contiene $n archivos"
fi

jq -n --arg d "$detalle" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("Borrado recursivo bloqueado. Medido: " + $d +
      ". Antes de borrar: (1) contar qué hay dentro, (2) preguntar qué depende de ello " +
      "—una carpeta puede ser un worktree enlazado y llevarse el git de la buena—, " +
      "(3) hacerlo en una rama aparte, (4) que lo autorice el dueño.")
  }
}'
