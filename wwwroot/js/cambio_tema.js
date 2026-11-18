document.addEventListener('DOMContentLoaded', () => {
    const htmlElement = document.documentElement;
    const switchElement = document.getElementById('darkModeSwitch');
    const modoLuz = document.querySelector('.modo-luz');
    if (!switchElement || !modoLuz) return;

    // SEC-13: Valida que el valor en localStorage pertenezca estrictamente a la lista blanca
    const storedTheme = localStorage.getItem('bsTheme');
    const currentTheme = (storedTheme === 'dark' || storedTheme === 'light') ? storedTheme : 'light';
    htmlElement.setAttribute('data-bs-theme', currentTheme);
    switchElement.checked = currentTheme === 'dark';
    modoLuz.classList.toggle('bi-moon-stars', currentTheme === 'dark');
    modoLuz.classList.toggle('bi-brightness-high', currentTheme !== 'dark');

    switchElement.addEventListener('change', function () {
        const newTheme = this.checked ? 'dark' : 'light';
        htmlElement.setAttribute('data-bs-theme', newTheme);
        localStorage.setItem('bsTheme', newTheme);
        modoLuz.classList.toggle('bi-moon-stars', newTheme === 'dark');
        modoLuz.classList.toggle('bi-brightness-high', newTheme !== 'dark');
    });
});