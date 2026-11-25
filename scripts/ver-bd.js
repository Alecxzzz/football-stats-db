/* Resumen rápido de la base de datos del visor.
 *
 * Uso (Git Bash, PowerShell o CMD):
 *   mongosh "mongodb://localhost:27017/threesixtybets_db" --file scripts/ver-bd.js
 */

const base = db.getSiblingDB("threesixtybets_db");

const colecciones = [
  { nombre: "PartidosFm", uso: "partidos del día traídos de FotMob" },
  { nombre: "LigasFm", uso: "competiciones y su id de FotMob" },
  { nombre: "Alineaciones", uso: "onces publicados, guardados por partido" },
  { nombre: "Estadisticas", uso: "posesión, tiros, córners… por partido" },
  { nombre: "Eventos", uso: "goles, tarjetas y cambios" },
  { nombre: "Jugadores", uso: "un documento por jugador y partido" },
  { nombre: "ConsultasApi", uso: "log de peticiones (TTL 7 días)" },
];

print("");
print("Base de datos: " + base.getName());
print("Colecciones del visor");
print("--------------------");

for (const c of colecciones) {
  const total = base.getCollection(c.nombre).countDocuments();
  const indices = base
    .getCollection(c.nombre)
    .getIndexes()
    .map((i) => i.name)
    .join(", ");
  print(`${c.nombre.padEnd(14)} ${String(total).padStart(5)} docs  · ${c.uso}`);
  print(`${"".padEnd(14)} índices: ${indices}`);
}

print("");
print("Vistazo a los datos");
print("-------------------");

const enVivo = base.PartidosFm.countDocuments({ estado: "in" });
const terminados = base.PartidosFm.countDocuments({ estado: "post" });
print(`Partidos en vivo: ${enVivo} · terminados: ${terminados}`);

const ultimo = base.PartidosFm.find().sort({ actualizado: -1 }).limit(1).toArray()[0];
if (ultimo) {
  print("Último partido guardado:");
  printjson({
    matchId: ultimo.match_id,
    dia: ultimo.dia,
    liga: ultimo.liga,
    partido: `${ultimo.local} ${ultimo.goles_local ?? "-"} : ${ultimo.goles_visitante ?? "-"} ${ultimo.visitante}`,
    estado: ultimo.estado,
    actualizado: ultimo.actualizado,
  });
}

const once = base.Alineaciones.find().sort({ guardado: -1 }).limit(1).toArray()[0];
if (once) {
  print("Última alineación guardada:");
  printjson({
    match_id: once.match_id,
    partido: `${once.local} vs ${once.visitante}`,
    formaciones: `${once.formacion_local} / ${once.formacion_visita}`,
    titulares: once.titulares,
    suplentes: once.suplentes,
    guardado: once.guardado,
  });
}

const mejores = base.Jugadores.find({ nota: { $ne: null } }).sort({ nota: -1 }).limit(3).toArray();
if (mejores.length) {
  print("Mejores notas guardadas:");
  mejores.forEach((j) => print(`  ${j.nota}  ${j.nombre} (${j.equipo}) · partido ${j.match_id}`));
}

const consultas = base.ConsultasApi.find().sort({ fecha: -1 }).limit(3).toArray();
if (consultas.length) {
  print("Últimas consultas al puente:");
  consultas.forEach((c) => print(`  ${c.estado}  ${c.ms} ms  ${c.ruta}${c.consulta}`));
}

print("");
