(() => {
    const messages = JSON.parse(document.getElementById("hrmsMessages")?.textContent || "{}");
    const t = key => messages[key] || key;
    const direction = document.documentElement.dir;
    let modalContent;
    let modal;
    const bindModal = () => {
        modal?.dispose();
        const element = document.getElementById("hrmsModal");
        modalContent = document.getElementById("hrmsModalContent");
        modal = element ? bootstrap.Modal.getOrCreateInstance(element) : null;
    };
    bindModal();
    document.addEventListener("hrms:content-updated", bindModal);

    const setStatus = (message, type = "success") => {
        const status = document.getElementById("pageStatus");
        if (!status) return;
        status.className = `alert alert-${type}`;
        status.textContent = message;
        status.classList.remove("d-none");
    };

    const showLoading = () => {
        modalContent.innerHTML = `
            <div class="modal-content" dir="${direction}">
                <div class="modal-body text-center py-5">
                    <div class="spinner-border text-primary" role="status"><span class="visually-hidden">${t("Loading")}</span></div>
                    <p class="mb-0 mt-3">${t("LoadingDialog")}</p>
                </div>
            </div>`;
    };

    const showModalError = (message) => {
        const content = document.createElement("div");
        content.className = "modal-content";
        content.dir = direction;
        const body = document.createElement("div");
        body.className = "modal-body";
        const alert = document.createElement("div");
        alert.className = "alert alert-danger mb-0";
        alert.setAttribute("role", "alert");
        alert.textContent = message;
        body.append(alert);
        content.append(body);
        modalContent.replaceChildren(content);
    };

    const parseValidation = (container) => {
        if (window.jQuery?.validator?.unobtrusive) {
            window.jQuery.validator.unobtrusive.parse(container);
        }
    };

    const refreshResults = async () => {
        const results = document.getElementById("resultsContainer");
        if (!results) return;
        const resultsBody = results.querySelector(".card-body") || results;
        results.setAttribute("aria-busy", "true");
        results.classList.add("opacity-50");
        try {
            const response = await fetch(window.location.href, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) throw new Error(t("Js0"));
            const wrapper = document.createElement("div");
            wrapper.innerHTML = await response.text();
            resultsBody.innerHTML = wrapper.innerHTML;
        } finally {
            results.removeAttribute("aria-busy");
            results.classList.remove("opacity-50");
        }
    };

    document.addEventListener("click", async (event) => {
        const trigger = event.target.closest("[data-modal-url]");
        if (!trigger || !modal || !modalContent) return;

        event.preventDefault();
        const status = document.getElementById("pageStatus");
        status?.classList.add("d-none");
        showLoading();
        modal.show();
        try {
            const response = await fetch(trigger.dataset.modalUrl, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) throw new Error(t("Js1"));
            modalContent.innerHTML = await response.text();
            parseValidation(modalContent);
        } catch (error) {
            showModalError(error.message || t("Js2"));
        }
    });

    document.addEventListener("submit", async (event) => {
        const activeForm = event.target.closest("form[data-active-form]");
        if (activeForm) {
            event.preventDefault();
            const submitButton = activeForm.querySelector('button[type="submit"]');
            const spinner = submitButton?.querySelector(".spinner-border");
            if (submitButton?.disabled) return;
            if (submitButton) submitButton.disabled = true;
            spinner?.classList.remove("d-none");

            try {
                const response = await fetch(activeForm.action, {
                    method: "POST",
                    body: new FormData(activeForm),
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                const contentType = response.headers.get("content-type") || "";
                const result = contentType.includes("application/json")
                    ? await response.json()
                    : null;
                if (!response.ok) {
                    throw new Error(result?.message || t("Js3"));
                }

                const row = activeForm.closest("tr");
                const badge = row?.querySelector("[data-record-status]");
                if (badge && typeof result?.isActive === "boolean") {
                    badge.textContent = result.isActive ? t("Js4") : t("Js5");
                    badge.classList.toggle("status-active", result.isActive);
                    badge.classList.toggle("status-inactive", !result.isActive);
                    const stateInput = activeForm.querySelector('input[name="isActive"]');
                    if (stateInput) stateInput.value = String(!result.isActive);
                    if (submitButton) {
                        submitButton.classList.toggle("btn-outline-danger", result.isActive);
                        submitButton.classList.toggle("btn-outline-success", !result.isActive);
                        submitButton.lastChild.textContent = result.isActive ? t("Js6") : t("Js7");
                    }
                } else if (row && result?.isActive === false) {
                    row.remove();
                }

                setStatus(result?.message || t("Js8"));
                try {
                    await refreshResults();
                } catch {
                    setStatus(t("Js9"), "warning");
                }
            } catch (error) {
                setStatus(error.message || t("Js10"), "danger");
            } finally {
                if (submitButton?.isConnected) submitButton.disabled = false;
                spinner?.classList.add("d-none");
            }
            return;
        }

        const form = event.target.closest("form[data-hrms-modal-form]");
        if (!form) return;

        event.preventDefault();
        if (!form.reportValidity()) return;

        const submitButton = form.querySelector('button[type="submit"]');
        const spinner = submitButton?.querySelector(".spinner-border");
        const label = submitButton?.querySelector(".submit-label");
        if (submitButton?.disabled) return;
        if (submitButton) submitButton.disabled = true;
        spinner?.classList.remove("d-none");
        if (label) label.textContent = t("Js11");

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const contentType = response.headers.get("content-type") || "";
            if (contentType.includes("application/json") && response.ok) {
                const result = await response.json();
                modal.hide();
                setStatus(result.message || t("Js12"));
                try {
                    await refreshResults();
                } catch {
                    setStatus(t("Js13"), "warning");
                }
                return;
            }

            if (!response.ok && response.status !== 422) {
                throw new Error(t("Js14"));
            }

            modalContent.innerHTML = await response.text();
            parseValidation(modalContent);
        } catch (error) {
            const alert = modalContent?.querySelector("[data-form-error]");
            if (alert) {
                alert.textContent = error.message || t("Js15");
                alert.classList.remove("d-none");
            } else {
                showModalError(error.message || t("Js15"));
            }
        } finally {
            const currentButton = modalContent?.querySelector('button[type="submit"]');
            if (currentButton) currentButton.disabled = false;
            const currentSpinner = modalContent?.querySelector(".spinner-border");
            currentSpinner?.classList.add("d-none");
            const currentLabel = modalContent?.querySelector(".submit-label");
            if (currentLabel) currentLabel.textContent = t("Js16");
        }
    });

    document.addEventListener("change", async (event) => {
        const departmentSelect = event.target;
        if (departmentSelect.id !== "DepartmentId") return;

        const form = departmentSelect.closest("form[data-position-url]");
        const positionSelect = form?.querySelector("#PositionId");
        if (!positionSelect) return;

        positionSelect.replaceChildren(new Option(t("Js17"), ""));
        if (!departmentSelect.value) return;

        positionSelect.disabled = true;
        try {
            const url = new URL(form.dataset.positionUrl, window.location.origin);
            url.searchParams.set("departmentId", departmentSelect.value);
            const response = await fetch(url);
            if (!response.ok) throw new Error(t("Js18"));
            const positions = await response.json();
            for (const position of positions) {
                positionSelect.add(new Option(position.text, position.id));
            }
        } catch {
            positionSelect.replaceChildren(new Option(t("Js19"), ""));
        } finally {
            positionSelect.disabled = false;
        }
    });
})();
