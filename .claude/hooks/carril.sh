#!/bin/bash
# carril.sh — PreToolUse, en TODOS los eventos de herramienta
#
# QUÉ HACE: comprueba que el agente que va a actuar está dentro de su alcance.
#           Si no lo está, deniega Y LE DICE DE QUIÉN ES. El reenvío es la parte
#           que importa: un rechazo que no nombra al titular es una excusa
#           disfrazada; uno que lo nombra es un reenvío.
#
# POR QUÉ EXISTE: "que no cambien sus responsabilidades porque el supervisor se
#           lo diga". Una orden del supervisor NO levanta una regla de la ficha.
#           El supervisor se equivoca en el marco, y un agente que obedece fuera
#           de su alcance convierte ese error en trabajo hecho.
#
# ⚠️ NO CONFUNDIR CON EL MANDAMIENTO V. Ese prohíbe decir "no puedo" sin haberlo
#           intentado, y habla de CAPACIDAD. Esto es ALCANCE: "no me corresponde".
#           La diferencia mecánica está en el reenvío, y por eso va en el mensaje.
#
# DÓNDE SE CONFIGURA: la tabla vive en .claude/agents/alcance-por-rol.json.
#           Añadir un rol es editar datos, no este script.

set -euo pipefail

if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo o esta puerta no protege nada." >&2
  exit 2
fi

input=$(cat)
rol=$(jq -r '.agent_type // empty' <<<"$input")
herramienta=$(jq -r '.tool_name // empty' <<<"$input")
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
tabla="${CLAUDE_PROJECT_DIR:-.}/.claude/agents/alcance-por-rol.json"

# Sin rol es el hilo principal: lo gobierna propiedad-por-rol.sh, no este.
[[ -z "$rol" || ! -f "$tabla" ]] && exit 0

# Normalizar el nombre: los plugins traen prefijo, y el guion no siempre coincide.
rol=$(printf '%s' "$rol" | sed 's/.*[:_]//' | tr 'A-Z' 'a-z')
rol=${rol//ñ/n}
jq -e --arg r "$rol" 'has($r)' "$tabla" >/dev/null 2>&1 || exit 0

titular=$(jq -r --arg r "$rol" '.[$r].titular_de' "$tabla")

# --- 1. Herramientas vedadas para este rol -----------------------------------
if jq -e --arg r "$rol" --arg t "$herramienta" \
     '(.[$r].herramientas_vedadas // []) | index($t)' "$tabla" >/dev/null 2>&1; then
  msg="Fuera de tu alcance: $rol no usa $herramienta. Tu puesto es: $titular. "
  msg+="Si el pase te lo pidió, el pase se equivocó: devuélvelo diciendo qué rol lo hace, en una frase. "
  msg+="Una orden del supervisor no levanta una regla de tu ficha."
  jq -n --arg m "$msg" '{
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: $m
    }
  }'
  exit 0
fi

# --- 2. Rutas que este rol no escribe ----------------------------------------
[[ -z "$ruta" ]] && exit 0
case "$herramienta" in Write|Edit|NotebookEdit) ;; *) exit 0 ;; esac

# ¿Casa con algún patrón prohibido? El emparejado es explícito a propósito:
# un glob como app/** dentro de [[ == ]] se expande de formas que no se ven venir,
# y un emparejador que casa de más deniega trabajo legítimo — que es peor que no tenerlo.
casa() {
  local ruta="$1" patron="$2" base
  if [[ "$patron" == */\*\* ]]; then
    base="${patron%/\*\*}"
    [[ "$ruta" == "$base"/* || "$ruta" == */"$base"/* ]] && return 0
    return 1
  fi
  # ⚠️ El lado derecho VA ENTRE COMILLAS. Sin ellas bash lo trata como glob, y
  #    basename("assets-portal/**") es "**", que casa con CUALQUIER COSA: el
  #    2026-08-25 eso denegaba a los 7 roles todos sus archivos, los suyos
  #    incluidos. Medido: los 10 casos "PERMITE" fallaban y los 12 "DENIEGA"
  #    pasaban — la firma exacta de un porton cerrado.
  #    Comprobado que no se pierde nada: TODOS los patrones con comodin de
  #    no_escribe son de la forma X/**, y esos los resuelve la rama de arriba.
  [[ "$(basename "$ruta")" == "$(basename "$patron")" ]] && return 0
  [[ "$ruta" == $patron ]] && return 0
  return 1
}

patron_roto=""
while IFS= read -r p; do
  [[ -z "$p" ]] && continue
  if casa "$ruta" "$p"; then patron_roto="$p"; break; fi
# ⚠️ El `tr -d '\r'` NO es cosmetico. jq.exe es un binario Windows y escribe su
#    salida en modo TEXTO: cada linea sale con \r\n. `read` se come el \n y deja
#    el \r pegado al patron. Medido el 2026-08-25: "assets-portal/**" llegaba con
#    longitud 17 en vez de 16, y con el \r al final la rama de X/** no reconoce el
#    patron y cae al emparejado por basename. Las capturas con $( ) SI salen
#    limpias —bash se come el \r al final—; solo `while read` lo arrastra.
done < <(jq -r --arg r "$rol" '(.[$r].no_escribe // [])[]' "$tabla" | tr -d '\r')

[[ -z "$patron_roto" ]] && exit 0

# ¿A quién se lo reenvío? Si la tabla no lo dice, se dice que no se sabe.
destino=$(jq -r --arg r "$rol" --arg p "$patron_roto" \
  '(.[$r].reenvia // {}) | to_entries | map(select(.key == $p)) | (.[0].value // "")' "$tabla")
[[ -z "$destino" ]] && destino="el rol titular de ese archivo — pregúntaselo al supervisor antes de tocarlo"

msg="Fuera de tu alcance: $ruta no es tuyo. Tú eres $rol, y tu puesto es: $titular. "
msg+="Eso lo hace $destino. Devuelve el encargo con esta frase: "
msg+="\"No me corresponde. Yo hago <tu puesto>. Lo que pides lo hace $destino.\" "
msg+="Ojo: esto NO es un \"no puedo\" del mandamiento V, que habla de CAPACIDAD y exige haberlo "
msg+="intentado. Esto es ALCANCE, y lo que lo hace válido en vez de una excusa es que nombra al titular."

jq -n --arg m "$msg" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: $m
  }
}'
