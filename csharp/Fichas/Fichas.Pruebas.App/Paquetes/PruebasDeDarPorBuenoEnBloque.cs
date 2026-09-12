using Fichas.App.Correccion;
using Fichas.App.Paquetes;
using Fichas.Contratos.Modelos;
using Fichas.Paquetes;

namespace Fichas.Pruebas.App.Paquetes;

/// <summary>
/// El botón que da por bueno de una vez lo que trajo el paquete de un agente.
/// </summary>
/// <remarks>
/// <para>Cada prueba sale del criterio de aceptación del pase del 2026-09-06, no del código.
/// El criterio lo movió el dueño con estas palabras: <i>«debe haber botones donde aplique
/// "todo completo", igual en los paquetes, porque ir uno por uno si está bien pero no es
/// suficiente»</i> y <i>«lo que debo revisar son los paquetes de los agentes. Solo verificar
/// las informaciones. Y ya.»</i></para>
///
/// <para>⛔ Las dos cosas que este archivo existe para que no se mezclen nunca: el
/// <b>estado de la recomendación</b> lo escribe el Excel del compañero con SU nombre, y la
/// <b>firma de campos</b> es de Miguel. Hay una prueba para cada mitad.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeDarPorBuenoEnBloque
{
    /// <summary>Una cédula de mentira, con la forma que la lectura acepta; no es de nadie.</summary>
    private const string CedulaDeUno = "055-1111-3853";
    /// <summary>Otra cédula de mentira, distinta de la primera.</summary>
    private const string CedulaDeDos = "066-2222-1331";
    /// <summary>Una tercera, para el documento de dos personas.</summary>
    private const string CedulaDeTres = "003-1122-4455";

    /// <summary>Lo que trajo se ve con su cuenta, y lo que no casó aparte con la suya.</summary>
    [TestMethod]
    public void LoQueTrajoSeVeConSuCuentaYLoQueNoCasoAparteConLaSuya()
    {
        using var basePrueba = new BaseDelPaquete();
        var sandy = basePrueba.Alta("Sandy");
        var uno = basePrueba.Caso("CASP2609");
        basePrueba.Persona(uno, "Hermana Uno", CedulaDeUno);
        basePrueba.Persona(uno, "Hermano Dos", CedulaDeDos, fila: 2);

        var ruta = basePrueba.Generar(sandy, uno);
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos, "Sí");
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos + 1, "Sí");
        BaseDelPaquete.BorrarLaClave(ruta, Columnas.PrimeraFilaDeDatos + 1);

        var trajo = basePrueba.Vuelta.AplicarYRevisar(sandy, ruta).LoQueTrajo;

        Assert.HasCount(1, trajo.Informaciones, trajo.Linea);
        Assert.HasCount(1, trajo.NoEntraron, trajo.LoQueQuedaFuera);
        Assert.Contains("Sandy trajo 1 información de 1 documento", trajo.Linea, StringComparison.Ordinal);
        Assert.Contains("1 no casó", trajo.LoQueQuedaFuera!, StringComparison.Ordinal);
    }

    /// <summary>El renglón que se pinta lleva los mismos siete campos que se van a firmar.</summary>
    [TestMethod]
    public void ElRenglonQueSePintaLlevaLosSieteCamposQueSeVanAFirmar()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out _, out _);
        var renglon = trajo.Informaciones[0].Renglon;

        Assert.Contains("CASP2609", renglon, StringComparison.Ordinal);
        Assert.Contains("Hermana Uno", renglon, StringComparison.Ordinal);
        Assert.Contains(CedulaDeUno, renglon, StringComparison.Ordinal);
        Assert.Contains("7000014", renglon, StringComparison.Ordinal);
        Assert.Contains("Cuatricentenaria", renglon, StringComparison.Ordinal);
        Assert.Contains("2026-10-15", renglon, StringComparison.Ordinal);
        Assert.Contains("Santo Domingo", renglon, StringComparison.Ordinal);
    }

    /// <summary>Antes de firmar, el botón dice cuántos campos va a firmar.</summary>
    [TestMethod]
    public void AntesDeFirmarDiceCuantosCamposVaAFirmar()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out _, out _);

        var cuenta = basePrueba.Firma.Contar(trajo);

        // Un documento (cinco campos) y una persona (dos): siete.
        Assert.AreEqual(7, cuenta.SeFirman, cuenta.Linea);
        Assert.AreEqual(1, cuenta.Documentos);
        Assert.AreEqual(1, cuenta.Personas);
        Assert.Contains("Va a firmar 7 campos", cuenta.Linea, StringComparison.Ordinal);
    }

    /// <summary>Contar NO escribe nada: si el dueño no sigue, la base queda igual.</summary>
    [TestMethod]
    public void ContarNoFirmaNadaEnLaBase()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out _, out _);

        basePrueba.Firma.Contar(trajo);
        basePrueba.Firma.Contar(trajo);

        Assert.IsEmpty(basePrueba.TodoLoFirmado(),
            "Enseñar la cuenta dejó firmas: entonces «cancelar» no sería cancelar.");
    }

    /// <summary>Al firmar, los campos quedan con el nombre de Miguel y con la fecha del reloj.</summary>
    [TestMethod]
    public void AlFirmarQuedaVerificadoPorMiguelYConSuFecha()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out var casoId, out var personaId);
        var miguel = basePrueba.Alta("Miguel", RolDeCompanero.Administrador);

        var resultado = basePrueba.Firma.Firmar(trajo, miguel);

        Assert.AreEqual(7, resultado.Firmados, resultado.Linea);
        Assert.AreEqual(0, resultado.NoSePudieron);
        Assert.AreEqual(5, basePrueba.Firmados(TablaDeProcedencia.Casos, casoId));
        Assert.AreEqual(2, basePrueba.Firmados(TablaDeProcedencia.Personas, personaId));

        foreach (var campo in basePrueba.TodoLoFirmado())
        {
            Assert.AreEqual(miguel.Id, campo.VerificadoPor, $"«{campo.Campo}» quedó firmado por otro.");
            Assert.AreEqual(BaseDelPaquete.ElInstante, campo.VerificadoEn, $"«{campo.Campo}» sin la fecha del reloj.");
        }
    }

    /// <summary>La firma NO se pone a nombre del compañero que devolvió la hoja.</summary>
    [TestMethod]
    public void LaFirmaNuncaQuedaANombreDelCompaneroQueDevolvioLaHoja()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out _, out _);
        var miguel = basePrueba.Alta("Miguel", RolDeCompanero.Administrador);
        var sandy = basePrueba.Companeros.Activos().Single(quien => quien.Nombre == "Sandy");

        basePrueba.Firma.Firmar(trajo, miguel);

        Assert.IsEmpty(basePrueba.TodoLoFirmado().Where(campo => campo.VerificadoPor == sandy.Id),
            "Un campo quedó firmado con el nombre de Sandy: es la mitad de la regla que no se puede mezclar.");
    }

    /// <summary>El botón NO toca el estado de la recomendación, que lo puso el Excel del compañero.</summary>
    [TestMethod]
    public void ElBotonNoCambiaElEstadoQuePusoElCompanero()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out var casoId, out _);
        var miguel = basePrueba.Alta("Miguel", RolDeCompanero.Administrador);

        var antes = basePrueba.Casos.Obtener(casoId)!;
        basePrueba.Firma.Firmar(trajo, miguel);
        var despues = basePrueba.Casos.Obtener(casoId)!;

        Assert.AreEqual(EstadoDeRecomendacion.Completa, antes.Estado, "La hoja tenía que dejarlo completo.");
        Assert.AreEqual(antes.EstadoRecomendacion, despues.EstadoRecomendacion);
        Assert.AreEqual(antes.EstadoDelCompanero, despues.EstadoDelCompanero);
        Assert.AreEqual(antes.EstadoDelCompaneroPor, despues.EstadoDelCompaneroPor);
        Assert.AreEqual(antes.EstadoMarcadoPor, despues.EstadoMarcadoPor);
        Assert.AreNotEqual(miguel.Id, despues.EstadoMarcadoPor, "El botón escribió el estado a nombre de Miguel.");
    }

    /// <summary>Un documento cuya fila NO vino en el Excel sigue sin firma después de pulsar.</summary>
    [TestMethod]
    public void UnDocumentoQueNoVinoEnElExcelSigueSinFirma()
    {
        using var basePrueba = new BaseDelPaquete();
        var sandy = basePrueba.Alta("Sandy");
        var miguel = basePrueba.Alta("Miguel", RolDeCompanero.Administrador);
        var elQueVa = basePrueba.Caso("CASP2609");
        basePrueba.Persona(elQueVa, "Hermana Uno", CedulaDeUno);
        var elQueNoVa = basePrueba.Caso("BALC2609");
        var quienNoVa = basePrueba.Persona(elQueNoVa, "Hermano Tres", CedulaDeTres);

        var ruta = basePrueba.Generar(sandy, elQueVa);
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos, "Sí");
        var trajo = basePrueba.Vuelta.AplicarYRevisar(sandy, ruta).LoQueTrajo;

        basePrueba.Firma.Firmar(trajo, miguel);

        Assert.AreEqual(0, basePrueba.Firmados(TablaDeProcedencia.Casos, elQueNoVa),
            "Se firmó un documento que no vino en el paquete.");
        Assert.AreEqual(0, basePrueba.Firmados(TablaDeProcedencia.Personas, quienNoVa),
            "Se firmó una persona que no vino en el paquete.");
    }

    /// <summary>La persona de una fila descartada NO se firma, aunque su documento sí volviera.</summary>
    [TestMethod]
    public void LaPersonaDeUnaFilaDescartadaNoSeFirma()
    {
        using var basePrueba = new BaseDelPaquete();
        var sandy = basePrueba.Alta("Sandy");
        var miguel = basePrueba.Alta("Miguel", RolDeCompanero.Administrador);
        var caso = basePrueba.Caso("CASP2609");
        basePrueba.Persona(caso, "Hermana Uno", CedulaDeUno);
        var elQueSeRompe = basePrueba.Persona(caso, "Hermano Dos", CedulaDeDos, fila: 2);

        var ruta = basePrueba.Generar(sandy, caso);
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos, "Sí");
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos + 1, "Sí");
        BaseDelPaquete.BorrarLaClave(ruta, Columnas.PrimeraFilaDeDatos + 1);

        var trajo = basePrueba.Vuelta.AplicarYRevisar(sandy, ruta).LoQueTrajo;
        basePrueba.Firma.Firmar(trajo, miguel);

        Assert.AreEqual(0, basePrueba.Firmados(TablaDeProcedencia.Personas, elQueSeRompe),
            "Se firmó la persona de una fila que no casó.");
    }

    /// <summary>Un campo vacío no se firma: firmar un hueco deja verificado sobre nada.</summary>
    [TestMethod]
    public void UnCampoVacioNoSeFirma()
    {
        using var basePrueba = new BaseDelPaquete();
        var sandy = basePrueba.Alta("Sandy");
        var miguel = basePrueba.Alta("Miguel", RolDeCompanero.Administrador);
        var caso = basePrueba.Caso("CASP2609", templo: null);
        basePrueba.Persona(caso, "Hermana Uno", CedulaDeUno);

        var ruta = basePrueba.Generar(sandy, caso);
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos, "Sí");
        var trajo = basePrueba.Vuelta.AplicarYRevisar(sandy, ruta).LoQueTrajo;

        Assert.AreEqual(6, basePrueba.Firma.Contar(trajo).SeFirman);
        basePrueba.Firma.Firmar(trajo, miguel);

        Assert.IsEmpty(basePrueba.TodoLoFirmado().Where(campo => campo.Campo == "templo_nombre"),
            "Se firmó un campo vacío.");
    }

    /// <summary>Un valor que no cumple su forma tampoco se firma, y las reglas son las de Corrección.</summary>
    [TestMethod]
    public void UnValorConLaFormaEquivocadaNoSeFirma()
    {
        using var basePrueba = new BaseDelPaquete();
        var sandy = basePrueba.Alta("Sandy");
        var miguel = basePrueba.Alta("Miguel", RolDeCompanero.Administrador);
        var caso = basePrueba.Caso("CASP2609", unidadNumero: "no es un número de unidad");
        basePrueba.Persona(caso, "Hermana Uno", CedulaDeUno);

        var ruta = basePrueba.Generar(sandy, caso);
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos, "Sí");
        var trajo = basePrueba.Vuelta.AplicarYRevisar(sandy, ruta).LoQueTrajo;

        var cuenta = basePrueba.Firma.Contar(trajo);
        basePrueba.Firma.Firmar(trajo, miguel);

        Assert.AreEqual(1, cuenta.VaciosOMalos, cuenta.Linea);
        Assert.IsEmpty(basePrueba.TodoLoFirmado().Where(campo => campo.Campo == "unidad_numero"),
            "Se dio por bueno un valor cuya forma ya se sabe mala.");
    }

    /// <summary>Los cinco campos del documento se cuentan UNA vez aunque vuelvan tres hermanos.</summary>
    [TestMethod]
    public void LosCamposDelDocumentoSeCuentanUnaVezAunqueVuelvanTresPersonas()
    {
        using var basePrueba = new BaseDelPaquete();
        var sandy = basePrueba.Alta("Sandy");
        var caso = basePrueba.Caso("CASP2609");
        basePrueba.Persona(caso, "Hermana Uno", CedulaDeUno);
        basePrueba.Persona(caso, "Hermano Dos", CedulaDeDos, fila: 2);
        basePrueba.Persona(caso, "Hermano Tres", CedulaDeTres, fila: 3);

        var ruta = basePrueba.Generar(sandy, caso);
        for (var fila = 0; fila < 3; fila++)
            BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos + fila, "Sí");

        var trajo = basePrueba.Vuelta.AplicarYRevisar(sandy, ruta).LoQueTrajo;
        var cuenta = basePrueba.Firma.Contar(trajo);

        // Cinco del documento, una sola vez, más dos por cada una de las tres personas.
        Assert.AreEqual(11, cuenta.SeFirman, cuenta.Linea);
        Assert.AreEqual(1, cuenta.Documentos);
        Assert.AreEqual(3, cuenta.Personas);
    }

    /// <summary>Un campo ya firmado no se vuelve a firmar: pisaría la fecha de quien lo hizo.</summary>
    [TestMethod]
    public void UnCampoYaFirmadoNoSeVuelveAFirmar()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out _, out _);
        var miguel = basePrueba.Alta("Miguel", RolDeCompanero.Administrador);

        basePrueba.Firma.Firmar(trajo, miguel);
        var segunda = basePrueba.Firma.Contar(trajo);
        var otraVez = basePrueba.Firma.Firmar(trajo, miguel);

        Assert.AreEqual(0, segunda.SeFirman, segunda.Linea);
        Assert.AreEqual(7, segunda.YaFirmados);
        Assert.AreEqual(0, otraVez.Firmados);
        Assert.IsFalse(segunda.HayAlgoQueFirmar);
    }

    /// <summary>
    /// El botón firma EXACTAMENTE la lista de campos que dibuja la pantalla de Corrección.
    /// </summary>
    /// <remarks>
    /// Si las dos listas se separan, el botón firmaría campos que Miguel no ve en ninguna
    /// pantalla, o dejaría sin firmar los que sí ve. Es el mismo defecto que ya costó
    /// <c>templo_nombre</c> el 2026-09-05, medido: 0 filas de procedencia de 7 documentos.
    /// </remarks>
    [TestMethod]
    public void ElBotonFirmaLosMismosCamposQueDibujaCorreccion()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out var casoId, out var personaId);

        var campos = CamposDelPaquete.De(trajo.Informaciones);

        CollectionAssert.AreEqual(
            ModeloDeCorreccion.CamposDelCasoQueSeDibujan.ToArray(),
            campos.Where(campo => campo.Tabla == TablaDeProcedencia.Casos && campo.RegistroId == casoId)
                  .Select(campo => campo.Campo).ToArray());
        CollectionAssert.AreEqual(
            ModeloDeCorreccion.CamposDeLaPersonaQueSeDibujan.ToArray(),
            campos.Where(campo => campo.Tabla == TablaDeProcedencia.Personas && campo.RegistroId == personaId)
                  .Select(campo => campo.Campo).ToArray());
    }

    /// <summary>Cuando el agente no pudo con todos, el sistema lo dice solo y con su nombre.</summary>
    /// <remarks>
    /// Son las palabras del dueño del 2026-09-05: «Sandy no pudo verificar todos, por favor
    /// verifica qué falta».
    /// </remarks>
    [TestMethod]
    public void CuandoElAgenteNoPudoConTodosSeDiceConSuNombre()
    {
        using var basePrueba = new BaseDelPaquete();
        var sandy = basePrueba.Alta("Sandy");
        var completo = basePrueba.Caso("CASP2609");
        basePrueba.Persona(completo, "Hermana Uno", CedulaDeUno);
        var aMedias = basePrueba.Caso("BALC2609");
        basePrueba.Persona(aMedias, "Hermano Tres", CedulaDeTres);

        var ruta = basePrueba.Generar(sandy, completo, aMedias);
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos, "Sí");
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos + 1, "No");

        var trajo = basePrueba.Vuelta.AplicarYRevisar(sandy, ruta).LoQueTrajo;

        Assert.AreEqual(2, trajo.Documentos);
        Assert.AreEqual(1, trajo.DocumentosCompletos);
        Assert.AreEqual("Sandy no pudo verificar todos: 1 documento sin completar. "
            + "Por favor verifique qué falta.", trajo.LoQueFalta);
    }

    /// <summary>Cuando el agente sí pudo con todos, no se le pide nada a nadie.</summary>
    [TestMethod]
    public void CuandoElAgentePudoConTodosNoSePideNada()
    {
        using var basePrueba = new BaseDelPaquete();
        var trajo = UnPaqueteDeVuelta(basePrueba, out _, out _);

        Assert.IsNull(trajo.LoQueFalta, trajo.Linea);
        Assert.AreEqual(1, trajo.DocumentosCompletos);
    }

    /// <summary>Sin administrador no hay a nombre de quién firmar, y se dice sin nombrar a nadie.</summary>
    /// <remarks>
    /// La regla es la de <see cref="ElAdministrador"/>, medida: la tabla <c>companeros</c> de
    /// la base del dueño tiene una sola fila y no es la suya. Coger «el primer activo» dejaría
    /// el paquete firmado por Sandy.
    /// </remarks>
    [TestMethod]
    public void SinAdministradorNoHayANombreDeQuienFirmar()
    {
        using var basePrueba = new BaseDelPaquete();
        UnPaqueteDeVuelta(basePrueba, out _, out _);
        var activos = basePrueba.Companeros.Activos();

        Assert.IsNull(ElAdministrador.De(activos));
        Assert.DoesNotContain("Sandy", ElAdministrador.PorQueNoSePuede(activos).Detalle!, StringComparison.Ordinal);
    }

    /// <summary>Una vuelta vacía no ofrece nada que firmar y lo dice.</summary>
    [TestMethod]
    public void UnaVueltaSinNadaQueRevisarNoOfreceFirmar()
    {
        using var basePrueba = new BaseDelPaquete();
        var vacio = LoQueTrajoElPaquete.Nada("Sandy");

        var cuenta = basePrueba.Firma.Contar(vacio);

        Assert.IsFalse(vacio.HayQueRevisar);
        Assert.IsFalse(cuenta.HayAlgoQueFirmar);
        Assert.Contains("No queda ningún campo", cuenta.Linea, StringComparison.Ordinal);
    }

    /// <summary>Un paquete de un documento con una persona, contestado que sí y ya devuelto.</summary>
    /// <param name="basePrueba">El banco de la prueba.</param>
    /// <param name="casoId">El documento que se dio de alta.</param>
    /// <param name="personaId">La persona que se le colgó.</param>
    private static LoQueTrajoElPaquete UnPaqueteDeVuelta(
        BaseDelPaquete basePrueba, out long casoId, out long personaId)
    {
        var sandy = basePrueba.Alta("Sandy");
        casoId = basePrueba.Caso("CASP2609");
        personaId = basePrueba.Persona(casoId, "Hermana Uno", CedulaDeUno);

        var ruta = basePrueba.Generar(sandy, casoId);
        BaseDelPaquete.Contestar(ruta, Columnas.PrimeraFilaDeDatos, "Sí");
        return basePrueba.Vuelta.AplicarYRevisar(sandy, ruta).LoQueTrajo;
    }
}
