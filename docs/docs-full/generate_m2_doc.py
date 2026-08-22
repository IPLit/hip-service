#!/usr/bin/env python3
"""Generate M2 linking documentation Word file for ABDM V3 HIP service."""

from pathlib import Path

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
from docx.shared import Cm, Inches, Pt, RGBColor

OUT = Path(__file__).resolve().parent / "FlowExpalined-M2.docx"
ASSETS = Path(
    r"C:\Users\Lenovo\.cursor\projects\c-ShilpaWkspace-current-iplit-ABDM-repos-2-5-14-Upgraded-hip-service\assets"
)

NAVY = RGBColor(0x1B, 0x3A, 0x5F)
TEAL = RGBColor(0x0D, 0x73, 0x73)
DARK = RGBColor(0x22, 0x22, 0x22)
GRAY = RGBColor(0x55, 0x55, 0x55)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
HEADER_FILL = "1B3A5F"
ALT_ROW = "F3F7FA"


def set_run(run, size=11, bold=False, color=DARK, font="Calibri"):
    run.font.name = font
    run._element.rPr.rFonts.set(qn("w:eastAsia"), font)
    run.font.size = Pt(size)
    run.bold = bold
    run.font.color.rgb = color


def shade_cell(cell, hex_color):
    tc = cell._tc
    tcPr = tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), hex_color)
    shd.set(qn("w:val"), "clear")
    tcPr.append(shd)


def set_cell_text(cell, text, bold=False, color=DARK, size=9, align="left"):
    cell.text = ""
    p = cell.paragraphs[0]
    if align == "center":
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run(text)
    set_run(run, size=size, bold=bold, color=color)
    p.paragraph_format.space_after = Pt(0)
    p.paragraph_format.space_before = Pt(0)


def add_heading_styled(doc, text, level):
    h = doc.add_heading(text, level=level)
    for run in h.runs:
        run.font.color.rgb = NAVY if level <= 2 else TEAL
        run.font.name = "Calibri"
    return h


def add_para(doc, text, size=11, bold=False, color=DARK, space_after=8):
    p = doc.add_paragraph()
    run = p.add_run(text)
    set_run(run, size=size, bold=bold, color=color)
    p.paragraph_format.space_after = Pt(space_after)
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.line_spacing = 1.15
    return p


def add_bullet(doc, text, level=0):
    p = doc.add_paragraph(style="List Bullet")
    p.clear()
    run = p.add_run(text)
    set_run(run, size=11)
    p.paragraph_format.left_indent = Cm(1.25 + level * 0.6)
    p.paragraph_format.space_after = Pt(3)
    return p


def add_code(doc, text):
    p = doc.add_paragraph()
    run = p.add_run(text)
    set_run(run, size=9, font="Consolas", color=RGBColor(0x1A, 0x1A, 0x1A))
    p.paragraph_format.space_after = Pt(8)
    p.paragraph_format.space_before = Pt(4)
    p.paragraph_format.left_indent = Cm(0.4)
    pPr = p._p.get_or_add_pPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), "F4F6F8")
    shd.set(qn("w:val"), "clear")
    pPr.append(shd)
    return p


def add_table(doc, headers, rows, col_widths=None):
    table = doc.add_table(rows=1 + len(rows), cols=len(headers))
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, h in enumerate(headers):
        cell = table.rows[0].cells[i]
        shade_cell(cell, HEADER_FILL)
        set_cell_text(cell, h, bold=True, color=WHITE, size=9, align="center")
    for r_idx, row in enumerate(rows):
        for c_idx, val in enumerate(row):
            cell = table.rows[r_idx + 1].cells[c_idx]
            if r_idx % 2 == 1:
                shade_cell(cell, ALT_ROW)
            set_cell_text(cell, str(val), size=8.5)
    if col_widths:
        for row in table.rows:
            for i, w in enumerate(col_widths):
                row.cells[i].width = Cm(w)
    doc.add_paragraph()
    return table


def add_image(doc, filename, caption, width_inches=6.6):
    path = ASSETS / filename
    if not path.exists():
        add_para(doc, f"[Diagram not found: {filename}]", color=GRAY, size=10)
        return
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p.add_run()
    run.add_picture(str(path), width=Inches(width_inches))
    cap = doc.add_paragraph()
    cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = cap.add_run(caption)
    set_run(r, size=9, bold=False, color=GRAY)
    cap.paragraph_format.space_after = Pt(12)


def add_api_block(doc, title, rows):
    add_heading_styled(doc, title, 3)
    add_table(doc, ["Item", "Value"], rows, col_widths=[4.5, 12.5])


