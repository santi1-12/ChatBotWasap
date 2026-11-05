(function (root, factory) {
    const api = factory();

    if (typeof module === "object" && module.exports) {
        module.exports = api;
    }
    if (root) {
        root.MetaEmbeddedSignupEvents = api;
    }
})(globalThis, function () {
    const supportedEvents = new Set([
        "FINISH",
        "FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING",
        "CANCEL",
        "ERROR"
    ]);

    function classifyMessage(rawMessage) {
        let message = rawMessage;
        if (typeof rawMessage === "string") {
            try {
                message = JSON.parse(rawMessage);
            } catch {
                return null;
            }
        }

        if (!message || typeof message !== "object" ||
            message.type !== "WA_EMBEDDED_SIGNUP" ||
            typeof message.event !== "string" ||
            !supportedEvents.has(message.event)) {
            return null;
        }

        return message.event;
    }

    function isTrustedMetaOrigin(originValue) {
        try {
            const origin = new URL(originValue);
            return origin.protocol === "https:" &&
                (origin.hostname === "facebook.com" || origin.hostname.endsWith(".facebook.com"));
        } catch {
            return false;
        }
    }

    function matchesOnboardingType(eventName, onboardingType) {
        return (eventName === "FINISH" && onboardingType === "Standard") ||
            (eventName === "FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING" &&
                onboardingType === "Coexistence");
    }

    return { classifyMessage, isTrustedMetaOrigin, matchesOnboardingType };
});
