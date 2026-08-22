#!/usr/bin/env python3
"""Generate M2 change documentation: only iplit V3 deltas vs community abdm-v3."""

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

OUT = Path(__file__).resolve().parent.parent / "V3ChangesExpalined-M2.docx"


def build():
    doc = new_doc()
    add_cover(
        doc,
        "ABDM V3 — Changes on top of Bahmni community hip-service",
        "Milestone 2 (M2): Link care contexts, visit-wise HFR, SMS notify, scan-and-share",
        [
            "Baseline: BahmniCommunity/hip-service abdm-v3 (already on V3 discover/link/carecontext/notify2/share)",
            "Changed code: ABDM-repos-2.5.14-Upgraded/hip-service trialsv3-hfrid after 83ddacfe “abdm V3”",
            "Official refs: sandbox V3 docs, HIP-initiated linking, user-initiated linking swagger",
            "Companion: V3ChangesExpalined-M1.docx (ABHA) and M3.docx (consent / data flow)",
        ],
    )

    add_heading_styled(doc, "1. Scope of this document", 1)
    add_para(
        doc,
        "Community abdm-v3 already migrated user-initiated linking, HIP-initiated linking, SMS notify2 "
        "and scan-and-share to ABDM V3 paths. This file documents only your extra behaviour: visit-wise "
        "HFR ID, link tokens keyed by ABHA+HIP, unique linkReferenceNumber, SMS notify against the latest "
        "visit, and thread-safe maps. HIP paths that Gateway already calls are the same as community; "
        "headers and stored keys are what changed.",
    )

    add_heading_styled(doc, "2. What community already had (same HIP / Gateway paths)", 1)
    add_table(
        doc,
        ["#", "Path", "Direction", "Your delta"],
        [
            ["A1", "POST /api/v3/hip/patient/care-context/discover", "GW → HIP", "Thread-safe DiscoveryReqMap"],
            ["A1b", "POST /api/hiecm/user-initiated-linking/v3/patient/care-context/on-discover", "HIP → GW", "None on path"],
            ["A2", "POST /api/v3/hip/link/care-context/init", "GW → HIP", "LinkPatient thread-safe / unique refs"],
            ["A2b", "POST /api/hiecm/user-initiated-linking/v3/link/care-context/on-init", "HIP → GW", "None on path"],
            ["A3", "POST /api/v3/hip/link/care-context/confirm", "GW → HIP", "None on path"],
            ["A3b", "POST /api/hiecm/user-initiated-linking/v3/link/care-context/on-confirm", "HIP → GW", "None on path"],
            ["B0", "POST /v0.5/hip/new-carecontext", "Bahmni → HIP", "Resolves HFR from visit before add/notify"],
            ["B1", "POST /api/hiecm/v3/token/generate-token", "HIP → GW", "X-HIP-ID = visit HFR, not global hip id"],
            ["B1b", "POST /api/v3/hip/token/on-generate-token", "GW → HIP", "Stores token per (abhaAddress, hipId)"],
            ["B2", "POST /api/hiecm/hip/v3/link/carecontext", "HIP → GW", "Unique linkReferenceNumber; X-HIP-ID visit HFR"],
            ["B2b", "POST /api/v3/link/on_carecontext", "GW → HIP", "None on path"],
            ["B3", "POST /api/hiecm/hip/v3/link/context/notify", "HIP → GW", "X-HIP-ID + token for that visit’s HFR"],
            ["C1", "POST /v0.5/hip/patients/sms/notify", "Bahmni → HIP", "Latest-visit HFR name/id; phone must be mapped"],
            ["C1b", "POST /api/hiecm/hip/v3/link/patient/links/sms/notify2", "HIP → GW", "hip.id / hip.name from visit cache"],
            ["D1", "POST /api/v3/hip/patient/share", "GW → HIP", "None on contract (community V3 already)"],
        ],
        col_widths=[1.2, 8.6, 2.8, 4.4],
    )

    add_heading_styled(doc, "3. M2 change summary", 1)
    add_table(
        doc,
        ["Change", "Kind", "Why"],
        [
            [
                "Visit-wise HFR ID and facility name",
                "New behaviour",
                "Multi-facility Bahmni: each visit location has its own ABDM HFR ID. Community sent one Bahmni.Id on every generate-token / carecontext / notify.",
            ],
            [
                "HfrIdCache / FacilityNameCache",
                "New types",
                "In-memory ConcurrentDictionary keyed by visitUuid.",
            ],
            [
                "AuthConfirm primary key (HealthId, HipId)",
                "DB migration",
                "Same ABHA can hold different X-LINK-TOKENs for different facilities.",
            ],
            [
                "Composite in-memory token key abha##:##hipId",
                "Behaviour",
                "UserAuthMap.HealthIdToAccessToken no longer keyed by ABHA alone.",
            ],
            [
                "Unique linkReferenceNumber per add-context",
                "Fix",
                "Community reused requestId as link ref; overlapping visits collided in Link DB.",
            ],
            [
                "SMS notify uses latest visit HFR",
                "Fix",
                "notify2 hip.id/name must match the facility the patient visited, not the default HIP.",
            ],
            [
                "PhoneNumberToHealthId + HealthIdToLatestVisitUuid",
                "New maps",
                "SMS notify looks up ABHA and visit from phone. CallAddContext seeds phone before generate-token.",
            ],
            [
                "SetAccessToken(healthId, hipId)",
                "Signature change",
                "Generate-token and token cache are facility-scoped.",
            ],
            [
                "ConcurrentDictionary on UserAuthMap / DiscoveryReqMap",
                "Hardening",
                "Avoid races under parallel discover/link/new-carecontext.",
            ],
            [
                "PATH_SET_HFR_ID defined, unused",
                "Note",
                "HFR is loaded from OpenMRS visit location attributes, not a HIP POST.",
            ],
        ],
        col_widths=[5.4, 2.8, 8.8],
    )

    add_heading_styled(doc, "4. Visit-wise HFR ID (core M2 change)", 1)
    add_para(
        doc,
        "ABDM HIP-initiated linking requires header X-HIP-ID on generate-token, POST /api/hiecm/hip/v3/link/carecontext "
        "and POST /api/hiecm/hip/v3/link/context/notify. In a multi-location hospital this must be the HFR ID of the "
        "visit’s OpenMRS location, not a single hospital-wide value from appsettings Bahmni.Id.",
    )
    add_para(doc, "Care context reference format (unchanged)", bold=True, space_after=4)
    add_code(
        doc,
        "careContexts[].referenceNumber = \"{openMrsPatientId}:{visitUuid}\"\n"
        "HIP splits on ':' and takes parts[1] as visitUuid",
    )
    add_para(doc, "Resolution", bold=True, space_after=4)
    add_bullet(doc, "On first care context of POST /v0.5/hip/new-carecontext, CareContextController calls BahmniConfiguration.SetHfrIdForVisitAsync(visitUuid).")
    add_bullet(doc, "HIP GET ws/rest/v1/visit/{visitUuid} then GET ws/rest/v1/location/{uuid}?v=full.")
    add_bullet(doc, "Location attribute display/name matching “ABDM HFR ID” (or containing “HFR ID”) → cache HFR.")
    add_bullet(doc, "Attribute “ABDM HFR Name” / “HFR Name” → cache facility name (SMS notify2 hip.name).")
    add_bullet(doc, "If missing, HIP falls back to BahmniConfiguration.Id / Name (community behaviour).")
    add_para(doc, "OpenMRS location attributes required", bold=True, space_after=4)
    add_table(
        doc,
        ["Attribute", "Used as"],
        [
            ["ABDM HFR ID", "X-HIP-ID, generate-token hipId, SMS hip.id, data-flow hipId (M3)"],
            ["ABDM HFR Name", "SMS notify2 hip.name (URL-encoded)"],
        ],
        col_widths=[5.0, 12.0],
    )

    add_heading_styled(doc, "5. Link token per (ABHA address, HFR ID)", 1)
    add_para(
        doc,
        "Community stored one X-LINK-TOKEN per ABHA address. ABDM tokens are facility-scoped (JWT claim abhaAddress, "
        "issued for an X-HIP-ID). Your branch stores them per pair.",
    )
    add_para(doc, "In-memory key", bold=True, space_after=4)
    add_code(doc, "compositeKey = abhaAddress + \"##:##\" + hipId\nUserAuthMap.HealthIdToAccessToken[compositeKey] = jwt")
    add_para(doc, "Database (migration 20260621120000_AddHipIdToAuthConfirm)", bold=True, space_after=4)
    add_bullet(doc, "Adds non-null HipId column on AuthConfirm.")
    add_bullet(doc, "Primary key becomes (HealthId, HipId). Down migration restores PK on HealthId only.")
    add_para(
        doc,
        "GetAccessToken / save path now take hipId. On-generate-token callback upserts AuthConfirm(healthId, hipId, token) "
        "using RequestIdToHipId[requestId] set before generate-token.",
        size=10,
        color=GRAY,
    )
    add_para(doc, "Generate-token body (unchanged ABDM shape; header is the change)", bold=True, space_after=4)
    add_code(
        doc,
        "POST /api/hiecm/v3/token/generate-token\n"
        "Headers: Authorization, REQUEST-ID, TIMESTAMP, X-CM-ID, X-HIP-ID=<visit HFR>\n"
        "{\n"
        '  "abhaAddress": "first.last@sbx",\n'
        '  "name": "First Last",\n'
        '  "gender": "F",\n'
        '  "yearOfBirth": "1990"\n'
        "}",
    )
    add_para(
        doc,
        "SetAccessToken now seeds PhoneNumberToHealthId from NdhmDemographics before generate-token so SMS notify "
        "does not race. Polling uses TryRemove on RequestIdToErrorMessage instead of ContainsKey + Remove.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "6. HIP-initiated add / notify", 1)
    add_heading_styled(doc, "6.1 Unique linkReferenceNumber", 2)
    add_para(
        doc,
        "AddContext now generates Guid.NewGuid() as linkReferenceNumber and uses that in SaveInitiatedLinkRequest "
        "and SaveRequestWith. Community used the inbound requestId for both, so two overlapping add-context calls "
        "could share a link row. Gateway payload patient[] grouping by hiType is unchanged from community V3.",
    )
    add_heading_styled(doc, "6.2 new-carecontext processing", 2)
    add_para(
        doc,
        "POST /v0.5/hip/new-carecontext still decides add vs notify by whether referenceNumber is already linked. "
        "Your loop additionally, on the first context, extracts visitUuid and warms HFR cache. "
        "CallNotifyContext / CallAddContext then read hipId from cache (or OpenMRS, or default).",
    )
    add_para(doc, "Bahmni body (unchanged)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "patientReferenceNumber": "openMrsPatientUuid",\n'
        '  "patientName": "First Last",\n'
        '  "healthId": "first.last@sbx",\n'
        '  "careContexts": [\n'
        "    {\n"
        '      "referenceNumber": "openMrsPatientUuid:visitUuid",\n'
        '      "display": "OPD 22-Aug-2026",\n'
        '      "hiTypes": ["OPConsultation", "Prescription"]\n'
        "    }\n"
        "  ]\n"
        "}",
    )
    add_para(
        doc,
        "Notify still POSTs /api/hiecm/hip/v3/link/context/notify. hip.id in the body and X-HIP-ID are the visit HFR. "
        "Missing in-memory token for that composite key is an error (500 on Bahmni call).",
        size=10,
    )

    add_heading_styled(doc, "7. SMS notify (no ABHA at registration)", 1)
    add_para(
        doc,
        "Community forwarded phoneNo with default Bahmni hip id/name to notify2. Your SmsNotificationService "
        "requires a phone already mapped to an ABHA (from M1/login or CallAddContext demographics). "
        "It then prefers HealthIdToLatestVisitUuid for that ABHA to fill hip.id and hip.name.",
    )
    add_api_block(
        doc,
        "POST /v0.5/hip/patients/sms/notify",
        [
            ["Bahmni body", '{ "phoneNo": "98xxxxxxxx" }'],
            ["Gateway", "POST /api/hiecm/hip/v3/link/patient/links/sms/notify2"],
            ["Callback", "POST /api/v3/patients/sms/on-notify (log only)"],
            ["Empty phone", "400 Phone number is required"],
            ["Unknown phone", "400 No details found for the given phone number …"],
        ],
    )
    add_para(doc, "HIP → Gateway body", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "notification": {\n'
        '    "phoneNo": "98xxxxxxxx",\n'
        '    "hip": {\n'
        '      "name": "<URL-encoded facility name>",\n'
        '      "id": "<URL-encoded HFR id>"\n'
        "    }\n"
        "  }\n"
        "}",
    )
    add_bullet(doc, "Phone is trimmed; lookup normalizes +91 / 91 prefix (UserAuthMap.NormalizePhoneNumber).")
    add_bullet(doc, "If latest visit has cached HFR id/name, those override defaults.")
    add_para(
        doc,
        "If the patient never completed M1/login on this HIP instance, SMS notify cannot resolve ABHA and returns 400. "
        "That is stricter than community, which always used default hip id.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "8. Scan and share / user-initiated linking", 1)
    add_para(
        doc,
        "No new HIP callback paths. Community already uses POST /api/v3/hip/patient/share and "
        "PHR sandbox QR https://phrsbx.abdm.gov.in/share-profile?hip-id={HFR}&counter-id={desk}. "
        "Your M2 work does not change the share payload; the HFR in the QR should match the location HFR "
        "used later in HIP-initiated linking.",
    )
    add_para(
        doc,
        "User-initiated discover/init/confirm paths stay V3. LinkPatient now uses a unique linkReferenceNumber "
        "per initiated link (same uniqueness idea as add-context). DiscoveryReqMap is ConcurrentDictionary.",
        size=10,
    )

    add_heading_styled(doc, "9. Operational notes for Bahmni / OpenMRS", 1)
    add_bullet(doc, "Every visit location that participates in ABDM must have ABDM HFR ID (and ideally ABHA HFR Name).")
    add_bullet(doc, "new-carecontext referenceNumber must remain patientId:visitUuid so HIP can load the visit.")
    add_bullet(doc, "Apply EF migration AddHipIdToAuthConfirm before going live; old AuthConfirm rows get HipId=\"\".")
    add_bullet(doc, "Link tokens and HFR caches are in-memory plus AuthConfirm DB. Restart loses cache; DB still has tokens per hipId.")
    add_bullet(doc, "Do not send a single hospital HIP id in X-HIP-ID if locations have different HFR IDs.")
    add_bullet(doc, "PATH_SET_HFR_ID (/v0.5/hip/set-hfr-id) is unused; do not build a UI around it.")

    add_heading_styled(doc, "10. New / changed files (M2)", 1)
    add_table(
        doc,
        ["File", "Role"],
        [
            ["Common/Model/HfrIdCache.cs", "NEW — visitUuid → HFR ID"],
            ["Common/Model/FacilityNameCache.cs", "NEW — visitUuid → facility name"],
            ["Common/Model/BahmniConfiguration.cs", "OpenMRS visit/location lookup; extract visitUuid"],
            ["UserAuth/Database/Migrations/20260621120000_AddHipIdToAuthConfirm.cs", "NEW — PK (HealthId, HipId)"],
            ["UserAuth/AuthConfirm.cs", "HipId property; constructor (healthId, hipId, token)"],
            ["UserAuth/UserAuthMap.cs", "Concurrent maps; RequestIdToHipId; phone/visit helpers"],
            ["UserAuth/UserAuthService.cs / Repository", "Token get/save by hipId"],
            ["Link/CareContextService.cs / Controller", "HFR warm-up; unique link ref; composite token"],
            ["SmsNotification/SmsNotificationService.cs", "Latest-visit hip id/name; 400 on unknown phone"],
            ["Common/Constants.cs", "COMPOSITE_AUTH_KEY_SEPARATOR, PATH_SET_HFR_ID"],
        ],
        col_widths=[8.5, 8.5],
    )

    add_heading_styled(doc, "11. Related commits (upgraded repo, M2)", 1)
    add_bullet(doc, "73d0328 — Passing hipId for token generation")
    add_bullet(doc, "6719449 / 1dbbd45 / 0ecc2d1 / f1077dd / b41e9e2 / 5757b3b — HFR id/name per visit/location")
    add_bullet(doc, "d2ec07d — Unique linkReferenceNumber per contexts")
    add_bullet(doc, "8079199 / f8d7f35 — SMS notify as per latest care context")
    add_bullet(doc, "c54283e … 06679ac — link context & DB fixes")
    add_bullet(doc, "31723e3 / 448375d — ConcurrentDictionary")

    add_heading_styled(doc, "12. External references", 1)
    add_bullet(doc, "HIP-initiated linking: https://sandbox.abdm.gov.in/sandbox/v3/new-documentation?doc=HIPInitiatedlinking")
    add_bullet(doc, "User-initiated linking swagger: sandbox v3 swagger integration_label abdm_user_initiated_linking_phr")
    add_bullet(doc, "ABDM Sandbox V3: https://sandbox.abdm.gov.in/sandbox/v3/new-documentation")
    add_bullet(doc, "Facility QR: PHR domain phrsbx.abdm.gov.in/share-profile (see docs/docs-iplit/ABDM_Facility_QR_Code_Guide.md)")

    footer = doc.add_paragraph()
    r = footer.add_run("End of M2 change notes. See V3ChangesExpalined-M3.docx for consent and health-information transfer.")
    set_run(r, size=10, color=GRAY)
    footer.paragraph_format.space_before = Pt(18)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(str(OUT))
    print(f"Wrote {OUT}")


if __name__ == "__main__":
    build()
