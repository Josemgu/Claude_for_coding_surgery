"""Los colores, las palabras y los estilos que comparten las dos pantallas.

**El tema `clam` no es una preferencia estetica y no se puede quitar.** Medido en
esta maquina (Tk 9.0.4, Windows 11, Python 3.14.7) contando los pixeles del color
que de verdad se pintan dentro del campo:

    ttk.Entry con el tema por defecto ('vista'):        0 px de ambar
    tk.Entry clasico con `background`:              23 083 px de ambar
    ttk.Entry con el tema 'clam':                   23 040 px de ambar

Y lo que hace el fallo peligroso: `style.lookup(...)` DEVUELVE `#FFF3C4` en los
tres casos. Con el tema `vista` el color esta configurado, se puede consultar, y
no se pinta ni un pixel. Sin `theme_use("clam")`, todo el codigo de color por
origen de este programa seria decorativo y nadie se enteraria — los campos de
confianza baja y los que no validan se verian exactamente igual que los buenos.

**El color nunca va solo.** Cada estado lleva ademas su palabra en espanol al lado
del campo. Lo exige el criterio 2 de la FASE 3, reescrito el 2026-09-02: la
distincion no puede depender de percibir un color. La palabra es lo que cumple el
criterio; el fondo solo acelera a quien si lo ve.
"""

from tkinter import ttk

TEMA_QUE_SI_PINTA = "clam"

# Los cuatro estados de un campo, con el fondo y el texto medidos por el
# disenador. El contraste de cada par esta en el mockup: 12.01, 16.91, 12.34 y
# 13.02 a 1, todos muy por encima del 4.5:1 que pide la WCAG para texto normal.
ANOTACION = "anotacion"
OCR = "ocr"
REVISAR = "revisar"
NO_VALIDO = "no_valido"
TACHADO = "tachado"

# El quinto estado, el que el pase no nombra y el material si produce: el
# extractor devuelve `anulado_por_tachon=True` cuando hay un tachon rojo sobre la
# banda y nadie escribio la correccion. No es «no valido» —no ha fallado ninguna
# validacion— y no es «vacio» sin mas: es un dato que una persona marco como
# equivocado a proposito. Metido en cualquiera de los otros cuatro cubos, Miguel
# pierde la unica pista de que el papel ya decia que ese dato estaba mal.
COLORES = {
    ANOTACION: ("#E7F5EC", "#12351F"),
    OCR: ("#FFFFFF", "#1A1D21"),
    REVISAR: ("#FFF3C4", "#3D2A00"),
    NO_VALIDO: ("#FDE7E7", "#4A0F0B"),
    TACHADO: ("#FFF3C4", "#3D2A00"),
}

PALABRAS = {
    ANOTACION: "anotación",
    OCR: "OCR",
    REVISAR: "revisar",
    NO_VALIDO: "no válido",
    TACHADO: "tachado, sin corrección",
}

# El borde del estado «no valido» mide 2 px y el de los demas 1. Es a proposito:
# asi la distincion sobrevive en escala de grises y en una impresion, donde los
# cuatro fondos claros se parecen mucho.
GROSOR_DEL_BORDE = {ANOTACION: 1, OCR: 1, REVISAR: 1, NO_VALIDO: 2, TACHADO: 1}

FONDO = "#F7F7F8"
TEXTO = "#1A1D21"
TEXTO_SECUNDARIO = "#5A5F66"
BORDE = "#C6CAD0"

# El anillo de foco. Oscuro y no el azul de sistema #0B5FFF, y esto tambien esta
# medido: ese azul da 1.27:1 sobre el rojo solido de la pantalla de inicio —
# practicamente invisible—, y el mismo anillo tiene que valer en las dos ventanas.
# El oscuro da entre 14.30:1 y 16.91:1 sobre los cinco fondos de la pantalla de
# correccion. Sobre el rojo solido, donde el oscuro cae a 2.59:1 y no llega al 3:1
# que pide la WCAG 1.4.11, el anillo se pinta BLANCO: 6.54:1.
ANILLO_DE_FOCO = "#1A1D21"
ANILLO_DE_FOCO_SOBRE_ROJO = "#FFFFFF"
GROSOR_DEL_ANILLO = 2

ROJO_SOLIDO = "#B3261E"
ROJO_DE_FILA = "#FDE7E7"
ROJO_VENCIDO = "#6E1B15"
VERDE_RESUELTO = "#1B6E3C"
VERDE_DE_FILA = "#FFFFFF"
AMBAR_SIN_FECHA = "#8A5B00"
AMBAR_DE_FILA = "#FFF3C4"

# El marino del visor del documento, que el disenador introduce en
# `mockups/mockup-correccion-partida.html` con su contraste medido. **No significa
# un estado y nunca rellena un campo**: es el chrome del visor y el trazo del
# rectangulo que resalta la banda del campo enfocado sobre el escaneo. Que no
# comparta tono con ninguno de los cinco estados es a proposito — el estado usa
# verde, ambar, rojo y blanco— para que dos codigos de color no se pisen.
#
# Contrastes que da el mockup: marino sobre blanco 14.48:1, y sobre el papel del
# escaneo `#F2F1EE` 12.82:1, que es donde de verdad se dibuja el resalte.
MARINO = "#0F2A4A"
MARINO_MEDIO = "#1E4E79"
MARINO_TINTE = "#EDF2F7"

