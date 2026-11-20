document.addEventListener("DOMContentLoaded", () => {
    const profileSelect = document.getElementById("Profile");
    const storeNameGroup = document.getElementById("storeNameGroup");
    const storeNameInput = document.getElementById("StoreName");
    if (!profileSelect || !storeNameGroup || !storeNameInput) return;

    function syncStoreNameVisibility() {
        const isSeller = profileSelect.value === "SELLER";
        storeNameGroup.classList.toggle("d-none", !isSeller);
        storeNameInput.required = isSeller;
        if (!isSeller) {
            storeNameInput.value = "";
        }
    }

    syncStoreNameVisibility();
    profileSelect.addEventListener("change", syncStoreNameVisibility);
});
