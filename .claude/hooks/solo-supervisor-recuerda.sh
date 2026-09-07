#!/bin/bash
# solo-supervisor-recuerda.sh — PreToolUse sobre mcp__gbrain__.*
#
# QUÉ HACE: deniega cualquier herramienta de gbrain a un SUBAGENTE. El cerebro es
#           del supervisor y de nadie más.
# CÓMO SABE QUIÉN LLAMA: el campo agent_id de la entrada JSON solo viene cuando el
#           hook dispara DENTRO de un subagente. Si no está, es el hilo principal
#           — el supervisor. Está documentado en la referencia de hooks de Claude
#           Code: "Present only when the hook fires inside a subagent call".
#
# POR QUÉ EXISTE, y son dos motivos distintos:
#   1. QA y el hacker trabajan a ciegas. Un cerebro consultable les daría los
#      hallazgos de la vuelta anterior, y un número leído antes de medir cambia
#      dónde se mira.
#   2. Cada herramienta MCP ocupa ventana de contexto en TODO agente que la tenga.
#      Seis agentes con siete verbos cada uno es contexto pagado seis veces para
#      que lo use uno.
#
# ⚠️ POR QUÉ NO BASTA CON EL frontmatter `tools:` DEL SUBAGENTE: ahí la restricción
#      depende de que alguien escriba bien la lista en seis archivos, y de que no
#      se le olvide al añadir el séptimo rol. Esto lo hace la máquina.

set -euo pipefail

if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH." >&2
  exit 2
fi

input=$(cat)
agente=$(jq -r '.agent_id // empty' <<<"$input")
herramienta=$(jq -r '.tool_name // empty' <<<"$input")

# Hilo principal: es el supervisor. El cerebro es suyo.
[[ -z "$agente" ]] && exit 0
[[ "$herramienta" != mcp__gbrain__* ]] && exit 0

tipo=$(jq -r '.agent_type // "este subagente"' <<<"$input")

jq -n --arg t "$herramienta" --arg r "$tipo" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("El cerebro es del SUPERVISOR. " + $r + " no usa " + $t +
      ". Lo que necesites saber viene en el pase, o lo pides al supervisor y él lo consulta. Si trabajas a ciegas —QA, hacker— esto es la ceguera funcionando: recordar los hallazgos de la vuelta anterior sobre el mismo artefacto es leer el número antes de medirlo. Si NO trabajas a ciegas y de verdad te falta un dato, devuélvelo como lo que es: el pase venía incompleto.")
  }
}'
