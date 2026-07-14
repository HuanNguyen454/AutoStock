// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.querySelectorAll("[data-owner-access-chart]").forEach((chart) => {
    const buttons = chart.querySelectorAll("[data-owner-access-toggle]");
    const panels = chart.querySelectorAll("[data-owner-access-period]");

    buttons.forEach((button) => {
        button.addEventListener("click", () => {
            const selectedPeriod = button.dataset.ownerAccessToggle;

            buttons.forEach((candidate) => {
                const isSelected = candidate === button;
                candidate.classList.toggle("active", isSelected);
                candidate.setAttribute("aria-pressed", isSelected.toString());
            });

            panels.forEach((panel) => {
                panel.hidden = panel.dataset.ownerAccessPeriod !== selectedPeriod;
            });
        });
    });
});

document.querySelectorAll("[data-team-owner-select]").forEach((ownerSelect) => {
    ownerSelect.addEventListener("change", () => {
        const warehouseSelect = ownerSelect.form?.querySelector("[data-team-warehouse-select]");
        if (warehouseSelect) {
            warehouseSelect.value = "";
        }
    });
});

document.querySelectorAll("[data-team-activity-filter]").forEach((form) => {
    const daysInput = form.querySelector("[data-team-days-input]");
    const daysButtons = form.querySelectorAll("[data-team-days-option]");

    daysButtons.forEach((button) => {
        button.addEventListener("click", () => {
            if (daysInput) {
                daysInput.value = button.value;
            }

            daysButtons.forEach((candidate) => {
                const isSelected = candidate === button;
                candidate.classList.toggle("active", isSelected);
                candidate.setAttribute("aria-pressed", isSelected.toString());
            });
        });
    });

    form.addEventListener("submit", () => {
        sessionStorage.setItem("asm:scroll-to-team-activity", "1");
        form.action = `${window.location.pathname}#team-activity`;
    });
});

if (window.location.hash === "#team-activity" || sessionStorage.getItem("asm:scroll-to-team-activity") === "1") {
    sessionStorage.removeItem("asm:scroll-to-team-activity");
    document.querySelector("[data-team-activity-section]")?.scrollIntoView({ block: "start" });
}
