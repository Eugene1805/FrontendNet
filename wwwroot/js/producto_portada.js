const imagen = document.querySelector(".portada");
const archivo = document.getElementById("FileId");

function CargaImagen() {

    if (archivo.selectedIndex > 0) {
        const path = imagen.dataset.url + "/api/files/" + archivo.options[archivo.selectedIndex].value;
        imagen.src = path;
    }
}

archivo.addEventListener("change", CargaImagen);