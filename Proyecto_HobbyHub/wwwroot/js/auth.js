document.addEventListener("DOMContentLoaded", function () {

    // Mostrar / ocultar contraseña
    const togglePassword =
        document.getElementById("togglePassword");

    const password =
        document.getElementById("password");

    if (togglePassword && password) {

        togglePassword.addEventListener("click", function () {

            if (password.type === "password") {

                password.type = "text";

                togglePassword.textContent = "🙈";

            } else {

                password.type = "password";

                togglePassword.textContent = "👁";

            }

        });
    }


    // Evitar doble envío del formulario
    const loginForm =
        document.getElementById("loginForm");

    const loginButton =
        document.getElementById("loginButton");

    if (loginForm && loginButton) {

        loginForm.addEventListener("submit", function () {

            loginButton.disabled = true;

            loginButton.textContent = "Iniciando sesión...";

        });
    }

});