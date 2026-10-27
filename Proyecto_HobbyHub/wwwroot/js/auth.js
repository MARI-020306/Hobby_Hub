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
        loginForm.addEventListener("submit", function (event) {
            if (event.defaultPrevented || !loginForm.checkValidity() ||
                (window.jQuery && window.jQuery.fn.valid && !window.jQuery(loginForm).valid())) {
                event.preventDefault();
                return;
            }
            if (loginButton.disabled) {
                event.preventDefault();
                return;
            }
            loginButton.disabled = true;
            loginButton.textContent = "Iniciando sesion...";
        });
        window.addEventListener("pageshow", function () {
            loginButton.disabled = false;
            loginButton.textContent = "Iniciar sesión";
        });
    }
});
