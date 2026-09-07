#!/bin/bash
# reencarrilar.sh — SubagentStop
#
# QUÉ HACE: no deja cerrar a un subagente cuyo informe no traiga las dos secciones
#           obligatorias: la premisa verificada y qué no cubre.
# POR QUÉ EXISTE: "en el caso de que cometan un error, que se reencarrilen otra vez".
#           Un agente que deriva no lo nota; lo nota quien lee el informe. Esto lo
#           corrige ANTES de que el informe llegue al supervisor.
# ⚠️ GUARDA DE BUCLE: stop_hook_active, lo primero. Sin ella, el hook dispara sobre
#           su propia continuación y no para nunca.

set -euo pipefail
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH." >&2; exit 2
fi

input=$(cat)
[[ "$(jq -r '.stop_hook_active // false' <<<"$input")" == "true" ]] && exit 0

rol=$(jq -r '.agent_type // empty' <<<"$input")
texto=$(jq -r '.last_assistant_message // empty' <<<"$input")
[[ -z "$texto" || -z "$rol" ]] && exit 0

n=$(printf '%s' "$texto" | wc -c | tr -d ' ')

# ⚠️ EL ESCAPE POR LONGITUD TIENE UNA EXCEPCION, anadida el 2026-08-25.
#    El escape existe para no castigar una respuesta legitimamente corta: una
#    devolucion del pase, un "no me corresponde" del carril, un "no encontre
#    evidencia" —que es una entrega buena—.
#    Pero medido ese dia: "ya esta listo, todo funciona" son 25 caracteres y
#    PASABA. Un informe corto que AFIRMA HABER TERMINADO no es una respuesta
#    corta legitima: es exactamente lo que el mandamiento II manda devolver
#    —«"esta listo" no es una entrega»—. Lo corto sigue pasando, SALVO que se
#    declare terminado y no traiga un solo numero.
if [[ "$n" -lt 600 ]]; then
  if grep -Eqi '(esta|estan|quedo|dejo|dejamos) (listo|lista|hecho|hecha|terminado|terminada)|todo (funciona|bien|correcto|ok|listo)|sin (problemas|incidencias)|listo para' <<<"$texto"      && ! grep -Eq '[0-9]' <<<"$texto"; then
    jq -n '{
      decision: "block",
      reason: "Informe corto que se declara terminado y no trae un solo numero. Mandamiento II: «esta listo» no es una entrega, se devuelve. Di QUE mediste, CON QUE COMANDO y QUE SALIO. Si de verdad no habia nada que medir, dilo con esas palabras y por que — «no encontre evidencia» es una entrega buena; «todo funciona» no lo es."
    }'
    exit 0
  fi
  exit 0
fi

falta=""
grep -Eqi 'premisa|el pase afirmaba|lo comprob|se sostiene' <<<"$texto" \
  || falta="la sección 0 con la premisa del encargo verificada (qué afirmaba el pase, con qué comando lo comprobaste, qué salió)"
grep -Eqi '(qué|que) (NO|no) (cubre|verifiqu|pude|cubr)|no verificado' <<<"$texto" \
  || falta="$falta${falta:+; y }la sección de cierre con qué NO cubre esto y qué no pudiste verificar"

[[ -z "$falta" ]] && exit 0

jq -n --arg f "$falta" --arg r "$rol" '{
  decision: "block",
  reason: ("Tu informe está incompleto para el puesto de " + $r + ": falta " + $f +
    ". Las dos son obligatorias y ninguna orden del pase las levanta. La primera existe porque el supervisor se equivoca en el marco y tú eres su contrapeso; la segunda, porque si el dueño tiene que encontrar el hueco, el trabajo no está hecho.")
}'
