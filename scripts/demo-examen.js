/* Demostración para la exposición.
 *
 * Uso:
 *   mongosh "mongodb://localhost:27017/threesixtybets_db" --file scripts/demo-examen.js
 *
 * Recorre las siete colecciones del visor, enseña datos reales y termina con
 * una prueba de escritura + lectura para demostrar que la base "hace algo".
 */

const base = db.getSiblingDB("threesixtybets_db");
const linea = (t) => print("\n=== " + t + " ===\n");

linea("1. Conexión y base de datos");
print("Servidor: localhost:27017");
print("Base:     " + base.getName());
print("Colecciones: " + base.getCollectionNames().sort().join(", "));

linea("2. Las siete colecciones y cuánto guardan");
const nombres = ["PartidosFm", "LigasFm", "Alineaciones", "Estadisticas", "Eventos", "Jugadores", "ConsultasApi"];
for (const n of nombres) {
  print(n.padEnd(14) + base.getCollection(n).countDocuments() + " documentos");
}

linea("3. Partidos guardados (PartidosFm)");
const total = base.PartidosFm.countDocuments();
const porEstado = base.PartidosFm
  .aggregate([{ $group: { _id: "$estado", partidos: { $sum: 1 } } }, { $sort: { partidos: -1 } }])
  .toArray();
print("Total: " + total);
porEstado.forEach((e) => print("  " + e._id + " -> " + e.partidos));

const muestra = base.PartidosFm.find({}, { _id: 0, local: 1, goles_local: 1, goles_visitante: 1, visitante: 1, liga: 1, estado: 1 })
  .sort({ actualizado: -1 })
  .limit(3)
  .toArray();
print("\nÚltimos tres guardados:");
muestra.forEach((p) =>
  print(`  ${p.liga}: ${p.local} ${p.goles_local ?? "-"} : ${p.goles_visitante ?? "-"} ${p.visitante}  [${p.estado}]`)
);

linea("4. Alineación guardada (Alineaciones)");
const once = base.Alineaciones.find().sort({ guardado: -1 }).limit(1).toArray()[0];
if (once) {
  print(`Partido ${once.match_id}: ${once.local} vs ${once.visitante}`);
  print(`Formaciones: ${once.formacion_local} / ${once.formacion_visita}`);
  print(`Titulares: ${once.titulares} · Suplentes: ${once.suplentes}`);
  print(`Guardada el: ${once.guardado}`);
  print("Contenido (sin el json):");
  printjson({
    local: once.local,
    visitante: once.visitante,
    formacion_local: once.formacion_local,
    formacion_visita: once.formacion_visita,
    titulares: once.titulares,
    suplentes: once.suplentes,
  });
} else {
  print("Todavía no hay alineaciones guardadas: abre un partido en el visor.");
}

linea("5. Estadísticas y eventos del mismo partido");
const ref = once ? once.match_id : base.Estadisticas.find().limit(1).toArray()[0]?.match_id;
if (ref) {
  const est = base.Estadisticas.findOne({ match_id: ref }, { json: 0 });
  const ev = base.Eventos.findOne({ match_id: ref }, { json: 0 });
  print("Estadísticas: " + (est ? `${est.liga} · guardadas ${est.guardado}` : "sin datos"));
  print("Eventos:      " + (ev ? `${ev.total} en total, ${ev.goles} goles` : "sin datos"));
}

linea("6. Jugadores (un documento por jugador y partido)");
const jugadores = base.Jugadores.find({}, { _id: 0, nombre: 1, equipo: 1, dorsal: 1, match_id: 1 }).limit(5).toArray();
print("Guardados: " + base.Jugadores.countDocuments() + " (mostrando 5)");
jugadores.forEach((j) => print(`  #${j.dorsal ?? "-"}  ${j.nombre} (${j.equipo}) · partido ${j.match_id}`));

linea("7. Índices: lo que hace rápida la base");
nombres.forEach((n) => {
  const idx = base.getCollection(n).getIndexes().map((i) => i.name).join(", ");
  print(n.padEnd(14) + idx);
});

linea("8. Prueba de escritura y lectura (la base trabaja de verdad)");
const prueba = { match_id: 999999999, local: "Equipo Demo", visitante: "Equipo Examen", estado: "pre", demo: true };
base.PartidosFm.insertOne(prueba);
print("Insertado un documento de prueba.");
print("Encontrado por match_id: " + JSON.stringify(base.PartidosFm.findOne({ match_id: 999999999 }, { _id: 0, local: 1, visitante: 1 })));
base.PartidosFm.deleteOne({ match_id: 999999999 });
print("Borrado. Documentos otra vez: " + base.PartidosFm.countDocuments());

linea("9. Consultas del puente (ConsultasApi, con TTL de 7 días)");
base.ConsultasApi
  .find()
  .sort({ fecha: -1 })
  .limit(3)
  .toArray()
  .forEach((c) => print(`  ${c.estado}  ${c.ms} ms  ${c.ruta}${c.consulta}`));

print("\nDemostración lista.\n");
