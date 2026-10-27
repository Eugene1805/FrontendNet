// Recalcula el carrito sin botón de actualizar: al cambiar una cantidad se envía su formulario
// (el servidor recalcula subtotal y total con los precios vigentes).
document.querySelectorAll("form[data-autoenviar]").forEach(function (form) {
    const cantidad = form.querySelector("input[name='cantidad']");
    let temporizador;

    function enviar() {
        const valor = parseInt(cantidad.value, 10);
        const min = parseInt(cantidad.min, 10) || 1;
        const max = parseInt(cantidad.max, 10) || 99;
        if (isNaN(valor)) return;                      // campo vacío mientras se escribe
        cantidad.value = Math.min(max, Math.max(min, valor));
        if (cantidad.value === cantidad.defaultValue) return;   // sin cambios
        cantidad.readOnly = true;
        form.requestSubmit();
    }

    // Al teclear se espera un momento; con las flechas o al salir del campo se envía de inmediato.
    cantidad.addEventListener("input", function () {
        clearTimeout(temporizador);
        temporizador = setTimeout(enviar, 700);
    });
    cantidad.addEventListener("change", function () {
        clearTimeout(temporizador);
        enviar();
    });
});
