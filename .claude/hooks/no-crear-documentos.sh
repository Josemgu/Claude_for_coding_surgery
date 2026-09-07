#!/bin/bash
# no-crear-documentos.sh — PreToolUse sobre Write
#
# QUÉ HACE: deniega la creación de cualquier .md O .txt del proyecto que no esté
#           en la lista blanca. El .txt entró el 2026-08-27: cerrada la puerta de
#           los .md, los documentos empezaron a salir con otra extensión.
# QUÉ NO LE TOCA: editar un archivo que YA existe (eso es corregir en el sitio,
#                 que es lo que el método pide). Solo bloquea archivos NUEVOS.
#                 Y lo que vive FUERA del proyecto (scratchpad, temp) no es un
#                 documento del proyecto: pasa.
# POR QUÉ EXISTE: "genera un documento siempre, cada cosa que le pides, y al fin y
#                 al cabo no lo lee" — y cada documento cargado degrada el contexto.
# ⚠️ HISTORIA: hasta el 2026-08-27 esta puerta NUNCA disparó. Su entrada en
#                 settings.json llevaba `"if": "Write(**/*.md)"`, y ese glob POSIX
#                 no casa jamás contra la ruta Windows con contrabarras que manda
#                 Claude Code: la puerta se saltaba entera. Así nacieron los
#                 PASE-*.md del 26-08. El filtro vive AQUÍ dentro, no en el "if".
# DEPENDE DE: jq y _rutas.sh.

set -euo pipefail

# Sin jq el script sale con 127, y Claude Code trata ese código como error NO
# BLOQUEANTE: la acción seguiría adelante y la puerta quedaría desactivada en
# silencio. Una puerta de política falla CERRADA (exit 2), nunca abierta.
if ! command -v jq >/dev/null 2>&1; then
  echo "Hook desactivado: falta jq en el PATH. Instálalo (apt install jq) o esta puerta no protege nada." >&2
  exit 2
fi
input=$(cat)
ruta=$(jq -r '.tool_input.file_path // empty' <<<"$input")

[[ -z "$ruta" ]] && exit 0
case "$ruta" in *.md|*.txt) ;; *) exit 0 ;; esac
# Editar lo que ya existe siempre se permite: la regla es corregir en el sitio.
[[ -f "$ruta" ]] && exit 0

# Normalizar: contrabarras a barras y fuera el prefijo del proyecto. Si después
# de eso la ruta SIGUE siendo absoluta, el archivo vive fuera del proyecto
# (scratchpad, temp) y no es un documento del proyecto.
_ayudante="${CLAUDE_PROJECT_DIR:-.}/.claude/hooks/_rutas.sh"
if [[ ! -f "$_ayudante" ]]; then
  echo "Hook desactivado: falta $_ayudante. Sin el, las rutas no se emparejan y la puerta protegeria de mentira." >&2
  exit 2
fi
# shellcheck source=/dev/null
source "$_ayudante"
rel=$(normaliza_ruta "$ruta")
case "$rel" in [A-Za-z]:/*) exit 0 ;; esac

base=$(basename "$rel")

permitido=0
# La lista blanca son NOMBRES EXACTOS más UN patrón (los ADR). Sin familias
# abiertas: una lista corta no se erosiona, una de ocho familias con comodines sí.
#
# Los CUATRO de estado (el ciclo vivo; EN-CURSO se vacía al cerrar):
#   ESTADO.md · DECISIONES.md · PENDIENTES.md · EN-CURSO.md
# Las reglas y el cuaderno:
#   CLAUDE.md · PROCESOS.md (del DUEÑO; candado-del-dueno lo protege)
# Los CINCO de conocimiento (dueño, 2026-08-28 — son los que CLAUDE.md §7
# declara obligatorios, cada uno con rol titular en propiedad-por-rol.sh):
#   docs/ARQUITECTURA.md              el mapa (planificador)
#   docs/RUNBOOK.md                   fallos y su resolución (programador)
#   docs/BITACORA-SESIONES.md         el acta de cada sesión (supervisor)
#   docs/GUIA-TECNOLOGIAS-BITACORA.md el libro (programador)
#   docs/INFORMES-QA.md               EL ÚNICO documento de QA para TODO el
#                                     proyecto (dueño, 2026-08-28: «un solo
#                                     documento», se acabaron los QA-*.md
#                                     regados por mockups/)
# Y el patrón de decisiones investigadas:
#   docs/adr/ADR-*.md                 un ADR por decisión, inmutable
# requirements.txt · congelados.txt   ya explicados en la versión anterior
case "$base" in
  ESTADO.md|DECISIONES.md|PENDIENTES.md|EN-CURSO.md|CLAUDE.md|PROCESOS.md|requirements.txt|congelados.txt) permitido=1 ;;
  ARQUITECTURA.md|RUNBOOK.md|BITACORA-SESIONES.md|GUIA-TECNOLOGIAS-BITACORA.md|INFORMES-QA.md)
    # Solo en docs/, no en cualquier parte: el nombre fuera de su sitio no vale.
    [[ "$rel" == docs/"$base" ]] && permitido=1 ;;
  ADR-*.md)
    [[ "$rel" == docs/adr/ADR-*.md ]] && permitido=1 ;;
esac

if [[ $permitido -eq 1 ]]; then
  exit 0
fi

jq -n --arg r "$rel" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("Este proyecto tiene CUATRO documentos de trabajo, y " + $r + " no es uno de ellos. " +
      "ESTADO.md (donde quedamos) · DECISIONES.md (que se decidio y por que) · " +
      "PENDIENTES.md (deuda abierta) · EN-CURSO.md (el ciclo activo: el pase, la entrega " +
      "y los hallazgos de QA, como SECCIONES). Un pase, un FIXES o un informe de QA no son " +
      "archivos: son secciones de EN-CURSO.md, que se vacia al cerrar el ciclo. Y cambiarle " +
      "la extension a .txt no lo convierte en otra cosa: la regla es sobre DOCUMENTOS, no " +
      "sobre extensiones. Si de verdad hace falta uno nuevo, lo autoriza el dueño y se " +
      "añade a este hook con su LLAVE (ver candado-del-dueno.sh).")
  }
}'