def build():
    doc = Document()
    section = doc.sections[0]
    section.top_margin = Cm(1.8)
    section.bottom_margin = Cm(1.8)
    section.left_margin = Cm(1.8)
    section.right_margin = Cm(1.8)

    t = doc.add_paragraph()
    t.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = t.add_run("ABDM V3 — HIP Service API Documentation")
    set_run(r, size=26, bold=True, color=NAVY)
    t.paragraph_format.space_after = Pt(6)

    s = doc.add_paragraph()
    s.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = s.add_run("Milestone 2 (M2): Link Care Contexts")
    set_run(r, size=16, bold=True, color=TEAL)

    for line in [
        "Service: hip-service (In.ProjectEKA.HipService)",
        "Companion to M1 in docs/changesExpalined.docx",
        "Starting commit: 83ddacfe — “abdm V3”, plus later linking / HFR / SMS / scan-and-share fixes",
        "References: ABDM sandbox V3, User-initiated linking swagger, HIP-initiated linking",
    ]:
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        r = p.add_run(line)
        set_run(r, size=11, color=GRAY)
        p.paragraph_format.space_after = Pt(2)

    add_para(doc, "")

    add_heading_styled(doc, "1. What M2 covers", 1)
    add_para(
        doc,
        "Milestone 2 links a hospital visit (care context) to the patient’s ABHA address so that later M3 data "
        "transfer can happen. ABDM describes two registration situations, plus discovery that a patient can start "
        "from a PHR app at any time.",
    )
    add_table(
        doc,
        ["Scenario", "When", "HIP action"],
        [
            [
                "HIP-initiated linking",
                "Patient shared ABHA address during hospital registration",
                "On each new visit, HIP obtains a link token (if needed) and POSTs carecontext to HIE-CM",
            ],
            [
                "SMS notify then user-initiated",
                "Patient did not share ABHA at registration",
                "HIP asks ABDM to SMS a deep link; patient discovers and links from PHR",
            ],
            [
                "User-initiated discovery and link",
                "Patient already has ABHA and uses PHR to find this facility",
                "Gateway calls HIP discover / link-init / link-confirm; HIP replies with on-* callbacks",
            ],
            [
                "Scan and share",
                "Patient scans facility QR in PHR at the counter",
                "Gateway POSTs profile to HIP /api/v3/hip/patient/share; HIP queues the patient",
            ],
        ],
        col_widths=[4.0, 6.5, 6.5],
    )
    add_para(
        doc,
        "V3 linking is still asynchronous on this HIP: Gateway (or Bahmni) POSTs, HIP returns 202 immediately, "
        "then Hangfire / background work talks to OpenMRS and callbacks HIE-CM. Clinical FHIR is not sent in M2 — "
        "only care-context reference numbers, display labels, and hiType.",
    )

    add_heading_styled(doc, "2. Actors, tokens and HFR ID", 1)
    add_heading_styled(doc, "2.1 Actors", 2)
    add_table(
        doc,
        ["Actor", "Role"],
        [
            ["PHR app", "Patient discovers facilities, confirms OTP, scans QR"],
            ["ABDM Gateway / HIE-CM", "Routes discover/link; issues link tokens; sends SMS; calls HIP callbacks"],
            ["hip-service", "Matches patients, sends OTP, stores links, posts on-discover / on-init / on-confirm / carecontext"],
            ["OpenMRS / Bahmni", "Source of patients, visits, location HFR attributes; triggers new-carecontext and SMS notify"],
            ["OTP service", "Sends hospital OTP for user-initiated link-init (not ABHA OTP)"],
        ],
        col_widths=[4.5, 12.5],
    )

    add_heading_styled(doc, "2.2 Link token vs gateway session token", 2)
    add_bullet(doc, "Gateway session token: client_credentials accessToken on every HIP-to-ABDM HTTP call (same as M1).")
    add_bullet(
        doc,
        "Link token (X-LINK-TOKEN): patient-scoped JWT for HIP-initiated linking. HIP stores it per ABHA address + HFR ID. "
        "JWT must contain claim abhaAddress and must not be expired (UserAuthService.CheckAccessToken).",
    )
    add_bullet(doc, "X-HIP-ID: HFR / HIP id of the visit location. Required on generate-token, add carecontext, and context notify.")
    add_bullet(
        doc,
        "Composite in-memory key: abhaAddress + \"##:##\" + hipId in UserAuthMap.HealthIdToAccessToken. "
        "The same ABHA can have different tokens for different facilities.",
    )

    add_heading_styled(doc, "2.3 Visit-wise HFR ID (V3 change)", 2)
    add_para(
        doc,
        "Care context referenceNumber in this service is patientId:visitUuid. HIP extracts visitUuid, loads the OpenMRS "
        "visit location, and reads HFR ID and ABDM facility name from location attributes. Values are cached in HfrIdCache "
        "/ FacilityNameCache. If the location has no HFR attribute, HIP falls back to BahmniConfiguration default hip id.",
    )
    add_code(
        doc,
        "careContexts[].referenceNumber = \"{openMrsPatientId}:{visitUuid}\"\n"
        "HIP -> OpenMRS visit -> location.attributes -> HFR ID + facility name\n"
        "Used as X-HIP-ID on generate-token, link/carecontext, context/notify, and SMS hip.id",
    )

    add_heading_styled(doc, "2.4 Common ABDM headers (HIP to Gateway)", 2)
    add_table(
        doc,
        ["Header", "When"],
        [
            ["Authorization", "Always — gateway session token"],
            ["REQUEST-ID", "Always — UUID; for generate-token HIP passes its own requestId so on-generate-token can match"],
            ["TIMESTAMP", "Always — UTC yyyy-MM-dd'T'HH:mm:ss.fff'Z'"],
            ["X-CM-ID", "Always — Gateway.cmSuffix"],
            ["CORRELATION-ID", "When present on inbound call"],
            ["X-HIP-ID", "HIP-initiated generate-token, add carecontext, context notify"],
            ["X-LINK-TOKEN", "HIP-initiated add carecontext and context notify"],
        ],
        col_widths=[4.5, 12.5],
    )

    add_heading_styled(doc, "3. Flow A — User-initiated linking", 1)
    add_para(
        doc,
        "Patient opens PHR, picks this facility, and asks ABDM to find records. Gateway calls HIP. HIP must return "
        "care contexts with no clinical content. After the patient selects visits, Gateway asks HIP to start linking; "
        "HIP sends an OTP to the hospital mobile and later verifies it.",
    )
    add_image(
        doc,
        "m2-user-initiated-linking.png",
        "Figure 1. User-initiated discover, then link-init (OTP), then link-confirm.",
    )

    add_heading_styled(doc, "3.1 Discover care contexts", 2)
    add_api_block(
        doc,
        "Discover (Gateway to HIP)",
        [
            ["Direction", "ABDM Gateway -> HIP callback"],
            ["HIP path", "POST /api/v3/hip/patient/care-context/discover"],
            ["HIP auth", "Gateway scheme (Authorize)"],
            ["HIP HTTP", "202 Accepted immediately; work on Hangfire"],
            ["HIP callback", "POST {Gateway.url}/api/hiecm/user-initiated-linking/v3/patient/care-context/on-discover"],
            ["Controller", "Discovery.CareContextDiscoveryController"],
            ["Purpose", "Match one patient in OpenMRS and return care contexts grouped by hiType"],
        ],
    )
    add_para(doc, "Inbound headers: CORRELATION-ID, REQUEST-ID, TIMESTAMP. If REQUEST-ID is empty, HIP generates a UUID.", size=10)
    add_para(doc, "Gateway request body (DiscoveryRequest)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "transactionId": "uuid-from-phr",\n'
        '  "patient": {\n'
        '    "id": "first.last@sbx",\n'
        '    "name": "First Last",\n'
        '    "gender": "F",\n'
        '    "yearOfBirth": 1990,\n'
        '    "verifiedIdentifiers": [\n'
        '      { "type": "MOBILE", "value": "98xxxxxxxx" },\n'
        '      { "type": "ABHA_NUMBER", "value": "91-xxxx-xxxx-xxxx" }\n'
        "    ],\n"
        '    "unverifiedIdentifiers": [\n'
        '      { "type": "MR", "value": "BAH123" }\n'
        "    ]\n"
        "  }\n"
        "}",
    )
    add_para(doc, "Matching order used by HIP (aligned with ABDM discovery algorithm):", bold=True, space_after=4)
    add_bullet(doc, "ABHA address / ABHA number on the OpenMRS patient")
    add_bullet(doc, "Else verified mobile, then gender, year of birth within +/- 5 years, phonetic name")
    add_bullet(doc, "Else medical record (MR) identifier")
    add_bullet(doc, "Zero or more than one match becomes an error on on-discover (No Matching Record Found or More than one Record Found)")
    add_para(doc, "HIP on-discover body (GatewayDiscoveryRepresentation) — success", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "transactionId": "uuid-from-phr",\n'
        '  "matchedBy": ["MOBILE"],\n'
        '  "patient": [\n'
        "    {\n"
        '      "referenceNumber": "openMrsPatientUuid",\n'
        '      "display": "First Last",\n'
        '      "hiType": "OPConsultation",\n'
        '      "count": 1,\n'
        '      "careContexts": [\n'
        '        { "referenceNumber": "openMrsPatientUuid:visitUuid", "display": "OPD 22-Aug-2026" }\n'
        "      ]\n"
        "    }\n"
        "  ],\n"
        '  "response": { "requestId": "<inbound REQUEST-ID>" }\n'
        "}",
    )
    add_para(
        doc,
        "Care contexts are grouped by hiType (OPConsultation, Prescription, DiagnosticReport, DischargeSummary, "
        "ImmunizationRecord, WellnessRecord, HealthDocumentRecord, Invoice). HIP also stores PatientInfoMap[abhaAddress] "
        "for later identifier write-back. Discovery transaction is saved so link-init can prove these contexts were discovered.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "3.2 Link init (send OTP)", 2)
    add_api_block(
        doc,
        "Link init (Gateway to HIP)",
        [
            ["Direction", "ABDM Gateway -> HIP callback"],
            ["HIP path", "POST /api/v3/hip/link/care-context/init"],
            ["HIP HTTP", "202 Accepted; Hangfire LinkPatient()"],
            ["HIP callback", "POST {Gateway.url}/api/hiecm/user-initiated-linking/v3/link/care-context/on-init"],
            ["Controller", "Link.LinkController.LinkFor"],
            ["Purpose", "Validate discovery, generate linkRefNumber, SMS OTP to hospital mobile"],
        ],
    )
    add_para(doc, "Gateway request body (LinkReferenceRequest)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "transactionId": "uuid-from-discover",\n'
        '  "abhaAddress": "first.last@sbx",\n'
        '  "patient": [\n'
        "    {\n"
        '      "referenceNumber": "openMrsPatientUuid",\n'
        '      "hiType": "OPConsultation",\n'
        '      "count": 1,\n'
        '      "careContexts": [\n'
        '        { "referenceNumber": "openMrsPatientUuid:visitUuid" }\n'
        "      ]\n"
        "    }\n"
        "  ]\n"
        "}",
    )
    add_para(doc, "HIP checks:", bold=True, space_after=4)
    add_bullet(doc, "Discovery request exists for this transactionId + abhaAddress + patient reference.")
    add_bullet(doc, "Patient and each care context exist in OpenMRS.")
    add_bullet(doc, "Saves a unique linkReferenceNumber, sends OTP via OTP service using visit facility name.")
    add_bullet(doc, "Deletes the discovery row after OTP is queued so it cannot be reused.")
    add_para(doc, "HIP on-init body (success) — GatewayLinkResponse", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "transactionId": "uuid-from-discover",\n'
        '  "link": {\n'
        '    "referenceNumber": "linkRef-uuid",\n'
        '    "authenticationType": "MEDIATED",\n'
        '    "meta": {\n'
        '      "communicationMedium": "MOBILE",\n'
        '      "communicationHint": "98xxxxxxxx",\n'
        '      "communicationExpiry": "2026-08-22T07:10:00.000Z"\n'
        "    }\n"
        "  },\n"
        '  "resp": { "requestId": "<inbound REQUEST-ID>" }\n'
        "}",
    )

    add_heading_styled(doc, "3.3 Link confirm (verify OTP)", 2)
    add_api_block(
        doc,
        "Link confirm (Gateway to HIP)",
        [
            ["Direction", "ABDM Gateway -> HIP callback"],
            ["HIP path", "POST /api/v3/hip/link/care-context/confirm"],
            ["HIP HTTP", "202 Accepted; Hangfire LinkPatientCareContextFor()"],
            ["HIP callback", "POST {Gateway.url}/api/hiecm/user-initiated-linking/v3/link/care-context/on-confirm"],
            ["Controller", "Link.LinkController.LinkPatientFor"],
            ["Purpose", "Verify OTP, persist linked accounts, write ABHA identifiers into OpenMRS"],
        ],
    )
    add_para(doc, "Gateway request body (LinkPatientRequest)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "confirmation": {\n'
        '    "linkRefNumber": "linkRef-uuid",\n'
        '    "token": "123456"\n'
        "  }\n"
        "}",
    )
    add_para(doc, "HIP on-confirm body (success) — patient list grouped by hiType", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "patient": [\n'
        "    {\n"
        '      "referenceNumber": "openMrsPatientUuid",\n'
        '      "display": "First Last",\n'
        '      "hiType": "OPConsultation",\n'
        '      "count": 1,\n'
        '      "careContexts": [\n'
        '        { "referenceNumber": "openMrsPatientUuid:visitUuid", "display": "OPD 22-Aug-2026" }\n'
        "      ]\n"
        "    }\n"
        "  ],\n"
        '  "resp": { "requestId": "<inbound REQUEST-ID>" }\n'
        "}",
    )
    add_para(
        doc,
        "On success HIP also POSTs ABHA number + ABHA address to OpenMRS "
        "ws/rest/v1/hip/existingPatients/update/{patientUuid} and dumps NdhmDemographics (healthId, name, gender, YOB, mobile) "
        "so later HIP-initiated linking can generate a link token from demographics.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "4. Flow B — HIP-initiated linking", 1)
    add_para(
        doc,
        "Used when the hospital already knows the ABHA address (M1 login/create, or scan-and-share, or a previous link). "
        "Bahmni notifies HIP of a new visit. If that care context is not yet linked, HIP gets a link token (cached or "
        "via demographic generate-token) and POSTs link/carecontext. If it is already linked, HIP only notifies the new records.",
    )
    add_image(
        doc,
        "m2-hip-initiated-linking.png",
        "Figure 2. HIP-initiated linking: new-carecontext, generate-token, add carecontext or context notify.",
    )

    add_heading_styled(doc, "4.1 Bahmni entry: new care context", 2)
    add_api_block(
        doc,
        "Pass new care contexts (Bahmni to HIP)",
        [
            ["Direction", "Bahmni / OpenMRS -> HIP"],
            ["HIP path", "POST /v0.5/hip/new-carecontext"],
            ["HIP HTTP", "200 OK when processed; 500 on failure"],
            ["Controller", "Link.CareContextController.PassContext"],
            ["Purpose", "For each care context: cache HFR from visit, then add (first link) or notify (already linked)"],
        ],
    )
    add_para(doc, "HIP request body (NewContextRequest)", bold=True, space_after=4)
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
    add_para(doc, "Per care context HIP does:", bold=True, space_after=4)
    add_bullet(doc, "On the first context, SetHfrIdForVisitAsync(visitUuid) from OpenMRS location attributes.")
    add_bullet(doc, "If referenceNumber is already in linked care contexts → CallNotifyContext.")
    add_bullet(doc, "Else → CallAddContext (generate/reuse link token, then POST carecontext).")

    add_heading_styled(doc, "4.2 Generate link token (demographic auth)", 2)
    add_para(
        doc,
        "CallAddContext first tries an in-memory token for abhaAddress##:##hipId. If missing or expired, it loads "
        "AuthConfirm from DB for that healthId+hipId. If still missing, it POSTs generate-token using dumped demographics "
        "and waits (polls RequestIdToAccessToken) until Gateway callbacks on-generate-token.",
    )
    add_api_block(
        doc,
        "Generate token (HIP to Gateway)",
        [
            ["Direction", "HIP -> Gateway"],
            ["Gateway path", "POST /api/hiecm/v3/token/generate-token"],
            ["HIP callback", "POST /api/v3/hip/token/on-generate-token"],
            ["Controller (callback)", "UserAuth.UserAuthController.OnGenerateLinkToken"],
            ["Purpose", "Obtain X-LINK-TOKEN for this ABHA at this HFR without a new OTP if demographics match"],
        ],
    )
    add_para(doc, "HIP generate-token body (GenerateLinkTokenRequest)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "abhaAddress": "first.last@sbx",\n'
        '  "name": "First Last",\n'
        '  "gender": "F",\n'
        '  "yearOfBirth": "1990"\n'
        "}",
    )
    add_para(doc, "Headers include X-HIP-ID = visit HFR id and REQUEST-ID = HIP’s requestId.", size=10)
    add_para(doc, "Gateway callback body (OnGenerateTokenRequest)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "abhaAddress": "first.last@sbx",\n'
        '  "linkToken": "<jwt>",\n'
        '  "response": { "requestId": "<HIP requestId>" },\n'
        '  "error": null\n'
        "}",
    )
    add_para(
        doc,
        "HIP validates JWT (abhaAddress claim, exp), upserts AuthConfirm(healthId, hipId, token) in DB, and sets "
        "HealthIdToAccessToken[abhaAddress##:##hipId]. Errors on the callback are stored in RequestIdToErrorMessage "
        "so the waiting add-context loop can stop.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "4.3 Add care context (first link of this visit)", 2)
    add_api_block(
        doc,
        "Add carecontext (HIP to Gateway)",
        [
            ["Direction", "HIP -> Gateway"],
            ["Gateway path", "POST /api/hiecm/hip/v3/link/carecontext"],
            ["HIP callback", "POST /api/v3/link/on_carecontext"],
            ["Controller (callback)", "Link.LinkController.HipLinkOnAddContexts"],
            ["Purpose", "Attach care contexts to ABHA using X-LINK-TOKEN and X-HIP-ID"],
        ],
    )
    add_para(doc, "HIP body (GatewayAddContextsRequestRepresentation)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "abhaAddress": "first.last@sbx",\n'
        '  "patient": [\n'
        "    {\n"
        '      "referenceNumber": "openMrsPatientUuid",\n'
        '      "display": "First Last",\n'
        '      "hiType": "OPConsultation",\n'
        '      "count": 1,\n'
        '      "careContexts": [\n'
        '        { "referenceNumber": "openMrsPatientUuid:visitUuid", "display": "OPD 22-Aug-2026" }\n'
        "      ]\n"
        "    }\n"
        "  ]\n"
        "}",
    )
    add_para(
        doc,
        "HIP generates a unique linkReferenceNumber per add request, saves initiated link + care context names, "
        "and groups patient[] by hiType (same shape as user-initiated on-confirm). Missing link token throws and "
        "the Bahmni call returns 500.",
        size=10,
        color=GRAY,
    )
    add_para(doc, "Callback HipLinkContextConfirmation: on Status success HIP VerifyAndLinkCareContexts(requestId) marks link rows and SaveLinkedAccounts.", size=10)

    add_heading_styled(doc, "4.4 Notify existing care context (new records on an already linked visit)", 2)
    add_api_block(
        doc,
        "Context notify (HIP to Gateway)",
        [
            ["Direction", "HIP -> Gateway"],
            ["Gateway path", "POST /api/hiecm/hip/v3/link/context/notify"],
            ["HIP callback", "POST /api/v3/links/context/on-notify"],
            ["Controller (callback)", "Link.CareContextController.HipLinkOnNotifyContexts"],
            ["Purpose", "Tell PHR apps that an already-linked care context has new hiTypes / records"],
        ],
    )
    add_para(doc, "HIP body (GatewayNotificationContextRepresentation)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "notification": {\n'
        '    "patient": { "id": "first.last@sbx" },\n'
        '    "careContext": {\n'
        '      "patientReference": "openMrsPatientUuid",\n'
        '      "careContextReference": "openMrsPatientUuid:visitUuid"\n'
        "    },\n"
        '    "hiTypes": ["OPConsultation", "Prescription"],\n'
        '    "date": "2026-08-22T06:40:00.000Z",\n'
        '    "hip": { "id": "IN2710001820" }\n'
        "  }\n"
        "}",
    )
    add_para(doc, "Requires a valid in-memory link token for that ABHA+HFR. Callback is logged only (acknowledgement status).", size=10)

    add_heading_styled(doc, "5. Flow C — SMS notify (no ABHA at registration)", 1)
    add_para(
        doc,
        "When Bahmni has only a phone number, HIP cannot HIP-initiate. It asks ABDM to SMS a deep link. The patient "
        "opens PHR and runs Flow A (discover + link) against this facility. HIP resolves facility id/name from the "
        "latest visit cached for that phone’s ABHA; if the phone is unknown it returns 400.",
    )
    add_image(
        doc,
        "m2-sms-and-scan-share.png",
        "Figure 3. Top: SMS notify. Bottom: scan-and-share facility QR.",
    )
    add_api_block(
        doc,
        "SMS notify (Bahmni to HIP to Gateway)",
        [
            ["Direction", "Bahmni -> HIP -> Gateway"],
            ["HIP path", "POST /v0.5/hip/patients/sms/notify"],
            ["Gateway path", "POST /api/hiecm/hip/v3/link/patient/links/sms/notify2"],
            ["Gateway callback", "POST /api/v3/patients/sms/on-notify"],
            ["Controller", "SmsNotification.SmsNotificationController"],
            ["Purpose", "ABDM sends SMS deep link so the patient can discover/link this HIP"],
        ],
    )
    add_para(doc, "HIP request body (SmsNotifyRequest)", bold=True, space_after=4)
    add_code(doc, '{\n  "phoneNo": "98xxxxxxxx"\n}')
    add_para(doc, "HIP to Gateway body", bold=True, space_after=4)
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
    add_para(doc, "Resolution rules in SmsNotificationService:", bold=True, space_after=4)
    add_bullet(doc, "phoneNo is required; empty → 400 Phone number is required.")
    add_bullet(doc, "Phone is normalized (strip +91 / 91) and looked up in PhoneNumberToHealthId.")
    add_bullet(doc, "Unknown phone → 400 No details found for the given phone number.")
    add_bullet(doc, "If HealthIdToLatestVisitUuid has a visit, use that visit’s HFR id and facility name; else Bahmni defaults.")
    add_para(
        doc,
        "CallAddContext seeds PhoneNumberToHealthId from demographics before generate-token so SMS notify does not race. "
        "on-notify is log-only (status or error).",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "6. Flow D — Scan and share (facility QR)", 1)
    add_para(
        doc,
        "The QR must use ABDM’s PHR domain, not the hospital domain. PHR validates the HIP id, then Gateway posts the "
        "patient profile to the registered HIP callback.",
    )
    add_para(doc, "Sandbox QR URL", bold=True, space_after=4)
    add_code(doc, "https://phrsbx.abdm.gov.in/share-profile?hip-id={HFR_ID}&counter-id={COUNTER}")
    add_api_block(
        doc,
        "Share profile (Gateway to HIP)",
        [
            ["Direction", "ABDM Gateway -> HIP callback"],
            ["HIP path", "POST /api/v3/hip/patient/share"],
            ["HIP HTTP", "202 Accepted if valid; 400 if invalid body"],
            ["HIP callback (after ~500 ms)", "POST {Gateway.url}/api/hiecm/patient-share/v3/on-share"],
            ["Bahmni poll", "GET /v3/hip/getPatientQueue (Bahmni auth)"],
            ["Controller", "Patient.PatientController"],
            ["Purpose", "Receive ABHA profile at the counter and queue the patient for registration"],
        ],
    )
    add_para(doc, "Gateway request body (ShareProfileRequest)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "intent": "PROFILE_SHARE",\n'
        '  "metadata": {\n'
        '    "hipId": "IN2710001820",\n'
        '    "context": "REGISTRATION-DESK-1",\n'
        '    "hprId": null,\n'
        '    "latitude": "...",\n'
        '    "longitude": "..."\n'
        "  },\n"
        '  "profile": {\n'
        '    "patient": {\n'
        '      "abhaAddress": "first.last@sbx",\n'
        '      "abhaNumber": "91-xxxx-xxxx-xxxx",\n'
        '      "name": "First Last",\n'
        '      "gender": "F",\n'
        '      "yearOfBirth": 1990,\n'
        '      "phoneNumber": "98xxxxxxxx"\n'
        "    }\n"
        "  }\n"
        "}",
    )
    add_para(doc, "HIP on-share acknowledgement (success)", bold=True, space_after=4)
    add_code(
        doc,
        "{\n"
        '  "acknowledgement": {\n'
        '    "status": "SUCCESS",\n'
        '    "abhaAddress": "first.last@sbx",\n'
        '    "profile": {\n'
        '      "context": "REGISTRATION-DESK-1",\n'
        '      "token": "<queue-token>",\n'
        '      "expiry": "1800"\n'
        "    }\n"
        "  },\n"
        '  "response": { "requestId": "<inbound request-id>" }\n'
        "}",
    )
    add_para(
        doc,
        "token is an integer queue number from SavePatient. Bahmni uses GET /v3/hip/getPatientQueue (Bahmni auth) to pick "
        "up shared profiles. metadata.context is the counter-id from the QR. Callback URL must be public HTTPS and "
        "registered in ABDM sandbox as https://{hip-host}/api/v3/hip/patient/share.",
        size=10,
        color=GRAY,
    )

    add_heading_styled(doc, "7. Optional: HIP-side user auth (fetch-modes / init / confirm)", 1)
    add_para(
        doc,
        "These v0.5 HIP APIs are still used when Bahmni wants a linking token through patient KYC (MOBILE_OTP, "
        "AADHAAR_OTP, DEMOGRAPHICS, DIRECT) instead of silent generate-token. They talk to Gateway users/auth/* and "
        "store the same HealthIdToAccessToken / AuthConfirm used by Flow B.",
    )
    add_table(
        doc,
        ["HIP API", "Gateway API", "Callback on HIP", "Notes"],
        [
            [
                "POST /v0.5/hip/fetch-modes (Bahmni auth)",
                "POST /v0.5/users/auth/fetch-modes",
                "POST /v0.5/users/auth/on-fetch-modes",
                "Returns auth modes + HIP always appends DIRECT. Polls until callback or 504.",
            ],
            [
                "POST /v0.5/hip/auth/init (Bahmni auth)",
                "POST /v0.5/users/auth/init",
                "POST /v0.5/users/auth/on-init",
                "Stores transactionId per healthId. purpose often KYC_AND_LINK.",
            ],
            [
                "POST /v0.5/hip/auth/confirm (Bahmni auth)",
                "POST /v0.5/users/auth/confirm",
                "POST /v0.5/users/auth/on-confirm",
                "authCode is Base64 OTP, or Demographic object. Returns patient details; saves accessToken.",
            ],
            [
                "POST /v0.5/hip/auth/direct (Bahmni auth)",
                "(patient approves in PHR)",
                "POST /v0.5/users/auth/notify then HIP POST /v0.5/users/auth/on-notify",
                "Polls GRANTED/DENIED. GRANTED also saves accessToken.",
            ],
            [
                "POST /v0.5/hip/ndhm-demographics",
                "local only",
                "—",
                "Persists NdhmDemographics for later generate-token.",
            ],
        ],
        col_widths=[4.6, 4.4, 4.2, 3.8],
    )
    add_para(doc, "fetch-modes / auth-init request sketch", bold=True, space_after=4)
    add_code(
        doc,
        "POST /v0.5/hip/fetch-modes\n"
        '{ "healthId": "first.last@sbx", "purpose": "KYC_AND_LINK" }\n\n'
        "POST /v0.5/hip/auth/init\n"
        '{ "healthId": "first.last@sbx", "authMode": "MOBILE_OTP", "purpose": "KYC_AND_LINK" }\n\n'
        "POST /v0.5/hip/auth/confirm\n"
        '{ "healthId": "first.last@sbx", "authCode": "<Base64 OTP>" }',
    )

    add_heading_styled(doc, "8. M2 API catalogue", 1)
    add_table(
        doc,
        ["#", "HIP / Gateway path", "Direction", "Flow"],
        [
            ["A1", "POST /api/v3/hip/patient/care-context/discover", "GW -> HIP", "User-initiated"],
            ["A1b", "POST /api/hiecm/user-initiated-linking/v3/patient/care-context/on-discover", "HIP -> GW", "User-initiated"],
            ["A2", "POST /api/v3/hip/link/care-context/init", "GW -> HIP", "User-initiated"],
            ["A2b", "POST /api/hiecm/user-initiated-linking/v3/link/care-context/on-init", "HIP -> GW", "User-initiated"],
            ["A3", "POST /api/v3/hip/link/care-context/confirm", "GW -> HIP", "User-initiated"],
            ["A3b", "POST /api/hiecm/user-initiated-linking/v3/link/care-context/on-confirm", "HIP -> GW", "User-initiated"],
            ["B0", "POST /v0.5/hip/new-carecontext", "Bahmni -> HIP", "HIP-initiated"],
            ["B1", "POST /api/hiecm/v3/token/generate-token", "HIP -> GW", "HIP-initiated"],
            ["B1b", "POST /api/v3/hip/token/on-generate-token", "GW -> HIP", "HIP-initiated"],
            ["B2", "POST /api/hiecm/hip/v3/link/carecontext", "HIP -> GW", "HIP-initiated first link"],
            ["B2b", "POST /api/v3/link/on_carecontext", "GW -> HIP", "HIP-initiated first link"],
            ["B3", "POST /api/hiecm/hip/v3/link/context/notify", "HIP -> GW", "HIP-initiated update"],
            ["B3b", "POST /api/v3/links/context/on-notify", "GW -> HIP", "HIP-initiated update"],
            ["C1", "POST /v0.5/hip/patients/sms/notify", "Bahmni -> HIP", "SMS notify"],
            ["C1b", "POST /api/hiecm/hip/v3/link/patient/links/sms/notify2", "HIP -> GW", "SMS notify"],
            ["C1c", "POST /api/v3/patients/sms/on-notify", "GW -> HIP", "SMS notify"],
            ["D1", "POST /api/v3/hip/patient/share", "GW -> HIP", "Scan and share"],
            ["D1b", "POST /api/hiecm/patient-share/v3/on-share", "HIP -> GW", "Scan and share"],
            ["D2", "GET /v3/hip/getPatientQueue", "Bahmni -> HIP", "Scan and share"],
        ],
        col_widths=[1.4, 8.8, 3.0, 3.8],
    )

    add_heading_styled(doc, "9. Typical call order", 1)
    add_heading_styled(doc, "9.1 Patient already has ABHA in Bahmni (HIP-initiated)", 2)
    add_para(
        doc,
        "1. Complete M1 (create or verify ABHA) so NdhmDemographics exist.\n"
        "2. Create visit in Bahmni. Location must have HFR ID attribute.\n"
        "3. Bahmni POST /v0.5/hip/new-carecontext with healthId and careContexts[].referenceNumber = patientId:visitUuid.\n"
        "4. HIP generate-token if needed; Gateway POST on-generate-token.\n"
        "5. HIP POST link/carecontext with X-HIP-ID and X-LINK-TOKEN.\n"
        "6. Gateway POST on_carecontext; HIP marks linked.\n"
        "7. Later records on the same visit → context/notify instead of add.",
    )
    add_heading_styled(doc, "9.2 Patient has no ABHA (SMS then user-initiated)", 2)
    add_para(
        doc,
        "1. Register patient with mobile only; create visit.\n"
        "2. Bahmni POST /v0.5/hip/patients/sms/notify { phoneNo } (only works if HIP already mapped that phone to an ABHA "
        "from a prior M1/login; otherwise patient must create/verify ABHA first).\n"
        "3. Patient receives SMS, opens PHR, discovers this HFR.\n"
        "4. Gateway discover → HIP on-discover; link-init OTP → on-init; link-confirm OTP → on-confirm.",
    )
    add_heading_styled(doc, "9.3 Counter QR (scan and share)", 2)
    add_para(
        doc,
        "1. Display QR https://phrsbx.abdm.gov.in/share-profile?hip-id={HFR}&counter-id={desk}.\n"
        "2. Patient scans in PHR sandbox app.\n"
        "3. Gateway POST /api/v3/hip/patient/share; HIP 202 and later on-share with queue token.\n"
        "4. Registration desk GET /v3/hip/getPatientQueue and continues M1/M2 as needed.",
    )

    add_heading_styled(doc, "10. Implementation notes from the V3 changes", 1)
    add_bullet(doc, "User-initiated and HIP-initiated payloads group care contexts by hiType and include count.")
    add_bullet(doc, "Each add-context request gets its own linkReferenceNumber (not shared across visits).")
    add_bullet(doc, "X-HIP-ID is visit/location HFR, not a single hospital-wide constant.")
    add_bullet(doc, "Link tokens are stored per (abhaAddress, hipId) in AuthConfirm (migration AddHipIdToAuthConfirm).")
    add_bullet(doc, "UserAuthMap dictionaries are ConcurrentDictionary (thread-safe).")
    add_bullet(doc, "SMS notify uses notify2 and visit-wise hip name/id when HealthIdToLatestVisitUuid is set.")
    add_bullet(doc, "HIP never puts clinical data on discover/link/notify — only referenceNumber, display, hiType.")
    add_bullet(doc, "Discover/link callbacks are Gateway-auth. new-carecontext and sms/notify are called by Bahmni without the same Bahmni-auth attribute as M1 (sms/notify has no [Authorize] on the outbound route).")
    add_bullet(doc, "PATH_SET_HFR_ID (/v0.5/hip/set-hfr-id) is defined but unused; HFR is resolved from OpenMRS visit location.")

    add_heading_styled(doc, "11. Code map (M2)", 1)
    add_table(
        doc,
        ["Area", "Primary types"],
        [
            ["User-initiated discover", "Discovery/PatientController.cs (CareContextDiscoveryController), IPatientDiscovery"],
            ["User-initiated link", "Link/LinkController.cs, Link/LinkPatient.cs, Link/PatientVerification.cs"],
            ["HIP-initiated", "Link/CareContextController.cs, Link/CareContextService.cs"],
            ["Link token", "UserAuth/UserAuthService.cs, UserAuth/UserAuthController.cs, UserAuth/UserAuthMap.cs"],
            ["SMS notify", "SmsNotification/SmsNotificationController.cs, SmsNotificationService.cs"],
            ["Scan and share", "Patient/PatientController.cs, IPatientProfileService"],
            ["HFR cache", "Common/Model/BahmniConfiguration.cs, HfrIdCache.cs, FacilityNameCache.cs"],
            ["Paths", "Common/Constants.cs (PATH_CARE_CONTEXTS_DISCOVER, PATH_LINKS_*, PATH_GENERATE_TOKEN, PATH_ADD_PATIENT_CONTEXTS, PATH_SMS_NOTIFY, PATH_PROFILE_SHARE)"],
        ],
        col_widths=[5.0, 12.0],
    )

    add_heading_styled(doc, "12. What comes next (M3)", 1)
    add_para(
        doc,
        "M3 is consent and health-information exchange: Gateway POST /api/v3/consent/request/hip/notify, HIP "
        "on-notify, then POST /api/v3/hip/health-information/request, HIP on-request, encrypted FHIR push to the HIU "
        "dataPushUrl, and health-information/notify with hipId. That section will be written after this M2 recap.",
    )

    add_heading_styled(doc, "13. External references", 1)
    add_bullet(doc, "ABDM Sandbox V3: https://sandbox.abdm.gov.in/sandbox/v3/new-documentation")
    add_bullet(doc, "HIP-initiated linking: https://sandbox.abdm.gov.in/sandbox/v3/new-documentation?doc=HIPInitiatedlinking")
    add_bullet(doc, "User-initiated linking swagger: sandbox v3 swagger with integration_label abdm_user_initiated_linking_phr")
    add_bullet(doc, "Facility QR: PHR domain phrsbx.abdm.gov.in/share-profile (see docs/ABDM_Facility_QR_Code_Guide.md)")

    footer = doc.add_paragraph()
    r = footer.add_run("End of Milestone 2. M3 (consent + data flow) can be added after recap.")
    set_run(r, size=10, color=GRAY)
    footer.paragraph_format.space_before = Pt(18)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(str(OUT))
    print(f"Wrote {OUT}")


if __name__ == "__main__":
    build()
