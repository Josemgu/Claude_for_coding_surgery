#!/bin/bash
# leer-antes-de-lanzar.sh — PreToolUse sobre Task
#
# QUÉ HACE: el supervisor no escribe los documentos de conocimiento, pero está
#           OBLIGADO a leerlos antes de lanzar trabajo que pueda estar ya hecho.
#           Antes de lanzar QA se lee docs/INFORMES-QA.md (¿esta prueba ya se
#           hizo?); antes de lanzar al programador, docs/RUNBOOK.md (¿este fallo
#           ya se resolvió?). Si el transcript de ESTA sesión no muestra una
#           lectura real del documento, el lanzamiento no sale.
# POR QUÉ EXISTE (dueño, 2026-08-28): «aunque el supervisor no escriba los
#           documentos, debe estar obligado a leerlos, porque puede enviar a
#           hacer pruebas que ya se hicieron en cambios de sesión si no los lee».
#           Es el mandamiento XVI con puerta: no hagas nada dos veces.
# CÓMO SABE QUE LEYÓ: busca en el transcript la huella de una llamada Read con
#           el file_path del documento — no el nombre suelto, que aparecería en
#           el propio mensaje de esta denegación y abriría la puerta sin leer.
# CUÁNDO NO EXIGE: si el documento sigue siendo solo su marco («a la espera de»),
#           no hay nada que leer y pasa. Y con la LLAVE del dueño puesta, pasa.
# DEPENDE DE: jq.

set -euo pipefail

if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq. Una puerta de política falla cerrada." >&2
  exit 2
fi
input=$(cat)

[[ -f "${CLAUDE_PROJECT_DIR:-.}/.claude/LLAVE-DEL-DUENO" ]] && exit 0

rol=$(jq -r '.tool_input.subagent_type // empty' <<<"$input")
transcript=$(jq -r '.transcript_path // empty' <<<"$input")
[[ -z "$rol" || -z "$transcript" || ! -f "$transcript" ]] && exit 0

rol=$(printf '%s' "$rol" | sed 's/.*[:_]//' | tr 'A-Z' 'a-z'); rol=${rol//ñ/n}

# Qué debe haberse leído antes de lanzar cada rol. Datos, no código.
requerido=""
case "$rol" in
  qa)                    requerido="docs/INFORMES-QA.md" ;;
  programador)           requerido="docs/RUNBOOK.md" ;;
  *)                     exit 0 ;;
esac

doc="${CLAUDE_PROJECT_DIR:-.}/$requerido"
[[ -f "$doc" ]] || exit 0
# Un marco sin contenido no exige lectura: no hay nada dentro que evite repetir.
if grep -q 'a la espera de su primera' "$doc" 2>/dev/null; then exit 0; fi

# La huella de una lectura REAL: una llamada con file_path apuntando al documento.
base=$(basename "$requerido")
if grep -Eq "file_path[^}]{0,120}${base//./\\.}" "$transcript" 2>/dev/null; then
  exit 0
fi

jq -n --arg r "$rol" --arg d "$requerido" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("LEE ANTES DE LANZAR — vas a lanzar a " + $r + " y en esta sesión no consta " +
      "ninguna lectura de " + $d + ". Mandamiento XVI: no hagas nada dos veces — ahí está lo que ya " +
      "se probó o ya se resolvió, y mandarlo de nuevo cuesta un agente entero y contamina el registro " +
      "con dos verdades. Ábrelo con Read, comprueba si este encargo (o parte) ya está hecho, y " +
      "relanza: con la lectura en el transcript esta puerta se abre sola. Regla del dueño, 2026-08-28: " +
      "«aunque el supervisor no escriba los documentos, debe estar obligado a leerlos».")
  }
}'
