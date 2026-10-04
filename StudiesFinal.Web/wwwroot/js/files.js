/* =========================================================================
   files.js — abrir los archivos del estudio
   - [data-open-beside]: abre el archivo en una ventana aparte en la mitad
     derecha de la pantalla, para leerlo mientras se llena el informe. Se
     reutiliza la misma ventana para todos los archivos.
   Por seguridad la web nunca muestra la ruta del servidor: los archivos se
   abren a través de la aplicación.
   ========================================================================= */
(function () {
    "use strict";

    var WINDOW_NAME = "studiesfinal-pdf";

    document.addEventListener("click", function (e) {
        var opener = e.target.closest("[data-open-beside]");
        if (!opener || !opener.href || opener.getAttribute("href") === "#") return;

        // Ctrl/Shift/clic central: comportamiento normal del navegador (pestaña nueva)
        if (e.ctrlKey || e.shiftKey || e.metaKey || e.button === 1) return;
        e.preventDefault();
        openBeside(opener.href);
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
})();
