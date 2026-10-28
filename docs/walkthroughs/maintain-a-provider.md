# Maintain a provider

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/MaintainAProviderTests.cs`, and a step you cannot perform
is a defect in the application or in this document — never something to work
around.

## User story

> As a surveillance administrator, I add a clinician to the provider registry
> with how to reach them, find them again by their last name, and retire them
> when they no longer practise — so that the providers a record can name are
> master data an administrator keeps, and a retired one is kept rather than
> lost.

This is administration, not case work: providers are reference data, and
keeping them is inferred from one legacy source, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → Admin → **Providers** | `../nedss/docs/functional/epitrax-core-workflows.md`, *Admin Pages → Key Admin Screens*, the **Providers** row (screen `24-admin-providers`), over the legacy `provider` and `provider_type` tables of `../nedss/legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql` | *Healthcare provider registry* — a provider's last, first and middle name, phone, email and provider type, added and edited through the admin section's shared list-and-edit pattern. |

Three differences from the legacy screen are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The provider type is chosen from a code list.** The legacy keeps the
  types in a `provider_type` table of their own; Survsys keeps them as the
  **Provider type** list on the **Code lists** screen, seeded with the one
  type the legacy seed names, **Clinician**.
- **A provider is retired, not deleted.** Each provider carries an
  **Active** switch the legacy does not have; turning it off keeps the
  provider on file, shown as inactive, so a record that names them still
  does.
- **The list is searched by the start of the last name.** Survsys shows one
  page of providers in name order with the columns **Last name**, **First
  name**, **Type**, **Phone**, **Email** and **Active**, an em dash (`—`)
  where a provider has no value on file.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   **Clinician** provider type is inserted by that migration; no provider is.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do.
   On a fresh database the only user is the administrator the API tier
   creates on its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. That
   administrator holds the Administrator role, which has every privilege.
   Open the application tier's address, sign in, then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level, clears the providers table, and performs the steps below in
a real browser. It records video of every test it runs, and writes the
recordings to `Survsys.E2ETests/bin/<configuration>/net10.0/videos` as
`.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under
   **Reference data**, choose **Providers**. (The providers screen is also at
   `/providers`.)
2. The browser tab reads **Providers — Survsys**, and below the application
   bar is the heading **Providers**. Above the list is a **New provider**
   button.
3. Press **New provider**.
4. The browser tab reads **New provider — Survsys**, and the heading reads
   **New provider**. Below it is the provider's form, with **Active** already
   switched on.
5. Fill the form in, box by box, leaving **Middle name** empty:
   - **First name**: `Rosa`
   - **Last name**: `Delgado`
   - **Provider type**: choose **Clinician** from the list.
   - **Phone**: `860-555-0142`
   - **Email**: `rosa.delgado@example.org`
6. Press **Save**. The screen goes back to the **Providers** list, under the
   heading **Providers**, which is how you know the save succeeded.
7. In the **Last name** box above the list type `Del` and press **Search**.
   The line of small text above the list reads **1 provider(s), showing 1.**,
   and the table's column headings, left to right, are **Last name**, **First
   name**, **Type**, **Phone**, **Email** and **Active**. The one row reads:

   | Last name | First name | Type | Phone | Email | Active |
   |---|---|---|---|---|---|
   | Delgado | Rosa | Clinician | 860-555-0142 | rosa.delgado@example.org | Yes |

8. Press **Delgado** to open the provider.
9. The heading reads **Edit provider**, and the form reads back what you
   entered: **First name** *Rosa*, **Last name** *Delgado*, **Provider type**
   *Clinician*, **Phone** *860-555-0142* and **Email**
   *rosa.delgado@example.org*, with **Active** on.
10. Switch **Active** off and press **Save**. The screen goes back to the
    **Providers** list.
11. The list still holds the provider — **1 provider(s), showing 1.** — and
    the row for **Delgado** now reads **No** under **Active**.

## What you should see

A provider added to the registry, found by their last name, and retired:

- The heading **Providers**, under an application bar reading **Survsys**,
  with a **New provider** button.
- After you fill the form for **Rosa Delgado**, a **Clinician**, and press
  **Save**, a search for `Del` finds the one row above.
- Opening the provider reads the whole record back under the heading **Edit
  provider**.
- After **Active** is switched off and saved, the provider is still listed,
  reading **No** under **Active**.

You now know how to add, find and retire a provider. If what you saw differs
from what is written above, the walkthrough has failed: say so rather than
adjusting what you did, because the promise this document makes is that it is
performable exactly as written.
