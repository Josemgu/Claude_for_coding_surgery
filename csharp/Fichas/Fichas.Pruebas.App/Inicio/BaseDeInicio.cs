using Fichas.App.Grupo;
using Fichas.App.Inicio;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// Lo comun a las pruebas de Inicio, del grupo que viaja y de lo que no esta completo.
/// </summary>
/// <remarks>
/// Todo se prueba SIN ABRIR VENTANA (ADR-0003 §8.1): lo que se comprueba es la REGLA de
/// cada pantalla, y por eso las reglas viven en <c>LectorDelInicio</c>,
/// <c>LectorDeGrupos</c> y <c>LectorDeIncompletos</c>, que solo hablan con los contratos.
/// </remarks>
public static class BaseDeInicio
{
    /// <summary>El dia en el que se paran todas estas pruebas; un viernes, para que la semana cruce el mes.</summary>
    public const string Hoy = "2026-09-04";

    /// <summary>Cuantos casos inventados usa la mayoria de las pruebas; suficiente para que haya de todo.</summary>
    public const int CasosDePrueba = 300;

    /// <summary>La semilla; la misma semilla da siempre exactamente la misma base.</summary>
    public const int Semilla = 20260904;

    /// <summary>Monta los servicios falsos con el reloj parado, para que el calendario no cambie manana.</summary>
    /// <param name="cuantosCasos">Cuántos casos inventa el generador.</param>
    public static ServiciosFalsos MontarServicios(int cuantosCasos)
        => new(cuantosCasos, Semilla, new RelojFijo(Hoy));

    /// <summary>Lee el resumen de Inicio tal como lo lee la pagina al llegar.</summary>
    /// <param name="servicios">Los servicios falsos.</param>
    /// <param name="desplazamientoDeMes">Cuántos meses mover el calendario; 0 es el de hoy.</param>
    public static ResumenDeInicio LeerInicio(ServiciosFalsos servicios, int desplazamientoDeMes = 0)
        => LectorDe(servicios).Leer(desplazamientoDeMes);

    /// <summary>El lector de Inicio atado a unos servicios falsos.</summary>
    /// <param name="servicios">Los servicios falsos.</param>
    public static LectorDelInicio LectorDe(ServiciosFalsos servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        return new LectorDelInicio(
            servicios.Casos, servicios.Personas, servicios.Companeros, servicios.Asignaciones,
            servicios.Reloj, servicios.Procedencia);
    }

