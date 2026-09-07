#!/bin/bash
# _rutas.sh — normaliza la ruta que manda Claude Code antes de emparejarla.
#
# ⚠️ NO ES UN HOOK. Es una funcion que cargan carril.sh, propiedad-por-rol.sh y
#    entregable-congelado.sh. El guion bajo del nombre lo marca.
#
# QUE PROBLEMA RESUELVE, medido el 2026-08-25 y es el peor de la jornada:
#   Claude Code en Windows manda la ruta ABSOLUTA y CON CONTRABARRAS:
#     C:\Users\Nombre Apellido\Bitacora-fix\app\main.py
#   Las tablas de alcance y congelados.txt guardan rutas RELATIVAS y con BARRAS:
#     app/**  ·  .claude/hooks/prueba-carril.sh
#   Un patron con barra NUNCA casa contra una ruta con contrabarra. Resultado
#   medido antes de esto:
#     carril.sh             5 de 5 rutas ajenas -> PERMITE
#     propiedad-por-rol.sh  3 de 6              -> PERMITE
#     entregable-congelado  1 de 1              -> PERMITE
#   Las tres puertas fallaban ABIERTAS, que es el peor modo posible.
#
# 📐 Por que se coló, y es la leccion que hay que llevarse: las tres se habian
#    probado con rutas POSIX escritas a mano. La prueba usaba una forma de ruta
#    que el sistema real nunca produce. Una puerta probada con una entrada
#    sintetica no esta probada: esta ensayada.
#
# LO QUE HACE, en tres pasos:
#   1. contrabarras -> barras
#   2. quita el prefijo del proyecto, para dejarla relativa
#   3. quita el "./" de delante si quedo

normaliza_ruta() {
  local ruta="$1"
  local base="${CLAUDE_PROJECT_DIR:-}"

  # 1 · Contrabarras a barras. Se hace SIEMPRE, tambien en Linux: alli no hay
  #     contrabarras que convertir, asi que no cambia nada y el codigo es uno solo.
  ruta="${ruta//\\//}"
  base="${base//\\//}"

  # 2 · Fuera el prefijo del proyecto. Con y sin barra final, porque las dos
  #     formas llegan.
  if [[ -n "$base" ]]; then
    ruta="${ruta#"$base"/}"
    ruta="${ruta#"$base"}"
  fi

  # 3 · Y el ./ de delante, que algunas herramientas anaden y otras no.
  ruta="${ruta#./}"
  ruta="${ruta#/}"

  printf '%s' "$ruta"
}
