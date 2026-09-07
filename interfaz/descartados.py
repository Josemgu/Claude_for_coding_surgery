"""La lista de descartados: lo que volvio en el Excel y NO entro en la base.

Es una ventana propia y no un `messagebox` con un resumen, y esa es toda la
decision de este archivo. Un aviso que dice «se descartaron 3 filas» se cierra con
Aceptar y no deja nada; lo que hace falta es la lista con el numero de fila de
Excel de cada descarte y su motivo escrito, porque **con eso Miguel puede abrir el
archivo y mirar esa fila**, que es la unica forma de decidir sobre ella.

✅ **Y desde el 2026-09-03 los descartes SI se guardan.** Este archivo llevaba
escrito de si mismo lo contrario —«no se guarda en la base [...] si quiere que los
descartes sobrevivan al cierre del programa, es una tabla nueva y su migracion»— y
el dueno lo decidio tras la auditoria final de QA. La tabla es `filas_descartadas`
(version 11 del esquema, `datos/descartadas.py`) y quien escribe en ella es
`paquete/reconciliacion.py`, en el camino comun y no desde esta pantalla: un
renglon que hay que acordarse de escribir es un renglon que algun dia no se
escribe.

Esta ventana sirve para las dos cosas y por eso tiene dos formas de abrirse:

  - `VentanaDeDescartados(padre, resultado)` — los de la carga que se acaba de
    hacer, que es el momento en que hay que mirarlos.
  - `VentanaDeDescartados.de_lo_guardado(padre, conexion)` — todos los que hay
    guardados, de todas las cargas y de todos los companeros. Es la que convierte
    «quedan guardados» en «se pueden mirar».
"""

import tkinter as tk
from tkinter import ttk

from datos.descartadas import filas_descartadas
from interfaz.tema import BORDE, LETRA_DATO, LETRA_PEQUENA, ROJO_DE_FILA, ROJO_VENCIDO


SIN_NUMERO_DE_CASO = "(sin número de caso)"
SIN_MRN = "(sin MRN)"

# Lo que se dice debajo del título, y es la mitad de la ventana: sin esta frase,
# «12 filas descartadas» se lee como un fallo del programa en vez de como la regla
# que impide crear registros fantasma.
POR_QUE_NO_ENTRARON = (
    "Ninguna de estas filas se insertó en la base, y eso es a propósito: una fila "
    "cuyo par «número de caso + MRN» no existe se insertaría a ciegas y crearía un "
    "registro fantasma. Revíselas una a una y decida qué hacer con cada una. Quedan "
    "guardadas: no se pierden al cerrar esta ventana."
)


def _renglon_de_la_descartada(descartada):
    """Una descartada de la vuelta escrita en dos líneas, para copiar y pegar."""
    return (
        f"Fila {descartada.numero}: "
        f"{descartada.numero_caso or SIN_NUMERO_DE_CASO} + "
        f"{descartada.mrn or SIN_MRN}"
        + (f" — «{descartada.nombre}»" if descartada.nombre else "")
        + f"\n    Motivo: {descartada.motivo}"
    )


def texto_de_los_descartes(resultado):
    """Los descartes como texto plano, uno por linea, para copiar y pegar.

    Lleva el numero de fila DE EXCEL delante de cada uno: pegado en un correo o en
    una nota, sigue sirviendo para ir a mirar el archivo.
    """
    lineas = [
        f"Descartes del Excel de «{resultado.companero['nombre']}» "
        f"({len(resultado.descartadas)} de {len(resultado.descartadas) + len(resultado.aplicadas)} "
        "filas con datos):",
        "",
    ]
    lineas.extend(
        _renglon_de_la_descartada(descartada) for descartada in resultado.descartadas
    )
    return "\n".join(lineas)


class _DescartadaGuardada:
    """Un renglón de `filas_descartadas` con la misma forma que uno de la vuelta.

    Existe para que la ventana dibuje las dos cosas con el mismo código: lo que
    acaba de descartarse y lo que quedó guardado de cargas anteriores son la misma
    lista para quien la mira, y dos funciones de pintar acabarían enseñándolas
    distinto.
    """

    def __init__(self, fila):
        self.numero = fila["fila_excel"]
        self.numero_caso = fila["numero_caso"]
        self.mrn = fila["mrn"]
        self.nombre = fila["nombre"]
        self.motivo = (
            f"{fila['motivo']}   ·   compañero: {fila['companero'] or 'no consta'}"
            f"   ·   cargado el {fila['registrado_en']}"
        )


