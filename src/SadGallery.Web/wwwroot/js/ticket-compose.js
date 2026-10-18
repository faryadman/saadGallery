(() => {
    "use strict";
    const number = new Intl.NumberFormat("fa-IR");
    document.querySelectorAll("[data-ticket-compose]").forEach(form => {
        const message = form.querySelector("[data-message-input]");
        const counter = form.querySelector("[data-message-counter]");
        if (message && counter) {
            const update = () => { counter.textContent = `${number.format(message.value.length)} از ${number.format(message.maxLength)} نویسه`; };
            message.addEventListener("input", update);
            update();
        }
        const picker = form.querySelector("[data-photo-picker]");
        if (!picker) return;
        const input = picker.querySelector("input[type=file]");
        const zone = picker.querySelector("[data-photo-drop]");
        const previews = picker.querySelector("[data-photo-previews]");
        const summary = picker.querySelector("[data-photo-summary]");
        const error = picker.querySelector("[data-photo-error]");
        const disclosure = picker.closest("[data-photo-disclosure]");
        const count = disclosure?.querySelector("[data-photo-count]");
        zone.hidden = false;
        input.classList.add("visually-hidden");
        const maxCount = Number(picker.dataset.maxCount);
        const maxBytes = Number(picker.dataset.maxBytes);
        let files = [];
        let urls = [];
        const supportsTransfer = typeof DataTransfer === "function";
        const showError = text => { error.textContent = text; error.hidden = !text; if (text && disclosure) disclosure.open = true; };
        const syncInput = () => {
            if (!supportsTransfer) return;
            const transfer = new DataTransfer();
            files.forEach(file => transfer.items.add(file));
            input.files = transfer.files;
        };
        const render = () => {
            urls.forEach(url => URL.revokeObjectURL(url));
            urls = [];
            previews.replaceChildren();
            previews.hidden = files.length === 0;
            summary.textContent = files.length ? `${number.format(files.length)} عکس از ${number.format(maxCount)} عکس انتخاب شده است.` : "هنوز عکسی انتخاب نشده است.";
            if (count) count.textContent = files.length ? ` · ${number.format(files.length)} عکس انتخاب‌شده` : "";
            files.forEach((file, index) => {
                const tile = document.createElement("figure");
                tile.className = "sg-photo-tile mb-0";
                const image = document.createElement("img");
                const url = URL.createObjectURL(file);
                urls.push(url);
                image.src = url;
                image.alt = `پیش‌نمایش ${file.name}`;
                const caption = document.createElement("figcaption");
                caption.className = "sg-photo-caption";
                const name = document.createElement("span");
                name.textContent = file.name;
                name.title = file.name;
                caption.append(name);
                if (supportsTransfer) {
                    const remove = document.createElement("button");
                    remove.type = "button";
                    remove.className = "btn btn-sm btn-outline-danger sg-touch";
                    remove.textContent = "حذف";
                    remove.setAttribute("aria-label", `حذف عکس ${file.name}`);
                    remove.addEventListener("click", () => {
                        files.splice(index, 1);
                        syncInput();
                        showError("");
                        render();
                        input.focus();
                    });
                    caption.append(remove);
                }
                tile.append(image, caption);
                previews.append(tile);
            });
        };
        const choose = incoming => {
            const combined = supportsTransfer ? [...files, ...incoming] : incoming;
            const unique = new Map(combined.map(file => [`${file.name}:${file.size}:${file.lastModified}`, file]));
            const next = [...unique.values()];
            let problem = "";
            if (next.length > maxCount) problem = "در هر پیام حداکثر ۳ عکس انتخاب کنید؛ ابتدا یکی از عکس‌های انتخاب‌شده را حذف کنید.";
            else if (next.some(file => !file.size || file.size > maxBytes)) problem = "هر عکس باید غیرخالی و حداکثر ۵ مگابایت باشد.";
            else if (next.some(file => !/\.(jpe?g|png|webp)$/i.test(file.name) || (file.type && !["image/jpeg", "image/png", "image/webp"].includes(file.type)))) problem = "فقط عکس‌های JPEG، PNG و WebP را انتخاب کنید.";
            if (problem) {
                showError(problem);
                if (supportsTransfer) syncInput();
                else { input.value = ""; files = []; render(); }
                return;
            }
            files = next;
            syncInput();
            showError("");
            render();
        };
        input.addEventListener("change", () => choose(Array.from(input.files)));
        if (supportsTransfer) {
            ["dragenter", "dragover"].forEach(event => zone.addEventListener(event, e => {
                e.preventDefault(); zone.classList.add("is-dragging");
            }));
            ["dragleave", "drop"].forEach(event => zone.addEventListener(event, e => {
                e.preventDefault(); zone.classList.remove("is-dragging");
            }));
            zone.addEventListener("drop", e => choose(Array.from(e.dataTransfer.files)));
        }
        window.addEventListener("pagehide", () => urls.forEach(url => URL.revokeObjectURL(url)));
        render();
    });
})();
