document.addEventListener("DOMContentLoaded", () => {
    const htmlElement = document.documentElement;
    const switchElement = document.getElementById("darkModeSwitch");
    const themeIcon = document.querySelector(".theme-icon");
    if (!switchElement || !themeIcon) return;

    const storedTheme = localStorage.getItem("bsTheme");
    const currentTheme = storedTheme === "dark" || storedTheme === "light" ? storedTheme : "light";

    htmlElement.setAttribute("data-bs-theme", currentTheme);
    switchElement.checked = currentTheme === "dark";
    themeIcon.classList.toggle("bi-moon-stars", currentTheme === "dark");
    themeIcon.classList.toggle("bi-brightness-high", currentTheme !== "dark");

    switchElement.addEventListener("change", function () {
        const nextTheme = this.checked ? "dark" : "light";
        htmlElement.setAttribute("data-bs-theme", nextTheme);
        localStorage.setItem("bsTheme", nextTheme);
        themeIcon.classList.toggle("bi-moon-stars", nextTheme === "dark");
        themeIcon.classList.toggle("bi-brightness-high", nextTheme !== "dark");
    });
});