    /// <summary>
    /// El lector de grupos, con el disco simulado.
    /// </summary>
    /// <param name="servicios">Los servicios falsos.</param>
    /// <param name="losPdfExisten">
    /// Si los PDF se dan por presentes. Se pasa desde fuera a proposito: una prueba que
    /// preguntara al disco de verdad diria una cosa en esta maquina y otra en la siguiente.
    /// </param>
    public static LectorDeGrupos LectorDeGruposDe(ServiciosFalsos servicios, bool losPdfExisten = true)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        return new LectorDeGrupos(
            servicios.Casos, servicios.Personas, servicios.Asignaciones, servicios.Companeros,
            servicios.Procedencia, ruta => losPdfExisten);
    }

    /// <summary>El lector de lo que no esta completo.</summary>
    /// <param name="servicios">Los servicios falsos.</param>
    public static LectorDeIncompletos LectorDeIncompletosDe(ServiciosFalsos servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        return new LectorDeIncompletos(
            servicios.Casos, servicios.Personas, servicios.Companeros, servicios.Asignaciones,
            servicios.Reloj, servicios.Procedencia);
    }

    /// <summary>Trae todos los casos de la base, archivados incluidos.</summary>
    /// <param name="servicios">Los servicios falsos.</param>
    public static IReadOnlyList<Caso> TodosLosCasos(ServiciosFalsos servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        var filtro = FiltroDeCasos.Todo with { IncluirArchivados = true };
        return servicios.Casos.Listar(filtro, new Pagina(0, servicios.Casos.Contar(filtro))).Elementos;
    }

    /// <summary>
    /// Mete en la base inventada un documento a medida, con TODOS sus campos llenos.
    /// </summary>
    /// <remarks>
    /// Nace lleno a proposito: asi cada prueba vacia SOLO el campo que quiere probar, y
    /// cuando falla se sabe cual era. Un documento que naciera medio vacio haria que
    /// «no esta listo» fuera cierto por muchas razones a la vez.
    /// </remarks>
    /// <param name="servicios">Donde se mete.</param>
    /// <param name="numeroCaso">El numero del documento.</param>
    /// <param name="fechaViaje">Su fecha de viaje, o nula.</param>
    /// <param name="estado">«completa», «no_completa» o nulo.</param>
    /// <param name="archivado">Si nace archivado.</param>
    /// <param name="cuantasPersonas">Cuantas personas lleva.</param>
    /// <param name="unidadNumero">El numero de unidad, que es lo que parte el dia en grupos.</param>
    /// <param name="temploNombre">El templo; ponerlo nulo es la forma de dejarle un hueco.</param>
    /// <param name="sinCedula">Si las personas nacen sin cedula, que es otro hueco.</param>
    /// <returns>El id del documento que se acaba de meter.</returns>
    public static long MeterCaso(
        ServiciosFalsos servicios,
        string numeroCaso,
        string? fechaViaje,
        string? estado = null,
        bool archivado = false,
        int cuantasPersonas = 1,
        string unidadNumero = "7000011",
        string? temploNombre = "Santo Domingo Dominican Republic",
        bool sinCedula = false)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        var casoId = servicios.Almacen.SiguienteId();
        servicios.Almacen.Casos[casoId] = new Caso
        {
            Id = casoId,
            NumeroCaso = numeroCaso,
            FechaViaje = fechaViaje,
            EstadoRecomendacion = estado,
            Archivado = archivado,
            FechaArchivado = archivado ? "2026-09-01" : null,
            CreadoEn = "2026-08-01",
            UnidadNumero = unidadNumero,
            UnidadNombre = "Rama de Prueba",
            TemploNombre = temploNombre,
            RutaPdf = @"C:\Users\josem\Documents\Fichas\pdf\prueba.pdf",
            PaginaPdf = 1,
        };

        AnotarLaProcedencia(
            servicios, TablaDeProcedencia.Casos, casoId,
            LoQueLeFalta.ColumnasDelCaso,
            [numeroCaso, unidadNumero, "Rama de Prueba", fechaViaje, temploNombre]);

        for (var fila = 1; fila <= cuantasPersonas; fila++)
        {
            var personaId = servicios.Almacen.SiguienteId();
            var cedula = sinCedula ? null : $"055-1111-38{fila:D2}";
            servicios.Almacen.Personas[personaId] = new Persona
            {
                Id = personaId,
                CasoId = casoId,
                Nombre = $"Persona {fila}",
                Mrn = cedula,
                FilaFormulario = fila,
            };

            AnotarLaProcedencia(
                servicios, TablaDeProcedencia.Personas, personaId,
                LoQueLeFalta.ColumnasDeLaPersona,
                [cedula, $"Persona {fila}"]);
        }

        return casoId;
    }

    /// <summary>
    /// Deja anotado de donde salio cada campo de un documento fabricado a mano.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Sin esto, TODO documento fabricado a mano sale incompleto, y con razon.</b>
    /// Desde el 2026-09-06 el veredicto de «listo para asignar» mira la procedencia, y un
    /// campo con valor y SIN fila es uno de los seis casos medidos: no consta de donde salio
    /// su valor y encima no se puede firmar. Doce pruebas se pusieron rojas el dia que se
    /// unifico el veredicto, y ninguna era un fallo del programa: era este ayudante
    /// fabricando documentos que la importacion de verdad nunca produce.
    /// <para>
    /// ⛔ Se escribe por <c>IProcedencia.Anotar</c>, que <b>nunca</b> pone verificado (regla
    /// permanente 5): estos documentos nacen leidos, no firmados.
    /// </para>
    /// <para>
    /// Un campo con valor nace del OCR con 0,99 —leido y bien, que es lo que estas pruebas
    /// quieren decir con «nace lleno»—, y uno vacio nace con origen «vacio» y sin confianza,
    /// que es lo que la importacion escribe cuando el lector no leyo nada.
    /// </para>
    /// </remarks>
    /// <param name="servicios">Donde se anota.</param>
    /// <param name="tabla">Si el registro es un caso o una persona.</param>
    /// <param name="registroId">El id del caso o de la persona.</param>
    /// <param name="columnas">Los nombres de columna, en orden.</param>
    /// <param name="valores">El valor de cada columna, en el mismo orden; nulo o en blanco es un hueco.</param>
    private static void AnotarLaProcedencia(
        ServiciosFalsos servicios,
        TablaDeProcedencia tabla,
        long registroId,
        IReadOnlyList<string> columnas,
        IReadOnlyList<string?> valores)
    {
        for (var i = 0; i < columnas.Count; i++)
        {
            var hayValor = !string.IsNullOrWhiteSpace(valores[i]);
            servicios.Procedencia.Anotar(new ProcedenciaDeCampo
            {
                Tabla = tabla,
                RegistroId = registroId,
                Campo = columnas[i],
                Origen = hayValor ? OrigenDeCampo.Ocr : OrigenDeCampo.Vacio,
                Confianza = hayValor ? 0.99 : null,
            });
        }
    }

    /// <summary>Ata un documento a un companero, como si se le hubiera asignado.</summary>
    /// <remarks>Escribe directo en el almacén, sin pasar por el motor: no se comprueba nada.</remarks>
    /// <param name="servicios">Donde se escribe.</param>
    /// <param name="casoId">El documento.</param>
    /// <param name="companeroId">A quién.</param>
    /// <returns>El id de la asignación.</returns>
    public static long Asignar(ServiciosFalsos servicios, long casoId, long companeroId)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        var id = servicios.Almacen.SiguienteId();
        servicios.Almacen.Asignaciones[id] = new Asignacion
        {
            Id = id,
            CasoId = casoId,
            CompaneroId = companeroId,
            AsignadoEn = Hoy,
            Activa = true,
        };
        return id;
    }

    /// <summary>El id del primer companero activo de la base inventada.</summary>
    /// <param name="servicios">Los servicios falsos.</param>
    public static long PrimerCompaneroActivo(ServiciosFalsos servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        return servicios.Companeros.Activos()[0].Id;
    }

    /// <summary>
    /// Contesta las seis preguntas del sistema del obispo de UNA persona de un documento.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Existe porque sin el no se puede demostrar nada de la FASE C18.</b> Los siete
    /// escaneos reales del dueno traen UNA persona cada uno (medicion del supervisor,
    /// 2026-09-04), asi que sobre sus datos de hoy contar por documentos y contar por
    /// personas da el MISMO numero. Para ver el cambio de unidad hace falta un documento con
    /// varias personas y con las seis preguntas distintas entre ellas, y eso hay que
    /// fabricarlo (criterio C18-4).
    /// <para>
    /// Lo que NO se toca aqui: <c>casos.estado_recomendacion</c>. Se contestan las seis
    /// columnas de la persona y nada mas, que es exactamente lo que hara la pantalla de la
    /// FASE C19.
    /// </para>
    /// </remarks>
    /// <param name="servicios">Donde vive la base inventada.</param>
    /// <param name="casoId">El documento del que es la persona.</param>
    /// <param name="fila">Su fila del formulario, la misma que le puso <see cref="MeterCaso"/>.</param>
    /// <param name="preparacion">Paso 1.</param>
    /// <param name="informacion">Paso 2.</param>
    /// <param name="citaDelTemplo">Paso 3.</param>
    /// <param name="accionesRequeridas">Paso 4.</param>
    /// <param name="entrevistas">Paso 5.</param>
    /// <param name="listoParaElTemplo">Paso 6.</param>
    public static void ContestarLasSeisDe(
        ServiciosFalsos servicios,
        long casoId,
        int fila,
        bool? preparacion = null,
        bool? informacion = null,
        bool? citaDelTemplo = null,
        bool? accionesRequeridas = null,
        bool? entrevistas = null,
        bool? listoParaElTemplo = null)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        var cual = servicios.Almacen.Personas
            .First(p => p.Value.CasoId == casoId && p.Value.FilaFormulario == fila);

        servicios.Almacen.Personas[cual.Key] = cual.Value with
        {
            PasoPreparacion = preparacion,
            PasoInformacion = informacion,
            PasoCitaDelTemplo = citaDelTemplo,
            PasoAccionesRequeridas = accionesRequeridas,
            PasoEntrevistas = entrevistas,
            PasoListoParaElTemplo = listoParaElTemplo,
        };
    }

    /// <summary>Deja a una persona con sus seis preguntas en si: lista para viajar.</summary>
    /// <param name="servicios">Donde vive la base inventada.</param>
    /// <param name="casoId">El documento del que es la persona.</param>
    /// <param name="fila">Su fila del formulario.</param>
    public static void DejarListaParaViajar(ServiciosFalsos servicios, long casoId, int fila)
        => ContestarLasSeisDe(servicios, casoId, fila, true, true, true, true, true, true);

    /// <summary>
    /// Deja a una persona con cinco preguntas en si y la quinta en no: no lista, y se sabe
    /// en cual se quedo.
    /// </summary>
    /// <param name="servicios">Donde vive la base inventada.</param>
    /// <param name="casoId">El documento del que es la persona.</param>
    /// <param name="fila">Su fila del formulario.</param>
    public static void DejarNoListaParaViajar(ServiciosFalsos servicios, long casoId, int fila)
        => ContestarLasSeisDe(servicios, casoId, fila, true, true, true, true, false, true);
}
