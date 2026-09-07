#!/bin/bash
# docs-al-dia.sh — PreToolUse sobre Bash, solo actúa en `git push`
#
# QUÉ HACE: no deja subir commits de trabajo si en el lote no viaja NINGUNO de
#           los cuatro documentos (ESTADO, EN-CURSO, DECISIONES, PENDIENTES).
# POR QUÉ EXISTE (dueño, 2026-08-27): «no se están actualizando los .md tampoco».
#           Medido ese día: EN-CURSO.md llevaba 19 commits sin tocarse mientras
#           el trabajo avanzaba. El push es el puente entre las dos máquinas del
#           dueño: un documento que no viaja con su código es un documento que
#           miente en la otra máquina.
# QUÉ NO LE TOCA: un push que solo sube documentos pasa; un push sin nada que
#           subir pasa; sin upstream configurado pasa (primer push de una rama).
# LA LLAVE: .claude/LLAVE-DEL-DUENO abre esta puerta, como las demás del dueño.
# DEPENDE DE: jq y git.

set -euo pipefail

command -v jq >/dev/null 2>&1 || { echo "Hook desactivado: falta jq." >&2; exit 2; }
input=$(cat)
cmd=$(jq -r '.tool_input.command // empty' <<<"$input")

# Solo el push. Todo lo demás no es asunto de esta puerta.
printf '%s' "$cmd" | grep -Eq '\bgit\b[^|;&]*\bpush\b' || exit 0

[[ -f "${CLAUDE_PROJECT_DIR:-.}/.claude/LLAVE-DEL-DUENO" ]] && exit 0

cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0

# Sin upstream no hay rango que medir: el primer push de una rama pasa.
upstream=$(git rev-parse --abbrev-ref '@{u}' 2>/dev/null) || exit 0

pendientes=$(git log "$upstream"..HEAD --name-only --pretty=format: 2>/dev/null | grep -v '^$' | sort -u)
[[ -z "$pendientes" ]] && exit 0

# ¿Viaja alguno de los cuatro? Entonces el lote lleva su registro y pasa.
printf '%s\n' "$pendientes" | grep -Eq '^(ESTADO|EN-CURSO|DECISIONES|PENDIENTES)\.md$' && exit 0

n_commits=$(git rev-list --count "$upstream"..HEAD 2>/dev/null || echo '?')
n_archivos=$(printf '%s\n' "$pendientes" | grep -c '' || echo '?')

jq -n --arg c "$n_commits" --arg a "$n_archivos" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("DOCUMENTOS QUE NO VIAJAN — vas a subir " + $c + " commit(s) que tocan " + $a +
      " archivo(s) y NINGUNO de los cuatro documentos va en el lote. El push es el puente entre " +
      "las dos máquinas del dueño: lo que no viaja con su registro llega a la otra máquina como " +
      "trabajo sin explicar. Antes de subir: actualiza EN-CURSO.md (el ciclo activo) o el que " +
      "corresponda —ESTADO, DECISIONES, PENDIENTES—, commitéalo, y vuelve a empujar. Si de verdad " +
      "no hay nada que registrar en este lote, esa rareza la decide el dueño con su LLAVE, no tú.")
  }
}'
