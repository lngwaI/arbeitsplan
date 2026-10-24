var CACHE = "arbeitsplan-v1";
var FILES = ["./", "index.html", "app.js", "data.js", "style.css", "manifest.json", "icon.svg"];
self.addEventListener("install", function (e) {
  e.waitUntil(caches.open(CACHE).then(function (c) { return c.addAll(FILES); }));
});
self.addEventListener("activate", function (e) {
  e.waitUntil(caches.keys().then(function (ks) {
    return Promise.all(ks.filter(function (k) { return k !== CACHE; }).map(function (k) { return caches.delete(k); }));
  }));
});
self.addEventListener("fetch", function (e) {
  e.respondWith(fetch(e.request).catch(function () { return caches.match(e.request); }));
});
