/* =========================================================================
   studies.js — editor de informes, selector de paciente y plantillas
   ========================================================================= */
(function () {
    "use strict";

    // ---- Editor de texto enriquecido (Quill) -------------------------------
    // <div data-rich-editor="Information"> + <input type="hidden" id="Information">
    var editors = {};

    document.querySelectorAll("[data-rich-editor]").forEach(function (wrap) {
        if (typeof Quill === "undefined") return;

        var input = document.getElementById(wrap.dataset.richEditor);
        var body = wrap.querySelector(".sf-editor-body");
        var readonly = wrap.classList.contains("is-readonly");

        var quill = new Quill(body, {
            theme: "snow",
            readOnly: readonly,
            modules: {
                toolbar: readonly ? false : [
                    [{ header: [1, 2, 3, false] }],
                    ["bold", "italic", "underline"],
                    [{ color: [] }, { background: [] }],
                    [{ list: "ordered" }, { list: "bullet" }],
                    [{ indent: "-1" }, { indent: "+1" }],
                    [{ align: [] }],
                    ["clean"]
                ]
            }
        });

        if (input && input.value) {
            quill.clipboard.dangerouslyPasteHTML(input.value);
        }

        var sync = function () {
            if (!input) return;
            // root.innerHTML y no getSemanticHTML(): este último convierte todos los espacios en &nbsp;
            input.value = quill.getLength() > 1 ? quill.root.innerHTML : "";
        };
        quill.on("text-change", sync);

        var form = wrap.closest("form");
        if (form) form.addEventListener("submit", sync);

        editors[wrap.dataset.richEditor] = quill;
    });

    // ---- Ruta donde se guardará el archivo subido ----------------------------
    // \\<servidor>\<carpeta compartida>\<tipo de reporte>\<iniciales>-<paciente>-<fecha de hoy>.pdf
    // (la calcula el servidor con StudyFiles:Server / Share de appsettings.json)
    var uploadBox = document.querySelector("[data-upload-target]");
    var uploadTimer = null;

    function refreshUploadTarget() {
        if (!uploadBox) return;
        clearTimeout(uploadTimer);
        uploadTimer = setTimeout(function () {
            var name = document.getElementById("StudyName");
            var patient = document.getElementById("PatientId");
            var qs = "?studyName=" + encodeURIComponent(name ? name.value : "") +
                     "&patientId=" + encodeURIComponent(patient ? patient.value : "");
            fetch(uploadBox.dataset.url + qs, { credentials: "same-origin" })
                .then(function (r) { return r.json(); })
                .then(function (data) { uploadBox.querySelector("[data-upload-path]").textContent = data.path; })
                .catch(function () { /* se deja la ruta anterior */ });
        }, 250);
    }

    var studyNameInput = document.getElementById("StudyName");
    if (studyNameInput) studyNameInput.addEventListener("input", refreshUploadTarget);
    refreshUploadTarget();

    // ---- Plantillas: rellena nombre del estudio e informe ------------------
    var templateSelect = document.querySelector("[data-template-select]");
    if (templateSelect) {
        templateSelect.addEventListener("change", function () {
            var id = templateSelect.value;
            if (!id) return;

            var editor = editors[templateSelect.dataset.templateTarget || "Information"];
            var hasText = editor && editor.getLength() > 1;
            if (hasText && !confirm("Replace the current report content with the template?")) {
                return;
            }

            fetch(templateSelect.dataset.templateUrl + "?id=" + encodeURIComponent(id), { credentials: "same-origin" })
                .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
                .then(function (data) {
                    var name = document.getElementById("StudyName");
                    if (name && data.studyName) name.value = data.studyName;
                    refreshUploadTarget();
                    if (editor) {
                        editor.setContents([]);
                        editor.clipboard.dangerouslyPasteHTML(data.info || "");
                    }
                })
                .catch(function () { alert("The template could not be loaded."); });
        });
    }

    // ---- Selector de paciente con búsqueda ---------------------------------
    document.querySelectorAll("[data-patient-picker]").forEach(function (picker) {
        var search = picker.querySelector("input[type=search]");
        var hidden = document.getElementById(picker.dataset.patientPicker);
        var results = picker.querySelector(".sf-results");
        var url = picker.dataset.searchUrl;
        var timer = null;

        var close = function () { results.hidden = true; results.innerHTML = ""; };

        var choose = function (p) {
            hidden.value = p.id;
            search.value = p.id + " · " + (p.name || "");
            close();
            refreshUploadTarget();
        };

        search.addEventListener("input", function () {
            hidden.value = "";
            clearTimeout(timer);
            var q = search.value.trim();
            if (q.length < 2) { close(); return; }

            timer = setTimeout(function () {
                fetch(url + "?q=" + encodeURIComponent(q), { credentials: "same-origin" })
                    .then(function (r) { return r.json(); })
                    .then(function (list) {
                        results.innerHTML = "";
                        if (!list.length) {
                            results.innerHTML = '<div class="p-2 small text-muted">No results</div>';
                        }
                        list.forEach(function (p) {
                            var b = document.createElement("button");
                            b.type = "button";
                            var id = document.createElement("span");
                            id.className = "id";
                            id.textContent = p.id;
                            var n = document.createElement("span");
                            n.className = "flex-fill";
                            n.textContent = p.name || "(no name)";
                            var d = document.createElement("span");
                            d.className = "small text-muted";
                            d.textContent = p.dob || "";
                            b.append(id, n, d);
                            b.addEventListener("click", function () { choose(p); });
                            results.appendChild(b);
                        });
                        results.hidden = false;
                    });
            }, 250);
        });

        document.addEventListener("click", function (e) {
            if (!picker.contains(e.target)) close();
        });
        search.addEventListener("keydown", function (e) {
            if (e.key === "Escape") close();
        });
    });
})();
