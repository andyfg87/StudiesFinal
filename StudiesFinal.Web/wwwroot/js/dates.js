/* =========================================================================
   dates.js — fechas siempre en MM/dd/yyyy
   El <input type="date"> nativo muestra el formato del idioma de Windows
   (dd/mm/aaaa en español). Se sustituye por flatpickr: el usuario ve y
   escribe MM/dd/yyyy y el formulario sigue enviando yyyy-MM-dd.
   ========================================================================= */
(function () {
    "use strict";
    if (typeof flatpickr === "undefined") return;

    // Acepta lo que escribe el usuario (08/27/1966, 8/27/1966, 08-27-1966)
    // y el valor interno del formulario (1966-08-27).
    function parseDate(text) {
        var s = (text || "").trim();
        var m = s.match(/^(\d{1,2})[\/\-.](\d{1,2})[\/\-.](\d{4})$/);
        if (m) return validDate(+m[3], +m[1], +m[2]);
        m = s.match(/^(\d{4})-(\d{1,2})-(\d{1,2})/);
        if (m) return validDate(+m[1], +m[2], +m[3]);
        return undefined;
    }

    function validDate(year, month, day) {
        var d = new Date(year, month - 1, day);
        // Rechaza fechas imposibles como 02/30/2020
        return d.getFullYear() === year && d.getMonth() === month - 1 && d.getDate() === day ? d : undefined;
    }

    document.querySelectorAll('input[type="date"]').forEach(function (input) {
        flatpickr(input, {
            dateFormat: "Y-m-d",      // valor enviado al servidor
            altInput: true,
            altFormat: "m/d/Y",       // lo que ve y escribe el usuario
            allowInput: true,         // se puede teclear 08/27/1966
            disableMobile: true,      // también en móvil, para mantener el formato
            parseDate: parseDate,
            onReady: function (_, __, fp) {
                fp.altInput.placeholder = "MM/DD/YYYY";
                // El campo visible hereda las clases de Bootstrap del original
                fp.altInput.className = input.className;
            }
        });
    });
})();
