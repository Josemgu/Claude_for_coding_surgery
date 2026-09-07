#!/bin/bash
# abogado-solo-sus-archivos.sh — PreToolUse dentro del subagente abogado
#
# QUÉ HACE: el abogado solo escribe en sus archivos (§3 de su ficha).
# POR QUÉ EXISTE: el reverso del mandamiento I-bis. Cada rol tiene su producto,
#                 y eso corta en las dos direcciones: nadie toca lo del abogado,
#                 y el abogado no toca lo de nadie.
# DÓNDE VIVE: se declara en el frontmatter del propio agente, así que Claude Code
#                 lo registra solo mientras ese subagente corre y lo retira al acabar.

set -euo pipefail

if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo o esta puerta no protege nada." >&2
  exit 2
fi

input=$(cat)
ruta=$(jq -r '.tool_input.file_path // empty' <<<"$input")
[[ -z "$ruta" ]] && exit 0

case "$(basename "$ruta")" in
  QUE-HACEMOS-Y-QUE-NO.md|ABOGADO-*.md|AUDITORIA-LICENCIAS.md|CREDITOS.md|\
  PRIVACIDAD.md|TERMINOS.md|RETENCION-DE-DATOS.md|LICENSE|LICENSE.md|*.license|LICENSE.txt)
      exit 0 ;;
esac
# Copiar un aviso de licencia junto a un asset es parte de su trabajo.
[[ "$ruta" == *"assets-portal/"* && "$ruta" == *LICEN* ]] && exit 0

jq -n --arg r "$ruta" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("El abogado no escribe en " + $r + ". Tus archivos son los de la §3 de tu ficha: QUE-HACEMOS-Y-QUE-NO.md, ABOGADO-*.md, AUDITORIA-LICENCIAS.md, CREDITOS.md, los avisos de licencia junto a los assets, y las políticas cuando existan. Si el código contradice una afirmación legal, va en tu informe y lo corrige quien corresponda — no lo arregles tú.")
  }
}'
