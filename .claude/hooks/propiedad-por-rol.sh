#!/bin/bash
# propiedad-por-rol.sh — PreToolUse sobre Edit|Write
#
# QUÉ HACE: si el que edita es el hilo principal (el supervisor) y el archivo es
#           producto de otro rol, deniega y dice de quién es.
# CÓMO SABE QUIÉN EDITA: el campo agent_id solo viene cuando el hook dispara
#           DENTRO de un subagente. Si no está, es el hilo principal.
# POR QUÉ EXISTE: mandamientos I y I-bis. "tú no actualizas nada, delegas a quien
#           le corresponde actualizar 8 números y la cuenta". La trampa: el I se
#           salta cuando la tarea parece demasiado pequeña para molestar a nadie.
#           "Son ocho números." "Es una línea." "Es aritmética." Esa frase ES la señal.

set -euo pipefail

# Sin jq el script sale con 127, y Claude Code trata ese código como error NO
# BLOQUEANTE: la acción seguiría adelante y la puerta quedaría desactivada en
# silencio. Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo (apt install jq) o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)
agente=$(jq -r '.agent_id // empty' <<<"$input")
ruta=$(jq -r '.tool_input.file_path // empty' <<<"$input")

# Con la LLAVE del dueño puesta habla el dueño, y esta puerta se abre — igual
# que candado-del-dueno.sh. Añadido el 2026-08-28: antes solo PROCESOS.md
# conocía la llave, y un marco ordenado por el dueño rebotaba contra su propia
# orden. La llave la crea él A MANO y la borra al terminar; con ella puesta,
# quien escribe ejecuta órdenes suyas.
[[ -f "${CLAUDE_PROJECT_DIR:-.}/.claude/LLAVE-DEL-DUENO" ]] && exit 0

# ⚠️ NORMALIZAR ANTES DE EMPAREJAR. Claude Code en Windows manda la ruta absoluta
#    y con contrabarras; los patrones de este proyecto son relativos y con barras,
#    y una barra nunca casa contra una contrabarra. Medido el 2026-08-25: sin esto
#    las tres puertas de ruta fallaban ABIERTAS. El porque completo, en _rutas.sh.
#    Si el ayudante falta, esta puerta NO puede decidir: falla CERRADA (exit 2).
_ayudante="${CLAUDE_PROJECT_DIR:-.}/.claude/hooks/_rutas.sh"
if [[ ! -f "$_ayudante" ]]; then
  echo "Hook desactivado: falta $_ayudante. Sin el, las rutas no se emparejan y la puerta protegeria de mentira." >&2
  exit 2
fi
# shellcheck source=/dev/null
source "$_ayudante"
ruta=$(normaliza_ruta "$ruta")

# Dentro de un subagente no aplica: cada rol edita lo suyo.
[[ -n "$agente" ]] && exit 0
[[ -z "$ruta" ]] && exit 0

dueno=""
case "$ruta" in
  # ⛔ static-nuevo/ pasó a llamarse static/ el 2026-08-30 (commit 24f0e86) y esta
  #    línea siguió apuntando al nombre muerto: los .html/.css del frontend real
  #    quedaron SIN vigilancia — fallo abierto, cazado por el supervisor con un
  #    control de cuatro rutas y confirmado por el CEO. La batería tiene desde ese
  #    día una sección de PATRONES MUERTOS para que el próximo renombrado cante.
  *app/*|*static/*|*tests/*|*alembic/*|*scripts/*|*.py|*.js) dueno="el PROGRAMADOR" ;;
  *assets-portal/mockups/*.html|*mockup*.html)                     dueno="el DISEÑADOR" ;;
  *QA-*findings.md)                                                dueno="QA" ;;
  # `docs/adr/*` VA ADEMAS de `*/docs/adr/*`: tras normalizar, la ruta queda
  # RELATIVA al proyecto, y un patron que empieza por */ exige algo delante.
  # Medido el 2026-08-25: con solo la segunda forma, docs/adr/ADR-0001.md PASABA.
  # Los documentos de conocimiento del §7 entraron el 2026-08-28 con rol titular
  # asignado por el dueño: «no todo se lo debemos dejar al supervisor».
  *PENDIENTES.md|*FASE-*.md|docs/adr/*|*/docs/adr/*|*ARQUITECTURA.md)  dueno="el PLANIFICADOR" ;;
  *GUIA-TECNOLOGIAS-BITACORA.md|*RUNBOOK.md)                       dueno="el PROGRAMADOR" ;;
  *INFORMES-QA.md)                                                 dueno="QA — es su ÚNICO documento, y nadie se lo edita" ;;
  *QUE-HACEMOS-Y-QUE-NO.md|*ABOGADO-*.md|*CREDITOS.md|*AUDITORIA-LICENCIAS.md) dueno="el ABOGADO" ;;
  # El cuaderno del dueño (2026-08-27). Lo escribe ÉL a mano; ningún agente lo
  # toca. Con la LLAVE puesta habla el dueño, y entonces sí pasa — igual que en
  # candado-del-dueno.sh, que es la otra mitad de esta protección.
  *PROCESOS.md) [[ -f "${CLAUDE_PROJECT_DIR:-.}/.claude/LLAVE-DEL-DUENO" ]] && exit 0
                                                                   dueno="el DUEÑO — es su cuaderno personal" ;;
esac

[[ -z "$dueno" ]] && exit 0

jq -n --arg r "$ruta" --arg d "$dueno" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("Mandamiento I-bis: " + $r + " es producto de " + $d +
      ". El supervisor escribe el PASE y lo lanza; no lo arregla. Suyos son: los PASE-*.md, " +
      "HANDOFF.md, BITACORA-SESIONES.md y los mandamientos. Si te descubres pensando " +
      "\"son ocho números\" o \"es aritmética\", esa frase es la señal de que te lo estás saltando.")
  }
}'
