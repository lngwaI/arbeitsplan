/* Erfundene Testdaten. Wochen relativ zu heute. */
var ROWS = [
  { name: "Suite 1", lkz: true }, { name: "Mix / Split", lkz: true },
  { name: "Suite 2", lkz: true }, { name: "Puffer", lkz: false },
  { name: "Lyo", lkz: true }, { name: "Freeze / Thaw", lkz: false },
  { name: "Versand", lkz: false }, { name: "Einwaage", lkz: false },
  { name: "CIP / SIP", lkz: false }, { name: "Raum CC", lkz: false }
];
var STATUS_COLORS = {
  "Gereinigt": "#7030A0", "Regeneriert": "#000080", "Sterilisiert": "#FF0000",
  "InProzess": "#00A000", "Freigabe": "#3EA9F5", "Quarantaene": "#8B0000",
  "NichtGereinigt": "#000000", "unbekannt": "#000000"
};
/* Erfundener Status je Equipment-Nummer */
var EQUIP_STATUS = {
  "H1-T-0101": "InProzess", "H1-T-0102": "Gereinigt", "H1-F-0201": "Sterilisiert",
  "H2-X-0301": "Freigabe", "H2-P-0401": "Regeneriert", "H1-X-0501-02": "Quarantaene",
  "H2-T-0602": "NichtGereinigt", "H1-P-0701": "Gereinigt"
};
var DAYS_DATA = (function () {
  var T = [
    ["Suite 1", "Lauf 1: Prozesstank H1-T-0101\nPumpe H1-P-0701 (Ausweich: H2-P-0401)\nSchläuche prüfen", ["MUST", "BEIS"], ["TEST"], ["NACH"], true],
    ["Mix / Split", "Mischen 50 % Puffer\nVerteilerstück H1-X-0501-02", ["ABCD"], [], ["WXYZ"], false],
    ["Suite 2", "Reinigung H1-T-0102 – Freigabe „gelesen“\nNotiz an QS: Größe ok @ 14:00", ["EFGH", "IJKL", "MNOP"], ["QRST"], [], true],
    ["Puffer", "", [], [], [], false],
    ["Lyo", "Beladung Lyo, Programm 7 (80 %)\nSkid H2-X-0301", [], ["UVWX"], ["YZAB"], false],
    ["Freeze / Thaw", "", [], [], [], false],
    ["Versand", "Abholung 10:30, Kühlkette prüfen!", ["CDEF"], [], [], false],
    ["Einwaage", "Einwaage für Charge 12 / Süßstoff-Lösung\nH2-T-0602 bereitstellen", ["GHIJ"], ["KLMN"], [], false],
    ["CIP / SIP", "SIP H1-F-0201", [], ["OPQR"], ["STUV"], true],
    ["Raum CC", "", [], [], [], false]
  ];
  var out = {}, i, d, r, t, n, e, base = new Date();
  function p(v) { return (v < 10 ? "0" : "") + v; }
  function key(x) { return x.getFullYear() + "-" + p(x.getMonth() + 1) + "-" + p(x.getDate()); }
  for (i = -10; i < 25; i++) {
    d = new Date(base.getFullYear(), base.getMonth(), base.getDate() + i);
    e = {};
    for (r = 0; r < T.length; r++) {
      t = T[r];
      n = (i + r * 3 + 70) % 5;
      /* aktuelle Woche dicht belegt, sonst luckenhaft */
      if ((i >= -3 && i <= 6) ? (n === 4 && r > 4) : (n > 1)) continue;
      if (t[1] === "") {
        if ((i + r) % 4 !== 0) continue;
        t = [t[0], "Ohne Besonderheiten", [], [], [], false];
      }
      e[t[0]] = { text: t[1], F: t[2], S: t[3], N: t[4], lkz: t[5] && (i % 2 === 0) };
    }
    out[key(d)] = e;
  }
  return out;
})();
