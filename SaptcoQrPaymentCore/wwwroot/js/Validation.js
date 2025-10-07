// ------------------------------------------------------------
// ✅ Saptco QR Payment - Client-side Validation Script
// ------------------------------------------------------------

// 📱 Validate Saudi mobile number format
function validateMobile(inputId, errorId) {
    const phoneInput = document.getElementById(inputId);
    const phoneError = document.getElementById(errorId);
    const value = phoneInput.value.trim();
    const phoneRegex = /^05\d{8}$/;

    if (!phoneRegex.test(value)) {
        phoneError.classList.remove("d-none");
        phoneInput.classList.add("is-invalid");
        phoneInput.focus();
        return false;
    }

    phoneError.classList.add("d-none");
    phoneInput.classList.remove("is-invalid");
    return true;
}

// 📧 Validate email format
function validateEmail(inputId, errorId) {
    const emailInput = document.getElementById(inputId);
    const emailError = document.getElementById(errorId);
    const value = emailInput.value.trim();
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (!emailRegex.test(value)) {
        emailError.classList.remove("d-none");
        emailInput.classList.add("is-invalid");
        emailInput.focus();
        return false;
    }

    emailError.classList.add("d-none");
    emailInput.classList.remove("is-invalid");
    return true;
}

// 🧾 Attach validation to any form easily
function attachValidation(formId, config) {
    const form = document.getElementById(formId);
    if (!form) return;

    form.addEventListener("submit", function (e) {
        let valid = true;

        if (config.phone) {
            valid = validateMobile(config.phone.inputId, config.phone.errorId) && valid;
        }

        if (config.email) {
            valid = validateEmail(config.email.inputId, config.email.errorId) && valid;
        }

        if (!valid) e.preventDefault();
    });
}

// 🧍 Validate name field (letters and spaces only)
// 🧾 Attach validation to any form easily
function attachValidation(formId, config) {
    const form = document.getElementById(formId);
    if (!form) return;

    form.addEventListener("submit", function (e) {
        let valid = true;

        // ✅ Add phone validation if configured
        if (config.phone) {
            valid = validateMobile(config.phone.inputId, config.phone.errorId) && valid;
        }

        // ✅ Add email validation if configured
        if (config.email) {
            valid = validateEmail(config.email.inputId, config.email.errorId) && valid;
        }

        // ✅ Add name validation if configured
        if (config.name) {
            valid = validateName(config.name.inputId, config.name.errorId) && valid;
        }

        if (!valid) e.preventDefault();
    });
}

 
// ------------------------------------------------------------
// Usage example inside any Razor view:
// attachValidation("mobileForm", { phone: { inputId: "phone", errorId: "phoneError" } });
// ------------------------------------------------------------
