/* =========================================================================
   files.js — abrir los PDF del estudio
   - [data-open-beside]: abre el PDF en una ventana aparte en la mitad derecha
     de la pantalla, para leerlo mientras se llena el informe. Se reutiliza la
     misma ventana para todos los PDF.
   - [data-copy-path]: copia la ruta \\servidor\... al portapapeles.
   ========================================================================= */
(function () {
    "use strict";

    var WINDOW_NAME = "studiesfinal-pdf";

    document.addEventListener("click", function (e) {
        var opener = e.target.closest("[data-open-beside]");
        if (opener) {
            // Ctrl/Shift/clic central: comportamiento normal del navegador (pestaña nueva)
            if (e.ctrlKey || e.shiftKey || e.metaKey || e.button === 1) return;
            e.preventDefault();
            openBeside(opener.href);
            return;
        }

        var copier = e.target.closest("[data-copy-path]");
        if (copier) {
            e.preventDefault();
            copyText(copier.getAttribute("data-copy-path"), copier);
        }
    });

    function openBeside(url) {
        var s = window.screen;
        var availLeft = s.availLeft || 0;
        var availTop = s.availTop || 0;
        var width = Math.round(s.availWidth / 2);
        var height = s.availHeight;
        var left = availLeft + s.availWidth - width;

        var features = "popup=yes,width=" + width + ",height=" + height +
                       ",left=" + left + ",top=" + availTop + ",resizable=yes,scrollbars=yes";

        var win = window.open(url, WINDOW_NAME, features);
        if (win) {
            win.focus();
        } else {
            // Ventanas emergentes bloqueadas: se abre en una pestaña normal
            window.open(url, "_blank", "noopener");
        }
    }

    function copyText(text, button) {
        var done = function () { flash(button, "Copied!"); };
        var fail = function () { flash(button, "Copy failed"); };

        // El portapapeles moderno solo funciona en https o localhost
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(done, function () { legacyCopy(text) ? done() : fail(); });
        } else {
            legacyCopy(text) ? done() : fail();
        }
    }

    // Alternativa para http:// en la red local
    function legacyCopy(text) {
        var area = document.createElement("textarea");
        area.value = text;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.select();
        var ok = false;
        try { ok = document.execCommand("copy"); } catch (err) { ok = false; }
        document.body.removeChild(area);
        return ok;
    }

    function flash(button, message) {
        var label = button.querySelector("span") || button;
        var original = label.getAttribute("data-label") || label.textContent;
        label.setAttribute("data-label", original);
        label.textContent = message;
        clearTimeout(button._sfTimer);
        button._sfTimer = setTimeout(function () { label.textContent = original; }, 1600);
    }
})();
