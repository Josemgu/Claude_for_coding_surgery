#!/bin/bash
# sin-add-all.sh — PreToolUse sobre Bash(git add *)
#
# QUÉ HACE: deniega `git add -A`, `git add .` y `git add --all`.
# POR QUÉ EXISTE: el commit 41d6263 se llamaba "El pase de i18n, escrito y listo
#                 para salir" y metió 22 archivos, de los cuales 21 eran el trabajo
#                 EN CURSO de otro agente, capturado en mitad de una edición.
#                 El commit siguiente no lo arregla: la historia ya lo dice.

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

if echo "$cmd" | grep -Eq 'git[[:space:]]+add[[:space:]]+(-A|--all|\.)([[:space:]]|$)'; then
  jq -n '{
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: "git add -A / . / --all está prohibido con agentes trabajando. Se añade POR RUTA EXPLÍCITA, solo lo del tema que se cierra. Comprueba antes con: git status --short"
    }
  }'
  exit 0
fi
exit 0
