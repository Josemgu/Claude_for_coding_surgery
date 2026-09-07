#!/bin/bash
# cita-el-mandamiento.sh — Stop
#
# QUÉ HACE: no deja cerrar el turno si la última respuesta no nombra un mandamiento.
# POR QUÉ EXISTE: mandamiento 0-bis, palabras del dueño: "Sítame los mandamientos
#                 en cada cosa que hagas". Y su precisión el mismo día: "solo el
#                 mandamiento, eso obliga a que lo leas y te corrijas".
#                 El mecanismo está en NOMBRARLO, no en explicarlo: para citar el
#                 número hay que abrir el documento, y al abrirlo uno se corrige.
#
# ⚠️ GUARDA DE BUCLE: stop_hook_active. Sin esto, el hook se dispara sobre su
#                 propia continuación y no para nunca. Todo el mundo aprende esto una vez.

set -euo pipefail

# Sin jq el script sale con 127, y Claude Code trata ese código como error NO
# BLOQUEANTE: la acción seguiría adelante y la puerta quedaría desactivada en
# silencio. Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo (apt install jq) o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)

# 1. La guarda, lo primero de todo.
activo=$(jq -r '.stop_hook_active // false' <<<"$input")
[[ "$activo" == "true" ]] && exit 0

texto=$(jq -r '.last_assistant_message // empty' <<<"$input")
[[ -z "$texto" ]] && exit 0

# ⚠️ La negrita fuera ANTES de comparar (2026-08-28). «Mandamiento **XVI**.» y
#    «**XVI**» —el número en negrita y nada más, la lectura MÁS fiel del 0-bis—
#    bloqueaban, porque los asteriscos se interponen entre la palabra y el
#    numeral. Cuatro turnos seguidos bloqueados esa tarde. Es la reincidencia
#    exacta del fallo del 2026-08-25 que este guion ya documenta abajo: premiar
#    la forma menos fiel a la regla que lo justifica.
texto=${texto//\*/}

# 2. Turnos de charla corta: no se les exige cita.
n=$(printf '%s' "$texto" | wc -c | tr -d ' ')
[[ "$n" -lt 400 ]] && exit 0

# 3. ¿Nombra algún mandamiento? Romano suelto, 0, 0-bis, I-bis, VII-bis...
if grep -Eq '(mandamiento|Mandamiento)[[:space:]]+(0(-bis)?|[IVX]+(-bis)?)' <<<"$texto"; then
  exit 0
fi
if grep -Eq '(^|[^A-Za-z])(0-bis|I-bis|VII-bis|XVII|XVIII|XIX|XVI|XV|XIV|XIII|XII|XI|IX|VIII|VII|VI|IV|III|II|V|I)([^A-Za-z]|$).*(verific|medi|cit|deleg|cierr|congel)' <<<"$texto"; then
  exit 0
fi

# 4. LA CITA DESNUDA, que es la forma que el 0-bis EXIGE. Anadida el 2026-08-25.
#
# ⚠️ Este hook bloqueaba «II · III · XII» —una linea con los numeros y nada mas—
#    porque las dos comprobaciones de arriba piden o la palabra «mandamiento»
#    delante o un verbo detras. Es decir: exigia justificar la cita, que es
#    literalmente lo que el 0-bis prohibe —«solo el mandamiento, nada de parrafos
#    justificando la cita»—. Un chequeo que rechaza la forma correcta entrena a
#    escribir la incorrecta, o a saltarselo. Medido ese dia, las dos direcciones:
#      «II · III · XII» (desnuda)     -> BLOQUEABA   (debia pasar)
#      «XII» (desnuda)                -> BLOQUEABA   (debia pasar)
#      «mandamiento XII»              -> pasaba
#      sin ninguna cita               -> bloqueaba   (correcto)
#
# 📐 Se exige que la linea sea SOLO numeros, separados por · , o «y». Asi no la
#    confunde con una «I» o una «V» sueltas en la prosa, que es el motivo por el
#    que las comprobaciones de arriba pedian un acompanante.
ROMANO='(0-bis|0|[IVX]{1,5}(-bis)?)'
if printf '%s' "$texto" | tail -6 \
   | grep -Eq "^[[:space:]]*$ROMANO([[:space:]]*(·|,|y|-|\|)[[:space:]]*$ROMANO)*[[:space:]]*$"; then
  # ⚠️ El separador va por ALTERNACION, no como clase [·,]. El · es U+00B7,
  #    multibyte: dentro de corchetes grep lo parte en bytes y la clase deja de
  #    casar. Medido el 2026-08-25: con [·,] las comas pasaban y los · NO.
  exit 0
fi

jq -n '{
  decision: "block",
  reason: "Mandamiento 0-bis: nombra el mandamiento que gobierna lo que acabas de hacer, antes de cerrar el turno. SOLO el número — nada de tablas explicando cuál gobierna qué, ni párrafos justificando la cita; eso vuelve a llenar de ruido lo que esta regla existe para adelgazar. Ejemplos: «Devuelto por el II: no me creo el informe, repetí las mediciones». «No lo arreglo yo: I-bis, ese documento es del abogado». «Antes de encargarlo comprobé si existía — XVI — y existía»."
}'
