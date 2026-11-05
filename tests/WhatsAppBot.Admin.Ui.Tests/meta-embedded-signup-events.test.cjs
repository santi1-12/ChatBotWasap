const assert = require("node:assert/strict");
const { test } = require("node:test");
const signupEvents = require("../../src/WhatsAppBot.Admin/wwwroot/js/meta-embedded-signup-events.js");

test("classifies standard completion", () => {
    assert.equal(
        signupEvents.classifyMessage({
            type: "WA_EMBEDDED_SIGNUP",
            event: "FINISH",
            data: { waba_id: "waba-1", phone_number_id: "phone-1" }
        }),
        "FINISH"
    );
    assert.equal(signupEvents.matchesOnboardingType("FINISH", "Standard"), true);
    assert.equal(signupEvents.matchesOnboardingType("FINISH", "Coexistence"), false);
});

test("classifies coexistence completion without requiring a phone number id", () => {
    assert.equal(
        signupEvents.classifyMessage(JSON.stringify({
            type: "WA_EMBEDDED_SIGNUP",
            event: "FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING",
            data: { waba_id: "waba-1" },
            version: 3
        })),
        "FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING"
    );
    assert.equal(
        signupEvents.matchesOnboardingType("FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING", "Coexistence"),
        true
    );
    assert.equal(
        signupEvents.matchesOnboardingType("FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING", "Standard"),
        false
    );
});

test("classifies cancel and error but ignores malformed or unknown messages", () => {
    assert.equal(signupEvents.classifyMessage({ type: "WA_EMBEDDED_SIGNUP", event: "CANCEL" }), "CANCEL");
    assert.equal(signupEvents.classifyMessage({ type: "WA_EMBEDDED_SIGNUP", event: "ERROR" }), "ERROR");
    assert.equal(signupEvents.classifyMessage("{invalid"), null);
    assert.equal(signupEvents.classifyMessage({ type: "OTHER", event: "FINISH" }), null);
    assert.equal(signupEvents.classifyMessage({ type: "WA_EMBEDDED_SIGNUP", event: "UNKNOWN" }), null);
});

test("trusts only HTTPS Meta Facebook origins", () => {
    assert.equal(signupEvents.isTrustedMetaOrigin("https://facebook.com"), true);
    assert.equal(signupEvents.isTrustedMetaOrigin("https://connect.facebook.com"), true);
    assert.equal(signupEvents.isTrustedMetaOrigin("http://facebook.com"), false);
    assert.equal(signupEvents.isTrustedMetaOrigin("https://facebook.com.attacker.test"), false);
    assert.equal(signupEvents.isTrustedMetaOrigin("https://evilfacebook.com"), false);
});
