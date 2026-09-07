#!/bin/bash
# pase-sin-sesgo.sh — PreToolUse sobre Task
#
# QUÉ HACE: deniega lanzar un subagente si el pase lleva dentro el diagnóstico o
#           la solución del supervisor. El pase lleva el objeto y la pregunta,
#           nunca la causa, el método ni lo que se espera encontrar.
# POR QUÉ EXISTE (dueño, 2026-08-27): «no le puedes dar tanto de la solución a los
#           subagentes». Medido ese día: el supervisor dictó en el pase las líneas
#           exactas del CSS, la causa y la dirección del arreglo. Tres veces el
#           agente devolvió el pase corrigiendo un diagnóstico del supervisor que
#           estaba MAL. Y cuando está bien, el agente deja de pensar: se pierde lo
#           único que vale de un segundo par de ojos.
# QUÉ NO LE TOCA: los criterios de cierre con números («contraste ≥ 4.5:1») son
#           legítimos y NO se bloquean. Se bloquea el lenguaje de causa/solución.
# DEPENDE DE: jq.

set -euo pipefail

# Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)
prompt=$(jq -r '.tool_input.prompt // empty' <<<"$input")
[[ -z "$prompt" ]] && exit 0

# Las frases que delatan un pase con la respuesta dentro. En minúsculas; el grep
# va con -i. Cada una es una FORMA DE DICTAR, no una palabra suelta: «la causa es»
# sesga aunque sea verdad, y sesga MÁS si es mentira.
marcadores=(
  'la causa (es|era|probable)'
  'el fallo est[aá] en'
  'el problema es que'
  'mi (hip[oó]tesis|teor[ií]a|diagn[oó]stico)'
  'la soluci[oó]n (es|pasa por|va por)'
  'arr[eé]gla(lo)? (cambiando|poniendo|quitando)'
  'c[aá]mbialo a'
  'sustit[uú]ye(lo)? por'
  'deber[ií]a (dar|salir|quedar|medir)'
  'vas a encontrar'
  'espero que (encuentres|d[eé]|salga)'
  'estoy seguro de que'
  'lo que hay que hacer es'
  'esto ya lo tengo claro'
  'l[ií]nea[s]? [0-9]+[^.]*(cambia|pon|borra|sustituye)'
  'el defecto (es|est[aá] en)'
  'ya s[eé] (qu[eé]|d[oó]nde|por qu[eé])'
)

frase_rota=""
for m in "${marcadores[@]}"; do
  if printf '%s' "$prompt" | grep -Eiq "$m"; then frase_rota="$m"; break; fi
done

[[ -z "$frase_rota" ]] && exit 0

jq -n --arg f "$frase_rota" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("PASE CON SESGO — el encargo contiene una frase de la forma «" + $f +
      "». Mandamiento I, la forma nueva medida el 2026-08-27: meter la solución dentro del pase " +
      "es dictar sin tocar el teclado. Si tu diagnóstico está mal, el agente construye encima " +
      "del error (pasó tres veces ese día); si está bien, el agente deja de pensar. " +
      "El pase lleva: el objeto, la pregunta y el criterio de cierre en observables — nunca la " +
      "causa, el método ni lo que se espera encontrar. Reescríbelo sin esa frase y vuelve a lanzarlo. " +
      "Tu diagnóstico guárdalo para compararlo DESPUÉS con lo que el agente encuentre solo.")
  }
}'
