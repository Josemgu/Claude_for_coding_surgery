using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.Reportes;

/// <summary>
/// La base inventada con la que se miden los reportes, y los retoques que necesita.
/// </summary>
/// <remarks>
/// ⚠️ <see cref="GeneradorFalso"/> deja SIEMPRE <c>PudoViajar</c> y <c>MotivoNoViajo</c>
/// en nulo y no escribe ni una fila de procedencia (medido leyendo el generador). Con esa
/// base tal cual, la Parte 2 del reporte y las tres metricas del final saldrian vacias
/// SIEMPRE, y una prueba sobre ellas pasaria en verde sin comprobar nada. Aqui se
/// retocan las copias del almacen —nunca el proyecto Fichas.Datos.Falso, que esta
/// congelado— para que haya algo de verdad que contar.
///
/// Ningun dato de aqui es de nadie: sale entero del generador determinista.
/// </remarks>
internal static class BaseDePrueba
{
    /// <summary>Un reloj fijo: sin el, «ya viajo» cambia de respuesta segun el dia que se corra.</summary>
    /// <param name="hoy">El día que el reloj dirá siempre, en «AAAA-MM-DD».</param>
    internal sealed class RelojFijo(string hoy) : IReloj
    {
        /// <summary>El día fijo, tal cual se le dio.</summary>
        public string Hoy() => hoy;

        /// <summary>El día fijo a las diez de la mañana, siempre la misma hora.</summary>
        public string Ahora() => $"{hoy} 10:00:00";

        /// <summary>El día fijo desplazado esos días; negativo va hacia atrás.</summary>
        /// <param name="dias">Cuántos días sumar.</param>
        public string HoyMasDias(int dias)
            => DateOnly.ParseExact(hoy, "yyyy-MM-dd").AddDays(dias).ToString("yyyy-MM-dd");
    }

    /// <summary>El dia con el que se generan todas las bases de estas pruebas.</summary>
    internal const string Hoy = "2026-09-20";

    /// <summary>Monta los servicios falsos con reloj fijo y les anade lo que el generador no pone.</summary>
    /// <param name="cuantosCasos">Cuántos casos genera la base falsa.</param>
    /// <param name="semilla">La semilla del generador; con la misma salen los mismos datos.</param>
    internal static ServiciosFalsos Montar(int cuantosCasos, int semilla = 20260904)
    {
        var servicios = new ServiciosFalsos(cuantosCasos, semilla, new RelojFijo(Hoy));
        AnotarQuienNoPudoViajar(servicios.Almacen);
        FirmarAlgunosCampos(servicios.Almacen);
        return servicios;
    }

    /// <summary>Anota a una de cada siete personas como que no pudo viajar, con su motivo.</summary>
    /// <remarks>Y a la siguiente como que sí pudo; las otras cinco se quedan en nulo, que es lo que trae el generador.</remarks>
    /// <param name="almacen">El almacén falso que se retoca en sitio.</param>
    private static void AnotarQuienNoPudoViajar(AlmacenFalso almacen)
    {
        var orden = 0;
        foreach (var id in almacen.Personas.Keys.OrderBy(k => k).ToList())
        {
            var persona = almacen.Personas[id];
            var suerte = orden++ % 7;
            almacen.Personas[id] = suerte switch
            {
                0 => persona with { PudoViajar = false, MotivoNoViajo = "La recomendación venció la semana antes del viaje." },
                1 => persona with { PudoViajar = true },
                _ => persona,
            };
        }
    }

    /// <summary>Firma todos los campos de uno de cada cinco casos, para que las metricas cuenten algo.</summary>
    /// <param name="almacen">El almacén falso que se retoca en sitio.</param>
    private static void FirmarAlgunosCampos(AlmacenFalso almacen)
    {
        var companeroId = almacen.Companeros.Keys.Order().First();
        var orden = 0;
        foreach (var casoId in almacen.Casos.Keys.OrderBy(k => k).ToList())
        {
            if (orden++ % 5 != 0) continue;
            var caso = almacen.Casos[casoId];
            // La marca de verificacion cae dos dias antes del viaje cuando lo hay:
            // asi la metrica 3 tiene casos «vistos a tiempo» de verdad que contar.
            var cuando = caso.FechaViaje is null
                ? $"{almacen.Reloj.HoyMasDias(-5)} 09:00:00"
                : $"{DateOnly.ParseExact(caso.FechaViaje, "yyyy-MM-dd").AddDays(-2):yyyy-MM-dd} 09:00:00";

            Anotar(almacen, TablaDeProcedencia.Casos, casoId, nameof(Caso.NumeroCaso), companeroId, cuando);
            Anotar(almacen, TablaDeProcedencia.Casos, casoId, nameof(Caso.FechaViaje), companeroId, cuando);
            foreach (var persona in almacen.PersonasDe(casoId))
            {
                Anotar(almacen, TablaDeProcedencia.Personas, persona.Id, nameof(Persona.Nombre), companeroId, cuando);
                Anotar(almacen, TablaDeProcedencia.Personas, persona.Id, nameof(Persona.Mrn), companeroId, cuando);
            }
        }
    }

    /// <summary>Escribe una fila de procedencia ya firmada para ese campo.</summary>
    /// <param name="almacen">El almacén falso.</param>
    /// <param name="tabla">Si el campo es de un caso o de una persona.</param>
    /// <param name="registroId">El id del caso o de la persona.</param>
    /// <param name="campo">El nombre del campo, tal como lo llama el modelo.</param>
    /// <param name="companeroId">Quién firma.</param>
    /// <param name="cuando">La marca «AAAA-MM-DD HH:mm:ss» de la firma.</param>
    private static void Anotar(
        AlmacenFalso almacen, TablaDeProcedencia tabla, long registroId, string campo, long companeroId, string cuando)
    {
        var id = almacen.SiguienteId();
        almacen.Procedencias[id] = new ProcedenciaDeCampo
        {
            Id = id,
            Tabla = tabla,
            RegistroId = registroId,
            Campo = campo,
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.93,
            Verificado = true,
            VerificadoPor = companeroId,
            VerificadoEn = cuando,
        };
    }
}
