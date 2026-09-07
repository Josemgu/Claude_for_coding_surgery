"""Un campo corregible: su valor, de donde salio, y el trozo de escaneo al lado.

Todo lo que la FASE 3 pide de un campo esta aqui y en un solo sitio, para que no
se pueda dibujar uno a medias:

  - el valor, editable;
  - **la palabra en espanol** que dice su estado —«anotación», «OCR», «revisar»,
    «no válido», «tachado, sin corrección»—, que es lo que cumple el criterio 2 de
    la fase: la distincion no puede depender de percibir un color;
  - el color de fondo, que solo acelera a quien si lo ve;
  - **el motivo escrito** cuando no vale, nombrando el campo y no un codigo;
  - **lo que el lector leyo** cuando el campo se quedo vacio, para que nada se
    pierda en silencio (`linea_de_lo_que_se_leyo`, abajo);
  - **la tira del escaneo** de esa fila del papel.

**Se revalida al soltar cada tecla, no al guardar.** Un campo que se pone rojo
media hora despues, cuando ya se pulso Guardar, obliga a volver a buscar donde
estaba el error. Puesto en el acto, se corrige mirando lo que se acaba de teclear.
"""

from tkinter import ttk

from datos.procedencia import banda_de_la_procedencia
from datos.validacion import ErrorDeValidacion
from extraccion.campos import ORIGEN_VACIO
from interfaz.tema import (
    LETRA_DATO,
    LETRA_NORMAL,
    LETRA_PEQUENA,
    NO_VALIDO,
    PALABRAS,
    descripcion_del_estado,
    estado_del_campo,
)
from interfaz.tira import TiraDelEscaneo

# Los dos repartos de esta pantalla, medidos en caracteres y en pixeles.
#
# Con tira, la fila ocupa el ancho entero de la ventana y la entrada puede ser
# ancha. Sin tira —la pantalla partida, con el documento a la izquierda— la
# columna de campos se queda en 532 px de los 1100, y el mockup le da 210 px a la
# entrada: 24 caracteres de Consolas 11 quedan en ese entorno. Con los 34 de
# antes, la linea de detalle y la palabra de estado se salian del panel.
ANCHO_DE_LA_ENTRADA_CON_TIRA = 34
ANCHO_DE_LA_ENTRADA_SIN_TIRA = 24

# Donde parte el texto de un aviso de campo. Con la columna a 532 px, 760 px de
# `wraplength` no parten nunca y el aviso desborda el panel por la derecha.
CORTE_DEL_AVISO_CON_TIRA = 760
CORTE_DEL_AVISO_SIN_TIRA = 430


def linea_de_lo_que_se_leyo(valor, procedencia):
    """Lo que el lector vio cuando el campo se quedo vacio. Cadena vacia si no aplica.

    **El problema que tapa.** Cuando el OCR lee algo que no encaja con el formato,
    el campo se queda vacio y lo leido se guarda en `procedencia_campo.valor_ocr`
    —donde el dueno no lo ve—. Medido el 2026-09-04 sobre sus siete PDF reales: dos
    cedulas terminadas en letra desaparecian de la pantalla sin dejar rastro. El
    formato ya se arreglo; esta linea es para la SIGUIENTE forma que el papel traiga
    y que todavia no conozcamos. El dueno verifica documento por documento, y lo que
    necesita es VER lo que decia el papel.

    **Las cuatro veces que devuelve cadena vacia, y por que cada una:**

      - El campo TIENE valor. Una cedula terminada en letra se guarda como
        cualquier otra: es formato normal y corriente, no un caso sospechoso
        (dueno, 2026-09-04: «muchas cedulas tienen una A u otra letra al final»).
        Senalar lo que esta bien ensena a ignorar la senal.
      - El lector no leyo nada ahi. No hay nada que ensenar y `descripcion_del_estado`
        ya lo dice con «el lector no leyó nada aquí».
      - ⚠️ **El campo esta anulado por un tachon.** Ahi `valor_ocr` guarda lo que
        habia DEBAJO del tachon, y eso no se resucita nunca: alguien lo tacho por
        algo, y ensenarlo como «lo que decia el papel» invita a copiarlo. Es el
        punto 4 de `extraccion/campos.py`, que dice literalmente que hay que
        defenderlo cuando alguien proponga aprovechar ese texto.
      - **El campo lo vacio una mano** (`origen='manual'`). Entonces no se perdio
        nada: alguien decidio que ahi no va nada, y decirle «no se guardó» seria
        contarle un problema que no existe. Solo habla el origen `'vacio'`, que es
        justo el que `extraccion/campos.py` pone cuando lo leido no encajo.

    Va como funcion suelta y no como metodo para poder comprobar el texto sin abrir
    una ventana, que es lo mismo que hace `texto_del_acuse` en
    `interfaz/acuse_de_guardado.py`.
    """
    if valor is not None and str(valor).strip():
        return ""
    if not procedencia:
        return ""
    if procedencia.get("origen") != ORIGEN_VACIO:
        return ""
    if procedencia.get("anulado_por_tachon"):
        return ""
    leido = (procedencia.get("valor_ocr") or "").strip()
    if not leido:
        return ""
    return f"El lector leyó aquí «{leido}» y no encaja con el formato: no se guardó."