# El gris del papel de un escaneo. Es el fondo del lienzo del visor mientras no
# hay imagen: un blanco puro pareceria una hoja en blanco ya cargada.
PAPEL_DEL_ESCANEO = "#F2F1EE"

LETRA = "Segoe UI"
LETRA_TITULO = (LETRA, 15, "bold")
LETRA_SECCION = (LETRA, 11, "bold")
LETRA_NORMAL = (LETRA, 10)
LETRA_PEQUENA = (LETRA, 9)
LETRA_DATO = ("Consolas", 11)


def _estilos_de_los_campos(estilo):
    """Un estilo de `ttk.Entry` y uno de `ttk.Label` por cada estado."""
    for estado, (fondo, texto) in COLORES.items():
        estilo.configure(
            f"{estado}.TEntry",
            fieldbackground=fondo,
            foreground=texto,
            bordercolor=BORDE,
            borderwidth=GROSOR_DEL_BORDE[estado],
            padding=4,
        )
        # `readonly` y `disabled` tienen que repetir el fondo: sin esto Tk pinta
        # el suyo gris y un campo de solo lectura pierde su color de origen, que
        # es justo lo que hay que ver de un campo que no se edita.
        estilo.map(
            f"{estado}.TEntry",
            fieldbackground=[("readonly", fondo), ("disabled", fondo)],
            foreground=[("readonly", texto), ("disabled", texto)],
            bordercolor=[("focus", ANILLO_DE_FOCO)],
            lightcolor=[("focus", ANILLO_DE_FOCO)],
            darkcolor=[("focus", ANILLO_DE_FOCO)],
        )
        estilo.configure(f"{estado}.TLabel", background=fondo, foreground=texto)


def _estilos_de_las_filas(estilo):
    """Los fondos de las filas de la pantalla de inicio, por nivel de alarma."""
    for nombre, fondo, texto in (
        ("Roja", ROJO_DE_FILA, ROJO_VENCIDO),
        ("Verde", VERDE_DE_FILA, VERDE_RESUELTO),
        ("Ambar", AMBAR_DE_FILA, AMBAR_SIN_FECHA),
        ("Franja", ROJO_SOLIDO, "#FFFFFF"),
    ):
        estilo.configure(f"{nombre}.TFrame", background=fondo)
        estilo.configure(f"{nombre}.TLabel", background=fondo, foreground=texto)


def aplicar_tema(raiz):
    """Deja la ventana con el tema que si pinta y todos los estilos definidos.

    Devuelve el `ttk.Style` por si hace falta consultarlo. Levanta si `clam` no
    esta: sin el, el programa se veria bien y estaria mintiendo, que es peor que
    no arrancar.
    """
    estilo = ttk.Style(raiz)
    if TEMA_QUE_SI_PINTA not in estilo.theme_names():
        raise RuntimeError(
            f"El tema '{TEMA_QUE_SI_PINTA}' no está en esta instalación de Tk, y "
            "sin él los colores por origen se configuran pero no se pintan: los "
            "campos que hay que revisar se verían igual que los buenos. "
            f"Temas disponibles: {estilo.theme_names()}."
        )
    estilo.theme_use(TEMA_QUE_SI_PINTA)

    raiz.configure(background=FONDO)
    estilo.configure(".", background=FONDO, foreground=TEXTO, font=LETRA_NORMAL)
    estilo.configure("TFrame", background=FONDO)
    estilo.configure("TLabel", background=FONDO, foreground=TEXTO)
    estilo.configure("Titulo.TLabel", font=LETRA_TITULO)
    estilo.configure("Seccion.TLabel", font=LETRA_SECCION)
    estilo.configure("Secundario.TLabel", foreground=TEXTO_SECUNDARIO, font=LETRA_PEQUENA)
    estilo.configure("TButton", padding=(10, 5))

    _estilos_de_los_campos(estilo)
    _estilos_de_las_filas(estilo)
    return estilo


def estado_del_campo(procedencia, es_valido, umbral_de_confianza_baja=0.6):
    """Cual de los cinco estados le toca a un campo, mirando su procedencia.

    El orden importa y es este: primero lo que no valida, porque un valor mal
    formado hay que arreglarlo venga de donde venga; despues el tachon sin
    correccion, que es una marca que puso una persona; despues el origen y la
    confianza. Al reves, un dato de anotacion que no valida saldria pintado de
    verde y nadie lo miraria.
    """
    if not es_valido:
        return NO_VALIDO
    if procedencia is None:
        return REVISAR
    if procedencia.get("anulado_por_tachon"):
        return TACHADO
    origen = procedencia.get("origen")
    if origen == "anotacion":
        return ANOTACION
    if origen == "manual":
        return OCR
    confianza = procedencia.get("confianza")
    if origen == "ocr" and confianza is not None and confianza >= umbral_de_confianza_baja:
        return OCR
    return REVISAR


def descripcion_del_estado(estado, procedencia):
    """La linea pequena que va bajo el campo: de donde salio y con que confianza."""
    if estado == ANOTACION:
        return "confianza 1.00 · dato exacto escrito en el PDF"
    if procedencia is None:
        return "sin lectura guardada"
    if procedencia.get("origen") == "manual":
        return "corregido a mano"
    confianza = procedencia.get("confianza")
    if confianza is None:
        return "el lector no leyó nada aquí"
    return f"confianza {confianza:.2f}"
