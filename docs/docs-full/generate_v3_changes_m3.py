#!/usr/bin/env python3
"""Generate M3 change documentation: only iplit V3 deltas vs community abdm-v3."""

from pathlib import Path

from docx.shared import Pt

from _doc_style import (
    GRAY,
    add_api_block,
    add_bullet,
    add_code,
    add_cover,
    add_heading_styled,
    add_para,
    add_table,
    new_doc,
    set_run,
)

OUT = Path(__file__).resolve().parent.parent / "V3ChangesExpalined-M3.docx"


def build():
    doc = new_doc()
    add_cover(
        doc,
        "ABDM V3 — Changes on top of Bahmni community hip-service",
        "Milestone 3 (M3): Consent notify and encrypted health-information transfer",
        [
            "Baseline: BahmniCommunity/hip-service abdm-v3 (already on V3 consent/hip/notify and health-information/request)",
            "Changed code: ABDM-repos-2.5.14-Upgraded/hip-service trialsv3-hfrid after 83ddacfe “abdm V3”",
            "Depends on M2 visit-wise HFR cache (same BahmniConfiguration / HfrIdCache)",
            "Official refs: sandbox V3 data-flow and consent HIP notify",
        ],
    )

    add_heading_styled(doc, "1. Scope of this document", 1)
    add_para(
        doc,
        "Community abdm-v3 already implements Gateway POST /api/v3/consent/request/hip/notify, HIP on-notify, "
        "Gateway POST /api/v3/hip/health-information/request, on-request, FHIR encrypt, HIU dataPushUrl, and "
        "POST /api/hiecm/data-flow/v3/health-information/notify. This file records only your deltas: visit HFR on "
        "the transfer and notify, authenticated FHIR push, date-range expansion, consent null-safety, and encryptor fixes.",
    )

    add_heading_styled(doc, "2. What community already had (same callback paths)", 1)
    add_table(
        doc,
        ["#", "Path", "Direction", "Your delta"],
        [
            ["C1", "POST /api/v3/consent/request/hip/notify", "GW → HIP", "REVOKED null-safe; UpdateAsync no-ops if missing"],
            ["C1b", "POST /api/hiecm/consent/v3/request/hip/on-notify", "HIP → GW", "Only when consent row exists"],
            ["H1", "POST /api/v3/hip/health-information/request", "GW → HIP", "REQUEST-ID optional; generate UUID if empty"],
            ["H1b", "POST /api/hiecm/data-flow/v3/health-information/hip/on-request", "HIP → GW", "None on path"],
            ["H2", "HIU dataPushUrl (FHIR bundles)", "HIP → HIU", "Gateway Bearer token; visit HFR header; no shared DefaultRequestHeaders"],
            ["H2b", "POST /api/hiecm/data-flow/v3/health-information/notify", "HIP → GW", "statusNotification.hipId = visit HFR, not clientId"],
        ],
        col_widths=[1.2, 7.2, 2.6, 6.0],
    )

    add_heading_styled(doc, "3. M3 change summary", 1)
    add_table(
        doc,
        ["Change", "Kind", "Why"],
        [
            [
                "FHIR push authenticated with gateway session token",
                "Fix / V3",
                "Community POSTed to dataPushUrl with no Authorization. ABDM V3 HIU pull expects gateway token plus standard headers.",
            ],
            [
                "X-HIP-ID / X-HIU-ID on data push = visit HFR",
                "Behaviour",
                "Same visit-wise HFR as M2. Community notify used gateway ClientId as hipId.",
            ],
            [
                "health-information/notify hipId",
                "Fix",
                "StatusNotification.HipId is the visit HFR so PHR/HIU attribute records to the correct facility.",
            ],
            [
                "doneAt uses DateTime.Today UTC",
                "Fix",
                "Community used DateTime.Now. Your notify uses Today.ToUniversalTime() in ABDM timestamp format.",
            ],
            [
                "OpenMRS visit fromDate −1 day, toDate +1 day",
                "Fix",
                "Boundary records on consent from/to were missed. Community only added +1 on toDate.",
            ],
            [
                "Consent UpdateAsync if artefact missing",
                "Fix",
                "REVOKED/EXPIRED notify for unknown consentId no longer NullReferenceException.",
            ],
            [
                "REVOKED on-notify only if consent loaded",
                "Fix",
                "Community called consent.ConsentArtefact after UpdateAsync even when GetFor returned null.",
            ],
            [
                "Health-information REQUEST-ID not required",
                "Hardening",
                "Empty header → HIP generates a UUID (same idea as community on-generate-token fix).",
            ],
            [
                "Encryptor RNG + XOR length",
                "Fix",
                "RNGCryptoServiceProvider → RandomNumberGenerator.Create(); XOR buffer sized to sender key.",
            ],
            [
                "Consent expiry parse format yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",
                "Fix",
                "ABDM timestamps with millisecond SSS were rejected.",
            ],
            [
                "DefaultHip Collect CareBundle",
                "Fix",
                "Passes FHIR JSON string into CareBundle instead of FileReader.ReadJsonAsync (fhir url / bundle handling).",
            ],
        ],
        col_widths=[5.4, 2.6, 9.0],
    )

    add_heading_styled(doc, "4. Unchanged M3 sequence (for context)", 1)
    add_para(
        doc,
        "Your branch does not add new M3 HIP URLs. The async sequence is the community V3 one, with different "
        "headers and hipId on the last two hops.",
    )
    add_para(
        doc,
        "1. Gateway POST /api/v3/consent/request/hip/notify (GRANTED) → HIP 202 → Hangfire stores artefact → on-notify OK.\n"
        "2. Gateway POST /api/v3/hip/health-information/request → HIP 202 → on-request ACK.\n"
        "3. HIP collects OpenMRS FHIR for granted care contexts, encrypts with HIU key material.\n"
        "4. HIP POST encrypted entries to hiRequest.dataPushUrl.\n"
        "5. HIP POST /api/hiecm/data-flow/v3/health-information/notify with sessionStatus TRANSFERRED or FAILED.",
    )

    add_heading_styled(doc, "5. Consent notify changes", 1)
    add_api_block(
        doc,
        "POST /api/v3/consent/request/hip/notify  (path unchanged)",
        [
            ["HIP HTTP", "202 Accepted; Hangfire StoreConsent"],
            ["Callback", "POST /api/hiecm/consent/v3/request/hip/on-notify"],
            ["GRANTED", "Insert Consent row, then on-notify acknowledgement OK (same as community)"],
            ["Other statuses", "UpdateAsync; if row missing, return without throw"],
            ["REVOKED", "on-notify only if GetFor(consentId) is not null"],
        ],
    )
    add_para(
        doc,
        "Community UpdateAsync loaded the artefact and set Status even when FirstOrDefault was null (NRE). "
        "REVOKED then did consent.ConsentArtefact.ConsentManager.Id on that null. Your ConsentRepository.UpdateAsync "
        "returns immediately if the artefact is absent; ConsentNotificationController checks consent != null before on-notify.",
    )
    add_para(doc, "on-notify body (unchanged shape)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "acknowledgement": { "status": "OK", "consentId": "<artefact consentId>" },\n'
        '  "error": null,\n'
        '  "resp": { "requestId": "<inbound REQUEST-ID>" }\n'
        "}",
    )

    add_heading_styled(doc, "6. Health-information request", 1)
    add_para(
        doc,
        "POST /api/v3/hip/health-information/request still returns 202 and enqueues HealthInformationOf. "
        "REQUEST-ID is no longer [Required]. If the gateway omits it, HIP uses Guid.NewGuid() so on-request "
        "still has a resp.requestId. Logging now includes transactionId, requestId and dataPushUrl.",
    )
    add_para(doc, "Inbound body (ABDM / community shape — unchanged)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "transactionId": "uuid",\n'
        '  "hiRequest": {\n'
        '    "consent": { "id": "<consentId>" },\n'
        '    "dateRange": { "from": "2026-08-01T00:00:00.000Z", "to": "2026-08-22T23:59:59.000Z" },\n'
        '    "dataPushUrl": "https://hiu.example/data/push",\n'
        '    "keyMaterial": { "cryptoAlg": "ECDH", "curve": "Curve25519", "dhPublicKey": { "...": "..." }, "nonce": "..." }\n'
        "  }\n"
        "}",
    )

    add_heading_styled(doc, "7. Collect from OpenMRS — expanded date window", 1)
    add_para(
        doc,
        "OpenMrsPatientData.GetForVisits still calls "
        "ws/rest/v1/hip/{hiTypeRoot}/visit?patientId=&visitUuid=&fromDate=&toDate=. "
        "Community set toDate = consentTo + 1 day and fromDate = consentFrom as-is. "
        "Your branch also subtracts one day from fromDate so same-day / timezone-edge encounters are included.",
    )
    add_code(
        doc,
        "query[\"fromDate\"] = DateTime.Parse(fromDate).AddDays(-1).ToString(\"yyyy-MM-dd\")\n"
        "query[\"toDate\"]   = DateTime.Parse(toDate).AddDays(1).ToString(\"yyyy-MM-dd\")",
    )
    add_para(
        doc,
        "Program care-context path still uses the consent from/to without the extra day. "
        "Empty OpenMRS content is logged at Debug and returns an empty list instead of failing the transfer.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "8. Encrypted FHIR push to HIU (largest M3 delta)", 1)
    add_para(
        doc,
        "DataFlowClient.PostTo now depends on GatewayClient and BahmniConfiguration (community only had "
        "HttpClient + DataFlowNotificationClient + GatewayConfiguration).",
    )
    add_para(doc, "Community", bold=True, space_after=4)
    add_bullet(doc, "Mutated httpClient.DefaultRequestHeaders Authorization (race-prone).")
    add_bullet(doc, "POST dataPushUrl with a minimal request (correlation id only).")
    add_bullet(doc, "Notify hipId = gatewayConfiguration.ClientId.")
    add_para(doc, "Your branch", bold=True, space_after=4)
    add_bullet(doc, "Authenticates via gatewayClient.Authenticate(correlationId); skip push if no token (FAILED notify).")
    add_bullet(doc, "Builds a per-request HttpRequestMessage (does not mutate DefaultRequestHeaders).")
    add_bullet(doc, "Resolves visitUuid from first granted careContextReference (patientId:visitUuid), then HFR cache / OpenMRS / default Id.")
    add_bullet(doc, "CreateHttpRequestWithContentType(..., hipId, requestId, transactionId) adds X-HIU-ID and Transaction_Id.")
    add_bullet(doc, "Same requestId is reused on health-information/notify.")
    add_para(doc, "HIP → HIU (conceptual)", bold=True, space_after=4)
    add_code(
        doc,
        "POST {hiRequest.dataPushUrl}\n"
        "Authorization: Bearer <gateway accessToken>\n"
        "REQUEST-ID / TIMESTAMP / X-CM-ID / CORRELATION-ID\n"
        "X-HIU-ID: <visit HFR id>\n"
        "Transaction_Id: <transactionId>\n"
        "Content-Type: application/json\n"
        "{ transactionId, entries: [ encrypted FHIR ], keyMaterial }",
    )
    add_para(
        doc,
        "Encryption of entries is still ECDH / AES as in community. EncryptorHelper.GenerateRandomKey uses "
        "RandomNumberGenerator.Create() (disposable). XOR of sender/receiver random keys allocates sb to "
        "randomKeySenderBytes.Length so a shorter receiver nonce cannot throw IndexOutOfRange.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "9. Health-information notify to Gateway", 1)
    add_api_block(
        doc,
        "POST /api/hiecm/data-flow/v3/health-information/notify  (path unchanged)",
        [
            ["When", "After push success or failure"],
            ["hipId (community)", "gatewayConfiguration.ClientId"],
            ["hipId (your branch)", "Visit HFR from first granted care context"],
            ["doneAt (community)", "DateTime.Now.ToUniversalTime() formatted"],
            ["doneAt (your branch)", "DateTime.Today.ToUniversalTime() formatted yyyy-MM-ddTHH:mm:ss.fffZ"],
            ["statusResponses", "One per granted careContextReference: DELIVERED or ERRORED"],
        ],
    )
    add_para(doc, "Notify body sketch", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "notification": {\n'
        '    "transactionId": "<same as request>",\n'
        '    "consentId": "<consentId>",\n'
        '    "doneAt": "2026-08-22T00:00:00.000Z",\n'
        '    "notifier": { "type": "HIP", "id": "<gateway clientId>" },\n'
        '    "statusNotification": {\n'
        '      "sessionStatus": "TRANSFERRED",\n'
        '      "hipId": "IN2710001820",\n'
        '      "statusResponses": [\n'
        '        { "careContextReference": "patientId:visitUuid", "hiStatus": "DELIVERED", "description": "Successfully delivered health information" }\n'
        "      ]\n"
        "    }\n"
        "  }\n"
        "}",
    )
    add_para(
        doc,
        "notifier.id remains gateway clientId. Only statusNotification.hipId switched to visit HFR. "
        "If authenticate fails or push throws, sessionStatus=FAILED, hiStatus=ERRORED, and notify still runs.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "10. Consent expiry parsing", 1)
    add_para(
        doc,
        "DataFlow consent-expiry TryParseExact list gained format yyyy-MM-dd'T'HH:mm:ss.SSS'Z' so artefacts "
        "with millisecond SSS from ABDM V3 are accepted when checking whether a request is still in range.",
    )

    add_heading_styled(doc, "11. Typical call order (unchanged product flow, new hipId)", 1)
    add_para(
        doc,
        "1. Complete M2 so care contexts are linked under the correct HFR.\n"
        "2. Patient grants consent in PHR. Gateway hip/notify GRANTED → HIP stores artefact.\n"
        "3. HIU requests data. Gateway health-information/request → HIP 202.\n"
        "4. HIP expands from/to, loads FHIR, encrypts, POSTs dataPushUrl with gateway token and visit HFR.\n"
        "5. HIP notifies Gateway with hipId = that HFR.\n"
        "6. If OpenMRS location has no HFR attribute, notify/push fall back to Bahmni.Id (same as M2).",
    )

    add_heading_styled(doc, "12. Changed files (M3)", 1)
    add_table(
        doc,
        ["File", "Role"],
        [
            ["DataFlow/DataFlowClient.cs", "Auth + visit HFR on push and notify"],
            ["DataFlow/DataFlowController.cs", "Optional REQUEST-ID"],
            ["DataFlow/DataFlow.cs", "Extra expiry datetime format"],
            ["DataFlow/OpenMrsPatientData.cs", "fromDate −1 / toDate +1; safer empty JSON"],
            ["DataFlow/Encryptor/EncryptorHelper.cs", "RNG + XOR length"],
            ["Consent/ConsentNotificationController.cs", "Null-safe REVOKED on-notify"],
            ["Consent/ConsentRepository.cs", "UpdateAsync no-op if missing"],
            ["DefaultHip/DataFlow/Collect.cs", "CareBundle from FHIR JSON string"],
            ["Common/HttpRequestHelper.cs", "CreateHttpRequestWithContentType X-HIU-ID / Transaction_Id"],
        ],
        col_widths=[7.0, 10.0],
    )

    add_heading_styled(doc, "13. Related commits (upgraded repo, M3)", 1)
    add_bullet(doc, "d92be2e / 184864f / … / 0a3c6fe / 35bf92d — Adding healthdata transfer")
    add_bullet(doc, "53cde46 — data encrypt trials")
    add_bullet(doc, "7d339ee — fhir url fix")
    add_bullet(doc, "7c730f7 — Expanding from,to date range")
    add_bullet(doc, "480f557 / d695f7b — Consents / ConsentNotification")
    add_bullet(doc, "0bea8d7 / 5141701 / b63e1e3 / 5668932 — Data transfer with hipId / DataFlowClient")
    add_bullet(doc, "315cd62 — DataNotificationRequest doneAt")
    add_bullet(doc, "b42d091 — Fixes for expiry")

    add_heading_styled(doc, "14. External references", 1)
    add_bullet(doc, "ABDM Sandbox V3: https://sandbox.abdm.gov.in/sandbox/v3/new-documentation")
    add_bullet(doc, "HIP consent notify: POST /api/v3/consent/request/hip/notify → on-notify /consent/v3/request/hip/on-notify")
    add_bullet(doc, "Health information: POST /api/v3/hip/health-information/request → on-request and notify under /api/hiecm/data-flow/v3/")
    add_bullet(doc, "HIP-initiated linking (M2, required before M3): https://sandbox.abdm.gov.in/sandbox/v3/new-documentation?doc=HIPInitiatedlinking")

    footer = doc.add_paragraph()
    r = footer.add_run("End of M3 change notes. M1 = V3ChangesExpalined-M1.docx, M2 = V3ChangesExpalined-M2.docx.")
    set_run(r, size=10, color=GRAY)
    footer.paragraph_format.space_before = Pt(18)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(str(OUT))
    print(f"Wrote {OUT}")


if __name__ == "__main__":
    build()