class CampoCorregible(ttk.Frame):
    """Una fila de la pantalla de correccion: etiqueta, valor, estado y tira.

    `validar` es la funcion de `datos/validacion.py` que le toca a este campo, o
    None cuando el campo no tiene regla que lo valide. El nombre —que es el que
    **siempre** se muestra para confirmar— es justo ese caso: no hay forma de
    decidir si «ANONIMO, J0SE M1GUEL» esta bien o mal, asi que no se pinta nunca de
    rojo y aun asi hay que mirarlo.
    """

    def __init__(
        self,
        padre,
        nombre_del_campo,
        etiqueta,
        etiqueta_del_papel="",
        validar=None,
        solo_lectura=False,
        al_cambiar=None,
        con_tira=True,
        pagina_pdf=None,
    ):
        super().__init__(padre, padding=(0, 6))
        self.nombre_del_campo = nombre_del_campo
        self.etiqueta = etiqueta
        self._validar = validar
        self._al_cambiar = al_cambiar
        self._procedencia = None
        self._motivo = None
        self._estado = None
        # Lo que se cargo, antes de que nadie teclee. Se pone aqui y no solo en
        # `cargar` porque `refrescar` lo lee: un campo que se refresque antes de
        # cargarse levantaria `AttributeError` en mitad de dibujar la pantalla.
        self._valor_original = ""
        # De que hoja del PDF salio este campo y donde estaba en ella. Los dos son
        # lo que el visor de la pantalla partida necesita para ensenar la hoja
        # correcta y marcar la fila correcta al recibir el foco. `banda` se rellena
        # en `cargar`, que es cuando se sabe: sale de la procedencia guardada.
        self.pagina_pdf = pagina_pdf
        self.banda = None

        self.columnconfigure(1, weight=1)
        self._titulo = ttk.Label(self, text=etiqueta, font=LETRA_NORMAL)
        self._titulo.grid(row=0, column=0, sticky="w")
        ttk.Label(self, text=etiqueta_del_papel, style="Secundario.TLabel").grid(
            row=1, column=0, sticky="nw", padx=(0, 12)
        )

        self.entrada = ttk.Entry(
            self,
            font=LETRA_DATO,
            width=ANCHO_DE_LA_ENTRADA_CON_TIRA if con_tira else ANCHO_DE_LA_ENTRADA_SIN_TIRA,
        )
        if solo_lectura:
            # `numero_caso` no se edita: es la mitad de la clave con la que se
            # reconcilia el Excel que vuelve (FASE 6). Cambiarlo aqui
            # rompe el par `numero_caso` + `mrn` sin que nada avise. `takefocus=0`
            # ademas lo saca del recorrido de Tab, para no gastar pulsaciones en
            # un campo donde no hay nada que hacer.
            self.entrada.state(["readonly"])
            self.entrada.configure(takefocus=0)
        self.entrada.grid(row=0, column=1, sticky="ew", padx=(0, 12))

        self._palabra = ttk.Label(self, font=LETRA_PEQUENA)
        self._palabra.grid(row=0, column=2, sticky="w")
        self._detalle = ttk.Label(self, style="Secundario.TLabel")
        self._detalle.grid(row=1, column=1, sticky="w", padx=(0, 12))

        # ⚠️ **La tira y el visor del documento NO conviven, y es aritmetica.** La
        # ventana mide 1100 px de minimo; la tira 380, la columna de campos 478 y
        # el visor 450 para ser util: 1308. En la pantalla partida el documento
        # entero ocupa el sitio de la tira y este marco se construye con
        # `con_tira=False`, asi que aqui no se crea nada.
        #
        # El parametro existe —en vez de borrar la tira— porque son dos repartos
        # distintos de la misma pantalla y el segundo se acaba de estrenar. Lo que
        # queda sin llamador vivo con esto va dicho en el informe, no borrado a
        # mitad de fase.
        self.tira = TiraDelEscaneo(self) if con_tira else None
        if self.tira is not None:
            self.tira.grid(row=0, column=3, rowspan=2, padx=(12, 0))

        self._aviso = ttk.Label(
            self,
            style="Secundario.TLabel",
            wraplength=CORTE_DEL_AVISO_CON_TIRA if con_tira else CORTE_DEL_AVISO_SIN_TIRA,
        )
        self._aviso.grid(row=2, column=1, columnspan=3, sticky="w", pady=(2, 0))

        # La linea que dice QUE leyo la maquina cuando el campo se quedo vacio. Va
        # en su propia fila y no dentro de `_aviso`: aquella dice por que lo
        # tecleado no vale AHORA y cambia con cada tecla; esta dice lo que se leyo
        # del papel y no cambia nunca. Dos cosas en una etiqueta son dos cosas que
        # se pisan.
        self._lo_leido = ttk.Label(
            self,
            style="Secundario.TLabel",
            wraplength=CORTE_DEL_AVISO_CON_TIRA if con_tira else CORTE_DEL_AVISO_SIN_TIRA,
        )
        self._lo_leido.grid(row=3, column=1, columnspan=3, sticky="w", pady=(2, 0))

        self.entrada.bind("<KeyRelease>", self._al_teclear)
        self.entrada.bind("<FocusOut>", self._al_teclear)
        self.entrada.bind("<Escape>", self._deshacer)

    def cargar(self, valor, procedencia, bytes_de_la_tira=None):
        """Pone el valor guardado, su procedencia y su tira. No valida todavia.

        Tambien guarda **la banda**, que es donde estaba este campo en la hoja. Se
        saca aqui y no fuera porque es la unica funcion que ve la procedencia
        entera, y tenerla en dos sitios es tener dos que se pueden separar.
        """
        self._procedencia = procedencia
        self.banda = banda_de_la_procedencia(procedencia)
        self._valor_original = "" if valor is None else str(valor)
        estaba_en_solo_lectura = "readonly" in self.entrada.state()
        if estaba_en_solo_lectura:
            self.entrada.state(["!readonly"])
        self.entrada.delete(0, "end")
        self.entrada.insert(0, self._valor_original)
        if estaba_en_solo_lectura:
            self.entrada.state(["readonly"])
        if self.tira is not None:
            self.tira.mostrar(bytes_de_la_tira)
        self.refrescar()

    def _deshacer(self, evento=None):
        """Esc devuelve el valor anterior. NO cierra la ventana.

        Cerrar con Esc es la forma mas barata de perder diez minutos de tecleo, y
        el reflejo de pulsarlo para «quitar esto de en medio» lo tiene todo el
        mundo. Aqui Esc es deshacer, y nada mas.
        """
        self.entrada.delete(0, "end")
        self.entrada.insert(0, self._valor_original)
        self.refrescar()
        return "break"

    def valor(self):
        """Lo que hay escrito ahora, o None si esta vacio.

        Vacio devuelve None y no cadena vacia: en esta base `NULL` significa «no
        hay dato» y `''` no significa nada. Dos formas de decir lo mismo son dos
        formas de que una consulta se olvide de una.
        """
        texto = self.entrada.get().strip()
        return texto or None

    def es_valido(self):
        """Si lo escrito pasa la validacion. Un campo sin regla siempre pasa."""
        return self._motivo is None

    def cambio(self):
        """Si lo escrito es distinto de lo que se cargo."""
        return self.entrada.get().strip() != self._valor_original.strip()

    def _comprobar(self):
        """Pasa el validador y guarda el motivo del rechazo, si lo hay."""
        if self._validar is None:
            self._motivo = None
            return
        try:
            self._validar(self.valor())
            self._motivo = None
        except ErrorDeValidacion as causa:
            self._motivo = str(causa)

    def refrescar(self):
        """Vuelve a decidir el estado y a repintar la palabra, el color y el motivo."""
        self._comprobar()
        self._estado = estado_del_campo(self._procedencia, self.es_valido())
        self.entrada.configure(style=f"{self._estado}.TEntry")
        self._palabra.configure(text=PALABRAS[self._estado], style=f"{self._estado}.TLabel")
        self._detalle.configure(text=descripcion_del_estado(self._estado, self._procedencia))
        self._aviso.configure(
            text=self._motivo if self._estado == NO_VALIDO else "",
            style=f"{NO_VALIDO}.TLabel" if self._estado == NO_VALIDO else "Secundario.TLabel",
        )
        # Se mira el valor CARGADO y no lo que hay tecleado ahora mismo. Con
        # `self.valor()` la linea desapareceria al pulsar la primera tecla, que es
        # justo cuando Miguel la esta copiando: se quedaria a medias sin poder
        # volver a verla. Se va cuando el campo se guarda y se recarga.
        self._lo_leido.configure(
            text=linea_de_lo_que_se_leyo(self._valor_original, self._procedencia)
        )

    def _al_teclear(self, evento=None):
        self.refrescar()
        if self._al_cambiar is not None:
            self._al_cambiar()

    def estado(self):
        """El estado actual, para que el pie pueda contar cuantos no validan."""
        return self._estado
