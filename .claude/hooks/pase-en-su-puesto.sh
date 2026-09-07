#!/bin/bash
# pase-en-su-puesto.sh — PreToolUse sobre Task
#
# QUÉ HACE: deniega lanzar un subagente cuando el ENCARGO pide trabajo de otro
#           puesto. Los patrones viven en alcance-por-rol.json, clave
#           `encargos_vedados` de cada rol: añadir uno es editar datos, no código.
# POR QUÉ EXISTE (dueño, 2026-08-27): «el supervisor pone mucho a verificar al
#           planificador — escanear el proyecto para saber el estado. El
#           planificador no hace eso: busca en internet la medida más segura de
#           arquitectura e implementación, verifica qué tecnología aplicamos, y
#           es el que indica cómo debe hacerse la construcción».
# CÓMO SE RELACIONA CON LO QUE YA HAY: carril.sh frena las ACCIONES del subagente
#           fuera de su alcance; la ficha le enseña a devolver el pase. Esta
#           puerta actúa ANTES: el pase mal dirigido no llega a gastar un agente.
# DEPENDE DE: jq y la tabla .claude/agents/alcance-por-rol.json.

set -euo pipefail

# Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)
rol=$(jq -r '.tool_input.subagent_type // empty' <<<"$input")
prompt=$(jq -r '.tool_input.prompt // empty' <<<"$input")
[[ -z "$rol" || -z "$prompt" ]] && exit 0

tabla="${CLAUDE_PROJECT_DIR:-.}/.claude/agents/alcance-por-rol.json"
[[ -f "$tabla" ]] || exit 0

# Normalizar el nombre igual que carril.sh: prefijo de plugin fuera, minúsculas, ñ→n.
rol=$(printf '%s' "$rol" | sed 's/.*[:_]//' | tr 'A-Z' 'a-z'); rol=${rol//ñ/n}
jq -e --arg r "$rol" 'has($r)' "$tabla" >/dev/null 2>&1 || exit 0

n=$(jq -r --arg r "$rol" '(.[$r].encargos_vedados // []) | length' "$tabla")
[[ "$n" -eq 0 ]] && exit 0

titular_de=$(jq -r --arg r "$rol" '.[$r].titular_de' "$tabla")

for ((i=0; i<n; i++)); do
  patron=$(jq -r --arg r "$rol" --argjson i "$i" '.[$r].encargos_vedados[$i].patron' "$tabla" | tr -d '\r')
  titular=$(jq -r --arg r "$rol" --argjson i "$i" '.[$r].encargos_vedados[$i].titular' "$tabla" | tr -d '\r')
  if printf '%s' "$prompt" | grep -Eiq "$patron"; then
    jq -n --arg rol "$rol" --arg p "$patron" --arg t "$titular" --arg td "$titular_de" '{
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: "deny",
        permissionDecisionReason: ("PASE FUERA DE PUESTO — le estás pidiendo a " + $rol +
          " un encargo de la forma «" + $p + "», y eso lo hace " + $t + ". " +
          "El puesto de " + $rol + " es: " + $td + ". " +
          "Mandar el encargo al rol equivocado cuesta doble: el agente que lo devuelve (si hace " +
          "bien su trabajo) o un entregable hecho por quien no era (si lo hace mal). " +
          "Reescribe el pase para el titular, o quita ese encargo del pase.")
      }
    }'
    exit 0
  fi
done
exit 0