class VentanaDeDescartados(tk.Toplevel):
    """Las filas que no entraron, con su numero de fila y su motivo."""

    def __init__(self, padre, resultado, descartadas=None, titulo=None):
        super().__init__(padre)
        self.resultado = resultado
        # Las que se pintan. Con `resultado` son las de esa carga; con
        # `descartadas` puesto, las que se le pasen — que es como entran las
        # guardadas de cargas anteriores.
        self.descartadas = (
            list(descartadas) if descartadas is not None else list(resultado.descartadas)
        )
        self.title(titulo or "Filas descartadas — no se insertó ninguna")
        self.transient(padre)
        self.minsize(760, 420)
        self.rowconfigure(1, weight=1)
        self.columnconfigure(0, weight=1)

        self._construir_cabecera()
        self._construir_lista()
        self._construir_pie()
        self.bind("<Escape>", lambda evento: self.destroy())
        self.grab_set()

    @classmethod
    def de_lo_guardado(cls, padre, conexion):
        """Todos los descartes guardados, de todas las cargas y compañeros.

        Es lo que convierte «quedan guardados» en «se pueden mirar». Sin esta
        puerta, la tabla existiría y nadie la vería nunca — que es el mismo defecto
        que tenía la ventana antes de que hubiera tabla, un escalón más abajo.
        """
        return cls(
            padre,
            resultado=None,
            descartadas=[
                _DescartadaGuardada(fila) for fila in filas_descartadas(conexion)
            ],
            titulo="Filas descartadas guardadas — de todas las cargas",
        )

    def _construir_cabecera(self):
        cabecera = ttk.Frame(self, padding=12)
        cabecera.grid(row=0, column=0, sticky="ew")
        ttk.Label(
            cabecera,
            text=f"{len(self.descartadas)} filas descartadas",
            style="Titulo.TLabel",
        ).pack(anchor="w")
        ttk.Label(
            cabecera,
            text=POR_QUE_NO_ENTRARON,
            style="Secundario.TLabel",
            wraplength=720,
            justify="left",
        ).pack(anchor="w", pady=(4, 0))

    def _construir_lista(self):
        """Las filas dentro de algo que rueda: pueden ser muchas."""
        marco = ttk.Frame(self, padding=(12, 0))
        marco.grid(row=1, column=0, sticky="nsew")
        marco.rowconfigure(0, weight=1)
        marco.columnconfigure(0, weight=1)

        lienzo = tk.Canvas(marco, highlightthickness=0, background="#F7F7F8")
        barra = ttk.Scrollbar(marco, orient="vertical", command=lienzo.yview)
        lienzo.configure(yscrollcommand=barra.set)
        lienzo.grid(row=0, column=0, sticky="nsew")
        barra.grid(row=0, column=1, sticky="ns")

        dentro = ttk.Frame(lienzo)
        ventana = lienzo.create_window((0, 0), window=dentro, anchor="nw")
        dentro.bind(
            "<Configure>", lambda evento: lienzo.configure(scrollregion=lienzo.bbox("all"))
        )
        lienzo.bind(
            "<Configure>", lambda evento: lienzo.itemconfigure(ventana, width=evento.width)
        )
        for descartada in self.descartadas:
            self._pintar(dentro, descartada)

    def _pintar(self, padre, descartada):
        fila = tk.Frame(
            padre, background=ROJO_DE_FILA, highlightthickness=1, highlightbackground=BORDE
        )
        fila.pack(fill="x", pady=1)
        tk.Label(
            fila,
            text=(
                f"Fila {descartada.numero}   "
                f"{descartada.numero_caso or SIN_NUMERO_DE_CASO}   "
                f"{descartada.mrn or SIN_MRN}"
            ),
            background=ROJO_DE_FILA, foreground=ROJO_VENCIDO, font=LETRA_DATO,
            anchor="w", padx=8, pady=0,
        ).pack(fill="x", pady=(6, 0))
        tk.Label(
            fila,
            text=descartada.motivo,
            background=ROJO_DE_FILA, foreground=ROJO_VENCIDO, font=LETRA_PEQUENA,
            anchor="w", padx=8, pady=0, wraplength=700, justify="left",
        ).pack(fill="x", pady=(0, 6))

    def _construir_pie(self):
        pie = ttk.Frame(self, padding=12)
        pie.grid(row=2, column=0, sticky="ew")
        ttk.Button(pie, text="Copiar la lista", command=self._copiar).pack(side="left")
        ttk.Button(pie, text="Cerrar  Esc", command=self.destroy).pack(side="right")

    def _copiar(self):
        """Deja la lista en el portapapeles. Es la unica forma de sacarla de aqui.

        `update()` despues de escribir el portapapeles no es adorno: sin el, en
        Windows el contenido se pierde cuando el programa se cierra antes de que
        otro lo pida.

        Con `resultado` se copia la cabecera que dice de quien era el Excel y
        cuantas filas de cuantas se quedaron fuera; sin el —la lista de lo
        guardado— se copian los renglones, que ya llevan dentro el companero y la
        fecha de la carga. Ese denominador no se puede componer para lo guardado:
        cuantas filas traia cada Excel de hace tres semanas no lo sabe nadie, y
        escribir un numero de esos seria inventarlo.
        """
        self.clipboard_clear()
        self.clipboard_append(
            texto_de_los_descartes(self.resultado)
            if self.resultado is not None
            else "\n".join(
                _renglon_de_la_descartada(descartada) for descartada in self.descartadas
            )
        )
        self.update()
