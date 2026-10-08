document.addEventListener("DOMContentLoaded", function () {
    const togglePassword = document.getElementById("togglePassword");
    const password = document.getElementById("password");

    if (togglePassword && password) {
        togglePassword.addEventListener("click", function () {
            if (password.type === "password") {
                password.type = "text";
                togglePassword.textContent = "Ocultar";
                togglePassword.setAttribute("aria-label", "Ocultar contrasena");
            } else {
                password.type = "password";
                togglePassword.textContent = "Ver";
                togglePassword.setAttribute("aria-label", "Mostrar contrasena");
            }
        });
    }

    const loginForm = document.getElementById("loginForm");
    const loginButton = document.getElementById("loginButton");

    if (loginForm && loginButton) {
        loginForm.addEventListener("submit", function () {
            loginButton.disabled = true;
            loginButton.textContent = "Iniciando sesion...";
        });
    }
});