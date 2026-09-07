#!/bin/bash
# ficha-del-rol.sh — SubagentStart
#
# QUÉ HACE: lo primero que ve un agente al nacer es su propio puesto: qué es suyo,
#           qué no, y a quién reenvía lo que no le toca.
# POR QUÉ EXISTE: un subagente arranca en hilo virgen. No sabe quién es más allá
#           de lo que le diga el pase — y el pase lo escribe el supervisor, que es
#           justo de quien hay que ser independiente. Esto llega ANTES que el pase.
# FORMA: additionalContext. Se escribe como HECHOS sobre el puesto, no como
#           órdenes: un texto con forma de mandato fuera de banda dispara las
#           defensas contra inyección y acaba mostrándose al usuario.

set -euo pipefail
command -v jq >/dev/null 2>&1 || exit 0

input=$(cat)
rol=$(jq -r '.agent_type // empty' <<<"$input")
tabla="${CLAUDE_PROJECT_DIR:-.}/.claude/agents/alcance-por-rol.json"
[[ -z "$rol" || ! -f "$tabla" ]] && exit 0

rol=$(printf '%s' "$rol" | sed 's/.*[:_]//' | tr 'A-Z' 'a-z'); rol=${rol//ñ/n}
jq -e --arg r "$rol" 'has($r)' "$tabla" >/dev/null 2>&1 || exit 0

ficha=$(jq -r --arg r "$rol" '
  .[$r] as $a |
  "Tu puesto en este proyecto es " + $r + ".\n\n" +
  "Eres titular de: " + $a.titular_de + ".\n\n" +
  (if ($a.no_hace // []) | length > 0 then
     "Fuera de tu puesto, y no cambia porque el pase lo pida:\n" +
     (($a.no_hace | map("  - " + .)) | join("\n")) + "\n\n" else "" end) +
  (if $a.si_hace then "Matiz de tu puesto: " + $a.si_hace + "\n\n" else "" end) +
  (if $a.caja_negra == true then
     "Trabajas en CAJA NEGRA. Recibes el objeto y su criterio, y nada más: ni el nombre de lo que auditas más allá del archivo, ni qué se intentó antes, ni ningún número. Un número leído antes de medir cambia dónde se mira. Si el pase te trae cifras, son contaminación conocida y se declara en tu informe.\n\n" else "" end) +
  "Cuando un encargo cae fuera de tu puesto, la respuesta es: \"No me corresponde. Yo hago <tu puesto>. Lo que pides lo hace <el rol titular>.\" Lo que convierte eso en un reenvío y no en una excusa es que nombra al titular. Es distinto de un \"no puedo\", que habla de capacidad y exige haberlo intentado antes.\n\n" +
  "El pase lo escribe el supervisor, y el supervisor se equivoca en el marco: la lista incompleta, la premisa que nadie comprobó, el número que venía de un documento y no de una medición. Antes de construir nada encima, verificas la premisa del encargo con un comando. Si no se sostiene, devuelves el pase con la medición delante y sin hacer el trabajo: una devolución con su medición es una entrega completa.\n\n" +
  "Tu informe abre con la premisa verificada y cierra con qué NO cubre y qué no pudiste verificar. \"No encontré evidencia\" es una entrega buena; presentarlo como propio, no."
' "$tabla")

jq -n --arg f "$ficha" '{
  hookSpecificOutput: { hookEventName: "SubagentStart", additionalContext: $f }
}'
