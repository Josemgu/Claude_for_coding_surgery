"""La pantalla de correccion: lo leido, lo que hay que arreglar, y quien lo confirma.

El objetivo del pase es medible y manda sobre todo lo demas: **un formulario de
tres personas en menos de un minuto sin tocar el raton**. De ahi salen las tres
decisiones que gobiernan este archivo:

  - Todo lo que NO es editable lleva `takefocus=0` —las tiras del escaneo, las
    etiquetas, las palabras de estado— para que Tab no pase por ellos ni una vez.
  - `Intro` confirma el bloque de la persona actual y **salta a la siguiente**. Es
    el camino del minuto: tres Intro recorren tres personas. Tab es para cuando
    hay que corregir dentro de un bloque.
  - La cabecera y el pie NO se desplazan. Con seis personas la region central mide
    mas que la pantalla, y «Verificar caso» con su motivo de bloqueo tiene que
    leerse siempre: un boton apagado que se fue por abajo es un callejon.

**Nada se marca solo** (regla permanente 5). El contador de verificados nace en 0
y solo sube cuando Miguel pulsa.

**Los dos botones del pie, y por que son dos y no uno.** El dueno lo pidio con
estas palabras: «de un lado el documento original y del otro la informacion que yo
necesito; si esta correcto, darle todo correcto». Eso es «Todo correcto», que firma
el caso entero de una vez. Pero antes hace falta el otro, y el motivo esta medido:
en sus formularios **la informacion de las ordenanzas no existe en ninguna capa del
PDF** —36 casillas con tinta 0.0000 y 45 botones `/Btn` en `/Off`—, asi que las seis
de cada persona llegan siempre sin leer. Con cuatro personas son 24, y resolverlas
de una en una cuesta **48 pulsaciones**, no 24: la barra entra por «marcada», de
modo que dejar una en «no» partiendo de «no leida» son dos.

Se piden por separado a proposito. «Ninguna ordenanza marcada» **pone un dato**;
«Todo correcto» **firma**. Juntarlos en un boton haria que confirmar el caso
implicara decir algo sobre unas casillas que nadie nombro, y eso es exactamente lo
que la regla permanente 5 prohibe.

**Guardar pasa SIEMPRE por `espejo.escritura.guardar_y_regenerar`.** No es una
comodidad: mientras esta pantalla llame ahi y no al repositorio directamente, no
existe ningun camino que deje la base al dia y el Excel atrasado, y no hay ningun
boton que a alguien se le pueda olvidar pulsar.
"""

import os
import tkinter as tk
from tkinter import ttk

from datos.ilegibles import renglones_del_caso
from datos.propuestas import propuesta_vigente
from datos.procedencia import (
    desmarcar_campo_verificado,
    marcar_campo_verificado,
    procedencia_por_campo,
)
from datos.repositorio import (
    actualizar_datos_del_caso,
    asignar_numero_de_caso,
    leer_caso_por_id,
    leer_personas_del_caso,
)
from datos.validacion import (
    ErrorDeValidacion,
    validar_fecha_viaje,
    validar_numero_caso_si_lo_hay,
    validar_unidad_nombre,
    validar_unidad_numero,
)
from espejo.escritura import guardar_y_regenerar
from extraccion.etiquetas import (
    CAMPO_DE_LA_FECHA_DE_VIAJE,
    CAMPO_DE_LA_UNIDAD,
    forma_espanola_de,
)
from interfaz import dialogos
from interfaz.acuse_de_guardado import AcuseDeGuardado
from interfaz.avisos import Aviso, FranjaDeAvisos
from interfaz.avisos_de_correccion import agrupados_por_linea, avisos_de_la_cabecera
from interfaz.campo import CampoCorregible
from interfaz.escaneo import PaginasDelPdf
from interfaz.etiquetas import numero_de_caso_visible
from datos.companeros import (
    NOMBRE_DEL_COMPANERO_POR_DEFECTO,
    companero_que_firma as _companero_que_verifica,
)
from datos.estados import COMPLETA, NO_COMPLETA
from datos.marcas_de_revision import marcar_a_mano
from interfaz.desplazamiento import atar_desplazamiento
from interfaz.persona import BloqueDePersona, _anotar_correccion_manual
from interfaz.seguimiento import PanelDeSeguimiento
# `AMBAR_DE_FILA` y `AMBAR_SIN_FECHA` salieron de aqui el 2026-09-04 con las
# bandas de aviso que los usaban: el ambar de la franja lo pone ahora
# `interfaz/avisos.py`, que es donde se dibuja. Siguen existiendo en el tema.
from interfaz.tema import FONDO, LETRA_SECCION, NO_VALIDO
from interfaz.visor import ANCHO_POR_DEFECTO_DEL_VISOR_PX, VisorDelDocumento

# Las cuatro etiquetas de los campos del caso, en el orden del papel: cada una con
# su nombre en espanol, la etiqueta impresa del formulario que ayuda a localizarlo
# en la hoja, y el validador que le toca.
#
# ⚠️ **La etiqueta del papel se dice en ESPANOL, sea cual sea el idioma del papel**
# (regla permanente 4, decision del 2026-09-03). Hasta ese dia estos cuatro
# renglones llevaban escrita a mano la forma inglesa —`Case Number`, `Date
# traveling to the temple`, `Ward/Branch Name and Unit Number`—, y QA sondeo la
# ventana real sobre los formularios espanoles del dueno: **0 de 5 aparecen en su
# papel**. Un rotulo que nombra una etiqueta que no esta en la hoja no ayuda a
# localizar nada; estorba.
#
# Y no se escriben a mano otra vez en espanol: se toman de `FORMAS_DE_CADA_CAMPO`
# (`extraccion/etiquetas.py`), que es el unico sitio donde se dice como se llama
# cada cosa en cada idioma. Escritas aqui, el dia que se corrija una forma se
# corregiria en un sitio y no en el otro, que es como llego el defecto.
# Lo que se espera antes de contar las hojas del PDF de un caso. Un milisegundo:
# no es una pausa, es sacar ese trabajo de la cola del dibujado para que la
# pantalla se pinte primero. Ver `_contar_las_hojas_cuando_ya_se_ve`.
MILISEGUNDOS_ANTES_DE_CONTAR_LAS_HOJAS = 1

CAMPOS_DEL_CASO = (
    # ⚠️ El quinto valor es «de solo lectura POR DEFECTO», no «siempre». Desde la
    # version 7 del esquema un caso puede haber entrado sin numero —la pagina se
    # guarda igual en vez de tirarse—, y entonces este campo se abre para que
    # Miguel lo teclee. Con numero ya puesto sigue cerrado: es la mitad del par
    # que reconcilia el Excel de los companeros y cambiarlo lo rompe en silencio.
    #
    # Lleva `validar_numero_caso_si_lo_hay` y no `validar_numero_caso`: vacio es un
    # estado legitimo mientras nadie lo haya tecleado, y pintar de rojo un campo
    # que todavia nadie ha tocado ensena a ignorar el rojo.
    #
    # `numero_caso` sale SIN etiqueta del papel, y no es un hueco que falte
    # rellenar: no se busca por ancla sino por patron —cuatro letras mayusculas y
    # cuatro digitos— en la pagina entera (`extraccion/formulario.py::_numero_de_caso`),
    # asi que no hay ninguna etiqueta impresa que senalar. Escribir una seria
    # inventar una pista sobre el papel.
    ("numero_caso", "N.º de caso", "", validar_numero_caso_si_lo_hay, True),
    ("fecha_viaje", "Fecha de viaje", forma_espanola_de(CAMPO_DE_LA_FECHA_DE_VIAJE),
     validar_fecha_viaje, False),
    ("unidad_numero", "N.º de unidad", forma_espanola_de(CAMPO_DE_LA_UNIDAD),
     validar_unidad_numero, False),
    ("unidad_nombre", "Unidad", forma_espanola_de(CAMPO_DE_LA_UNIDAD),
     validar_unidad_nombre, False),
)


# Quien firma la verificacion. La tabla `procedencia_campo` exige un compañero con
# `verificado_por`, y los compañeros son la FASE 6, que todavia no existe.
#
# ⚠️ Mientras eso llegue, el boton «Verificar caso» pide el compañero y no hay
# ninguno. Se resuelve dando de alta un compañero unico —el propio Miguel— la
# primera vez que se verifica algo. NO es una firma inventada: es la persona que
# esta usando el programa, y el dia que la FASE 6 traiga varios, esta fila es una
# mas y no hay nada que deshacer.
# ⚠️ El nombre y la funcion que da de alta a ese companero **subieron a
# `datos/companeros.py` el 2026-09-03**, y aqui solo se reexportan para que nada
# de lo que ya los importaba de esta pantalla tenga que cambiar. El motivo: la
# pantalla «Revisar» firma tambien, y dos definiciones de «quien es Miguel»
# acabarian dando de alta dos companeros con el mismo nombre.

# Cuanto papel se deja por encima y por debajo del control al que salta el foco,
# en pixeles. Sin margen el control queda pegado al borde del hueco y no se ve la
# etiqueta que dice que campo es, que es la mitad de la informacion.
MARGEN_AL_SEGUIR_EL_FOCO = 16

# Los tres campos del caso que esta pantalla escribe. `numero_caso` NO esta y no es
# un olvido: se escribe por `asignar_numero_de_caso`, que es la unica puerta que
# abre en un solo sentido —de «no se sabe» a un numero— y nunca al reves.
CAMPOS_DEL_CASO_QUE_SE_ESCRIBEN = ("unidad_numero", "fecha_viaje", "unidad_nombre")


