#!/bin/bash
# pase-completo.sh — PreToolUse sobre Task
#
# QUÉ HACE: no deja lanzar un subagente si el encargo no trae sus cuatro piezas.
# POR QUÉ EXISTE: mandamiento VIII — "el brief es tu producto, y sus listas se
#                 verifican; un informe correcto sobre una lista incompleta parece
#                 un trabajo bien hecho". Pasó dos veces en un día: la lista del
#                 pase dejó fuera app-roadmap.html primero y app-empleos.html después.
#                 El diseñador cumplió; el fallo era del brief.
# Y XVI: "no hagas nada dos veces, primero se verifica todo". El modo de fallo no
#                 es no buscar: es buscar y no abrir.

set -euo pipefail

# Sin jq el script sale con 127, y Claude Code trata ese código como error NO
# BLOQUEANTE: la acción seguiría adelante y la puerta quedaría desactivada en
# silencio. Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo (apt install jq) o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)
prompt=$(jq -r '.tool_input.prompt // .tool_input.description // empty' <<<"$input")
[[ -z "$prompt" ]] && exit 0

n=$(printf '%s' "$prompt" | wc -c | tr -d ' ')
faltan=""

# ⚠️ ESTAS EXPRESIONES SE AMPLIARON EL 2026-08-25, y el motivo importa más que el
#    cambio. Tal como estaban, este hook respondía "ask" a TODO — incluido un pase
#    real, completo y escrito en EN-CURSO.md. Medido ese día con el prompt literal
#    que se le mandó al planificador: las tres respuestas fueron "ask".
#    Un chequeo que siempre pregunta es peor que no tenerlo: enseña a aprobar sin
#    leer. Es el mandamiento XV-VII, dicho por el dueño: «un chequeo con falsos
#    positivos entrena al equipo a saltárselo y protege MENOS que no tenerlo».
#    La causa era de vocabulario: las expresiones buscaban palabras que este
#    proyecto no usa. El proyecto dice «criterio de cierre», «informa con» y «qué
#    entrega»; el hook exigía «cierra cuando», «criterio de aceptación» y
#    «entregable». Nadie lo vio porque solo se había probado que BLOQUEA un pase
#    malo, nunca que DEJA PASAR uno bueno.
[[ "$n" -lt 500 ]] && faltan="el encargo es demasiado corto para llevar las cuatro piezas"
grep -Eqi 'entregable|entregas|entrega|produces|escribes|informa con|qué (entrega|produce)' <<<"$prompt" || faltan="$faltan; falta el FORMATO DE SALIDA (qué archivo entrega y con qué forma)"
grep -Eqi 'no (toques|escribes|hagas|midas|reordenes)|fuera de alcance|⛔|prohibid|NO cubre|frontera' <<<"$prompt" || faltan="$faltan; faltan las FRONTERAS (qué NO toca)"
grep -Eqi 'cierra cuando|criterio de (acept|cierre)|dado que|given|se mide con|comprobable' <<<"$prompt" || faltan="$faltan; falta el CRITERIO DE CIERRE comprobable"

[[ -z "$faltan" ]] && exit 0

jq -n --arg f "$faltan" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "ask",
    permissionDecisionReason: ("Mandamiento VIII — el pase parece incompleto: " + $f +
      ". Las cuatro piezas: objetivo · formato de salida · herramientas · fronteras, más el " +
      "criterio de cierre. Y XVI antes de lanzar: ¿buscaste si ya está hecho, y ABRISTE lo que " +
      "devolvió la búsqueda? Una lista de nombres no es una respuesta. Si el pase está completo, " +
      "aprueba y sigue.")
  }
}'
