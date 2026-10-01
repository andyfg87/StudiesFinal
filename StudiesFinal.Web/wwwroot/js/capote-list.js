// capote-list.js — comportamiento común de listados (se carga en _Layout).
//   [data-ls-toggle] / [data-ls-copy]  dentro de .ls-secret → mostrar / copiar
//   .ls-seg [data-ls-state] + input[data-ls-filter]         → filtro en cliente
// Listeners delegados: no hace falta inicializar nada en cada vista.
(function () {
    "use strict";

    function notify(type, msg) {
        // OJO: "toastr?.success" lanza ReferenceError si toastr no está cargado;
        // hay que comprobarlo a través de window.
        if (window.toastr && typeof window.toastr[type] === "function") {
            window.toastr[type](msg);
        }
    }

    function flash(btn, iconClass) {
        var icon = btn.querySelector("i");
        if (!icon) return;
        var original = icon.className;
        icon.className = "bi " + iconClass;
        setTimeout(function () { icon.className = original; }, 1400);
    }

    function fallbackCopy(text) {
        var temp = document.createElement("textarea");
        temp.value = text;
        temp.setAttribute("readonly", "");
        temp.style.position = "fixed";
        temp.style.opacity = "0";
        document.body.appendChild(temp);
        temp.select();
        var ok = false;
        try { ok = document.execCommand("copy"); } catch (e) { ok = false; }
        document.body.removeChild(temp);
        return ok;
    }

    document.addEventListener("click", function (e) {
        var toggle = e.target.closest("[data-ls-toggle]");
        if (toggle) {
            var input = toggle.closest(".ls-secret").querySelector("input");
            var show = input.type === "password";
            input.type = show ? "text" : "password";
            var i = toggle.querySelector("i");
            i.classList.toggle("bi-eye", !show);
            i.classList.toggle("bi-eye-slash", show);
            toggle.setAttribute("aria-label", show ? "Hide" : "Show");
            toggle.setAttribute("title", show ? "Hide" : "Show");
            return;
        }

        var copy = e.target.closest("[data-ls-copy]");
        if (copy) {
            var value = copy.closest(".ls-secret").querySelector("input").value;
            if (!value) { notify("warning", "No hay nada que copiar"); return; }

            var done = function () { flash(copy, "bi-check2"); notify("success", "Copiado al portapapeles"); };
            var fail = function () {
                if (fallbackCopy(value)) { done(); }
                else { flash(copy, "bi-x-lg"); notify("error", "No se pudo copiar"); }
            };

            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(value).then(done, fail);
            } else {
                fail();
            }
            return;
        }

        // Filtros rápidos (Todas / Online / Offline / Con problemas)
        var seg = e.target.closest("[data-ls-state]");
        if (seg) {
            var group = seg.closest(".ls-seg");
            group.querySelectorAll("[data-ls-state]").forEach(function (b) {
                b.classList.toggle("is-active", b === seg);
                b.setAttribute("aria-pressed", b === seg ? "true" : "false");
            });
            applyFilters(group.closest(".tab-pane, .card, body"));
        }
    });

    // ---- Búsqueda + filtro por estado (solo en cliente) -------------------
    // Estructura esperada dentro del mismo contenedor:
    //   .ls-seg [data-ls-state]        → botones de estado
    //   input[data-ls-filter="#tabla"] → buscador
    //   tr[data-ls-row data-search data-online data-issues]
    //   tr[data-ls-empty]              → fila "sin resultados"
    function applyFilters(scope) {
        if (!scope) return;
        var input = scope.querySelector("input[data-ls-filter]");
        var table = input ? document.querySelector(input.getAttribute("data-ls-filter")) : scope.querySelector("table");
        if (!table) return;

        var term = (input ? input.value : "").trim().toLowerCase();
        var active = scope.querySelector(".ls-seg .is-active");
        var state = active ? active.getAttribute("data-ls-state") : "all";

        var visible = 0;
        table.querySelectorAll("tr[data-ls-row]").forEach(function (tr) {
            var ok = !term || (tr.getAttribute("data-search") || "").indexOf(term) !== -1;
            if (ok && state === "online")  ok = tr.getAttribute("data-online") === "1";
            if (ok && state === "offline") ok = tr.getAttribute("data-online") === "0";
            if (ok && state === "issues")  ok = tr.getAttribute("data-issues") === "1";
            tr.hidden = !ok;
            if (ok) visible++;
        });

        var empty = table.querySelector("tr[data-ls-empty]");
        if (empty) empty.hidden = visible > 0;
    }

    document.addEventListener("input", function (e) {
        if (e.target.matches("input[data-ls-filter]")) {
            applyFilters(e.target.closest(".tab-pane, .card, body"));
        }
    });
})();
