(function () {
  var WD = ["Sonntag", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag"];
  var EQ = /\bH[12]-[TFXP]-\d{4}(?:-\d{2})?\b/g;
  var start = monday(new Date());
  var query = "";
  var open = {};
  function $(id) { return document.getElementById(id); }

  function p(v) { return (v < 10 ? "0" : "") + v; }
  function key(d) { return d.getFullYear() + "-" + p(d.getMonth() + 1) + "-" + p(d.getDate()); }
  function fmt(d) { return p(d.getDate()) + "." + p(d.getMonth() + 1) + "." + d.getFullYear(); }
  function addDays(d, n) { return new Date(d.getFullYear(), d.getMonth(), d.getDate() + n); }
  function monday(d) { return addDays(d, -((d.getDay() + 6) % 7)); }
  function kw(d) {
    var t = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
    var n = t.getUTCDay() || 7;
    t.setUTCDate(t.getUTCDate() + 4 - n);
    var y0 = new Date(Date.UTC(t.getUTCFullYear(), 0, 1));
    return Math.ceil(((t - y0) / 86400000 + 1) / 7);
  }
  function esc(s) { return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;"); }
  function reEsc(s) { return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"); }

  function hl(s) {
    if (!query) return esc(s);
    var re = new RegExp(reEsc(query), "gi"), o = "", l = 0, m;
    while ((m = re.exec(s)) !== null) {
      o += esc(s.substring(l, m.index)) + "<mark>" + esc(m[0]) + "</mark>";
      l = m.index + m[0].length;
    }
    return o + esc(s.substring(l));
  }
  // Nur die Nummer faerben, Status nie als Text
  function render(text) {
    var out = "", last = 0, m, st;
    EQ.lastIndex = 0;
    while ((m = EQ.exec(text)) !== null) {
      out += hl(text.substring(last, m.index));
      st = EQUIP_STATUS[m[0]] || "unbekannt";
      out += '<span style="color:' + (STATUS_COLORS[st] || STATUS_COLORS.unbekannt) + ';font-weight:600">' + hl(m[0]) + "</span>";
      last = m.index + m[0].length;
    }
    return out + hl(text.substring(last));
  }
  function count(s) {
    if (!query || !s) return 0;
    var n = 0, i = 0, q = query.toLowerCase(), l = s.toLowerCase();
    while ((i = l.indexOf(q, i)) >= 0) { n++; i += q.length; }
    return n;
  }

  function draw() {
    var end = addDays(start, 6), html = "", total = 0, i, r, s, today = key(new Date());
    $("kw").textContent = "KW" + kw(start) + " (" + fmt(start) + " - " + fmt(end) + ")";
    for (i = 0; i < 7; i++) {
      var d = addDays(start, i), k = key(d), data = DAYS_DATA[k] || {};
      html += '<section class="day' + (k === today ? " today" : "") + '"><div class="dh"><b>' + WD[d.getDay()] + "</b><span>" + fmt(d) + "</span></div>";
      for (r = 0; r < ROWS.length; r++) {
        var row = ROWS[r], e = data[row.name];
        var content = !!e && (e.text !== "" || e.F.length + e.S.length + e.N.length > 0 || e.lkz);
        var hits = e ? count(e.text) + count(e.F.join(", ")) + count(e.S.join(", ")) + count(e.N.join(", ")) : 0;
        total += hits;
        var id = k + "|" + row.name;
        var isOpen = hits > 0 ? true : (id in open ? open[id] : content);
        var lk = row.lkz ? ' <span class="lkz ' + (e && e.lkz ? "g" : "r") + '">(LKZ)</span>' : "";
        html += '<div class="row"><div class="rh" data-id="' + esc(id) + '" data-o="' + (isOpen ? 1 : 0) + '"><span class="rn">' + esc(row.name) + lk + "</span>" +
          (!isOpen && content ? '<span class="dot">&#9679;</span>' : "") + '<span class="arr">' + (isOpen ? "&#9662;" : "&#9656;") + "</span></div>";
        if (isOpen) {
          html += '<div class="rb">';
          if (e && e.text) html += '<div class="txt">' + render(e.text) + "</div>";
          html += '<div class="sh">';
          var sh = [["F", e ? e.F : []], ["S", e ? e.S : []], ["N", e ? e.N : []]];
          for (s = 0; s < 3; s++) html += '<div class="' + sh[s][0] + '"><i>' + sh[s][0] + "</i>" + hl(sh[s][1].join(", ")) + "</div>";
          html += "</div></div>";
        }
        html += "</div>";
      }
      html += "</section>";
    }
    $("days").innerHTML = html;
    $("cnt").textContent = query ? total + " Treffer" : "";
  }

  function go(n) { start = addDays(start, n); open = {}; window.scrollTo(0, 0); draw(); }
  $("pw").onclick = function () { go(-7); };
  $("nw").onclick = function () { go(7); };
  $("pd").onclick = function () { go(-1); };
  $("nd").onclick = function () { go(1); };
  $("today").onclick = function () { start = monday(new Date()); open = {}; window.scrollTo(0, 0); draw(); };
  $("q").oninput = function () { query = this.value; draw(); };
  $("days").onclick = function (ev) {
    var t = ev.target;
    while (t && t !== this && !(t.className && String(t.className) === "rh")) t = t.parentNode;
    if (!t || t === this) return;
    open[t.getAttribute("data-id")] = t.getAttribute("data-o") !== "1";
    var y = window.pageYOffset;
    draw();
    window.scrollTo(0, y);
  };
  draw();
  if ("serviceWorker" in navigator && (location.protocol === "https:" || location.hostname === "localhost")) {
    navigator.serviceWorker.register("sw.js");
  }
})();
