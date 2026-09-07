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
    internal sealed class RelojFijo(string hoy) : IReloj
    {
        public string Hoy() => hoy;

        public string Ahora() => $"{hoy} 10:00:00";

        public string HoyMasDias(int dias)
            => DateOnly.ParseExact(hoy, "yyyy-MM-dd").AddDays(dias).ToString("yyyy-MM-dd");
    }

    /// <summary>El dia con el que se generan todas las bases de estas pruebas.</summary>
    internal const string Hoy = "2026-09-20";

    /// <summary>Monta los servicios falsos con reloj fijo y les anade lo que el generador no pone.</summary>
    internal static ServiciosFalsos Montar(int cuantosCasos, int semilla = 20260904)
    {
        var servicios = new ServiciosFalsos(cuantosCasos, semilla, new RelojFijo(Hoy));
        AnotarQuienNoPudoViajar(servicios.Almacen);
        FirmarAlgunosCampos(servicios.Almacen);
        return servicios;
    }

    /// <summary>Anota a una de cada siete personas como que no pudo viajar, con su motivo.</summary>
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
