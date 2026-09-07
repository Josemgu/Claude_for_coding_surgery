#!/bin/bash
# candado-del-dueno.sh — PreToolUse sobre Edit|Write|NotebookEdit|Bash
#
# QUÉ HACE: deniega que CUALQUIER agente —supervisor incluido— modifique las
#           barandillas del proyecto: los hooks, su cableado, las fichas de rol,
#           los mandamientos y el cuaderno del dueño (PROCESOS.md).
# POR QUÉ EXISTE (dueño, 2026-08-27): las medidas de seguridad las pone el dueño
#           (o quien él mande), no el equipo al que gobiernan. Un supervisor que
#           puede editar sus propios mandamientos puede, con la mejor intención,
#           ablandarlos el día que estorben.
# LA LLAVE: si existe el archivo .claude/LLAVE-DEL-DUENO, esta puerta se abre.
#           Ese archivo lo crea el dueño A MANO (en el explorador, o en una
#           terminal SUYA: `ni .claude\LLAVE-DEL-DUENO` en PowerShell) y lo borra
#           al terminar. Crearlo por herramienta también está denegado aquí.
# LÍMITE DECLARADO: un hook no puede frenar a quien edita fuera de Claude Code.
#           No lo pretende: esa es justamente la puerta del dueño.
# DEPENDE DE: jq.

set -euo pipefail

# Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)
herramienta=$(jq -r '.tool_name // empty' <<<"$input")

llave="${CLAUDE_PROJECT_DIR:-.}/.claude/LLAVE-DEL-DUENO"
[[ -f "$llave" ]] && exit 0

# Lo protegido, como expresión sobre rutas ya pasadas a barras normales.
protegidas='\.claude/settings(\.local)?\.json|\.claude/hooks/|\.claude/agents/|docs/MANDAMIENTOS-DEL-SUPERVISOR\.md|PROCESOS\.md|LLAVE-DEL-DUENO'

deniega() {
  jq -n --arg q "$1" '{
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: ("CANDADO DEL DUEÑO — " + $q + " es una barandilla: hooks, su " +
        "cableado, fichas de rol, mandamientos o el cuaderno del dueño. Ningún agente las " +
        "modifica, supervisor incluido: las medidas de seguridad las pone el dueño, no el " +
        "equipo al que gobiernan. Si un cambio hace falta de verdad, se le PIDE al dueño con " +
        "el motivo escrito, y es él quien crea .claude/LLAVE-DEL-DUENO a mano para abrir esta " +
        "puerta mientras dura el cambio. Leer y ejecutar sigue permitido.")
    }
  }'
  exit 0
}

case "$herramienta" in
  Edit|Write|NotebookEdit)
    ruta=$(jq -r '.tool_input.file_path // empty' <<<"$input")
    [[ -z "$ruta" ]] && exit 0
    ruta="${ruta//\\//}"
    if printf '%s' "$ruta" | grep -Eq "$protegidas"; then deniega "$ruta"; fi
    ;;
  Bash|PowerShell)
    cmd=$(jq -r '.tool_input.command // empty' <<<"$input")
    [[ -z "$cmd" ]] && exit 0
    norm="${cmd//\\//}"
    # ⚠️ SOLO EL TEXTO ESTRUCTURAL (2026-08-28). Antes se analizaba el comando
    #    entero, y la PROSA disparaba la puerta: un heredoc con una cita
    #    markdown «> » se leía como redirección, y «PROCESOS.md» mencionado
    #    como texto contaba como ruta protegida — medido: un comando que solo
    #    escribía en el scratchpad fue denegado. Cuarto falso positivo de la
    #    misma familia (tras 2>/dev/null, «->» y ni/del). La cura: quitar los
    #    CUERPOS de heredoc antes de mirar rutas y verbos — lo que va ahí es
    #    dato, no instrucción.
    #    ⚠️ Las cadenas entrecomilladas NO se quitan, y el porqué pesa más que
    #    la simetría: la ruta de destino vive muchas veces entre comillas
    #    (rm '.claude/hooks/x.sh'), y quitarlas dejaría la puerta ABIERTA a
    #    toda mutación con la ruta citada — fallar abierto es peor que el falso
    #    positivo que se cura. Residuo declarado: prosa entre comillas que
    #    nombre una barandilla, junto a una redirección real a OTRO sitio,
    #    seguirá denegando. Se paga a sabiendas.
    norm=$(printf '%s\n' "$norm" | awk '
      !en_doc && match($0, /<<-?[[:space:]]*['"'"'"]?[A-Za-z_]+/) {
        marca=substr($0, RSTART, RLENGTH)
        gsub(/<<-?[[:space:]]*['"'"'"]?/, "", marca)
        en_doc=1; fin_doc=marca
        print; next
      }
      en_doc { if ($0 == fin_doc || $0 == "\t" fin_doc) { en_doc=0 }; next }
      { print }
    ')
    # Solo se deniega si el comando NOMBRA algo protegido Y trae intención de
    # escritura. cat, grep, ls y bash (ejecutar la batería) siguen libres.
    #
    # ⚠️ El detector de redirección se afinó el mismo día con TRES falsos
    #    positivos medidos en vivo: `2>/dev/null` (el 2 delante), `->` (flecha
    #    en prosa) y `ni`/`del`, que son alias de PowerShell pero también
    #    palabras del español — denegaban un commit por decir «candado del
    #    dueño». Un `>` solo cuenta si NO le precede dígito, `-`, `=` o `>`.
    # LÍMITE DECLARADO: los alias cortos de PowerShell (ni, del, sc) quedan
    #    fuera a propósito; sus cmdlets completos SÍ se detectan. Esto frena el
    #    hábito, no al malicioso — para eso está que el settings vaya commiteado
    #    y el diff se vea.
    if printf '%s' "$norm" | grep -Eq "$protegidas"; then
      escritura='(^|[^0-9=>-])>>?[[:space:]]*[^&[:space:]]|\bsed[[:space:]][^|;]*-i|\btee\b|\bmv\b|\bcp\b|\brm\b|\bchmod\b|\btruncate\b|\btouch\b|New-Item|Set-Content|Out-File|Add-Content|Remove-Item|Move-Item|Copy-Item|git[[:space:]]+(restore|checkout|rm)\b'
      if printf '%s' "$norm" | grep -Eq "$escritura"; then deniega "lo que ese comando toca"; fi
    fi
    ;;
esac
exit 0
