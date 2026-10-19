(() => {
    "use strict";
    if (!document.body.classList.contains("workspace-layout")) return;
    const ar = document.documentElement.lang.startsWith("ar");
    const words = ar ? {
        loading: "جارٍ التحميل…", saving: "جارٍ الحفظ…", wait: "انتظر انتهاء الحفظ الحالي.",
        network: "تعذر التحميل. تحقق من الاتصال وحاول مجددًا.",
        saveError: "تعذر التأكد من نتيجة الحفظ. راجع السجل قبل إعادة الإرسال.",
        failed: "تعذر تنفيذ الطلب. بقيت البيانات المدخلة محفوظة في النموذج.",
        limit: "وصلت إلى حد الطلبات. حاول بعد", seconds: "ثانية."
    } : {
        loading: "Loading…", saving: "Saving…", wait: "Wait for the current save to finish.",
        network: "Unable to load. Check your connection and try again.",
        saveError: "Unable to confirm the save result. Check the record before submitting again.",
        failed: "The request failed. Your form entries have been kept.",
        limit: "Request limit reached. Try again in", seconds: "seconds."
    };
    const status = document.createElement("span");
    status.className = "workspace-request-status";
    status.setAttribute("role", "status");
    status.setAttribute("aria-live", "polite");
    document.querySelector(".workspace-menu-bar")?.append(status);
    const sidebar = document.getElementById("workspaceSidebar");
    const menuButton = document.querySelector('[data-bs-target="#workspaceSidebar"]');
    sidebar?.addEventListener("show.bs.offcanvas", () => menuButton?.setAttribute("aria-expanded", "true"));
    sidebar?.addEventListener("hide.bs.offcanvas", () => menuButton?.setAttribute("aria-expanded", "false"));
    let current = null;
    const tell = (message, error = false) => {
        status.textContent = message;
        status.dataset.error = String(error);
    };
    const eligible = url => url.origin === location.origin &&
        /^\/(Home(?:\/|$)|MyProfile(?:\/|$)|UserAccounts(?:\/|$)|Attendance(?:\/|$)|LeaveManagement(?:\/|$)|Employees(?:\/|$)|Departments(?:\/|$)|Positions(?:\/|$)|Reports(?:\/|$)|AuditLogs(?:\/|$)|$)/i.test(url.pathname) &&
        !/^\/Home\/SetLanguage/i.test(url.pathname);
    const closeSidebar = () => {
        const sidebar = document.getElementById("workspaceSidebar");
        if (sidebar) bootstrap.Offcanvas.getInstance(sidebar)?.hide();
    };
    const load = async (url, { method = "GET", body, historyMode = "push", form } = {}) => {
        if (current?.writing) { tell(words.wait); return; }
        current?.controller.abort();
        const operation = { controller: new AbortController(), writing: method !== "GET" };
        current = operation;
        const main = document.getElementById("main-content");
        const buttons = form ? [...form.querySelectorAll("button[type=submit], input[type=submit]")].filter(b => !b.disabled) : [];
        buttons.forEach(b => { b.disabled = true; });
        main.setAttribute("aria-busy", "true");
        tell(operation.writing ? words.saving : words.loading);
        closeSidebar();
        try {
            const response = await fetch(url, {
                method, body, credentials: "same-origin", signal: operation.controller.signal,
                // Keep the full layout response: older X-Requested-With handlers return modal fragments.
                headers: { "X-HRMS-Navigation": "1", "Accept": "text/html" }, cache: "no-store"
            });
            if (response.status === 429) {
                const seconds = response.headers.get("Retry-After") || "60";
                throw new Error(`${words.limit} ${seconds} ${words.seconds}`);
            }
            if (!response.ok && response.status !== 422) throw new Error(words.failed);
            if (!response.headers.get("content-type")?.includes("text/html")) throw new Error(words.failed);
            const doc = new DOMParser().parseFromString(await response.text(), "text/html");
            if (!doc.body.classList.contains("workspace-layout")) {
                // An expired session must return to a complete login page.
                location.assign(response.url);
                return;
            }
            const incoming = doc.getElementById("main-content");
            if (!incoming) throw new Error(words.failed);
            incoming.querySelectorAll("script").forEach(script => script.remove());
            document.querySelectorAll(".modal.show").forEach(el => bootstrap.Modal.getInstance(el)?.hide());
            main.replaceChildren(...[...incoming.childNodes].map(node => document.importNode(node, true)));
            document.title = doc.title;
            document.querySelectorAll(".main-nav a").forEach(link => {
                const fresh = [...doc.querySelectorAll(".main-nav a")].find(a => a.getAttribute("href") === link.getAttribute("href"));
                link.classList.toggle("active", !!fresh?.classList.contains("active"));
                if (link.classList.contains("active")) link.setAttribute("aria-current", "page");
                else link.removeAttribute("aria-current");
            });
            if (historyMode === "push" && response.url !== location.href) history.pushState({}, "", response.url);
            window.jQuery?.validator?.unobtrusive?.parse(main);
            document.dispatchEvent(new CustomEvent("hrms:content-updated"));
            const heading = main.querySelector("h1");
            heading?.setAttribute("tabindex", "-1");
            heading?.focus({ preventScroll: true });
            window.scrollTo({ top: 0, behavior: "instant" });
            tell("");
        } catch (error) {
            if (error.name !== "AbortError") tell(error instanceof TypeError ? (operation.writing ? words.saveError : words.network) : error.message, true);
        } finally {
            buttons.forEach(b => { b.disabled = false; });
            if (current === operation) { main.removeAttribute("aria-busy"); current = null; }
        }
    };
    history.replaceState({}, "", location.href);
    document.addEventListener("click", async event => {
        const link = event.target.closest("a[href]");
        if (link?.hasAttribute("data-report-download") && event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {
            event.preventDefault();
            if (link.getAttribute("aria-disabled") === "true") return;
            link.setAttribute("aria-disabled", "true");
            tell(words.loading);
            try {
                const response = await fetch(link.href, { credentials: "same-origin", cache: "no-store" });
                if (!response.ok) {
                    const problem = await response.json().catch(() => null);
                    throw new Error(problem?.title || words.failed);
                }
                const mime = response.headers.get("content-type") || "";
                if (!mime.includes("application/pdf") && !mime.includes("spreadsheetml.sheet")) throw new Error(words.failed);
                const filename = /filename="?([^";]+)/i.exec(response.headers.get("content-disposition") || "")?.[1] || "report";
                const url = URL.createObjectURL(await response.blob());
                const anchor = document.createElement("a");
                anchor.href = url; anchor.download = filename;
                document.body.append(anchor); anchor.click(); anchor.remove();
                setTimeout(() => URL.revokeObjectURL(url), 1000);
                tell("");
            } catch (error) { tell(error instanceof TypeError ? words.network : error.message, true); }
            finally { link.removeAttribute("aria-disabled"); }
            return;
        }
        if (event.defaultPrevented || !link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey ||
            link.hasAttribute("download") || link.target || link.closest(".language-switch") || link.matches("[data-modal-url], [data-bs-toggle]") ||
            link.closest(".disabled, .modal, [data-no-ajax]")) return;
        if (!link.closest("#main-content, .main-nav")) return;
        const url = new URL(link.href);
        if (!eligible(url) || (url.hash && url.pathname === location.pathname && url.search === location.search)) return;
        event.preventDefault();
        load(url.href);
    });
    document.addEventListener("submit", event => {
        const form = event.target;
        if (event.defaultPrevented || !(form instanceof HTMLFormElement) || !form.closest("#main-content") ||
            form.matches("[data-active-form], [data-hrms-modal-form], [data-no-ajax]") || form.target) return;
        const url = new URL(form.action || location.href);
        if (!eligible(url)) return;
        event.preventDefault();
        if (!form.reportValidity() || (window.jQuery?.validator && !window.jQuery(form).valid())) return;
        const method = (form.method || "GET").toUpperCase();
        const data = new FormData(form);
        if (event.submitter?.name) data.append(event.submitter.name, event.submitter.value);
        if (method === "GET") {
            url.search = new URLSearchParams(data).toString();
            load(url.href);
        } else if (method === "POST") {
            load(url.href, { method, body: data, form });
        }
    });
    window.addEventListener("popstate", () => load(location.href, { historyMode: "none" }));
})();
