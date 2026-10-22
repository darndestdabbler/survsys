# Configure a notification guide

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ConfigureANotificationGuideTests.cs`, and a step you
cannot perform is a defect in the application or in this document — never
something to work around.

## User story

> As a surveillance administrator, I add a notification guide, give it a PHIN
> variable to carry and its own sending facility in place of the default, save
> both, and read them back — so that the messages built under that guide carry
> what CDC's guide for the condition asks for.

Configuring a notification guide is inferred from the legacy NMI, read-only
here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| NMI → **Guides** | `../nedss/docs/functional/nmi-ui-overview.md`, *Navigation Map* and *Core Export Tables* | The guides a condition is exported under, each with its version, the PHIN variables it carries (`guide_phin_var`) and its own header values. |
| NMI → PHIN variables | `../nedss/docs/business-logic/NMI_XML_GENERATION.md`, *XML Generation Pipeline* | The PHIN variable catalogue a guide's variables are drawn from, and the header — sending and receiving application and facility, HL7 version — every message is written with. |

Three differences from the legacy are deliberate, and this walkthrough
describes what Survsys shows rather than what NMI showed:

- **A guide carries only catalogued variables.** A PHIN variable is added to
  the catalogue on the **PHIN variables** screen first; a guide refuses one
  the catalogue does not list.
- **The header falls back to defaults.** Each header variable has a default,
  kept on the **Notification guides** screen; a guide's own value replaces it
  for that guide only, and a blank one uses the default.
- **Each part of a guide saves on its own.** Its name and version, its PHIN
  variables, its linked guides and its header each have their own save
  button and confirmation.

Assigning a condition to a guide is not part of this walkthrough: Survsys has
no screen for it yet, so the guide configured here exports no condition, and
nothing another walkthrough sends is changed by it.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   migration puts the five header variable defaults on file; the guide this
   walkthrough adds and the PHIN variable it gives that guide must not
   already be there. The suite's guide fixture,
   `Survsys.E2ETests/Fixtures/notification-guide.sql`, adds one guide,
   **Walkthrough guide**, which this walkthrough leaves alone.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do. On
   a fresh database the only user is the administrator the API tier creates on
   its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. Open the
   application tier's address, sign in, then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level and seeds it, and performs the steps below in a real browser.
It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **PHIN variables**. The browser
   tab reads **PHIN variables — Survsys** and the heading reads **PHIN
   variables**.
2. In the box labelled **Identifier**, type `INV169`. In the box labelled
   **Description**, type `Condition`. In the box labelled **HL7 data type**,
   type `CWE`. Press **Save**.
3. The table has a row for **INV169**, reading **Condition** for its
   **Description** and **CWE** for its **Data type**.
4. In the application bar, press **Notification guides**. The browser tab
   reads **Notification guides — Survsys** and the heading reads
   **Notification guides**. The table of guides lists **Walkthrough guide**.
5. Under **New guide**, in the box labelled **Name**, type
   `Configured guide`, and in the box labelled **Version**, type
   `Generic_MMG_V2.0`. Press **Add**.
6. The guide opens in its editor. The browser tab reads **Notification guide
   — Survsys**, the heading reads **Configured guide**, and the box labelled
   **Version (profile)** holds `Generic_MMG_V2.0`.
7. In the panel headed **PHIN variables**, in the box labelled **PHIN
   variable**, type `INV169` and press **Add**. A chip reading **INV169**
   appears. Press **Save variables**. A green confirmation reads **The
   guide's variables were saved.**
8. In the panel headed **Header variables**, find the row whose **Variable**
   is **sending_facility**. Its **Default** reads
   **CTDPH^2.16.840.1.114222.4.1.3650^ISO**. In its box under **This guide's
   value**, type `CTDPH_TRAINING^2.16.840.1.114222.4.1.3650^ISO`. Press
   **Save header**. A green confirmation reads **The guide's header was
   saved.**
9. Press **All guides** at the top of the editor. On the **Notification
   guides** screen, press **Configured guide** in the table of guides.
10. The editor opens again, headed **Configured guide**. The **PHIN
    variables** panel holds the chip **INV169**, and the **sending_facility**
    row's box under **This guide's value** holds
    `CTDPH_TRAINING^2.16.840.1.114222.4.1.3650^ISO`.

## What you should see

One notification guide configured:

- On the **PHIN variables** screen, **INV169**, **Condition**, **CWE** in the
  catalogue.
- A guide, **Configured guide**, version **Generic_MMG_V2.0**, carrying the
  PHIN variable **INV169**.
- Its own sending facility, **CTDPH_TRAINING^2.16.840.1.114222.4.1.3650^ISO**,
  in place of the default, kept when the guide is opened again.

You now know how to configure a notification guide. If what you saw differs
from what is written above, the walkthrough has failed: say so rather than
adjusting what you did, because the promise this document makes is that it is
performable exactly as written.
