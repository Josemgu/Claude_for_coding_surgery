using Fichas.App.Asignar;
using Fichas.App.Correccion;
using Fichas.App.Revisar;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Consultas;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// Las pruebas de repartir POR GRUPO desde la pantalla de Asignar, sin ventana.
/// </summary>
/// <remarks>
/// <para>Nacen del encargo del dueño del 2026-09-09, con sus palabras: <i>«En asignar debe
/// poder asignarlo por grupo. Cargar todos los documentos en un solo lugar no me conviene
/// para nada»</i>, y de su ejemplo: <i>«Rama San Juan No 325535, 10 personas viajarán el 12
/// de septiembre. Barrio Marito 656351, 5 personas viajarán el 12 de septiembre»</i>.</para>
///
/// <para>⚠️ Están escritas desde ESE criterio y no desde el código: cada una dice qué gesto
/// del dueño defiende. La que más pesa es
/// <see cref="ElAgrupadoEsElMismoQueElDeCorreccionYNoOtro"/>, porque lo que se pidió no es
/// «una forma de agrupar» sino la que él ya conoce de Revisar y de Corrección.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeAsignarPorGrupo
{
    /// <summary>El día en que viaja el grupo del ejemplo del dueño.</summary>
    private const string ElDoceDeSeptiembre = "2026-09-12";

    /// <summary>Otro día, para que haya más de una fecha y el agrupado tenga algo que separar.</summary>
    private const string ElVeinteDeSeptiembre = "2026-09-20";

    /// <summary>
    /// El ejemplo del dueño, tal cual: dos unidades que viajan el mismo día y una tercera
    /// que viaja otro, más un documento sin fecha de viaje.
    /// </summary>
    /// <remarks>
    /// Los nombres de unidad están INVENTADOS salvo los dos que él escribió en su mensaje,
    /// que son los que dan sentido a la prueba. Ninguna persona real entra aquí.
    /// </remarks>
    private static BaseDePrueba ConElEjemploDelDueno()
    {
        var banco = new BaseDePrueba();

        // Rama San Juan No 325535: 10 personas el 12 de septiembre.
        for (var cual = 1; cual <= 10; cual++)
        {
            banco.MeterConUnidad($"SJ{cual:D6}", ElDoceDeSeptiembre, "325535", "Rama San Juan");
        }

        // Barrio Marito 656351: 5 personas el mismo 12 de septiembre.
        for (var cual = 1; cual <= 5; cual++)
        {
            banco.MeterConUnidad($"MA{cual:D6}", ElDoceDeSeptiembre, "656351", "Barrio Marito");
        }

        // Una unidad que viaja OTRO día: si el agrupado fuera por unidad y no por fecha,
        // esta caería junto a las de arriba y la prueba lo caza.
        for (var cual = 1; cual <= 3; cual++)
        {
            banco.MeterConUnidad($"OT{cual:D6}", ElVeinteDeSeptiembre, "700001", "Rama Otra");
        }

        // Y uno del que no se pudo leer la fecha: va al final y pide que lo miren.
        banco.MeterConUnidad("SF000001", null, "700002", "Rama Sin Fecha");

        return banco;
    }

    // ---- criterio 1: se ven agrupados, con la cifra de cada grupo y el total ----

    /// <summary>
    /// Al abrir Asignar con documentos de varias fechas y varias unidades, salen agrupados
    /// por fecha de viaje y, dentro, por unidad; cada grupo dice cuántos trae.
    /// </summary>
    [TestMethod]
    public void LosDocumentosSalenAgrupadosPorFechaYDentroPorUnidadConSuCifra()
    {
        var banco = ConElEjemploDelDueno();

        var grupos = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos);

        Assert.HasCount(3, grupos, "Tres fechas: el 12, el 20 y la de los que no tienen fecha.");

        var elDoce = grupos[0];
        Assert.AreEqual(ElDoceDeSeptiembre, elDoce.FechaIso, "Lo que viaja antes va arriba.");
        Assert.AreEqual("Grupo del 12 de septiembre", elDoce.Carpeta, "Con las palabras que usa Revisar.");
        Assert.AreEqual(15, elDoce.CuantosDocumentos, "Los 10 de San Juan y los 5 de Marito.");
        Assert.HasCount(2, elDoce.Unidades, "Y dentro, las dos unidades de ese día.");

        var sanJuan = elDoce.Unidades.Single(u => u.Carpeta.Contains("San Juan", StringComparison.Ordinal));
        var marito = elDoce.Unidades.Single(u => u.Carpeta.Contains("Marito", StringComparison.Ordinal));
        Assert.HasCount(10, sanJuan.Documentos, "«Rama San Juan No 325535, 10 personas».");
        Assert.HasCount(5, marito.Documentos, "«Barrio Marito 656351, 5 personas».");
        Assert.AreEqual("325535 · Rama San Juan", sanJuan.Carpeta, "El número Y el nombre, como en el árbol.");

        Assert.AreEqual(3, grupos[1].CuantosDocumentos, "El 20 de septiembre trae los suyos y solo los suyos.");
    }

    /// <summary>
    /// La cifra que se lee en cada renglón del panel de grupos lleva el número dentro; sin
    /// ella, elegir un grupo sería abrir trabajo a ciegas (CLAUDE.md §8).
    /// </summary>
    [TestMethod]
    public void CadaGrupoYCadaUnidadDicenCuantosDocumentosTraen()
    {
        var banco = ConElEjemploDelDueno();

        var grupos = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos);
        var elDoce = grupos[0];

        Assert.AreEqual("Grupo del 12 de septiembre · 15 documentos", elDoce.Etiqueta);
        Assert.AreEqual(
            "325535 · Rama San Juan · 10 documentos",
            elDoce.Unidades.Single(u => u.Carpeta.StartsWith("325535", StringComparison.Ordinal)).Etiqueta);
    }

    /// <summary>
    /// La suma de los grupos es el total ofrecido: agrupar reparte, no descarta.
    /// </summary>
    /// <remarks>
    /// Es la prueba que separa «agrupar» de «filtrar». Si un documento no cayera en ningún
    /// grupo desaparecería de la pantalla sin que nadie lo dijera, que es exactamente la
    /// queja del programa viejo —«no me deja asignar»— con otra cara.
    /// </remarks>
    [TestMethod]
    public void LaSumaDeLosGruposEsElTotalOfrecidoYNoSePierdeNiUno()
    {
        var banco = ConElEjemploDelDueno();
        var ofrecidos = banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos;

        var grupos = GruposParaAsignar.Armar(ofrecidos);

        Assert.HasCount(19, ofrecidos, "Los 10 + 5 + 3 + 1 que se metieron.");
        Assert.AreEqual(ofrecidos.Count, grupos.Sum(g => g.CuantosDocumentos), "La suma de los grupos es el total.");
        CollectionAssert.AreEquivalent(
            ofrecidos.Select(r => r.CasoId).ToList(),
            grupos.SelectMany(g => g.CasosDeLaFecha).ToList(),
            "Y son exactamente los mismos documentos, ni uno menos.");
    }

    /// <summary>
    /// Lo que no tiene fecha de viaje va al FINAL y pide que lo miren, igual que en Revisar.
    /// </summary>
    /// <remarks>
    /// No se le inventa una fecha para colocarlo (regla permanente 1) ni se esconde: se
    /// nombra con las palabras del dueño, «revisar este documento».
    /// </remarks>
    [TestMethod]
    public void LoQueNoTieneFechaVaAlFinalYPideQueLoMiren()
    {
        var banco = ConElEjemploDelDueno();

        var grupos = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos);
        var ultimo = grupos[^1];

        Assert.IsEmpty(ultimo.FechaIso, "Del último no se pudo leer la fecha.");
        Assert.IsTrue(ultimo.HayQueRevisarlo, "Y por eso es el que hay que mirar.");
        Assert.StartsWith(ArbolDeRevisar.LlamadaARevisar, ultimo.Etiqueta, StringComparison.Ordinal);
        Assert.AreEqual(1, ultimo.CuantosDocumentos, "Trae el único documento sin fecha.");
    }

    /// <summary>
    /// ⛔ El agrupado es el MISMO que el de Corrección y el del árbol de Revisar, no otro.
    /// </summary>
    /// <remarks>
    /// <para>Es la prueba que más pesa de este archivo. Con dos reglas de agrupado, el «grupo
    /// del 12 de septiembre» de Asignar podría no ser el de Corrección, y el dueño repartiría
    /// una lista creyendo que es la que va a corregir. Aquí se comprueba contra
    /// <see cref="GruposParaCorregir"/>, que es quien ya llama a
    /// <see cref="ArbolDeRevisar.Agrupar"/>: si alguien escribe un agrupado propio en Asignar,
    /// esta prueba se cae.</para>
    ///
    /// <para>Se comparan los pares (fecha, unidad) en su ORDEN, que es la mitad que importa:
    /// dos listas con los mismos grupos en distinto orden son dos pantallas distintas para
    /// quien las mira.</para>
    /// </remarks>
    [TestMethod]
    public void ElAgrupadoEsElMismoQueElDeCorreccionYNoOtro()
    {
        var banco = ConElEjemploDelDueno();
        banco.Tablero.Cargar();

        var deCorreccion = GruposParaCorregir.Armar(banco.Tablero.Todas(FiltroDeTarjeta.Todo))
            .Select(g => $"{g.CarpetaDeLaFecha} · {g.CarpetaDeLaUnidad} · {g.CuantosDocumentos}")
            .ToList();

        var deAsignar = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)
            .SelectMany(fecha => fecha.Unidades
                .Select(unidad => $"{fecha.Carpeta} · {unidad.Carpeta} · {unidad.Documentos.Count}"))
            .ToList();

        Assert.IsNotEmpty(deCorreccion, "La comparación no vale si la otra pantalla no armó nada.");
        CollectionAssert.AreEqual(
            deCorreccion,
            deAsignar,
            "Asignar y Corrección tienen que ver los MISMOS grupos, en el MISMO orden.");
    }

    // ---- criterio 4: agrupar no impide; el documento suelto sigue ----

    /// <summary>
    /// Agrupar no esconde nada: el filtro de la pantalla sigue sin ningún estado dentro y
    /// los ofrecidos siguen siendo todos los que no están archivados.
    /// </summary>
    /// <remarks>
    /// La queja del programa viejo era «no me deja asignar». Un grupo que además filtrara la
    /// consulta la resucitaría; por eso se comprueba que el agrupado NO toca lo que se pide a
    /// la base, que es donde vive ese defecto.
    /// </remarks>
    [TestMethod]
    public void AgruparNoCambiaLoQueSeLePideALaBaseNiEscondeNingunEstado()
    {
        var banco = ConElEjemploDelDueno();
        var ids = banco.MeterUnCasoDeCadaEstado();

        var ofrecidos = banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos;
        var grupos = GruposParaAsignar.Armar(ofrecidos);
        var enLosGrupos = grupos.SelectMany(g => g.CasosDeLaFecha).ToHashSet();

        Assert.IsNull(ListaParaAsignar.SinFiltroDeEstadoNiArchivados.Estado, "Sigue sin filtro de estado.");
        foreach (var vivo in ids.Take(6))
        {
            Assert.Contains(vivo, enLosGrupos, $"El caso {vivo} tiene que estar en algún grupo: no se esconde ninguno.");
        }

        Assert.DoesNotContain(ids[6], enLosGrupos, "El archivado sigue fuera, que es el otro eje y no el del estado.");
    }

    /// <summary>
    /// Un documento suelto se sigue asignando por la misma puerta, y agrupar no lo estorba.
    /// </summary>
    [TestMethod]
    public void UnDocumentoSueltoSeSigueAsignandoPorLaMismaPuerta()
    {
        var banco = ConElEjemploDelDueno();
        var quien = banco.Activos[0];
        var uno = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)[0]
            .Unidades[0].Documentos[0];

        var resultado = banco.Operacion.Asignar(uno.CasoId, quien.Id);

        Assert.IsTrue(resultado.SeEscribio, "Un documento suelto se asigna igual que antes.");
        Assert.AreEqual(1, banco.CuantasAsignacionesVivas(quien.Id), "Y solo se asignó ese.");
    }

    // ---- criterio 2 y 3: se dice cuántos ANTES, y cancelar no escribe ----

    /// <summary>
    /// Mirar lo que se va a asignar dice el número y NO toca la base. Es lo que hace que
    /// cancelar sea de verdad cancelar.
    /// </summary>
    /// <remarks>
    /// Misma forma que <c>MirarLoQueSeLeQuitaria</c>, que es como este programa pregunta
    /// antes de tocar muchos documentos de una vez.
    /// </remarks>
    [TestMethod]
    public void MirarLoQueSeVaAAsignarDiceElNumeroYNoEscribeNada()
    {
        var banco = ConElEjemploDelDueno();
        var quien = banco.Activos[0];
        var elDoce = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)[0];

        var loQueSeVa = banco.Operacion.MirarLoQueSeVaAAsignar(
            elDoce.CasosDeLaFecha, quien.Nombre, elDoce.Carpeta);

        Assert.AreEqual(15, loQueSeVa.Cuantos, "Los 15 del 12 de septiembre.");
        Assert.IsTrue(loQueSeVa.HayAlgoQueAsignar);
        Assert.Contains("15", loQueSeVa.Pregunta, StringComparison.Ordinal, "El número va delante en la pregunta.");
        Assert.Contains(quien.Nombre, loQueSeVa.Pregunta, StringComparison.Ordinal, "Y a quién se le dan.");
        Assert.Contains(elDoce.Carpeta, loQueSeVa.Pregunta, StringComparison.Ordinal, "Y de qué grupo son.");
        Assert.AreEqual(0, banco.CuantasAsignacionesVivas(quien.Id), "MIRAR NO ESCRIBE: en la base no hay nada.");
    }

    /// <summary>
    /// La pregunta concuerda en singular y en plural: sujeto y verbo, no solo el verbo.
    /// </summary>
    /// <remarks>
    /// Se vio pintado en la ventana el 2026-09-09: con un solo documento la frase decía «los
    /// documentos se quedan como estaba». La comprobación de tildes de la pantalla no caza
    /// esto —las tildes están bien—, así que hace falta una prueba que lea la frase.
    /// </remarks>
    [TestMethod]
    public void LaPreguntaConcuerdaEnSingularYEnPlural()
    {
        var banco = ConElEjemploDelDueno();
        var quien = banco.Activos[0];
        var elDoce = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)[0];
        var unSolo = elDoce.Unidades[0].CasosDeLaUnidad.Take(1).ToList();

        var deUno = banco.Operacion.MirarLoQueSeVaAAsignar(unSolo, quien.Nombre, "una unidad").Pregunta;
        var deMuchos = banco.Operacion
            .MirarLoQueSeVaAAsignar(elDoce.CasosDeLaFecha, quien.Nombre, elDoce.Carpeta).Pregunta;

        Assert.Contains("Se va a asignar", deUno, StringComparison.Ordinal);
        Assert.Contains("el documento se queda como estaba", deUno, StringComparison.Ordinal);
        Assert.DoesNotContain("documentos se quedan como estaba.", deUno, StringComparison.Ordinal);

        Assert.Contains("Se van a asignar", deMuchos, StringComparison.Ordinal);
        Assert.Contains("los documentos se quedan como estaban", deMuchos, StringComparison.Ordinal);
    }

    /// <summary>Con un grupo vacío no se pregunta: se dice que no hay nada que asignar.</summary>
    /// <remarks>
    /// Un botón que no hace nada y no dice por qué es un botón roto; es la misma decisión que
    /// ya tomó <c>PaginaDeGrupo.Asignar</c> el 2026-09-06.
    /// </remarks>
    [TestMethod]
    public void ConUnGrupoVacioSeDiceQueNoHayNadaQueAsignar()
    {
        var banco = ConElEjemploDelDueno();
        var quien = banco.Activos[0];

        var loQueSeVa = banco.Operacion.MirarLoQueSeVaAAsignar([], quien.Nombre, "Grupo del 12 de septiembre");

        Assert.AreEqual(0, loQueSeVa.Cuantos);
        Assert.IsFalse(loQueSeVa.HayAlgoQueAsignar, "Con cero no se enciende el botón de confirmar.");
        Assert.Contains("no hay", loQueSeVa.Pregunta, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Criterio 2. El grupo entero de una fecha se asigna en un gesto, y solo ese.
    /// </summary>
    [TestMethod]
    public void ElGrupoEnteroDeUnaFechaSeAsignaEnUnGestoYNoTocaLasOtrasFechas()
    {
        var banco = ConElEjemploDelDueno();
        var quien = banco.Activos[0];
        var elDoce = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)[0];

        var resumen = banco.Operacion.AsignarVarios(elDoce.CasosDeLaFecha, quien.Id, quien.Nombre);

        Assert.AreEqual(15, resumen.Asignados, "Los 15 del día, de una vez.");
        Assert.AreEqual(0, resumen.NoSePudieron);
        Assert.AreEqual(15, banco.CuantasAsignacionesVivas(quien.Id), "Y en la base hay 15 asignaciones vivas.");
        Assert.Contains("15", resumen.Linea, StringComparison.Ordinal, "La línea del acuse dice cuántos fueron.");

        var otrasFechas = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)
            .Where(g => g.FechaIso != ElDoceDeSeptiembre)
            .SelectMany(g => g.Unidades)
            .SelectMany(u => u.Documentos);
        foreach (var suelto in otrasFechas)
        {
            Assert.AreEqual(
                RenglonParaAsignar.SinAsignar,
                suelto.AsignadoA,
                "Asignar el grupo del 12 no puede tocar lo que viaja otro día.");
        }
    }

    /// <summary>
    /// Criterio 3. Una unidad dentro de una fecha se asigna sola, sin llevarse a la de al lado.
    /// </summary>
    /// <remarks>
    /// Es el caso del dueño: San Juan y Marito viajan el MISMO día y son dos líderes distintos
    /// (ADR-0005 §2.5). Repartir uno no puede repartir el otro.
    /// </remarks>
    [TestMethod]
    public void UnaUnidadDeUnaFechaSeAsignaSolaYNoSeLlevaALaDeAlLado()
    {
        var banco = ConElEjemploDelDueno();
        var quien = banco.Activos[0];
        var elDoce = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)[0];
        var sanJuan = elDoce.Unidades.Single(u => u.Carpeta.StartsWith("325535", StringComparison.Ordinal));

        var resumen = banco.Operacion.AsignarVarios(sanJuan.CasosDeLaUnidad, quien.Id, quien.Nombre);

        Assert.AreEqual(10, resumen.Asignados, "Los 10 de Rama San Juan.");
        Assert.AreEqual(10, banco.CuantasAsignacionesVivas(quien.Id), "Diez asignaciones vivas, ni una más.");

        var despues = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)[0];
        var marito = despues.Unidades.Single(u => u.Carpeta.StartsWith("656351", StringComparison.Ordinal));
        foreach (var documento in marito.Documentos)
        {
            Assert.AreEqual(
                RenglonParaAsignar.SinAsignar,
                documento.AsignadoA,
                "Barrio Marito viaja el mismo día pero es otro líder: no se reparte con San Juan.");
        }
    }

    // ---- criterio 5: solo se leen las dos palabras ----

    /// <summary>
    /// De un documento de un grupo solo se leen «resuelto» y «me falta»; no hay una tercera.
    /// </summary>
    [TestMethod]
    public void DeLosDocumentosDeUnGrupoSoloSeLeenLasDosPalabras()
    {
        var banco = ConElEjemploDelDueno();
        banco.MeterUnCasoDeCadaEstado();

        var documentos = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos)
            .SelectMany(g => g.Unidades)
            .SelectMany(u => u.Documentos)
            .ToList();

        Assert.IsNotEmpty(documentos);
        foreach (var documento in documentos)
        {
            Assert.Contains(
                documento.PalabraDelEstado,
                DosEstados.LasDos,
                $"«{documento.PalabraDelEstado}» no es ninguna de las dos palabras.");
        }
    }

    /// <summary>
    /// Y las cabeceras del panel de grupos tampoco dicen ninguna palabra de estado: dicen
    /// cuántos documentos hay, que es lo que hace falta para repartir.
    /// </summary>
    [TestMethod]
    public void LasCabecerasDelPanelDeGruposNoDicenNingunaPalabraDeEstado()
    {
        var banco = ConElEjemploDelDueno();
        banco.MeterUnCasoDeCadaEstado();

        var renglones = GruposParaAsignar.EnUnaSolaLista(
            GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos));

        Assert.IsNotEmpty(renglones, "El panel de grupos tiene renglones que pintar.");
        foreach (var prohibida in new[] { "sin revisar", "no está completa", "completa", "archivado" })
        {
            Assert.IsEmpty(
                renglones.Where(r => r.Etiqueta.Contains(prohibida, StringComparison.OrdinalIgnoreCase)).ToList(),
                $"Ninguna cabecera puede decir «{prohibida}»: el vocabulario es de dos palabras.");
        }
    }

    // ---- el panel aplanado que pinta la ventana ----

    /// <summary>
    /// El panel de grupos va en UNA lista: la fecha y, debajo, sus unidades. Cada renglón
    /// sabe qué documentos asigna su botón.
    /// </summary>
    /// <remarks>
    /// Aplanado y no un repetidor dentro de otro, por lo mismo que
    /// <c>GrupoDelDia.EnUnaSolaLista</c>: con la base llena, un repetidor anidado construye
    /// todos los renglones de golpe y el criterio pide que el número de elementos visuales
    /// tenga tope.
    /// </remarks>
    [TestMethod]
    public void ElPanelDeGruposVaEnUnaSolaListaYCadaRenglonSabeQueAsigna()
    {
        var banco = ConElEjemploDelDueno();
        var grupos = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos);

        var renglones = GruposParaAsignar.EnUnaSolaLista(grupos);

        // 3 fechas + (2 + 1 + 1) unidades = 7 renglones.
        Assert.HasCount(7, renglones);
        Assert.IsTrue(renglones[0].EsLaFecha, "El primero es la cabecera de la fecha más cercana.");
        Assert.HasCount(15, renglones[0].Casos, "Y su botón asigna el grupo entero del día.");
        Assert.IsFalse(renglones[1].EsLaFecha, "Debajo van sus unidades.");
        Assert.HasCount(10, renglones[1].Casos, "La primera unidad del día asigna sus 10.");
        Assert.IsTrue(
            renglones.Where(r => !r.EsLaFecha).All(r => r.Casos.Count > 0),
            "Ninguna unidad llega vacía al panel.");
    }

    /// <summary>
    /// El renglón guarda el nombre del grupo SIN la cifra, además de la etiqueta que la lleva.
    /// </summary>
    /// <remarks>
    /// <para>Son dos textos y no uno porque se leen en dos sitios distintos: en el panel, la
    /// cifra hace falta —es lo que dice cuánto trabajo se abre—; en la pregunta de confirmar,
    /// la cifra ya va delante, y meter la etiqueta entera hace que se lea «se van a asignar 3
    /// documentos de Grupo del 12 de septiembre · 3 documentos».</para>
    ///
    /// <para>Se vio así, con la ventana abierta y 60 documentos inventados, el 2026-09-09.</para>
    /// </remarks>
    [TestMethod]
    public void LaPreguntaDeConfirmarNombraElGrupoSinRepetirLaCifra()
    {
        var banco = ConElEjemploDelDueno();
        var quien = banco.Activos[0];
        var renglones = GruposParaAsignar.EnUnaSolaLista(
            GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos));
        var laFecha = renglones[0];

        Assert.AreEqual("Grupo del 12 de septiembre", laFecha.Nombre, "El nombre va sin cifra.");
        Assert.AreEqual("Grupo del 12 de septiembre · 15 documentos", laFecha.Etiqueta, "La etiqueta sí la lleva.");

        var pregunta = banco.Operacion
            .MirarLoQueSeVaAAsignar(laFecha.Casos, quien.Nombre, laFecha.Nombre)
            .Pregunta;

        Assert.DoesNotContain(
            "· 15 documentos",
            pregunta,
            StringComparison.Ordinal,
            "La pregunta no puede decir la cifra dos veces.");
        Assert.Contains("15 documentos", pregunta, StringComparison.Ordinal, "Pero la dice una: es el número que se confirma.");
    }

    /// <summary>Sin ningún documento, el panel de grupos sale vacío y no inventa carpetas.</summary>
    [TestMethod]
    public void SinDocumentosElPanelDeGruposSaleVacio()
    {
        var banco = new BaseDePrueba();

        var grupos = GruposParaAsignar.Armar(banco.Lista.Ofrecer(Pagina.Primera(int.MaxValue)).Elementos);

        Assert.IsEmpty(grupos, "Cero documentos son cero grupos, no una carpeta vacía.");
        Assert.IsEmpty(GruposParaAsignar.EnUnaSolaLista(grupos));
    }
}
