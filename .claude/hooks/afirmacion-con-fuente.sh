#!/bin/bash
# afirmacion-con-fuente.sh — Stop (el turno del SUPERVISOR, no de los subagentes)
#
# QUÉ HACE: si el turno que cierra AFIRMA («verificado», «funciona», «ya está»,
#           «todo pasa») sin UNA SOLA marca de fuente —un comando entre acentos
#           graves, «medido», «rc=», o el honesto «no lo he comprobado»—, no cierra.
# POR QUÉ EXISTE (dueño, 2026-08-27): «volvió a lo mismo: el supervisor no
#           verifica informaciones, dice lo que cree». Los subagentes ya tienen
#           reencarrilar.sh en su cierre; el supervisor no tenía puerta sobre sí.
#           Mandamiento XIII: ningún dato sale de tu boca sin decir de dónde viene.
# CALIBRADO CONTRA EL FALSO POSITIVO: la lista de afirmaciones es CORTA y la de
#           fuentes es ANCHA a propósito. El propio kit lo escribió en
#           pase-completo.sh: «un chequeo que siempre pregunta es peor que no
#           tenerlo: enseña a aprobar sin leer». Un turno conversacional sin
#           afirmaciones duras pasa sin tocarse.
# DEPENDE DE: jq.

set -euo pipefail

command -v jq >/dev/null 2>&1 || exit 0
input=$(cat)

# Si este mismo hook ya bloqueó una vez este cierre, no entra en bucle.
activo=$(jq -r '.stop_hook_active // false' <<<"$input")
[[ "$activo" == "true" ]] && exit 0

msg=$(jq -r '.last_assistant_message // empty' <<<"$input")
[[ -z "$msg" ]] && exit 0

# 1 · ¿El turno AFIRMA? Formas duras de dar algo por hecho.
afirma='verificad[oa]|comprobad[oa]|funciona( |\.|,)|ya est[aá]( |\.|,)|arreglad[oa]|corregid[oa]|todo (bien|verde|pasa)|las pruebas pasan|confirmad[oa]|qued[oó] (listo|resuelto)|(sí|si) existe|no existe'
printf '%s' "$msg" | grep -Eiq "$afirma" || exit 0

# 2 · ¿Trae fuente? Cualquiera de estas marcas cuenta.
fuente='`[^`]+`|medid[oa]|med[ií] |comando|rc=|exit [0-9]|wc -l|grep -c|git (status|diff|log|show|ls-files|rev-parse)|pytest|-> *[0-9]|→ *[0-9]|no lo he comprobado|no verificad|lo dice '
printf '%s' "$msg" | grep -Eiq "$fuente" && exit 0

jq -n '{
  decision: "block",
  reason: "Mandamiento XIII: este turno AFIRMA («verificado», «funciona», «ya está»…) y no trae ni una marca de fuente. Cada afirmación se escribe de una de dos formas: «lo medí yo, con este comando» —y el comando va en el mensaje, entre acentos graves, con su salida— o «lo dice X y NO lo he comprobado». Un documento del repositorio tampoco es una medición: se cita como cita. Reescribe el cierre poniendo la fuente al lado de cada afirmación, o degradando la afirmación a «no verificado»."
}'
