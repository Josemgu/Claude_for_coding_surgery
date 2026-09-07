#!/bin/bash
# avisos-de-escritura.sh — PostToolUse sobre Write|Edit (async)
#
# QUÉ HACE: dos avisos que no bloquean, porque el archivo ya está escrito.
#   1. Mandamiento XIX: las letras A-Z son del plan. Nadie numera con P1, D2, etc.
#   2. Mandamiento XVIII: una auditoría a ciegas no es ciega si el artefacto lleva
#      las cifras del que lo arregló dentro, en comentarios.
# NOTA: async=true. No bloquea el flujo; el aviso llega a Claude para la próxima vuelta.

set -euo pipefail

# Este hook solo avisa; no puede bloquear. Sin jq sale limpio: perder un aviso no
# justifica ensuciar el flujo con un error.
command -v jq >/dev/null 2>&1 || exit 0
input=$(cat)
ruta=$(jq -r '.tool_input.file_path // empty' <<<"$input")
[[ -z "$ruta" || ! -f "$ruta" ]] && exit 0

avisos=""

# XIX — numeración que compite con las letras del plan.
if grep -Eq '(^|[^A-Za-z0-9])[A-Z][0-9]{1,2}([^0-9]|$)' "$ruta" 2>/dev/null; then
  if ! grep -Eq 'c[0-9]+·T[0-9]|·T[0-9]' "$ruta" 2>/dev/null; then
    avisos="Mandamiento XIX: hay etiquetas del tipo P1/D2 en este archivo. Las letras A-Z están reservadas al plan. Las tareas de un informe se numeran ancladas a su paso: c24·T1, c24·T2. Y nunca se releva al dueño una numeración interna sin traducirla."
  fi
fi

# XVIII — cifras del autor dentro del artefacto que QA va a auditar.
case "$ruta" in
  *.html|*.css|*.js)
    if grep -Eq '/\*.*[0-9]+([.,][0-9]+)?[[:space:]]*(px|:1|%)' "$ruta" 2>/dev/null; then
      avisos="$avisos
Mandamiento XVIII: este artefacto lleva mediciones en comentarios. QA tiene que abrirlo para auditarlo, así que una auditoría a ciegas sobre él no será del todo ciega. Lo barato: el pase avisa a QA de que el archivo contiene cifras del autor. Lo correcto: las mediciones viven en el FIXES-*.md; el archivo lleva el porqué de la regla, el número no."
    fi
    ;;
esac

[[ -z "$avisos" ]] && exit 0

jq -n --arg a "$avisos" '{
  hookSpecificOutput: { hookEventName: "PostToolUse", additionalContext: $a }
}'
