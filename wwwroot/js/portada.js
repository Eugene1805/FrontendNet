const imagen = document.querySelector(".portada");
const archivo = document.getElementById("Cover");

function CargaImagen() {
    if (!archivo || !archivo.files || !archivo.files.length || !imagen) return;
    const file = archivo.files[0];
    if (!file.type || !file.type.match(/^image\/(jpeg|jpg)$/i)) return;

    const fileReader = new FileReader();
    fileReader.onload = function (event) {
        imagen.setAttribute('src', event.target.result);
    };
    fileReader.readAsDataURL(file);
}

if (archivo) {
    archivo.addEventListener("change", CargaImagen);
    CargaImagen();
}