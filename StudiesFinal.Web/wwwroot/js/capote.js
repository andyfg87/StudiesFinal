/* ==========================================================================
   capote.js — comportamiento común de CapoteOne
   Menús desplegables, menú móvil y tema claro/oscuro.
   Sin dependencias: no necesita jQuery ni Bootstrap JS.
   ========================================================================== */
(function () {
    "use strict";

    /* ---------- menús desplegables de la barra superior ---------- */
    var menus = document.querySelectorAll("[data-menu]");

    menus.forEach(function (m) {
        var btn = m.querySelector("button");
        if (!btn) return;

        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            var open = m.dataset.open === "true";
            closeMenus();
            m.dataset.open = String(!open);
            btn.setAttribute("aria-expanded", String(!open));
        });
    });

    function closeMenus() {
        menus.forEach(function (o) {
            o.dataset.open = "false";
            var b = o.querySelector("button");
            if (b) b.setAttribute("aria-expanded", "false");
        });
    }

    document.addEventListener("click", closeMenus);
    window.addEventListener("keydown", function (e) {
        if (e.key === "Escape") closeMenus();
    });

    /* ---------- menú móvil ---------- */
    var burger = document.getElementById("burger");
    var drawer = document.getElementById("drawer");

    if (burger && drawer) {
        burger.addEventListener("click", function () {
            var open = drawer.classList.toggle("open");
            burger.setAttribute("aria-expanded", String(open));
        });
    }

    /* ---------- tema claro / oscuro ---------- */
    var THEME_KEY = "capoteone-theme";
    var root = document.documentElement;

    function readTheme() {
        var v = root.getAttribute("data-theme");
        if (v === "light" || v === "dark") return v;
        return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    }

    function saveTheme(v) {
        try { localStorage.setItem(THEME_KEY, v); } catch (e) { /* sin almacenamiento */ }
    }

    function loadTheme() {
        try { return localStorage.getItem(THEME_KEY); } catch (e) { return null; }
    }

    var themeBtn = document.getElementById("themeBtn");
    if (themeBtn) {
        themeBtn.addEventListener("click", function () {
            var next = readTheme() === "dark" ? "light" : "dark";
            root.setAttribute("data-theme", next);
            saveTheme(next);
        });
    }

    /* El atributo del tema vive en <html>: si algo externo lo borra, se
       restaura la elección guardada. Un cambio a un valor válido se respeta. */
    if (window.MutationObserver) {
        new MutationObserver(function () {
            var v = root.getAttribute("data-theme");
            if (v === "light" || v === "dark") { saveTheme(v); return; }
            var saved = loadTheme();
            if (saved === "light" || saved === "dark") root.setAttribute("data-theme", saved);
        }).observe(root, { attributes: true, attributeFilter: ["data-theme"] });
    }

    /* ---------- tablas: etiquetas para la vista de fichas en móvil ----------
       Por debajo de 780px las tablas se apilan como fichas y cada celda
       muestra el nombre de su columna. Se rellena aquí para no tener que
       repetir data-l a mano en cada vista de Razor. */
    document.querySelectorAll("table.t-cards").forEach(function (table) {
        var heads = Array.prototype.map.call(
            table.querySelectorAll("thead th"),
            function (th) { return th.textContent.trim(); }
        );
        table.querySelectorAll("tbody tr").forEach(function (tr) {
            Array.prototype.forEach.call(tr.children, function (td, i) {
                if (i === 0) { td.classList.add("t-lead"); return; }
                if (heads[i] && !td.hasAttribute("data-l")) td.setAttribute("data-l", heads[i]);
            });
        });
    });

    /* Nota: la clase no puede llamarse «lead»: Bootstrap ya define .lead con
   font-size 1.25rem y agrandaría la primera celda de cada fila. */

    /* ---------- selector de columnas ---------- */
    document.querySelectorAll("[data-cols-for]").forEach(function (menu) {
        var table = document.getElementById(menu.dataset.colsFor);
        if (!table) return;

        function apply(key, on) {
            table.querySelectorAll('[data-col="' + key + '"]').forEach(function (cell) {
                cell.hidden = !on;
            });
        }

        menu.querySelectorAll("input[data-col]").forEach(function (cb) {
            apply(cb.dataset.col, cb.checked);
            cb.addEventListener("click", function (e) { e.stopPropagation(); });
            cb.addEventListener("change", function () { apply(cb.dataset.col, cb.checked); });
        });
    });

    /* ======================================================================
       GRÁFICOS
       Se dibujan en SVG a mano, sin librerías: no añade ninguna dependencia
       al proyecto y hereda los colores del tema mediante variables CSS.

       Uso desde Razor:

         <div class="chart-wrap" data-chart="area"
              data-series='[{"key":"base","label":"Mensualidades","color":"--series-1"}]'
              data-format="money">
           <svg class="chart"></svg>
           <div class="tip"></div>
           <script type="application/json">[{"label":"ago","year":2025,"base":21980}]</script>
         </div>

       data-chart : "area" (apilado, con cruz de seguimiento) o "bars" (barras
                    verticales apiladas).
       data-format: "money" | "int" | "short".
       ====================================================================== */

    var nf = new Intl.NumberFormat("es-ES");

    function fmtShort(n) {
        if (n >= 1e6) return (n / 1e6).toFixed(1).replace(".", ",") + "M";
        if (n >= 1e3) return Math.round(n / 1e3) + "k";
        return String(Math.round(n));
    }

    function formatter(kind) {
        if (kind === "money") return function (n) { return "$" + nf.format(Math.round(n)); };
        if (kind === "short") return fmtShort;
        return function (n) { return nf.format(Math.round(n)); };
    }

    /* Escala "bonita": el paso es 1/2/2,5/5 ×10ⁿ, para que el eje dé valores
       redondos (10k, 20k…) en vez de 12.500 / 37.500. */
    function niceTop(max, divisions) {
        var raw = max / divisions;
        var mag = Math.pow(10, Math.floor(Math.log10(raw) || 0));
        var tick = null;
        [1, 2, 2.5, 5, 10].forEach(function (f) {
            if (tick === null && f * mag >= raw) tick = f * mag;
        });
        if (tick === null) tick = mag * 10;
        return { top: tick * divisions, tick: tick };
    }

    function drawChart(wrap) {
        var svg = wrap.querySelector("svg.chart");
        var tip = wrap.querySelector(".tip");
        var json = wrap.querySelector('script[type="application/json"]');
        if (!svg || !json) return;

        var rows, series;
        try {
            rows = JSON.parse(json.textContent);
            series = JSON.parse(wrap.dataset.series);
        } catch (e) { return; }
        if (!rows.length || !series.length) return;

        var fmt = formatter(wrap.dataset.format);
        var isArea = wrap.dataset.chart !== "bars";

        // El SVG se dibuja a la anchura real del contenedor: con un viewBox
        // fijo dentro de una tarjeta más ancha, el texto se escala y desborda.
        var W = Math.max(300, Math.round(wrap.getBoundingClientRect().width) || 300);
        var H = 240, L = 58, R = 14, T = 12, B = 26;
        var iw = W - L - R, ih = H - T - B;
        svg.setAttribute("viewBox", "0 0 " + W + " " + H);

        var totals = rows.map(function (m) {
            return series.reduce(function (a, s) { return a + (m[s.key] || 0); }, 0);
        });
        var scale = niceTop(Math.max(1, Math.max.apply(null, totals)), 4);
        var y = function (v) { return T + ih - (v / scale.top) * ih; };

        var g = "";
        for (var k = 0; k <= 4; k++) {
            var v = scale.tick * k, yy = y(v).toFixed(1);
            g += '<line x1="' + L + '" y1="' + yy + '" x2="' + (W - R) + '" y2="' + yy +
                 '" stroke="var(--grid)" stroke-width="1"/>' +
                 '<text x="' + (L - 8) + '" y="' + (y(v) + 4).toFixed(1) +
                 '" text-anchor="end" font-size="10.5" fill="var(--ink-3)" ' +
                 'style="font-variant-numeric:tabular-nums">' + fmtShort(v) + '</text>';
        }

        var body = "", x;
        if (isArea) {
            x = function (i) { return L + (rows.length < 2 ? iw / 2 : (i / (rows.length - 1)) * iw); };
            var base = rows.map(function () { return 0; });
            series.forEach(function (s) {
                var up = rows.map(function (m, i) { return base[i] + (m[s.key] || 0); });
                var line = up.map(function (v, i) { return x(i).toFixed(1) + "," + y(v).toFixed(1); }).join(" ");
                var down = base.map(function (v, i) { return x(i).toFixed(1) + "," + y(v).toFixed(1); })
                               .reverse().join(" L");
                body += '<path d="M' + line + " L" + down + ' Z" fill="var(' + s.color + ')" fill-opacity=".20"/>' +
                        '<polyline points="' + line + '" fill="none" stroke="var(--surface)" stroke-width="3.5" stroke-linejoin="round"/>' +
                        '<polyline points="' + line + '" fill="none" stroke="var(' + s.color +
                        ')" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>';
                base = up;
            });
        } else {
            var slot = iw / rows.length, bw = Math.min(30, slot - 6);
            x = function (i) { return L + (i + 0.5) * slot; };
            rows.forEach(function (m, i) {
                var acc = 0, cx = x(i);
                series.forEach(function (s) {
                    var val = m[s.key] || 0;
                    if (!val) return;
                    var y1 = y(acc + val), y0 = y(acc);
                    acc += val;
                    body += '<rect x="' + (cx - bw / 2).toFixed(1) + '" y="' + y1.toFixed(1) +
                            '" width="' + bw.toFixed(1) + '" height="' +
                            Math.max(1, y0 - y1 - 2).toFixed(1) +
                            '" rx="3" fill="var(' + s.color + ')"/>';
                });
            });
        }

        var every = rows.length > 15 ? 3 : rows.length > 8 ? 2 : 1;
        var axis = rows.map(function (m, i) {
            if (i % every !== 0 && i !== rows.length - 1) return "";
            return '<text x="' + x(i).toFixed(1) + '" y="' + (H - 8) +
                   '" text-anchor="middle" font-size="10.5" fill="var(--ink-3)">' + m.label + '</text>';
        }).join("");

        svg.innerHTML = g + body +
            '<line x1="' + L + '" y1="' + (T + ih) + '" x2="' + (W - R) + '" y2="' + (T + ih) +
            '" stroke="var(--axis)" stroke-width="1"/>' + axis +
            '<g class="hover"></g><rect x="' + L + '" y="' + T + '" width="' + iw +
            '" height="' + ih + '" fill="transparent" class="hit"/>';

        if (!tip) return;
        var hover = svg.querySelector(".hover");
        var hit = svg.querySelector(".hit");

        hit.addEventListener("pointermove", function (ev) {
            var r = svg.getBoundingClientRect();
            var px = ((ev.clientX - r.left) / r.width) * W;
            var i = isArea
                ? Math.round(((px - L) / iw) * (rows.length - 1))
                : Math.floor(((px - L) / iw) * rows.length);
            i = Math.max(0, Math.min(rows.length - 1, i));
            var m = rows[i], acc = 0, dots = "";

            if (isArea) {
                series.forEach(function (s) {
                    acc += (m[s.key] || 0);
                    dots += '<circle cx="' + x(i).toFixed(1) + '" cy="' + y(acc).toFixed(1) +
                            '" r="4.5" fill="var(' + s.color + ')" stroke="var(--surface)" stroke-width="2"/>';
                });
                hover.innerHTML = '<line x1="' + x(i).toFixed(1) + '" y1="' + T + '" x2="' +
                    x(i).toFixed(1) + '" y2="' + (T + ih) +
                    '" stroke="var(--axis)" stroke-width="1" stroke-dasharray="3 3"/>' + dots;
            } else {
                series.forEach(function (s) { acc += (m[s.key] || 0); });
            }

            var total = series.reduce(function (a, s) { return a + (m[s.key] || 0); }, 0);
            tip.innerHTML = '<div class="t-hd">' + m.label + " " + (m.year || "") + "</div>" +
                series.map(function (s) {
                    return '<div class="t-row"><i class="sw" style="background:var(' + s.color + ')"></i>' +
                        s.label + '<span class="v">' + fmt(m[s.key] || 0) + "</span></div>";
                }).join("") +
                (series.length > 1
                    ? '<div class="t-row t-sum">Total<span class="v">' + fmt(total) + "</span></div>"
                    : "");

            tip.style.left = ((x(i) / W) * wrap.clientWidth) + "px";
            tip.style.top = (((isArea ? y(acc) : T) / H) * wrap.clientHeight - 12) + "px";
            tip.classList.add("on");
        });

        hit.addEventListener("pointerleave", function () {
            if (hover) hover.innerHTML = "";
            tip.classList.remove("on");
        });
    }

    function drawAllCharts() {
        document.querySelectorAll("[data-chart]").forEach(drawChart);
    }

    drawAllCharts();

    var resizeTimer;
    window.addEventListener("resize", function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(drawAllCharts, 150);
    });

    /* ======================================================================
       Volver arriba
       ====================================================================== */

    /* ======================================================================
       DASHBOARD: filtrado en cliente y orden de tablas

       El modelo ya trae todo el conjunto, así que filtrar y ordenar no
       necesita ni recargar ni consultar al servidor. Las filas se marcan con
       data-k (texto normalizado para buscar) y data-model (para el filtro por
       modelo); cada lista lleva su propio aviso data-dash-empty.
       ====================================================================== */

    var dash = document.querySelector("[data-dash]");

    if (dash) {
        var search = dash.querySelector("[data-dash-search]");
        var chips = [].slice.call(dash.querySelectorAll("[data-dash-model]"));
        var counter = dash.querySelector("[data-dash-count]");
        var resetBtn = dash.querySelector("[data-dash-reset]");
        var lists = [].slice.call(dash.querySelectorAll("[data-dash-list]"));

        var query = "";
        var model = "";

        /* Quita acentos para que "canon" encuentre "Canón" y viceversa. */
        function norm(s) {
            return (s || "").toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "");
        }

        function apply() {
            var q = norm(query);
            var shownTotal = 0;

            lists.forEach(function (list) {
                var rows = [].slice.call(list.querySelectorAll("[data-row]"));
                var shown = 0;

                rows.forEach(function (row) {
                    var okText = !q || norm(row.dataset.k).indexOf(q) !== -1;
                    var okModel = !model || row.dataset.model === model;
                    var on = okText && okModel;
                    row.hidden = !on;
                    if (on) { shown++; }
                });

                shownTotal += shown;

                /* El aviso de "sin resultados" solo aparece si había filas que
                   ocultar: si la lista ya venía vacía, su mensaje propio vale. */
                var empty = list.querySelector("[data-dash-empty]");
                if (empty) { empty.hidden = !(rows.length > 0 && shown === 0); }
            });

            if (counter) {
                counter.textContent = (query || model)
                    ? shownTotal + (shownTotal === 1 ? " coincidencia" : " coincidencias")
                    : "";
            }

            if (resetBtn) { resetBtn.hidden = !(query || model); }
        }

        if (search) {
            search.addEventListener("input", function () {
                query = search.value.trim();
                apply();
            });
            /* Escape limpia el cuadro sin sacar el foco. */
            search.addEventListener("keydown", function (e) {
                if (e.key === "Escape" && search.value) {
                    e.stopPropagation();
                    search.value = "";
                    query = "";
                    apply();
                }
            });
        }

        chips.forEach(function (chip) {
            chip.addEventListener("click", function () {
                model = chip.dataset.dashModel || "";
                chips.forEach(function (c) { c.classList.toggle("is-on", c === chip); });
                apply();
            });
        });

        if (resetBtn) {
            resetBtn.addEventListener("click", function () {
                query = "";
                model = "";
                if (search) { search.value = ""; }
                chips.forEach(function (c, i) { c.classList.toggle("is-on", i === 0); });
                apply();
            });
        }

        apply();

        /* ---------- orden de tablas ---------- */
        dash.querySelectorAll("table[data-sortable]").forEach(function (table) {
            var headers = [].slice.call(table.querySelectorAll("th[data-sort]"));

            headers.forEach(function (th, index) {
                th.tabIndex = 0;
                th.setAttribute("role", "button");

                function sort() {
                    var body = table.tBodies[0];
                    if (!body) { return; }

                    var asc = th.getAttribute("aria-sort") !== "ascending";
                    var numeric = th.dataset.sort === "num";

                    headers.forEach(function (h) { h.removeAttribute("aria-sort"); });
                    th.setAttribute("aria-sort", asc ? "ascending" : "descending");

                    var rows = [].slice.call(body.rows);
                    rows.sort(function (a, b) {
                        var ca = a.cells[index], cb = b.cells[index];
                        if (!ca || !cb) { return 0; }

                        if (numeric) {
                            /* data-v trae el valor sin formatear: sin él,
                               "1.234" se compararía como texto. */
                            var va = parseFloat(ca.dataset.v || ca.textContent.replace(/[^\d.-]/g, "")) || 0;
                            var vb = parseFloat(cb.dataset.v || cb.textContent.replace(/[^\d.-]/g, "")) || 0;
                            return asc ? va - vb : vb - va;
                        }

                        var ta = norm(ca.textContent.trim()), tb = norm(cb.textContent.trim());
                        return asc ? ta.localeCompare(tb) : tb.localeCompare(ta);
                    });

                    rows.forEach(function (r) { body.appendChild(r); });
                }

                th.addEventListener("click", sort);
                th.addEventListener("keydown", function (e) {
                    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); sort(); }
                });
            });
        });
    }

    var toTop = document.getElementById("toTop");

    if (toTop) {
        var SHOW_AT = 400;   // px de scroll a partir de los cuales aparece
        var visible = false;

        function setVisible(on) {
            if (on === visible) { return; }
            visible = on;

            if (on) {
                toTop.hidden = false;
                /* Leer una propiedad de layout fuerza el reflow: sin esto el
                   navegador agrupa el quitar [hidden] con el añadir .is-on y
                   se salta la transición. No se usa requestAnimationFrame
                   porque queda congelado en pestañas en segundo plano y el
                   botón no llegaría a aparecer. */
                void toTop.offsetWidth;
                toTop.classList.add("is-on");
            } else {
                toTop.classList.remove("is-on");
                /* Se retira del flujo al acabar el desvanecido. */
                setTimeout(function () { if (!visible) { toTop.hidden = true; } }, 200);
            }
        }

        function onScroll() {
            setVisible((window.pageYOffset || document.documentElement.scrollTop) > SHOW_AT);
        }

        window.addEventListener("scroll", onScroll, { passive: true });
        onScroll();

        toTop.addEventListener("click", function () {
            var reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            window.scrollTo({ top: 0, behavior: reduce ? "auto" : "smooth" });

            /* Devolver el foco al principio: si no, el lector de pantalla y el
               tabulador se quedan al final de la página tras subir. */
            var first = document.querySelector("header a, header button");
            if (first) { first.focus({ preventScroll: true }); }
        });
    }
})();
