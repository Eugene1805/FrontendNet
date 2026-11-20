const validationSettings = {
    validClass: "is-valid",
    errorClass: "is-invalid"
};

$.validator.setDefaults(validationSettings);
$.validator.unobtrusive.options = validationSettings;

function hasUnpairedSurrogate(value) {
    for (let i = 0; i < value.length; i++) {
        const code = value.charCodeAt(i);
        if (code >= 0xD800 && code <= 0xDBFF) {
            if (i + 1 >= value.length) return true;
            const next = value.charCodeAt(i + 1);
            if (next < 0xDC00 || next > 0xDFFF) return true;
            i++;
        } else if (code >= 0xDC00 && code <= 0xDFFF) {
            return true;
        }
    }
    return false;
}

function isSecurePasswordCompliant(value) {
    if (typeof value !== "string" || value.length === 0 || value.length > 256) return false;
    if (value.includes("\0") || hasUnpairedSurrogate(value)) return false;

    const codePoints = Array.from(value).length;
    if (codePoints < 8 || codePoints > 128) return false;

    if (typeof TextEncoder !== "undefined") {
        if (new TextEncoder().encode(value).length > 512) return false;
    }

    return /\p{L}/u.test(value) && /\p{N}/u.test(value) && /[\p{P}\p{S}]/u.test(value);
}

$.validator.addMethod("securepassword", function (value) {
    if (!value) return true;
    return isSecurePasswordCompliant(value);
});

$.validator.unobtrusive.adapters.addBool("securepassword");
