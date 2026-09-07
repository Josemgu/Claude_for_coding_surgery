#!/bin/bash
# entregable-congelado.sh — PreToolUse sobre Edit|Write
#
# QUÉ HACE: deniega tocar cualquier archivo listado en .claude/congelados.txt.
# QUÉ NO LE TOCA: nada más. No juzga el contenido.
# POR QUÉ EXISTE: mandamiento XVII. QA empezó a auditar un archivo de 229.181
#                 bytes y al cerrar pesaba 558.163 (+143%), porque se autorizó a
#                 un diseñador a tocarlo con la auditoría corriendo. Un informe
#                 sobre una versión muerta trae cifras, trae comandos, parece
#                 bueno, y describe algo que ya nadie puede reproducir.
# LÍMITE DECLARADO: no protege frente a una sesión BIFURCADA, que comparte el
#                 mismo árbol de trabajo. Eso solo lo revela git.
#
# USO: el supervisor escribe una ruta por línea en .claude/congelados.txt al
#      lanzar una auditoría a ciegas, y borra el archivo al cerrarla.

set -euo pipefail

# Sin jq el script sale con 127, y Claude Code trata ese código como error NO
# BLOQUEANTE: la acción seguiría adelante y la puerta quedaría desactivada en
# silencio. Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo (apt install jq) o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)
ruta=$(jq -r '.tool_input.file_path // empty' <<<"$input")

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
lista="${CLAUDE_PROJECT_DIR:-.}/.claude/congelados.txt"

[[ -z "$ruta" || ! -f "$lista" ]] && exit 0

while IFS= read -r linea; do
  [[ -z "$linea" || "$linea" == \#* ]] && continue
  if [[ "$ruta" == *"$linea"* ]]; then
    jq -n --arg r "$ruta" '{
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: "deny",
        permissionDecisionReason: ("CONGELADO — hay una auditoría a ciegas corriendo sobre " + $r +
          ". Mandamiento XVII: el entregable no se toca hasta que QA cierre. Si el dueño " +
          "pide el cambio igualmente, el supervisor le dice lo que cuesta, se retira la " +
          "ruta de .claude/congelados.txt Y SE LE DECLARA A QA, en vez de dejar que lo descubra.")
      }
    }'
    exit 0
  fi
# ⚠️ El `tr -d '\r'` cierra un fallo que dejaba esta puerta ABIERTA, que es el peor
#    modo posible para una puerta de politica. Si congelados.txt se guarda con
#    finales CRLF —lo normal en Windows—, `read` deja el \r pegado y la
#    comparacion *"$linea"* no casa nunca. Medido el 2026-08-25 sobre la misma
#    ruta congelada: con LF -> DENIEGA; con CRLF -> PERMITE.
done < <(tr -d '\r' < "$lista")
exit 0
