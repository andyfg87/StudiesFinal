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

    // ---- Explorador de archivos del servidor (FilesController) ---------------
    // Primer nivel: las ubicaciones configuradas (StudyFiles:BrowseRoots). Cada hueco
    // [data-file-slot] guarda en un campo oculto la ruta del explorador del archivo elegido
    // ("Ubicación\carpeta\archivo"; "" = sin cambios, "-" = quitar). Solo se muestra el nombre.
    var picker = document.getElementById("filePicker");
    if (picker && window.bootstrap) {
        var modal = bootstrap.Modal.getOrCreateInstance(picker);
        var listEl = picker.querySelector("[data-picker-list]");
        var crumbsEl = picker.querySelector("[data-picker-crumbs]");
        var searchEl = picker.querySelector("[data-picker-search]");
        var statusEl = picker.querySelector("[data-picker-status]");
        var LAST_DIR = "studiesfinal-last-folder";
        var currentSlot = null;
        var currentDir = "";
        var searchTimer = null;

        var lastDir = function () { try { return localStorage.getItem(LAST_DIR) || ""; } catch (e) { return ""; } };
        var saveDir = function (d) { try { localStorage.setItem(LAST_DIR, d); } catch (e) { /* sin almacenamiento */ } };

        var formatSize = function (bytes) {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1048576) return Math.round(bytes / 1024) + " KB";
            return (bytes / 1048576).toFixed(1) + " MB";
        };

        var iconFor = function (name) {
            var ext = (name.split(".").pop() || "").toLowerCase();
            if (ext === "pdf") return "bi-file-earmark-pdf text-danger";
            if (["jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff"].indexOf(ext) >= 0) return "bi-file-earmark-image text-primary";
            if (["doc", "docx"].indexOf(ext) >= 0) return "bi-file-earmark-word text-primary";
            return "bi-file-earmark text-muted";
        };

        var row = function (icon, text, meta) {
            var b = document.createElement("button");
            b.type = "button";
            b.className = "sf-picker-item";
            var i = document.createElement("i");
            i.className = "bi " + icon;
            i.setAttribute("aria-hidden", "true");
            var n = document.createElement("span");
            n.className = "name";
            n.textContent = text;
            b.append(i, n);
            if (meta) {
                var m = document.createElement("span");
                m.className = "meta";
                m.textContent = meta;
                b.appendChild(m);
            }
            return b;
        };

        var load = function (dir) {
            var q = searchEl.value.trim();
            statusEl.textContent = "Loading…";
            listEl.setAttribute("aria-busy", "true");

            fetch(picker.dataset.browseUrl + "?dir=" + encodeURIComponent(dir) + "&q=" + encodeURIComponent(q), { credentials: "same-origin" })
                .then(function (r) { return r.json().then(function (data) { return { ok: r.ok, status: r.status, data: data }; }); })
                .then(function (res) {
                    listEl.removeAttribute("aria-busy");
                    if (!res.ok) {
                        // Carpeta recordada que ya no existe o no es válida: volver a las ubicaciones
                        if (dir && res.status !== 503) { saveDir(""); load(""); return; }
                        listEl.innerHTML = "";
                        statusEl.textContent = res.data.error || "The folder could not be opened.";
                        return;
                    }

                    var data = res.data;
                    currentDir = data.dir;
                    saveDir(currentDir);

                    // Migas
                    crumbsEl.innerHTML = "";
                    data.crumbs.forEach(function (c, idx) {
                        if (idx > 0) crumbsEl.appendChild(document.createTextNode(" / "));
                        var a = document.createElement("button");
                        a.type = "button";
                        a.className = "btn btn-link btn-sm p-0 align-baseline";
                        a.textContent = c.name;
                        if (idx === data.crumbs.length - 1) a.classList.add("fw-semibold", "text-reset");
                        a.addEventListener("click", function () { searchEl.value = ""; load(c.dir); });
                        crumbsEl.appendChild(a);
                    });

                    // Carpetas y archivos
                    listEl.innerHTML = "";
                    data.folders.forEach(function (f) {
                        var b = row("bi-folder-fill text-warning", f.name);
                        b.addEventListener("click", function () { searchEl.value = ""; load(f.dir); });
                        listEl.appendChild(b);
                    });
                    data.files.forEach(function (f) {
                        var b = row(iconFor(f.name), f.name, f.modified + " · " + formatSize(f.size));
                        b.addEventListener("click", function () { choose(f); });
                        listEl.appendChild(b);
                    });

                    if (!data.folders.length && !data.files.length) {
                        statusEl.textContent = q ? "Nothing matches the search in this folder." : "This folder is empty.";
                    } else {
                        statusEl.textContent = data.folders.length + " folder(s), " + data.total + " file(s)" +
                            (data.truncated ? " — showing the " + data.files.length + " most recent; use the search to find older files." : "");
                    }
                })
                .catch(function () {
                    listEl.removeAttribute("aria-busy");
                    statusEl.textContent = "The studies server is not available.";
                });
        };

        var choose = function (file) {
            if (!currentSlot) return;
            currentSlot.querySelector("[data-slot-selection]").value = file.path;
            setSlot(currentSlot, file.name, picker.dataset.openUrl + "?p=" + encodeURIComponent(file.path));
            modal.hide();
        };

        var setSlot = function (slot, name, openUrl) {
            var nameEl = slot.querySelector("[data-slot-name]");
            nameEl.textContent = name || "No file";
            slot.querySelector(".sf-slot-file").classList.toggle("is-empty", !name);

            var open = slot.querySelector("[data-slot-open]");
            open.classList.toggle("d-none", !openUrl);
            open.href = openUrl || "#";
            slot.querySelector("[data-slot-remove]").classList.toggle("d-none", !name);
        };

        document.querySelectorAll("[data-file-slot]").forEach(function (slot) {
            slot.querySelector("[data-slot-browse]").addEventListener("click", function () {
                currentSlot = slot;
                searchEl.value = "";
                modal.show();
                load(lastDir());
            });
            slot.querySelector("[data-slot-remove]").addEventListener("click", function () {
                slot.querySelector("[data-slot-selection]").value = "-";
                setSlot(slot, null, null);
            });
        });

        searchEl.addEventListener("input", function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () { load(currentDir); }, 300);
        });
    }

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
