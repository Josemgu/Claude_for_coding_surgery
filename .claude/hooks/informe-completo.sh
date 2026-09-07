#!/bin/bash
# informe-completo.sh — SubagentStop
#
# QUÉ HACE: no deja que un subagente termine si su informe no declara lo que NO cubre.
# POR QUÉ EXISTE: es la ficha de rol del planificador y la exigencia del dueño:
#                 "que cada especificación cierre con qué NO cubre esto y por qué.
#                 Si me toca a mí encontrar el hueco, el agente no hizo su trabajo."
#                 El caso: fue el dueño quien detectó que Microsoft, Google y las
#                 empresas locales publican vacantes y no entraban por ninguna
#                 fuente especificada. Esa pregunta le tocaba al planificador.

set -euo pipefail

# Sin jq el script sale con 127, y Claude Code trata ese código como error NO
# BLOQUEANTE: la acción seguiría adelante y la puerta quedaría desactivada en
# silencio. Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo (apt install jq) o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)

activo=$(jq -r '.stop_hook_active // false' <<<"$input")
[[ "$activo" == "true" ]] && exit 0

tipo=$(jq -r '.agent_type // empty' <<<"$input")
texto=$(jq -r '.last_assistant_message // empty' <<<"$input")
[[ -z "$texto" ]] && exit 0

# Solo a los roles que entregan un informe. Los de ejecución pura quedan fuera.
case "$tipo" in
  planificador|qa|abogado|hacker-seguridad|hacker) ;;
  *) exit 0 ;;
esac

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

if grep -Eqi '(qué|que) (NO|no) (cubre|verifiqu|pude|cubri)|no verificado|huecos declarados' <<<"$texto"; then
  exit 0
fi

jq -n '{
  decision: "block",
  reason: "Falta la sección obligatoria de tu ficha de rol: «Qué NO cubre esto y por qué». Dos partes, las dos: (1) lo que dejaste fuera A PROPÓSITO, con el motivo; (2) lo que NO pudiste verificar, dicho tal cual — «no encontré evidencia» es una entrega buena; presentarlo como propio, no. Si el dueño tiene que encontrar el hueco, el trabajo no está hecho."
}'
