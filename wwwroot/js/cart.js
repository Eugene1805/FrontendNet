// Automatically submits cart quantity changes after a short typing debounce so totals recalculate server-side.
document.querySelectorAll("form[data-autosubmit]").forEach(function (form) {
    const quantityInput = form.querySelector("input[name='quantity']");
    if (!quantityInput) return;

    let debounceTimer;

    function submitQuantity() {
        const parsed = parseInt(quantityInput.value, 10);
        const min = parseInt(quantityInput.min, 10) || 1;
        const max = parseInt(quantityInput.max, 10) || 99;
        if (isNaN(parsed)) return;

        quantityInput.value = Math.min(max, Math.max(min, parsed));
        if (quantityInput.value === quantityInput.defaultValue) return;

        quantityInput.readOnly = true;
        form.requestSubmit();
    }

    quantityInput.addEventListener("input", function () {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(submitQuantity, 700);
    });

    quantityInput.addEventListener("change", function () {
        clearTimeout(debounceTimer);
        submitQuantity();
    });
});