class PantallaDeCorreccion(ttk.Frame):
    """La ventana donde se corrige un caso entero con el teclado."""

    def __init__(
        self, padre, conexion, caso_id, al_volver, carpeta_de_datos=None,
        al_archivar=None,
    ):
        super().__init__(padre, padding=12)
        self.conexion = conexion
        self.caso_id = caso_id
        self._al_volver = al_volver
        self._carpeta_de_datos = carpeta_de_datos
        # `al_archivar` admite None: las pruebas de esta pantalla la construyen sin
        # la de archivo, y un boton que no lleva a ningun sitio es peor que ninguno.
        self._al_archivar = al_archivar
        self.caso = leer_caso_por_id(conexion, caso_id)
        if self.caso is None:
            raise ErrorDeValidacion(f"No hay ningún caso con el id {caso_id!r}.")
        # Las hojas del PDF, no LA hoja: un caso de grupo reparte sus personas en
        # varias, y cada una se mira en la suya.
        self.paginas = PaginasDelPdf(self.caso["ruta_pdf"])
        # ⚠️ `self.pagina` es la hoja que abrio el caso. **Desde que el documento
        # entero sustituye a la tira, ya nadie la lee**: los cuatro campos del caso
        # a los que alimentaba se dibujan con `con_tira=False` y quien ensena su
        # hoja es el visor. Se deja porque durante el desarrollo «sin usar» y «sin
        # terminar» se ven igual desde fuera, y porque es publica: quitarla es un
        # cambio de interfaz que se decide al cerrar la fase, no a mitad. Va dicho
        # en el informe de este pase.
        self.pagina = self.paginas.pagina(self.caso["pagina_pdf"])
        # El temporizador que cuenta las hojas, para poder cancelarlo al salir. Se
        # declara aqui —antes de construir nada— porque `soltar_atajos()` puede
        # llamarse sobre una pantalla que no llego a terminar de construirse.
        self._conteo_de_hojas = None

        self.rowconfigure(2, weight=1)
        self.columnconfigure(0, weight=1)
        self._construir_cabecera()
        self._construir_region_central()
        self._construir_pie()
        self.atar_atajos()
        # Cancelar tambien al destruirse, y no solo en `soltar_atajos()`. La
        # ventana llama a `soltar_atajos()` al cambiar de pantalla, pero una
        # pantalla se puede destruir por otros caminos —cerrar la ventana entera—,
        # y entonces Tk borra el comando del temporizador y contesta «invalid
        # command name ..._contar_las_hojas_cuando_ya_se_ve» al dispararse. Medido:
        # esa linea salia por consola durante la suite.
        self.bind("<Destroy>", self._al_destruirse, add="+")
        self._recontar()

    def _al_destruirse(self, evento=None):
        """Cancela el temporizador si esta pantalla —no un hijo suyo— se destruye.

        Se comprueba el widget del evento porque `<Destroy>` sube desde cada uno de
        los controles de dentro, y sin el filtro esto correria decenas de veces.
        """
        if evento is not None and evento.widget is not self:
            return
        if self._conteo_de_hojas is not None:
            try:
                self.after_cancel(self._conteo_de_hojas)
            except tk.TclError:
                # El interprete ya se estaba cerrando: no queda cola que tocar.
                pass
            self._conteo_de_hojas = None

    # ---- cabecera -------------------------------------------------------

    def _construir_cabecera(self):
        """El numero de caso, la pagina, y las bandas de aviso del formulario."""
        cabecera = ttk.Frame(self)
        cabecera.grid(row=0, column=0, sticky="ew")
        cabecera.columnconfigure(0, weight=1)

        ttk.Label(
            cabecera, text=numero_de_caso_visible(self.caso), style="Titulo.TLabel"
        ).grid(
            row=0, column=0, sticky="w"
        )
        pagina = self.caso["pagina_pdf"]
        ttk.Label(
            cabecera,
            text=(
                f"página {pagina} del PDF" if pagina else "página del PDF no guardada"
            )
            + f" · importado el {self.caso['creado_en']}",
            style="Secundario.TLabel",
        ).grid(row=1, column=0, sticky="w")

        botones = ttk.Frame(cabecera)
        botones.grid(row=0, column=1, rowspan=2, sticky="e")
        ttk.Button(botones, text="Abrir el PDF completo  F2", command=self.abrir_pdf).grid(
            row=0, column=0, padx=4
        )
        ttk.Button(botones, text="Volver al inicio  Ctrl+Inicio", command=self._volver).grid(
            row=0, column=1, padx=4
        )
        # La campana solo se dibuja cuando Miguel cierra la franja. Se construye
        # ANTES que la franja porque `FranjaDeAvisos` llama a `al_cambiar` ya
        # dentro de su propio constructor.
        self._campana = ttk.Button(botones, text="", command=self._reabrir_los_avisos)

        # ⚠️ **UNA franja, no una banda por aviso.** Hasta el 2026-09-04 esto
        # pintaba un `tk.Label` ambar por cada aviso, con `wraplength=980`. Medido
        # sobre un caso con los cinco: **5 etiquetas, la peor de 5 lineas, 310 px**,
        # y la region central donde estan los campos que hay que corregir reducida
        # a 241 px de una ventana de 720. Palabras del dueno: «las letras en
        # amarillo toman todo el espacio, es demasiado texto».
        self._marco_de_avisos = ttk.Frame(self)
        self._marco_de_avisos.grid(row=1, column=0, sticky="ew", pady=(8, 0))
        self.franja = FranjaDeAvisos(
            self._marco_de_avisos, al_cambiar=self._al_cambiar_los_avisos
        )
        self.franja.pack(fill="x")

    def _pintar_avisos(self):
        """Pone en la franja los avisos de ahora mismo, cada uno en una linea.

        Se vuelven a poner enteros en cada recuento y no se van acumulando: el
        aviso del mes cruzado tiene que **desaparecer en el acto** cuando se
        corrige la fecha, sin esperar a Guardar. Un aviso que sobrevive a su causa
        ensena a ignorar los avisos.
        """
        self.franja.poner(
            Aviso(linea, cuantos, al_ver=self._ver_el_detalle(detalle))
            for linea, cuantos, detalle in agrupados_por_linea(self._avisos_de_la_cabecera())
        )

    def _ver_el_detalle(self, detalle):
        """La orden de «ver cuáles»: ensena el texto entero de ESE aviso."""
        return lambda: dialogos.informar("Sobre este caso", detalle, parent=self)

    def _al_cambiar_los_avisos(self, cuantos, esta_cerrada):
        """Dibuja la campana cuando Miguel cierra la franja. Nada se pierde."""
        if esta_cerrada and cuantos:
            self._campana.configure(text=f"🔔 {cuantos}")
            self._campana.grid(row=0, column=2, padx=4)
        else:
            self._campana.grid_remove()

    def _reabrir_los_avisos(self):
        self.franja.reabrir()

    def _avisos_de_la_cabecera(self):
        """Lo que hay que decir arriba, partido en linea y detalle.

        La linea es lo unico que ocupa sitio en la pantalla; el detalle —el texto
        largo de siempre, palabra por palabra— se lee al pulsar «ver cuáles». Las
        frases viven en `interfaz/avisos_de_correccion.py`, que no dibuja nada: es
        lo que permite leerlas en una prueba sin abrir una ventana.
        """
        return avisos_de_la_cabecera(
            self.caso,
            self._campos_del_caso["fecha_viaje"].valor() if self._ya_hay_campos() else None,
            self._renglones_de_la_importacion(),
            self.paginas.motivo_del_fallo,
        )

    def _textos_de_aviso(self):
        """El texto ENTERO de cada aviso: lo que se lee al pedir el detalle.

        Sigue existiendo —y sigue diciendo lo mismo que antes del 2026-09-04—
        porque acortar la cabecera no puede significar perder una palabra: las
        pruebas de los pases anteriores comprueban aqui que el aviso nombra las
        anclas que se perdieron, que dice «SIN número de caso» y que repite el
        motivo por el que el escaneo no se ve.
        """
        return [aviso.detalle for aviso in self._avisos_de_la_cabecera()]

    def _renglones_de_la_importacion(self):
        """Los renglones de `documentos_ilegibles` de este caso, para la cabecera.

        ⚠️ **Esto es el arreglo del hallazgo ALTO de QA del 2026-09-03.** Un grupo
        español de dos hojas leyó 2026-08-25 en una y 2026-08-26 en la otra; el
        caso se quedó —bien— con la de la hoja que lo abrió, y esta pantalla
        enseñaba **una sola fecha sin decir que otra hoja decía otra cosa**. Mandar
        a alguien al templo el día equivocado es el daño que este programa existe
        para evitar, y para verlo hay que saber que hubo dos lecturas.

        Se leen de la base y no se pasan desde la importación a propósito: quien
        abre este caso tres días después no vio el resumen de aquella importación,
        y un aviso que solo existió mientras el cuadro estaba abierto no es un
        aviso. Por eso el renglón se guarda, y por eso se lee aquí.
        """
        return renglones_del_caso(self.conexion, self.caso_id)

    def _ya_hay_campos(self):
        return hasattr(self, "_campos_del_caso")

    def _falta_el_numero_de_caso(self):
        """Si este caso entro sin numero y todavia nadie se lo ha puesto.

        Se pregunta a la fila leida de la base y no al campo de la pantalla: el
        campo puede tener ya algo tecleado y sin guardar, y lo que decide si se
        puede escribir el numero es lo que hay guardado, no lo que hay a medias.
        """
        return self.caso["numero_caso"] is None

    # ---- region central -------------------------------------------------

    def _construir_region_central(self):
        """La pantalla PARTIDA: el documento entero a la izquierda, los campos a la derecha.

        Es lo que el dueno pidio dos veces con estas palabras: «necesito ver el pdf
        completo a un lado para confirmar que lo que se leyo es correcto a lo que
        esta en el documento», y «al dar clic, de un lado el documento original y
        del otro lado la informacion que yo necesito».

        **El documento sustituye a la tira; no conviven, y es aritmetica.** La
        ventana mide 1100 px de minimo (`interfaz/aplicacion.ANCHO_MINIMO`), la
        tira 380 (`extraccion/recorte.ANCHO_DE_LA_TIRA_PX`), la columna de campos
        necesita 478 y el visor 450 para ser util: **1308 px, 208 mas de los que
        hay**. Con el visor a 560 y el divisor a 6 quedan 532 para los campos, que
        necesitan 478: sobran 54. Por eso los campos se construyen con
        `con_tira=False`.

        Se parte con `ttk.PanedWindow` y no con dos marcos fijos para que el
        divisor se pueda arrastrar: 560 px es el reparto de diseno, no una verdad
        para todas las hojas, y quien mira un escaneo torcido quiere mas papel.
        """
        partida = ttk.PanedWindow(self, orient="horizontal")
        partida.grid(row=2, column=0, sticky="nsew", pady=8)
        self._partida = partida
        self._visor = VisorDelDocumento(
            partida,
            self.paginas,
            pagina_inicial=self.caso["pagina_pdf"] or 1,
            # ⚠️ **El total de hojas NO se cuenta aquí, y ese cambio es el que hace
            # que esta pantalla abra rápido.** Medido con `cProfile` sobre un caso
            # de 6 hojas con 300 casos en la base: `_hojas_del_pdf()` costaba
            # **4,52 s**, y de esos **4,46 s eran importar `pypdfium2` por primera
            # vez** (`_library_scope`, `raw`, y 2,79 s de `platform.system()`).
            # Para pintar el denominador de «hoja 1 / 6».
            #
            # El caso se pinta con el contador a medias —«hoja 1»— y el
            # denominador llega en cuanto la ventana ya responde. Nada de lo que se
            # teclea depende de él.
            total_de_hojas=None,
        )
        # El identificador se guarda para poder CANCELARLO al salir de la pantalla,
        # igual que `interfaz/inicio.py` hace con su vigilancia del cambio de dia.
        # Sin cancelarlo, este temporizador dispara sobre una pantalla ya destruida
        # y Tk contesta «invalid command name ..._contar_las_hojas_cuando_ya_se_ve»
        # por consola. `winfo_exists()` protege del `TclError`, pero no evita que
        # quede un temporizador vivo por cada caso que se abre y se cierra.
        self._conteo_de_hojas = self.after(
            MILISEGUNDOS_ANTES_DE_CONTAR_LAS_HOJAS, self._contar_las_hojas_cuando_ya_se_ve
        )
        # `weight=0` en el visor y `weight=1` en los campos: al agrandar la ventana
        # crece la columna de campos, que es donde se teclea. Un visor que se come
        # todo el ancho nuevo dejaria los campos igual de apretados en una pantalla
        # grande que en el minimo de 1100.
        partida.add(self._visor, weight=0)

        marco = ttk.Frame(partida)
        partida.add(marco, weight=1)
        marco.rowconfigure(0, weight=1)
        marco.columnconfigure(0, weight=1)

        lienzo = tk.Canvas(marco, highlightthickness=0, takefocus=0, background=FONDO)
        barra = ttk.Scrollbar(marco, orient="vertical", command=lienzo.yview)
        lienzo.configure(yscrollcommand=barra.set)
        lienzo.grid(row=0, column=0, sticky="nsew")
        barra.grid(row=0, column=1, sticky="ns")

        dentro = ttk.Frame(lienzo, padding=(0, 0, 12, 0))
        ventana = lienzo.create_window((0, 0), window=dentro, anchor="nw")
        dentro.bind(
            "<Configure>", lambda evento: lienzo.configure(scrollregion=lienzo.bbox("all"))
        )
        lienzo.bind(
            "<Configure>", lambda evento: lienzo.itemconfigure(ventana, width=evento.width)
        )
        # ⚠️ **El gesto de dos dedos del trackpad NO es `<MouseWheel>` en Tk 9**, y
        # por eso esta columna no bajaba con el trackpad del portatil del dueño —lo
        # midió él el 2026-09-03: «le doy para abajo con el trackpad de mi laptop y
        # no baja»—. Tk 9 lo entrega como `<TouchpadScroll>`, y no trae binding por
        # defecto para `Canvas`.
        #
        # **Hay UN solo ayudante de rueda y trackpad en todo el programa**, y es
        # `interfaz/desplazamiento.py`. Aquí había dos `bind_all` escritos a mano
        # que nadie soltaba nunca, y `bind_all` REEMPLAZA en vez de sumar: los de
        # esta pantalla sobrevivían a la pantalla, la de inicio los pisaba al
        # entrar el ratón en su cola, y al salir el ratón de ahí la ventana entera
        # se quedaba sin nadie oyendo la rueda. Un solo ayudante, que se suelta en
        # `soltar_atajos()`, quita las tres cosas de golpe.
        self._desplazador = atar_desplazamiento(lienzo)
        dentro.columnconfigure(0, weight=1)

        self._lienzo = lienzo
        self._dentro = dentro
        self._construir_campos_del_caso(dentro)
        self._construir_personas(dentro)
        # El seguimiento va DEBAJO de las personas y no en una pestaña: lo que un
        # compañero propuso sobre una persona no se entiende sin ver a esa persona,
        # y un contacto que hay que ir a buscar a otro sitio es un contacto que no
        # se mira. Mismo criterio que la pantalla de inicio (FASE 5).
        self._seguimiento = PanelDeSeguimiento(
            dentro,
            self.conexion,
            self.caso,
            al_cambiar=self._recontar,
            carpeta_de_datos=self._carpeta_de_datos,
        )
        self._seguimiento.grid(sticky="ew", pady=(6, 0))
        self._seguir_al_foco(dentro)
        self._colocar_el_divisor()
        # Arranca enfocando el primer campo del caso para que el visor abra sobre
        # una banda y no sobre una hoja entera ilegible. Sin esto, la pantalla se
        # abre con el documento a tamano de sello hasta que alguien pulsa Tab.
        self._enfocar_el_visor_en(self._campos_del_caso.get("fecha_viaje"))

    def _firmar_el_estado_si_es_de_los_dos_que_marcan(self):
        """Si el desplegable dejó «completa» o «no completa», se firma con Miguel.

        ⚠️ **La decisión, dicha porque el pase pedía elegir entre dos.** El
        desplegable de esta pantalla podía escribir `completa` sin que constara
        quién: el estado quedaba puesto y las tres columnas de la marca en blanco,
        o sea un documento dado por completo que nadie firmó. Las dos salidas eran
        quitar esos dos valores del desplegable, o firmarlos. **Se firman.**

        El motivo: este desplegable ES la mano de Miguel, que es exactamente quien
        el dueño dice que marca —«el documento que ellos llenan de Excel es el que
        marca, y lo que Miguel no comparta lo corrige él»—. Quitarle los dos valores
        le quitaría la forma de corregir desde el caso que está mirando, y lo
        obligaría a salir a otra pantalla para decir lo que ya sabe.

        Se firma con `marcar_a_mano`, que es **la misma función** que usan los dos
        botones de la tarjeta de «Revisar»: escribe quién, cuándo y de dónde. Dos
        maneras de marcar a mano acabarían escribiendo dos verdades distintas sobre
        el mismo documento.
        """
        estado = self._seguimiento.valor_del_estado()
        if estado not in (COMPLETA, NO_COMPLETA):
            return
        marcar_a_mano(
            self.conexion, self.caso_id, estado, _companero_que_verifica(self.conexion)
        )

    def _contar_las_hojas_cuando_ya_se_ve(self):
        """Cuenta las hojas del PDF **despues** de que la pantalla este pintada.

        Lo que cuesta —abrir `pypdfium2` la primera vez, 4,46 s medidos— deja de
        estar entre el clic y el caso en pantalla, y pasa a estar detras.

        ⚠️ Va con `after` de un milisegundo y **no con `after_idle`**, aunque
        «cuando no quede nada por dibujar» suene mejor. `after_idle` encola en la
        misma cola que los redibujados, asi que `update_idletasks()` —el paso que
        pinta— lo ejecuta tambien, y entonces el coste seguiria dentro del dibujado
        aunque el usuario viera la ventana antes. Con un temporizador queda fuera de
        esa cola, y eso se puede **medir**: es la diferencia entre creerlo y saberlo.

        Se comprueba que la ventana siga viva: entre el `after_idle` y esta llamada
        el usuario puede haber vuelto al inicio, y escribir en un widget destruido
        levanta un `TclError` que nadie ve venir.
        """
        # Ya disparó: no queda nada que cancelar, y dejar el identificador puesto
        # haría que `soltar_atajos()` intentara cancelar un temporizador muerto.
        self._conteo_de_hojas = None
        if not self.winfo_exists():
            return
        try:
            self._visor.fijar_el_total_de_hojas(self._hojas_del_pdf())
        except tk.TclError:
            # La ventana se cerro mientras se contaba. No hay nada que actualizar y
            # tampoco nada que avisar: el caso se cerro, que es lo que se pidio.
            pass

    def _hojas_del_pdf(self):
        """Cuantas hojas tiene el PDF de este caso, o None si no se puede saber.

        None no es un fallo y se dibuja como tal: el contador del visor dice «hoja
        2» en vez de «hoja 2 / 6» y los botones de hoja siguen andando. El PDF se
        mueve, se renombra y a veces el disco no esta puesto, y ninguna de esas
        cosas puede impedir corregir el caso.
        """
        ruta = self.caso["ruta_pdf"]
        if not ruta or not os.path.isfile(ruta):
            return None
        try:
            from extraccion.rasterizado import contar_paginas

            return contar_paginas(ruta)
        except Exception:
            # Se atrapa ancho y NO se silencia: lo que se pierde es el denominador
            # del contador de hojas, y el visor ya lo dibuja sin el.
            return None

    def _colocar_el_divisor(self):
        """Deja el visor en su ancho de diseno la primera vez que se dibuja.

        Se hace en `<Map>` y no en el constructor porque `sashpos` sobre un panel
        que Tk todavia no ha colocado no tiene contra que medir y se ignora en
        silencio. Se desata a la primera para no volver a mover un divisor que
        alguien ya haya arrastrado.
        """

        def colocar(evento=None):
            self._partida.unbind("<Map>", atadura)
            self._colocar_el_divisor_ahora()

        atadura = self._partida.bind("<Map>", colocar, add="+")

    # ---- el foco arrastra la vista ---------------------------------------

    def _seguir_al_foco(self, widget):
        """Hace que la region central se mueva detras del control que toma el foco.

        Es lo que sostiene la promesa de esta pantalla: **se recorre sin tocar el
        raton**. Medido en la FASE 9 sin esto: 22 tabulaciones y la vista no se
        movio ni un pixel, asi que a partir de la tercera persona Miguel estaria
        tecleando sobre casillas que no ve. Un formulario de seis personas mide
        mas de 1 300 px y el hueco no llega a 500.

        Se ata control a control y NO con `bind_all`. `bind_all` sobrevive al
        cambio de pantalla y seguiria moviendo un lienzo que ya no esta dibujado,
        que es el mismo motivo por el que los atajos se atan al toplevel.

        ⚠️ **Solo se atan los controles que YA entran en el recorrido de Tab**, y
        eso no es una optimizacion: `tk::FocusOK` mete en el recorrido a cualquier
        widget que tenga una atadura de foco, asi que atar `<FocusIn>` a las
        etiquetas y a los marcos las convertia en paradas de Tab. Medido en esta
        maquina con seis personas: el recorrido pasaba de **54 paradas a 279**, y
        el minuto por formulario se iba al quintuple. Se distingue por
        `takefocus`: los ttk que
        toman el foco lo traen resuelto —`ttk::takefocus`—, la casilla de
        ordenanza lo trae en 1, y las etiquetas, los marcos y la tira del escaneo
        lo traen vacio o en 0.
        """
        if str(widget.cget("takefocus")) not in ("", "0"):
            widget.bind("<FocusIn>", self._traer_a_la_vista, add="+")
        for hijo in widget.winfo_children():
            self._seguir_al_foco(hijo)

    def _campo_del_widget(self, widget):
        """Que `CampoCorregible` contiene ese control, subiendo por sus padres.

        Se sube por la cadena de padres y NO se comparan nombres de ruta de Tk, por
        el mismo motivo medido que `_bloque_del_widget`: Tk numera los hermanos y
        el nombre del primero es prefijo del de los demas.
        """
        while widget is not None:
            if isinstance(widget, CampoCorregible):
                return widget
            widget = getattr(widget, "master", None)
        return None

    def _enfocar_el_visor_en(self, campo):
        """Manda al visor a la hoja y la banda de ese campo. Sin mover ningun foco."""
        if campo is None:
            return
        self._visor.enfocar(
            campo.pagina_pdf or self.caso["pagina_pdf"], campo.banda, campo.etiqueta
        )

    def _seguir_con_el_visor(self, widget):
        """Mueve el resalte del documento al campo que acaba de recibir el foco.

        Es el criterio 2 del pase: tabulando por los campos, el resalte se mueve
        sobre el documento y la banda enfocada queda a la vista.

        Con el foco en una casilla de ordenanza no hay banda que resaltar —las
        ordenanzas no existen en ninguna capa del PDF de este dueno, y por eso
        llegan siempre sin leer—, pero **si se sabe de que hoja es esa persona**, y
        ensenar su hoja sigue siendo lo util: es donde hay que mirar para decidir
        si esa ordenanza esta marcada en el papel.
        """
        campo = self._campo_del_widget(widget)
        if campo is not None:
            self._enfocar_el_visor_en(campo)
            return
        indice = self._bloque_del_widget(widget)
        pagina = (
            self._bloques[indice].pagina_pdf if indice is not None else None
        ) or self.caso["pagina_pdf"]
        self._visor.enfocar(pagina, None)

    def _traer_a_la_vista(self, evento):
        """Desplaza el lienzo lo justo para que el control del evento se vea entero.

        Si ya se ve, no se mueve nada: una vista que salta en cada Tab marea y
        hace perder de vista el campo anterior, que es contra lo que se compara.

        Y **de paso mueve el resalte del documento**, que es lo que ata las dos
        mitades de la pantalla partida. Va aqui y no en una atadura propia porque
        `tk::FocusOK` mete en el recorrido de Tab a cualquier widget con una
        atadura de foco: una segunda atadura sobre los mismos controles no anade
        paradas, pero atarla a otros —etiquetas, marcos— si, y eso ya costo medido
        pasar de 54 paradas a 279.
        """
        self._seguir_con_el_visor(evento.widget)
        lienzo, dentro = self._lienzo, self._dentro
        alto_visible = lienzo.winfo_height()
        alto_total = dentro.winfo_height()
        # Mientras la ventana no este dibujada, Tk devuelve 1 en vez de un alto:
        # no se sabe donde esta nada y mover el lienzo seria mover a ciegas.
        if alto_visible <= 1 or alto_total <= alto_visible:
            return
        arriba = evento.widget.winfo_rooty() - dentro.winfo_rooty()
        abajo = arriba + evento.widget.winfo_height()
        vista_arriba = lienzo.canvasy(0)

        if arriba - MARGEN_AL_SEGUIR_EL_FOCO < vista_arriba:
            nuevo = arriba - MARGEN_AL_SEGUIR_EL_FOCO
        elif abajo + MARGEN_AL_SEGUIR_EL_FOCO > vista_arriba + alto_visible:
            nuevo = abajo + MARGEN_AL_SEGUIR_EL_FOCO - alto_visible
        else:
            return
        tope = alto_total - alto_visible
        lienzo.yview_moveto(min(max(nuevo, 0), tope) / alto_total)

    def _construir_campos_del_caso(self, padre):
        """Los cuatro campos del caso, cada uno con su tira del escaneo."""
        ttk.Label(padre, text="Datos del caso", style="Seccion.TLabel").grid(
            sticky="w", pady=(0, 4)
        )
        procedencia = procedencia_por_campo(self.conexion, "casos", self.caso_id)
        self._campos_del_caso = {}
        for nombre, etiqueta, del_papel, validar, solo_lectura in CAMPOS_DEL_CASO:
            campo = CampoCorregible(
                padre,
                nombre,
                etiqueta,
                etiqueta_del_papel=del_papel,
                validar=validar,
                solo_lectura=solo_lectura and not self._falta_el_numero_de_caso(),
                al_cambiar=self._recontar,
                # Sin tira: el documento entero de la izquierda ocupa su sitio.
                con_tira=False,
                pagina_pdf=self.caso["pagina_pdf"],
            )
            campo.grid(sticky="ew")
            campo.cargar(self.caso[nombre], procedencia.get(nombre))
            self._campos_del_caso[nombre] = campo

    def _construir_personas(self, padre):
        """Un bloque por persona, con el denominador escrito en el titulo."""
        personas = leer_personas_del_caso(self.conexion, self.caso_id)
        ttk.Label(
            padre,
            text=f"Personas — {len(personas)} filas leídas de 6",
            style="Seccion.TLabel",
        ).grid(sticky="w", pady=(14, 4))

        self._bloques = []
        for persona in personas:
            # La hoja de ESTA persona, no la del caso. Una persona guardada antes
            # de la version 4 del esquema no la tiene, y entonces se cae a la del
            # caso: es lo que hacia el programa entero hasta ahora, y sigue siendo
            # lo mejor disponible cuando no hay dato.
            bloque = BloqueDePersona(
                padre,
                persona,
                self.paginas.pagina(persona["pagina_pdf"] or self.caso["pagina_pdf"]),
                procedencia_por_campo(self.conexion, "personas", persona["id"]),
                self._recontar,
                con_tira=False,
                # Lo que un companero contesto sobre ESTA persona, o None si
                # nadie contesto nada. Se lee aqui y no dentro del bloque para
                # que `interfaz/persona.py` siga sin consultar la base al
                # dibujarse: recibe un diccionario y lo pinta.
                propuesta=propuesta_vigente(self.conexion, persona["id"]),
            )
            bloque.grid(sticky="ew", pady=6)
            self._bloques.append(bloque)

        if not personas:
            ttk.Label(
                padre,
                text=(
                    "Este caso no tiene ninguna persona guardada. El lector no "
                    "encontró ninguna fila legible en esta página."
                ),
                style="Secundario.TLabel",
            ).grid(sticky="w")

    # ---- pie ------------------------------------------------------------

    def _construir_pie(self):
        """El contador, el motivo del bloqueo, y los tres botones."""
        pie = ttk.Frame(self)
        pie.grid(row=3, column=0, sticky="ew", pady=(8, 0))
        pie.columnconfigure(0, weight=1)

        self._contador = ttk.Label(pie, font=LETRA_SECCION)
        self._contador.grid(row=0, column=0, sticky="w")
        self._motivo_del_bloqueo = ttk.Label(pie, style="Secundario.TLabel", wraplength=600)
        self._motivo_del_bloqueo.grid(row=1, column=0, sticky="w")
        # ⚠️ **Se construye AQUI y vacio, no al guardar.** Es lo que hace que el pie
        # mida siempre lo mismo y el boton no cambie de sitio al pulsarlo. Ver
        # `interfaz/acuse_de_guardado.py`, que trae la medicion de por que existe.
        self._acuse = AcuseDeGuardado(pie)
        self._acuse.grid(row=2, column=0, sticky="w")

        botones = ttk.Frame(pie)
        botones.grid(row=0, column=1, rowspan=3, sticky="e")
        # Va el PRIMERO de los tres, a la izquierda de «Todo correcto», porque ese
        # es el orden en que se usan: en los formularios del dueño las seis
        # ordenanzas de cada persona llegan siempre sin leer —no existen en
        # ninguna capa del PDF—, así que resolverlas es el paso de antes de poder
        # confirmar, no una excepción.
        self._boton_de_las_casillas = ttk.Button(
            botones,
            text="Ninguna ordenanza marcada  Ctrl+0",
            command=self.resolver_las_casillas_sin_leer,
        )
        self._boton_de_las_casillas.grid(row=0, column=0, padx=4)
        self._boton_verificar = ttk.Button(
            botones, text="Todo correcto  Ctrl+Intro", command=self.verificar
        )
        self._boton_verificar.grid(row=0, column=1, padx=4)
        ttk.Button(botones, text="Guardar  Ctrl+G", command=self.guardar).grid(
            row=0, column=2, padx=4
        )
        # ⚠️ **Sin atajo de teclado, y a proposito**, al reves que los otros tres.
        # «Todo correcto» tiene Ctrl+Intro porque se usa una vez por caso y es el
        # camino del minuto; deshacer se usa cuando algo salio mal, y una tecla que
        # retira 36 firmas no tiene que estar a un dedo de distancia de la que las
        # pone. Se pulsa con el raton, mirando.
        #
        # El rotulo nombra al otro boton —«Deshacer "Todo correcto"»— y no dice
        # «desverificar»: lo que Miguel recuerda haber hecho es haber pulsado ese
        # boton, no el nombre de la columna que se movio.
        self._boton_de_deshacer = ttk.Button(
            botones,
            text="Deshacer «Todo correcto»",
            command=self.deshacer_la_verificacion,
        )
        self._boton_de_deshacer.grid(row=0, column=3, padx=4)
        if self._al_archivar is not None:
            # Va el ultimo y sin atajo de teclado, al reves que los otros dos: es
            # la accion que saca el caso de todas las listas, y no tiene que estar
            # a un dedo de distancia de «Guardar».
            ttk.Button(botones, text="Archivar caso…", command=self._archivar).grid(
                row=0, column=4, padx=4
            )

    # ---- teclado --------------------------------------------------------

    # Los atajos se atan al TOPLEVEL, no con `bind_all`. Con `bind_all` seguirian
    # vivos despues de cambiar de pantalla —Intro saltaria a una persona que ya no
    # esta dibujada— y no habria forma limpia de soltarlos. Atados al toplevel, un
    # solo `soltar_atajos()` los quita todos cuando esta pantalla se va.
    # ⚠️ **El mockup pide `Ctrl+0` para «ajustar a la banda» y esa tecla ya esta
    # ocupada**: es «Ninguna ordenanza marcada», que entro antes y que este pase
    # prohibe tocar. Medido: `<Control-Key-0>` esta atado en la linea de abajo
    # desde la FASE 7. Asi que el par de zoom queda **Ctrl+1 pagina** —tal cual lo
    # pide el mockup— y **Ctrl+2 banda**, que es la desviacion mas pequena posible.
    # Va al informe como decision del dueno: si prefiere el mockup literal, lo que
    # se mueve es el atajo de las ordenanzas, no este.
    _ATAJOS = ("<F2>", "<Control-g>", "<Control-G>", "<Control-Return>",
               "<Control-Home>", "<Return>", "<Control-Key-0>",
               "<Control-Key-1>", "<Control-Key-2>", "<Control-Key-3>",
               "<Control-plus>",
               "<Control-equal>", "<Control-minus>", "<Control-Prior>",
               "<Control-Next>", "<F4>",
               "<Control-Shift-Up>", "<Control-Shift-Down>",
               "<Control-Shift-Left>", "<Control-Shift-Right>")

    def atar_atajos(self):
        """El recorrido de teclado que hace posible el minuto por formulario.

        Un evento de tecla recorre las etiquetas del widget con el foco, su clase,
        el toplevel y luego «all». Por eso atarlos al toplevel funciona aunque el
        foco este dentro de un `ttk.Entry`, y no hace falta `bind_all`.

        **Los del visor se atan aqui a proposito, y no al lienzo.** El lienzo lleva
        `takefocus=0` y nunca tiene el foco: atados a el no se dispararian nunca.
        Atados al toplevel funcionan con el cursor dentro de un campo de texto, que
        es la unica forma de mirar el papel mientras se teclea. Todos devuelven
        «break» para que la tecla no siga su camino y acabe haciendo otra cosa.
        """
        raiz = self.winfo_toplevel()
        raiz.bind("<F2>", lambda evento: self.abrir_pdf())
        raiz.bind("<Control-g>", lambda evento: self.guardar())
        raiz.bind("<Control-G>", lambda evento: self.guardar())
        raiz.bind("<Control-Return>", lambda evento: self.verificar())
        raiz.bind("<Control-Home>", lambda evento: self._volver())
        raiz.bind("<Return>", self._saltar_a_la_persona_siguiente)
        # Cero de «ninguna». Va con Control como los otros dos para que no se
        # dispare tecleando un cero dentro de un campo.
        raiz.bind("<Control-Key-0>", lambda evento: self.resolver_las_casillas_sin_leer())
        self._atajos_del_visor(raiz)

    @staticmethod
    def _sin_seguir_camino(hacer):
        """Envuelve una orden del visor en un manejador que corta la tecla.

        Devolver «break» es lo que impide que, por ejemplo, `Ctrl+Mayús+Abajo` haga
        ademas lo suyo dentro del campo de texto que tiene el foco: seleccionar
        hasta el final. Escrito como funcion con nombre y no como una lambda con un
        indice al final, que era lo mismo y no se leia.
        """

        def manejar(evento=None):
            hacer()
            return "break"

        return manejar

    def _atajos_del_visor(self, raiz):
        """Zoom, desplazamiento y paso de hoja. Ninguno mueve el foco del campo."""
        for atajo, orden in (
            ("<Control-Key-1>", self._visor.ajustar_a_la_pagina),
            ("<Control-Key-2>", self._visor.ajustar_a_la_banda),
            # ⚠️ Ctrl+3 entro el 2026-09-03 con el modo de arranque nuevo, y no es
            # una tecla de mas: desde ese dia el visor abre con la hoja al ancho del
            # panel, y sin este atajo ese modo seria el unico al que no se puede
            # volver despues de arrastrar o de ampliar. Un modo por defecto
            # irrecuperable es peor que no tenerlo.
            ("<Control-Key-3>", self._visor.ajustar_al_ancho),
            # Las tres del signo mas: `Ctrl+=` es la que se teclea de verdad en un
            # teclado espanol sin pulsar Mayus, y sin ella «ampliar» pide una
            # combinacion de tres dedos.
            ("<Control-plus>", self._visor.acercar),
            ("<Control-equal>", self._visor.acercar),
            ("<Control-minus>", self._visor.alejar),
            ("<Control-Prior>", self._visor.hoja_anterior),
            ("<Control-Next>", self._visor.hoja_siguiente),
            ("<F4>", self._alternar_el_visor),
        ):
            raiz.bind(atajo, self._sin_seguir_camino(orden))
        # Ctrl+Mayus+flechas y no las flechas a secas: dentro de un campo de texto
        # las flechas mueven el cursor, y quitarselas seria romper el tecleo para
        # ganar un desplazamiento.
        for atajo, cuartos in (
            ("<Control-Shift-Up>", (0, -1)),
            ("<Control-Shift-Down>", (0, 1)),
            ("<Control-Shift-Left>", (-1, 0)),
            ("<Control-Shift-Right>", (1, 0)),
        ):
            raiz.bind(
                atajo,
                self._sin_seguir_camino(
                    lambda paso=cuartos: self._visor.desplazar(*paso)
                ),
            )

    def _alternar_el_visor(self):
        """F4 esconde y devuelve el documento. Colapsado, los campos ocupan todo.

        Existe para el caso en que toca teclear mucho —una hoja que no se pudo
        leer— y el papel ya se ha mirado: en un portatil a 1100 px, 560 de esos son
        la mitad de la ventana dedicada a algo que en ese momento estorba.
        """
        # `panes()` devuelve NOMBRES DE RUTA de Tk, no widgets: comparar el objeto
        # contra esa lista da siempre falso y F4 solo sabria insertar.
        if str(self._visor) in [str(panel) for panel in self._partida.panes()]:
            self._partida.forget(self._visor)
            return
        self._partida.insert(0, self._visor, weight=0)
        self._colocar_el_divisor_ahora()

    def _colocar_el_divisor_ahora(self):
        """Deja el visor en su ancho de diseno. Se traga el fallo de una ventana estrecha.

        Una ventana mas estrecha que el reparto no es un error que valga la pena
        levantar: Tk pone el divisor donde puede y la pantalla se dibuja igual.
        """
        try:
            self._partida.sashpos(0, ANCHO_POR_DEFECTO_DEL_VISOR_PX)
        except tk.TclError:
            pass

    def soltar_atajos(self):
        """Quita los atajos y el temporizador de esta pantalla. Se llama al cambiar.

        El temporizador se cancela **aquí** y no en un `<Destroy>`, por la misma
        razón que en `interfaz/inicio.py`: la ventana llama a este método justo
        antes de quitar la pantalla, así que es el único sitio donde se sabe con
        certeza que la pantalla se va.
        """
        raiz = self.winfo_toplevel()
        for atajo in self._ATAJOS:
            raiz.unbind(atajo)
        if self._conteo_de_hojas is not None:
            self.after_cancel(self._conteo_de_hojas)
            self._conteo_de_hojas = None
        self._desplazador.soltar()

    def _bloque_del_widget(self, widget):
        """En que bloque de persona vive ese control, subiendo por sus padres.

        Se sube por la cadena de padres y NO se comparan los nombres de ruta de Tk
        con `startswith`. Medido: Tk numera los hermanos «!labelframe»,
        «!labelframe2», «!labelframe3», y el nombre del primero es PREFIJO del de
        los otros dos. Comparando texto, todo control de la persona 3 salia como
        si fuera de la persona 1, e Intro saltaba de la 3 a la 2 en vez de al
        boton de verificar.
        """
        while widget is not None:
            for indice, bloque in enumerate(self._bloques):
                if widget is bloque:
                    return indice
            widget = getattr(widget, "master", None)
        return None

    def _saltar_a_la_persona_siguiente(self, evento=None):
        """Intro confirma el bloque actual y salta al primer campo del siguiente.

        En la ultima persona salta a «Verificar caso», que es donde termina el
        recorrido. Es el camino del minuto: tres Intro recorren tres personas sin
        tocar el raton.
        """
        indice = self._bloque_del_widget(self.focus_get())
        if indice is None:
            if self._bloques:
                self._bloques[0].primer_control().focus_set()
            return "break"
        if indice + 1 < len(self._bloques):
            self._bloques[indice + 1].primer_control().focus_set()
        else:
            self._boton_verificar.focus_set()
        return "break"

    # ---- acciones -------------------------------------------------------

    def _pagina_en_foco(self):
        """La hoja del PDF que hay que anunciar: la de la persona enfocada.

        Un caso de grupo reparte sus personas en varias hojas, asi que «la pagina
        de este caso» ya no es una respuesta util: la que hace falta es la de lo
        que se esta corrigiendo AHORA. Con el foco fuera de los bloques —en los
        cuatro campos del caso, o en los botones— manda la del caso, que es la
        hoja que lo abrio.
        """
        indice = self._bloque_del_widget(self.focus_get())
        if indice is None:
            return self.caso["pagina_pdf"]
        return self._bloques[indice].pagina_pdf or self.caso["pagina_pdf"]

    def _aviso_de_la_pagina(self, pagina):
        """Lo que hay que leer antes de abrir el visor, o None si no hace falta.

        Devuelve el texto en vez de ensenarlo para que se pueda comprobar sin
        abrir un cuadro de dialogo, que en una prueba se queda esperando a que
        alguien pulse.
        """
        if not pagina or pagina <= 1:
            return None
        return (
            f"Lo que está corrigiendo viene de la PÁGINA {pagina} del PDF.\n\n"
            "El visor se abre por la primera página: Windows no permite pedirle "
            "una página concreta sin depender de qué visor esté instalado. "
            f"Salte a la página {pagina} una vez abierto."
        )

    def abrir_pdf(self):
        """Abre el PDF en el visor del sistema y dice EN QUE PAGINA mirar.

        La pagina que se anuncia es la de la persona en la que esta el foco, no la
        del caso. Un formulario de grupo ocupa seis hojas —medido sobre un
        documento real—, y desde que las seis se unen en un solo caso, decir «la
        pagina del caso» mandaria a la hoja 1 a quien esta corrigiendo a alguien
        de la hoja 4.

        ⚠️ Lo que NO se pudo hacer, dicho y no tapado: `os.startfile` abre el
        archivo y **no acepta un numero de pagina**. Windows no ofrece ninguna
        forma de pedir una pagina que no dependa de que el visor instalado sea uno
        concreto. Asi que el numero de pagina se ENSENA para poder teclearlo en el
        visor, y no se finge que se salta solo. Fingirlo seria peor: se buscaria el
        formulario en la pagina 1.
        """
        ruta = self.caso["ruta_pdf"]
        if not ruta or not os.path.isfile(ruta):
            dialogos.advertir(
                "No se encuentra el PDF",
                f"Este caso dice venir de «{ruta or 'ningún archivo'}» y ahí no hay "
                "nada. Puede que el archivo se haya movido o renombrado.",
                parent=self,
            )
            return
        aviso = self._aviso_de_la_pagina(self._pagina_en_foco())
        if aviso is not None:
            dialogos.informar("Página del formulario", aviso, parent=self)
        os.startfile(ruta)

    def _asignar_el_numero_si_falta(self):
        """Escribe el numero de caso tecleado, si este caso habia entrado sin el.

        Va por `asignar_numero_de_caso` y no por `actualizar_datos_del_caso`, que
        sigue sin tocar `numero_caso` a proposito: esa puerta solo abre en un
        sentido —de «no se sabe» a un numero—, y un caso que ya lo tenia no se
        puede cambiar por aqui ni por descuido.

        Un campo vacio no escribe nada y no es un error: el caso se guarda igual y
        el aviso de arriba sigue diciendo lo que falta. Negarse a guardar el resto
        por no saber todavia el numero seria volver a perder el trabajo hecho, que
        es justo lo que se acaba de arreglar.
        """
        if not self._falta_el_numero_de_caso():
            return ()
        campo = self._campos_del_caso["numero_caso"]
        # Un numero mal tecleado tampoco tumba el guardado: se queda en la pantalla
        # en rojo y el pie lo nombra, como cualquier otro campo que no vale.
        if not campo.es_valido():
            return ()
        tecleado = campo.valor()
        if tecleado is None:
            return ()
        return tuple(asignar_numero_de_caso(self.conexion, self.caso_id, tecleado))

    def _aviso_de_las_firmas_retiradas(self, firmados_antes):
        """El aviso de cuantas firmas se cayeron al guardar, o nada si no cayo ninguna.

        **Por que hace falta decirlo con palabras.** `datos/repositorio.py` retira la
        firma de todo campo cuyo valor cambia —una firma dice «di por bueno ESTE
        valor», y con otro valor ya no habla de el—. Pero eso solo se ve en el
        contador del pie, que baja de 36 a 35 mientras Miguel esta mirando el campo
        que acaba de teclear. Sin esta frase, el caso vuelve a la lista de pendientes
        y no hay forma de saber por que.

        Se cuenta contra la base antes y despues, y no se pregunta a nadie cuantas
        retiro: es el mismo criterio que `_campos_verificados`, que consulta en vez
        de llevar la cuenta en memoria. Un aviso que dijera otra cosa que la base
        seria justo la mentira que hay que cazar.
        """
        firmados_ahora, _ = self._campos_verificados()
        retiradas = firmados_antes - firmados_ahora
        if retiradas <= 0:
            return []
        return [
            f"Se retiró la firma de {retiradas} campo"
            f"{'s' if retiradas != 1 else ''} porque su valor cambió. Una firma dice "
            "que usted dio por bueno ESE valor, así que al cambiarlo deja de valer. "
            "El caso vuelve a salir como pendiente hasta que lo confirme otra vez con "
            "«Todo correcto»."
        ]

    def _valores_del_caso_que_si_se_escriben(self):
        """Los tres campos del caso que valen, listos para el repositorio.

        **El que no vale no se pasa, y omitirlo es exactamente lo que
        `actualizar_datos_del_caso` entiende por «esto no se toca».** Es la mitad
        del arreglo del 2026-09-04: hasta ese dia se pasaban los tres siempre, el
        repositorio levantaba `ErrorDeValidacion` al primero malo, y con el se caia
        **el guardado de todo lo demas** —medido: con un MRN de tres digitos en la
        tercera de cuatro personas, la cuarta no se guardaba y salia un cuadro que
        decia «No se pudo guardar» sobre unos datos que si se habian guardado—.
        Justo lo que el docstring de `guardar` prometia que no pasaba.
        """
        return {
            nombre: campo.valor()
            for nombre, campo in self._campos_del_caso.items()
            if nombre in CAMPOS_DEL_CASO_QUE_SE_ESCRIBEN and campo.es_valido()
        }

    def _guardar_el_caso(self):
        """Escribe los campos del caso y las personas. Devuelve los avisos."""
        campos = self._campos_del_caso
        firmados_antes, _ = self._campos_verificados()
        avisos_del_numero = self._asignar_el_numero_si_falta()
        avisos = actualizar_datos_del_caso(
            self.conexion,
            self.caso_id,
            # El estado de la recomendación sale del panel de seguimiento, que es
            # donde se ve al lado de lo que propusieron los compañeros. Se escribe
            # aquí, con el resto del caso, para que no exista un segundo botón de
            # guardar que alguien se pueda dejar sin pulsar.
            estado_recomendacion=self._seguimiento.valor_del_estado(),
            **self._valores_del_caso_que_si_se_escriben(),
        )
        self._firmar_el_estado_si_es_de_los_dos_que_marcan()
        for nombre, campo in campos.items():
            if campo.cambio() and campo.es_valido():
                _anotar_correccion_manual(self.conexion, "casos", self.caso_id, nombre)
        for bloque in self._bloques:
            bloque.guardar(self.conexion)
        # Los avisos del numero van DELANTE: el del mes cruzado solo se puede
        # calcular cuando ya hay numero con el que comparar la fecha, y es el
        # primero que hay que leer cuando acaba de aparecer.
        return (
            list(avisos_del_numero)
            + list(avisos)
            + self._aviso_de_las_firmas_retiradas(firmados_antes)
        )

    def _campos_por_guardar(self):
        """Los que se van a escribir y los que no, para poder decirlo en el pie.

        Se cuenta ANTES de guardar y no despues: al volver de `guardar_y_regenerar`
        los campos ya se han vuelto a cargar y ninguno «cambio».

        **El estado de la recomendacion cuenta como un campo mas**, por el mismo
        motivo que entra en `_hay_cambios_sin_guardar`: se escribe con el caso y es
        lo que marca la hoja del companero. Sin esto, cambiarlo y guardar decia
        «sin cambios» sobre algo que si se acababa de escribir.
        """
        cambiados = [campo for campo in self._todos_los_campos() if campo.cambio()]
        guardados = sum(1 for campo in cambiados if campo.es_valido())
        if self._seguimiento.valor_del_estado() != self.caso["estado_recomendacion"]:
            guardados += 1
        return guardados, self._campos_que_no_valen()

    def guardar(self, evento=None):
        """Guarda el caso entero y regenera el Excel espejo. Siempre juntos.

        Un campo que no valida NO impide guardar: la regla es ensenar lo raro, no
        negarse a registrarlo. Lo que no se puede es verificarlo, y de eso avisa el
        pie. Si un MRN mal escrito tumbara el guardado, se perderia todo lo demas
        que se acaba de teclear (criterio 5 de la FASE 3).

        ⚠️ **Hasta el 2026-09-04 eso lo decia este docstring y no lo hacia el
        codigo.** Los valores se pasaban al repositorio validaran o no, el
        repositorio levantaba, y el `except` de abajo sacaba un cuadro que decia
        «No se pudo guardar» —mintiendo, porque la conexion va en
        autoconfirmacion y lo escrito antes del campo malo SI se habia guardado—.
        Ahora los que no valen no se pasan (`_valores_del_caso_que_si_se_escriben`
        y `BloqueDePersona.guardar`), y el cuadro queda solo para lo que ninguna
        pantalla puede prever.

        Y guarde lo que guarde, **lo dice en el pie**: un boton que hace su trabajo
        en silencio es, para quien lo mira, un boton roto.
        """
        guardados, sin_guardar = self._campos_por_guardar()
        try:
            resultado = guardar_y_regenerar(
                self.conexion,
                self._guardar_el_caso,
                carpeta_de_datos=self._carpeta_de_datos,
                avisar=lambda aviso: dialogos.advertir(
                    "El Excel espejo no se pudo actualizar", aviso, parent=self
                ),
            )
        except ErrorDeValidacion as causa:
            dialogos.avisar_de_un_error("No se pudo guardar", str(causa), parent=self)
            return False

        self.caso = leer_caso_por_id(self.conexion, self.caso_id)
        self._recontar()
        self._acuse.anunciar(guardados, [campo.etiqueta for campo in sin_guardar])
        for aviso in resultado.guardado:
            dialogos.advertir("Aviso sobre este caso", aviso, parent=self)
        return True

    # ---- las casillas que el lector no pudo leer -------------------------

    def _pregunta_de_las_casillas_sin_leer(self):
        """Lo que hay que leer antes de resolver las casillas de golpe.

        Devuelve el texto en vez de ensenarlo, como `_aviso_de_la_pagina`: asi se
        puede comprobar palabra por palabra sin abrir un cuadro que en una prueba
        se queda esperando a que alguien pulse.

        Dice **que** se va a hacer, **a cuantas**, **de cuantas personas** y —lo
        que mas importa— **que no es una lectura del papel**: lo dice el, mirando
        el formulario. Un «¿seguro?» a secas no es una confirmacion informada.
        """
        sin_leer = self._casillas_sin_leer()
        personas = sum(1 for bloque in self._bloques if bloque.casillas_sin_leer())
        return (
            f"En el caso {numero_de_caso_visible(self.caso)} quedan {sin_leer} "
            f"casilla{'s' if sin_leer != 1 else ''} de ordenanza que el lector NO "
            f"pudo leer, repartidas entre {personas} persona"
            f"{'s' if personas != 1 else ''}.\n\n"
            "Van a quedar todas en «no marcada», porque usted, mirando el papel, "
            "dice que en este documento esas ordenanzas no están marcadas. Las que "
            "usted ya haya marcado NO se tocan.\n\n"
            "Queda registrado como corrección suya, no como una lectura del "
            "formulario. Esto NO da el caso por verificado: eso sigue siendo el "
            "botón «Todo correcto».\n\n"
            "¿Sigue?"
        )

    def resolver_las_casillas_sin_leer(self, evento=None):
        """Deja en «no marcada» todas las casillas que siguen sin leer.

        **Por que existe.** Los formularios del dueno llegan con las seis
        ordenanzas de cada persona sin leer —la informacion no esta en ninguna
        capa del PDF: 36 casillas con tinta 0.0000 y 45 botones en `/Off`—. Con
        cuatro personas son 24, y resolverlas de una en una cuesta **48
        pulsaciones**, medido: la barra entra por «marcada», asi que dejar una en
        «no» partiendo de «no leida» son dos.

        **Por que no rompe la regla permanente 5.** No marca nada como verificado:
        deja el dato puesto, con `origen='manual'` en `procedencia_campo`, y el
        caso sigue sin verificar hasta que Miguel pulse «Todo correcto». Son dos
        actos distintos y se piden por separado a proposito.
        """
        if not self._casillas_sin_leer():
            dialogos.informar(
                "No queda ninguna casilla sin leer",
                "Todas las casillas de ordenanza de este caso ya están resueltas: "
                "o el lector las leyó, o usted las resolvió. No hay nada que hacer "
                "aquí.",
                parent=self,
            )
            return
        if not dialogos.preguntar_si_o_no(
            "Ninguna ordenanza marcada",
            self._pregunta_de_las_casillas_sin_leer(),
            parent=self,
        ):
            return
        movidas = sum(
            bloque.resolver_las_sin_leer_como_no_marcadas() for bloque in self._bloques
        )
        if not self.guardar():
            return
        self._recontar()
        dialogos.informar(
            "Casillas resueltas",
            f"{movidas} casilla{'s' if movidas != 1 else ''} quedan en «no marcada», "
            f"anotadas como corrección de {NOMBRE_DEL_COMPANERO_POR_DEFECTO}. El caso "
            "todavía NO está verificado.",
            parent=self,
        )

    # ---- todo correcto ---------------------------------------------------

    def _campos_que_se_confirman(self):
        """Todo lo pintado en esta pantalla que TIENE procedencia Y TIENE valor.

        Se listan los campos de la pantalla y se cruzan con lo que hay guardado, en
        vez de firmar todas las filas de `procedencia_campo` de este caso: lo que
        Miguel confirma es lo que esta viendo, y firmar una fila que no esta
        dibujada seria firmar a ciegas.

        Y al reves: un campo dibujado sin procedencia guardada **no se puede**
        firmar —`marcar_campo_verificado` lo rechaza, porque no se verifica lo que
        no se leyo—, asi que se deja fuera aqui y no revienta al escribir.

        ⚠️ **Un campo VACIO tampoco entra, y esto es el arreglo de un defecto
        critico medido por QA el 2026-09-03.** Pulsando los dos botones del pie sin
        teclear nada, en la base quedaba esto:

            ('casos',    1, 'unidad_numero', 'vacio', 1, 1)   <<< VACIO FIRMADO
            ('personas', 3, 'mrn',           'vacio', 1, 1)   <<< VACIO FIRMADO

        Una persona **con `mrn = NULL` marcada como verificada por Miguel, con
        fecha y hora**. El caso *parecia* completo, que es peor que no haberlo
        tocado, y es justo el dano que este programa existe para evitar.

        **Se mira el VALOR de la pantalla y no `origen='vacio'`**, que era la otra
        opcion. El origen es lo que encontro la importacion; el valor es lo que
        Miguel esta mirando cuando firma. Se separan en un caso que importa: si
        Miguel BORRA un valor mal leido **y despues firma**, el origen pasa a
        `'manual'` y una regla escrita sobre el origen firmaria ese vacio igual. La
        regla sobre el valor, no.

        ⚠️ **Y aqui habia escrito eso mismo sin el «y despues firma», como si valiera
        siempre. Es falso, y lo midio QA el 2026-09-03.** Esta funcion decide a quien
        se firma **en el momento de firmar**, asi que no puede decir nada del orden
        contrario: firmar primero y borrar despues. Por esa puerta quedaba un caso
        verificado, con fecha y hora, sin fecha de viaje, fuera de los tres avisos.
        Ese otro orden lo cubre `datos/repositorio.py`, que retira la firma de todo
        campo cuyo valor cambia. Son dos reglas y hacen falta las dos: esta mira
        hacia adelante y aquella hacia atras.

        **Y no se bloquea la verificacion, que era la otra propuesta de QA.** Un
        campo vacio no siempre se puede llenar —si el papel no trae el numero de
        unidad, no hay nada que teclear— y el caso se quedaria sin poder cerrar
        nunca. La regla permanente 5 prohibe las dos direcciones: ni firmar sin que
        Miguel pulse, ni impedirle confirmar lo que si ha revisado. Dejandolo
        fuera, el contador dice «34 de 36» y esa es la verdad; el caso sigue
        saliendo en `casos_pendientes_de_verificar`, que es el aviso que hace
        falta.

        **Las casillas de ordenanza no se miran aqui**: su valor es 0, 1 o «sin
        leer», y las sin leer ya bloquean la verificacion por otro camino
        (`_motivo_por_el_que_no_se_puede_verificar`). Un 0 puesto por Miguel es un
        valor que el afirmo, no un vacio, y tiene que firmarse o el boton
        «Ninguna ordenanza marcada» pierde su motivo.
        """
        confirmables = []
        del_caso = procedencia_por_campo(self.conexion, "casos", self.caso_id)
        confirmables.extend(
            ("casos", self.caso_id, nombre)
            for nombre, campo in self._campos_del_caso.items()
            if nombre in del_caso and campo.valor() is not None
        )
        for bloque in self._bloques:
            de_la_persona = procedencia_por_campo(
                self.conexion, "personas", bloque.persona_id
            )
            confirmables.extend(
                ("personas", bloque.persona_id, nombre)
                for nombre, campo in bloque.campos.items()
                if nombre in de_la_persona and campo.valor() is not None
            )
            confirmables.extend(
                ("personas", bloque.persona_id, nombre)
                for nombre in bloque.casillas
                if nombre in de_la_persona
            )
        return confirmables

    def _campos_vacios_que_no_se_firman(self):
        """Los nombres de los campos que se quedan fuera de la firma por vacios.

        Se devuelven las etiquetas en espanol y no los nombres de columna: es lo
        que se le lee a Miguel en el dialogo, y «unidad_numero» no es una palabra
        que nadie diga.
        """
        vacios = [
            campo.etiqueta
            for campo in self._campos_del_caso.values()
            if campo.valor() is None
        ]
        for indice, bloque in enumerate(self._bloques, start=1):
            vacios.extend(
                f"{campo.etiqueta} de la persona {indice}"
                for campo in bloque.campos.values()
                if campo.valor() is None
            )
        return vacios

    def _pregunta_de_todo_correcto(self):
        """Lo que hay que leer antes de firmar el caso entero.

        Lleva el numero delante y no un «¿seguro?»: es el freno que separa una
        confirmacion de un automatismo.

        ⚠️ **Y dice cuantos se quedan FUERA por venir vacios, con sus nombres.** Un
        campo que no se firma y no se dice es un campo perdido en silencio, y el
        silencio es exactamente lo que hacia peligroso el defecto que esto arregla:
        el caso parecia entero. Con esta frase, Miguel sabe al firmar que dos
        campos siguen sin dar por buenos y cuales son.

        Ya no dice «esto no se puede deshacer». Desde el 2026-09-03 si se puede:
        `datos/procedencia.py` tiene `desmarcar_campo_verificado` y el pie tiene su
        boton. Dejar la frase habria sido mentir al reves.
        """
        cuantos = len(self._campos_que_se_confirman())
        personas = len(self._bloques)
        vacios = self._campos_vacios_que_no_se_firman()
        aviso = ""
        if vacios:
            aviso = (
                f"\n\n{len(vacios)} campo{'s' if len(vacios) != 1 else ''} NO se "
                f"firma{'n' if len(vacios) != 1 else ''} porque "
                f"{'vienen' if len(vacios) != 1 else 'viene'} vacío"
                f"{'s' if len(vacios) != 1 else ''}: "
                + ", ".join(vacios)
                + ". El caso seguirá saliendo como pendiente hasta que tengan algo "
                "escrito, y eso es a propósito: nadie ha podido darlos por buenos."
            )
        return (
            f"Va a dar por buenos {cuantos} campo{'s' if cuantos != 1 else ''} de "
            f"{personas} persona{'s' if personas != 1 else ''} del caso "
            f"{numero_de_caso_visible(self.caso)}, las seis casillas de ordenanza de "
            "cada una incluidas.\n\n"
            f"Quedan firmados con su nombre ({NOMBRE_DEL_COMPANERO_POR_DEFECTO}) y "
            f"con la fecha y la hora de ahora.{aviso}\n\n"
            "¿Sigue?"
        )

    def verificar(self, evento=None):
        """Marca verificados TODOS los campos del caso, con quien y cuando.

        Sin efecto mientras el boton este deshabilitado, y entonces el foco se
        mueve al primer campo que lo bloquea: un atajo que no hace nada y no dice
        por que es peor que no tenerlo.

        **Pregunta antes.** Es el boton que el dueno pidio con esas palabras —«si
        está correcto, darle todo correcto»— y el que convierte 12 confirmaciones
        en una. Que sea una sola pulsacion es justo el motivo por el que tiene que
        preguntar con el numero delante: la regla permanente 5 dice que Miguel
        confirma, y una confirmacion que no dice que confirma no lo es.
        """
        bloqueo = self._motivo_por_el_que_no_se_puede_verificar()
        if bloqueo is not None:
            primero = self._primer_campo_no_valido()
            if primero is not None:
                primero.entrada.focus_set()
            dialogos.informar("Todavía no se puede verificar", bloqueo, parent=self)
            return
        # ⚠️ **Se guarda ANTES de preguntar, y el orden no es cosmetico.** Guardar
        # es lo que crea las filas de procedencia de las casillas que se
        # resolvieron a mano, y la pregunta lleva el numero de campos que se van a
        # firmar. Preguntando primero, ese numero se contaba sobre las filas que
        # habia ANTES: medido, el dialogo decia «12 campos» y se firmaban 36. Un
        # numero que no es el que se firma no es un freno, es un adorno.
        #
        # Guardar sin haber confirmado no adelanta nada que Miguel no hubiera
        # pedido: guardar no es verificar, y es lo que «Guardar» habria hecho.
        # Mismo criterio que `_archivar`.
        if not self.guardar():
            return
        if not dialogos.preguntar_si_o_no(
            "Todo correcto", self._pregunta_de_todo_correcto(), parent=self
        ):
            return

        companero_id = _companero_que_verifica(self.conexion)
        for tabla, registro_id, campo in self._campos_que_se_confirman():
            marcar_campo_verificado(self.conexion, tabla, registro_id, campo, companero_id)
        self._recontar()
        dialogos.informar(
            "Caso verificado",
            f"El caso {numero_de_caso_visible(self.caso)} queda verificado por "
            f"{NOMBRE_DEL_COMPANERO_POR_DEFECTO}, con la fecha y la hora de ahora.",
            parent=self,
        )

    # ---- deshacer todo correcto ------------------------------------------

    def _campos_firmados_de_este_caso(self):
        """Las tripletas de los campos de este caso que llevan firma, de la base.

        Se leen de `procedencia_campo` y NO de `_campos_que_se_confirman()`: lo que
        hay que soltar es lo que esta firmado, y eso incluye lo que se firmo en una
        sesion anterior con otra pantalla delante. Preguntando a la pantalla, un
        campo que hoy viene vacio se quedaria firmado para siempre — que es
        exactamente el estado del que hay que poder salir.
        """
        filas = self.conexion.execute(
            "SELECT tabla, registro_id, campo FROM procedencia_campo "
            "WHERE verificado = 1 AND ("
            "  (tabla = 'casos' AND registro_id = ?) "
            "  OR (tabla = 'personas' AND registro_id IN "
            "      (SELECT id FROM personas WHERE caso_id = ?)))",
            (self.caso_id, self.caso_id),
        ).fetchall()
        return [(fila["tabla"], fila["registro_id"], fila["campo"]) for fila in filas]

    def deshacer_la_verificacion(self, evento=None):
        """Retira la firma de todos los campos de este caso. Pregunta antes.

        **Por que existe.** Hasta el 2026-09-03 no habia forma de deshacer una
        verificacion desde el programa: `datos/procedencia.py` tenia
        `marcar_campo_verificado` y ninguna inversa, y el propio dialogo de «Todo
        correcto» lo decia. Un error de dos pulsaciones solo se arreglaba abriendo
        la base con otra herramienta, que es tanto como decir que no se arreglaba.

        **Pregunta, por el mismo motivo que firmar.** Es una sola pulsacion y mueve
        36 filas; un boton que hace eso sin decir cuantas no es un boton, es una
        trampa. Y dice el numero delante, no un «¿seguro?».

        **No toca ningun dato del caso.** Deshacer la firma no deshace las
        correcciones: lo tecleado, las casillas resueltas y de donde salio cada
        valor se quedan como estan. Lo unico que cambia es quien ha dado esto por
        bueno, que pasa a ser nadie.
        """
        firmados = self._campos_firmados_de_este_caso()
        if not firmados:
            dialogos.informar(
                "No hay ninguna firma que deshacer",
                f"Nadie ha dado por bueno ningún campo del caso "
                f"{numero_de_caso_visible(self.caso)} todavía, así que no hay nada "
                "que retirar aquí.",
                parent=self,
            )
            return
        if not dialogos.preguntar_si_o_no(
            "Deshacer «Todo correcto»",
            f"Va a retirar la firma de {len(firmados)} campo"
            f"{'s' if len(firmados) != 1 else ''} del caso "
            f"{numero_de_caso_visible(self.caso)}.\n\n"
            "El caso vuelve a la lista de pendientes y habrá que volver a "
            "confirmarlo. Lo tecleado, las casillas y las correcciones NO se "
            "tocan: lo único que se retira es quién lo dio por bueno.\n\n"
            "¿Sigue?",
            parent=self,
        ):
            return

        for tabla, registro_id, campo in firmados:
            desmarcar_campo_verificado(self.conexion, tabla, registro_id, campo)
        self.conexion.commit()
        self._recontar()
        dialogos.informar(
            "Firma retirada",
            f"El caso {numero_de_caso_visible(self.caso)} vuelve a estar sin "
            "verificar y sale otra vez en la lista de pendientes.",
            parent=self,
        )

    def _archivar(self):
        """Va a la pantalla de archivar, guardando antes lo que este sin guardar.

        Se guarda primero y no se pregunta: lo tecleado aqui y lo que se anote alli
        son del mismo caso, y perder lo tecleado por pasar de una pantalla a la
        otra seria un castigo por usar el boton. Si el guardado falla, no se pasa:
        archivar un caso cuya correccion no entro lo saca de las listas justo
        cuando todavia hacia falta.
        """
        if self._hay_cambios_sin_guardar() and not self.guardar():
            return "break"
        # ⚠️ **Y tampoco se pasa con un campo que no vale.** Desde el 2026-09-04 un
        # campo malo ya no tumba el guardado, asi que `guardar()` devuelve cierto
        # aunque algo se haya quedado sin escribir; sin esta comprobacion,
        # archivar se llevaria por delante lo tecleado en ese campo —que es lo
        # mismo que este archivo lleva todo el pase evitando— y ademas sacaria el
        # caso de las listas justo cuando todavia hacia falta.
        sin_guardar = self._campos_que_no_valen()
        if sin_guardar:
            dialogos.informar(
                "Antes de archivar",
                "No se archiva todavía: "
                + ", ".join(campo.etiqueta for campo in sin_guardar)
                + (" no vale" if len(sin_guardar) == 1 else " no valen")
                + " y por eso no se ha guardado. Corríjalo y guarde; después se "
                "puede archivar.",
                parent=self,
            )
            return "break"
        self._al_archivar(self.caso_id, numero_de_caso_visible(self.caso))
        return "break"

    def _volver(self, evento=None):
        """Vuelve a la pantalla de inicio, preguntando si hay cambios sin guardar."""
        if self._hay_cambios_sin_guardar() and not dialogos.preguntar_si_o_no(
            "Hay cambios sin guardar",
            "Ha cambiado algo en este caso y no lo ha guardado. ¿Volver al inicio "
            "de todas formas? Lo tecleado se pierde.",
            parent=self,
        ):
            return "break"
        self._al_volver()
        return "break"

    # ---- recuento -------------------------------------------------------

    def _todos_los_campos(self):
        """Los campos del caso y los de todas las personas, en una sola lista."""
        campos = list(self._campos_del_caso.values())
        for bloque in self._bloques:
            campos.extend(bloque.campos.values())
        return campos

    def _hay_cambios_sin_guardar(self):
        """Los campos, y también el estado de la recomendación.

        El estado se compara contra lo que trae el caso leído de la base y no
        contra el valor de arranque del desplegable: elegir «Usar esta» de una
        propuesta y volver al inicio sin guardar tiene que preguntar, o el trabajo
        del compañero se pierde en silencio.
        """
        if any(campo.cambio() for campo in self._todos_los_campos()):
            return True
        return self._seguimiento.valor_del_estado() != self.caso["estado_recomendacion"]

    def _primer_campo_no_valido(self):
        for campo in self._todos_los_campos():
            if not campo.es_valido():
                return campo
        return None

    def _casillas_sin_leer(self):
        return sum(bloque.casillas_sin_leer() for bloque in self._bloques)

    def _campos_que_no_valen(self):
        """Los campos que no pasan su validacion, en el orden en que se ven."""
        return [campo for campo in self._todos_los_campos() if not campo.es_valido()]

    def _campos_no_validos(self):
        return len(self._campos_que_no_valen())

    def _campos_verificados(self):
        """Cuantos campos de este caso llevan `verificado = 1`, leidos de la base.

        Se cuenta consultando y no llevando la cuenta en memoria: el criterio 3 de
        la FASE 3 se comprueba con un `SELECT` sobre la base, y un contador de
        pantalla que dijera otra cosa que la base seria justo la mentira que ese
        criterio existe para cazar.
        """
        fila = self.conexion.execute(
            "SELECT COUNT(*) AS todos, "
            "SUM(CASE WHEN verificado = 1 THEN 1 ELSE 0 END) AS verificados "
            "FROM procedencia_campo "
            "WHERE (tabla = 'casos' AND registro_id = ?) "
            "OR (tabla = 'personas' AND registro_id IN "
            "    (SELECT id FROM personas WHERE caso_id = ?))",
            (self.caso_id, self.caso_id),
        ).fetchone()
        return (fila["verificados"] or 0), (fila["todos"] or 0)

    def _motivo_por_el_que_no_se_puede_verificar(self):
        """El texto que explica el bloqueo, o None cuando no hay bloqueo.

        Un boton apagado sin motivo escrito al lado es un callejon: se ve que no se
        puede seguir y no se ve que falta. Por eso esto devuelve la frase y no un
        booleano.
        """
        no_validos = self._campos_no_validos()
        sin_leer = self._casillas_sin_leer()
        # Un caso sin numero no se puede dar por verificado, y no es una regla
        # nueva: verificar significa «esto ya está bien», y un caso que no se puede
        # cruzar con el Excel de los compañeros no está bien todavía. Va antes que
        # las otras dos porque es la que se arregla primero.
        if self._falta_el_numero_de_caso() and self._campos_del_caso["numero_caso"].valor() is None:
            return (
                "Falta el número de caso, y sin él este caso no se puede cruzar con "
                "el Excel que devuelven los compañeros. Escríbalo arriba mirando el "
                "PDF (F2) y guarde; después se podrá verificar."
            )
        if not no_validos and not sin_leer:
            return None
        partes = []
        if no_validos:
            partes.append(
                f"{no_validos} campo{'s' if no_validos != 1 else ''} que no vale"
                f"{'n' if no_validos != 1 else ''}"
            )
        if sin_leer:
            partes.append(
                f"{sin_leer} casilla{'s' if sin_leer != 1 else ''} sin resolver"
            )
        motivo = " y ".join(partes) + ". Hasta que no quede ninguno no se puede verificar."
        if sin_leer:
            # El motivo nombra la salida. Sin esto el pie decia cuantas faltaban y
            # dejaba a Miguel con 24 casillas y ninguna pista de que hubiera otra
            # forma de resolverlas que ir de una en una.
            motivo += (
                " Si en este documento no hay ninguna ordenanza marcada, use "
                "«Ninguna ordenanza marcada» y quedan resueltas de una vez; marque "
                "a mano solo las que el papel sí traiga."
            )
        return motivo

    def _recontar(self):
        """Repinta el contador, el motivo del bloqueo y el estado del boton.

        Se llama cada vez que cambia algo, no solo al guardar: es lo que hace que
        el pie diga la verdad mientras se teclea.
        """
        verificados, todos = self._campos_verificados()
        self._contador.configure(
            text=(
                f"Verificados {verificados} de {todos} campos · "
                f"{self._campos_no_validos()} no válidos · "
                f"{self._casillas_sin_leer()} casillas no leídas"
            )
        )
        bloqueo = self._motivo_por_el_que_no_se_puede_verificar()
        self._motivo_del_bloqueo.configure(
            text=bloqueo or "Todo validado: este caso ya se puede verificar.",
            style=f"{NO_VALIDO}.TLabel" if bloqueo else "Secundario.TLabel",
        )
        # ⚠️ **Aqui habia escrito que el boton apagado «sigue tomando el foco con
        # Tab a proposito». Es falso, y se midio.** El recorrido completo de esta
        # pantalla con seis personas da **57 paradas**; si el boton apagado parara
        # el Tab darian 58 (`pruebas/prueba_correccion.py`). Un `ttk.Button` en
        # `disabled` no entra en el recorrido en esta maquina (Tk 9.0.4, Windows
        # 11). Corregido segun `CLAUDE.md` §8: manda el numero.
        #
        # Lo que la afirmacion falsa pretendia proteger —que se pueda saber POR QUE
        # no se puede verificar sin usar el raton— lo sostiene el motivo escrito en
        # el pie, que se pinta justo arriba y no se desplaza nunca. Por eso ese
        # texto no es adorno.
        self._boton_verificar.state(["disabled"] if bloqueo else ["!disabled"])
        # Y el de las casillas al reves: se apaga cuando ya no queda ninguna sin
        # leer. Los dos no estan encendidos a la vez mientras las casillas sean el
        # unico bloqueo, y eso es lo que ensena el orden — primero resolver, luego
        # confirmar — sin ninguna instruccion escrita.
        self._boton_de_las_casillas.state(
            ["!disabled"] if self._casillas_sin_leer() else ["disabled"]
        )
        self._pintar_avisos()
