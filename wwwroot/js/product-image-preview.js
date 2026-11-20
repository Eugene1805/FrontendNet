document.addEventListener("DOMContentLoaded", () => {
    const coverImage = document.querySelector(".product-cover");
    const fileInput = document.getElementById("Image");
    if (!coverImage || !fileInput) return;

    const defaultSrc = coverImage.dataset.defaultSrc || coverImage.getAttribute("src") || "";

    fileInput.addEventListener("change", () => {
        if (!fileInput.files || fileInput.files.length === 0) {
            if (defaultSrc) coverImage.setAttribute("src", defaultSrc);
            return;
        }

        const file = fileInput.files[0];
        if (!/^image\/(jpeg|png|webp)$/i.test(file.type)) return;

        const reader = new FileReader();
        reader.onload = (event) => {
            if (event.target && typeof event.target.result === "string") {
                coverImage.setAttribute("src", event.target.result);
            }
        };
        reader.readAsDataURL(file);
    });
});
